using EventSystem.Events;
using EventSystem.Utils;
using NLog;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VRage.Collections;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace EventSystem.Managers
{
    public class ActiveEventsLCDManager
    {
        private readonly EventManager _eventManager;
        private readonly EventSystemConfig _config;
        private MyConcurrentHashSet<IMyTerminalBlock> _blocksToUpdate = new MyConcurrentHashSet<IMyTerminalBlock>();
        public static readonly Logger Log = LogManager.GetLogger("EventSystem/LCDManager");

        public ActiveEventsLCDManager(EventManager eventManager, EventSystemConfig config)
        {
            _eventManager = eventManager;
            _config = config;
            LoggerHelper.DebugLog(Log, _config, "LCDManager initialization.");
            CacheBlocksForUpdate();
        }

        public void UpdateMonitorBlocks()
        {
            LoggerHelper.DebugLog(Log, _config, $"Updating {_blocksToUpdate.Count} LCDs.");
            foreach (var block in _blocksToUpdate)
            {
                UpdateBlockIfApplicable(block);
            }
        }

        private void UpdateBlockIfApplicable(IMyTerminalBlock block)
        {
            if (block is IMyTextSurface textSurface)
            {
                UpdateTextSurfaceAsync(block, textSurface).Wait();
            }
            else if (block is IMyTextSurfaceProvider provider)
            {
                for (int i = 0; i < provider.SurfaceCount; i++)
                {
                    var surface = provider.GetSurface(i);
                    UpdateTextSurfaceAsync(block, (IMyTextSurface)surface).Wait();
                }
            }
            else if (block is IMyTextPanel panel)
            {
                UpdateTextSurfaceAsync(block, panel).Wait();
            }
            else if (block is Sandbox.ModAPI.Ingame.IMyTextSurfaceProvider ingameProvider)
            {
                for (int i = 0; i < ingameProvider.SurfaceCount; i++)
                {
                    var surface = ingameProvider.GetSurface(i);
                    UpdateTextSurfaceAsync(block, (IMyTextSurface)surface).Wait();
                }
            }
            else
            {
                Log.Warn($"Block '{block.CustomName}' is not a text surface, surface provider, or panel.");
            }
        }

        public void CacheBlocksForUpdate()
        {
            _blocksToUpdate.Clear();
            var grids = GetAllGrids();
            foreach (var grid in grids)
            {
                var blocks = GetAllBlocks(grid);
                foreach (var block in blocks)
                {
                    if (block.CustomName.Contains(_config.ActiveEventsLCDManagerTagName))
                    {
                        _blocksToUpdate.Add(block);
                        LoggerHelper.DebugLog(Log, _config, $"Added block '{block.CustomName}' to update list.");
                    }
                }
            }
        }

        private HashSet<IMyCubeGrid> GetAllGrids()
        {
            var entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities, e => e is IMyCubeGrid);
            return entities.OfType<IMyCubeGrid>().ToHashSet();
        }

        private IEnumerable<IMyTerminalBlock> GetAllBlocks(IMyCubeGrid grid)
        {
            var terminalSystem = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);
            var blocks = new List<IMyTerminalBlock>();
            terminalSystem?.GetBlocksOfType<IMyTerminalBlock>(blocks, block => block is IMyTextSurface || block is IMyTextSurfaceProvider && block.CustomName.Contains(_config.ActiveEventsLCDManagerTagName));
            return blocks;
        }

        private async Task UpdateTextSurfaceAsync(IMyTerminalBlock block, IMyTextSurface textSurface)
        {
            try
            {
                var activeEvents = _eventManager.Events.Where(e => e.IsActiveNow()).ToList();
                string displayText = GenerateDisplayTextForLCD(activeEvents);
                textSurface.ContentType = ContentType.TEXT_AND_IMAGE;
                textSurface.WriteText(displayText, false);
                textSurface.FontColor = activeEvents.Any() ? Color.Green : Color.Red;
                LoggerHelper.DebugLog(Log, _config, $"LCD '{block.CustomName}' updated successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error while updating the LCD: {block.CustomName}");
            }
        }

        private string GenerateDisplayTextForLCD(IEnumerable<EventsBase> activeEvents)
        {
            if (!activeEvents.Any()) return "No active events currently.";

            string text = "Active Events:\n";
            foreach (var eventItem in activeEvents)
            {
                text += $"{eventItem.EventName} - Start: {eventItem.StartTime.ToString(@"hh\:mm\:ss")}, End: {eventItem.EndTime.ToString(@"hh\:mm\:ss")}\n";
            }

            return text;
        }

    }
}
