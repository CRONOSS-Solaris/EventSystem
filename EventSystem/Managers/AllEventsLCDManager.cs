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

        public void CacheBlocksForUpdate()
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
            terminalSystem?.GetBlocksOfType<IMyTerminalBlock>(blocks, block => block is IMyTextSurface || block is IMyTextSurfaceProvider && block.CustomName.Contains(_config.ActiveEventsLCDManagerTagName));
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
            var now = DateTime.Now;

            int currentMonth = now.Month;
            int currentYear = now.Year;
            var monthsToCheck = new List<(int year, int month)>
            {
                (currentYear, currentMonth),
                (currentMonth == 12 ? currentYear + 1 : currentYear, (currentMonth % 12) + 1),
                (currentMonth >= 11 ? currentYear + 1 : currentYear, (currentMonth + 1) % 12 + 1)
            };

            var upcomingEvents = new List<(DateTime start, DateTime end, string eventName)>();

            foreach (var eventItem in _eventManager.Events)
            {
                foreach (var (year, month) in monthsToCheck)
                {
                    var nextEventDates = FindAllEventDatesInMonth(eventItem, year, month);
                    foreach (var nextEventDate in nextEventDates)
                    {
                        var nextStartDate = nextEventDate;
                        var nextEndDate = nextEventDate.Date.Add(eventItem.EndTime);
                        upcomingEvents.Add((nextStartDate, nextEndDate, eventItem.EventName));
                    }
                }
            }

            // Sortuj wydarzenia i ogranicz do pierwszych 10
            foreach (var eventInfo in upcomingEvents.OrderBy(e => e.start).Take(10))
            {
                text += $"{eventInfo.eventName} - Start: {eventInfo.start:dd/MM/yyyy HH:mm:ss}, End: {eventInfo.end:dd/MM/yyyy HH:mm:ss}\n";
            }

            return text;
        }

        private IEnumerable<DateTime> FindAllEventDatesInMonth(EventsBase eventItem, int year, int month)
        {
            var dates = new List<DateTime>();
            int daysInMonth = DateTime.DaysInMonth(year, month);

            foreach (var day in eventItem.ActiveDaysOfMonth.OrderBy(d => d))
            {
                if (day <= daysInMonth)
                {
                    var potentialNextDate = new DateTime(year, month, day,
                                                         eventItem.StartTime.Hours, eventItem.StartTime.Minutes, eventItem.StartTime.Seconds);
                    if (potentialNextDate > DateTime.Now)
                    {
                        dates.Add(potentialNextDate);
                    }
                }
            }
            return dates;
        }

    }
}
