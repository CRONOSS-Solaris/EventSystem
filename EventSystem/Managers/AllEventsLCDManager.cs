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
    public class AllEventsLCDManager
    {
        private readonly EventManager _eventManager;
        private readonly EventSystemConfig _config;
        private MyConcurrentHashSet<IMyTerminalBlock> _blocksToUpdate = new MyConcurrentHashSet<IMyTerminalBlock>();
        public static readonly Logger Log = LogManager.GetLogger("EventSystem/AllEventsLCDManager");

        public AllEventsLCDManager(EventManager eventManager, EventSystemConfig config)
        {
            _eventManager = eventManager;
            _config = config;
            LoggerHelper.DebugLog(Log, _config, "AllEventsLCDManager initialization.");
            CacheBlocksForUpdate();
        }

        public void UpdateMonitorBlocks()
        {
            LoggerHelper.DebugLog(Log, _config, $"Updating {_blocksToUpdate.Count} all-events LCDs.");
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

        private void CacheBlocksForUpdate()
        {
            _blocksToUpdate.Clear();
            var grids = GetAllGrids();
            foreach (var grid in grids)
            {
                var blocks = GetAllBlocks(grid);
                foreach (var block in blocks)
                {
                    if (block.CustomName.Contains(_config.AllEventsLcdTagName)) // Nowy tag dla wszystkich eventów
                    {
                        _blocksToUpdate.Add(block);
                        LoggerHelper.DebugLog(Log, _config, $"Added block '{block.CustomName}' to all-events update list.");
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
            terminalSystem?.GetBlocksOfType<IMyTerminalBlock>(blocks, block => block is IMyTextSurface || block is IMyTextSurfaceProvider && block.CustomName.Contains(_config.lcdTagName));
            return blocks;
        }

        private async Task UpdateTextSurfaceAsync(IMyTerminalBlock block, IMyTextSurface textSurface)
        {
            try
            {
                string displayText = GenerateFullScheduleTextForLCD();
                textSurface.ContentType = ContentType.TEXT_AND_IMAGE;
                textSurface.WriteText(displayText, false);
                textSurface.FontColor = Color.White;
                LoggerHelper.DebugLog(Log, _config, $"All-events LCD '{block.CustomName}' updated successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error while updating the all-events LCD: {block.CustomName}");
            }
        }

        private string GenerateFullScheduleTextForLCD()
        {
            if (!_eventManager.Events.Any()) return "No scheduled events.";

            string text = "All Scheduled Events:\n";
            var today = DateTime.Now.Date;

            foreach (var eventItem in _eventManager.Events)
            {
                foreach (var day in eventItem.ActiveDaysOfMonth)
                {
                    var eventDate = new DateTime(today.Year, today.Month, day);
                    var nextStartDate = eventDate.Add(eventItem.StartTime);
                    var nextEndDate = eventDate.Add(eventItem.EndTime);

                    // Sprawdzenie, czy data wydarzenia jest w przyszłości
                    if (nextEndDate > DateTime.Now)
                    {
                        text += $"{eventItem.EventName} - Start: {nextStartDate.ToString("dd/MM/yyyy HH:mm:ss")}, End: {nextEndDate.ToString("dd/MM/yyyy HH:mm:ss")}\n";
                    }
                }
            }
            return text;
        }

    }
}
