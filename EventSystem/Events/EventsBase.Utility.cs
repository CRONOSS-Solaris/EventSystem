using EventSystem.Utils;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using VRage.Game.ObjectBuilders.Components;
using VRage;
using VRageMath;
using System.Linq;
using Newtonsoft.Json;
using System.IO;

namespace EventSystem.Events
{
    public abstract partial class EventsBase
    {

        /// <summary>
        /// Asynchronously removes an entity from the world via an event.
        /// </summary>
        /// <param name="gridId">The EntityId of the grid to be removed.</param>
        /// <returns>A task representing the asynchronous operation. True if the removal was successful, otherwise false.</returns>
        protected Task RemoveEntityAsync(long gridId)
        {
            var tcs = new TaskCompletionSource<bool>();

            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                try
                {
                    var entity = MyAPIGateway.Entities.GetEntityById(gridId);
                    var grid = entity as MyCubeGrid;
                    if (grid != null)
                    {
                        grid.Close();
                        MyAPIGateway.Entities.RemoveEntity(entity);
                        LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Grid with EntityId: {gridId} closed and removed successfully.");
                        tcs.SetResult(true);
                    }
                    else
                    {
                        Log.Warn($"Grid with EntityId: {gridId} not found.");
                        tcs.SetResult(false);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error removing grid.");
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }

        /// <summary>
        /// Subscribes a method to be executed on each update, approximately once per minute.
        /// This method should be used for regular updates that do not require high frequency,
        /// such as checking conditions or updating states less frequently. The subscribed actions
        /// are executed in order of their subscription priority.
        /// </summary>
        /// <param name="updateAction">The action to be executed on each update.
        /// This action should contain the logic that needs to be executed on a roughly per-minute basis.</param>
        /// <param name="priority">The priority of the action. Lower numbers indicate higher priority.</param>
        protected void SubscribeToUpdate(Action updateAction, int priority = 0)
        {
            EventSystemMain.Instance.UpdateManager.AddUpdateSubscriber(updateAction, priority);
        }

        /// <summary>
        /// Unsubscribes a previously subscribed method from the per-minute update calls.
        /// Use this method to clean up any subscriptions when they are no longer needed,
        /// such as when the event ends or specific conditions are met.
        /// </summary>
        /// <param name="updateAction">The action to unsubscribe from the per-minute update calls.
        /// This should be the same action that was previously subscribed using SubscribeToUpdate.</param>
        protected void UnsubscribeFromUpdate(Action updateAction)
        {
            EventSystemMain.Instance.UpdateManager.RemoveUpdateSubscriber(updateAction);
        }

        /// <summary>
        /// Subscribes a method to be executed on each update, approximately once per second.
        /// This method is suitable for more frequent updates, such as real-time checks or
        /// rapid state changes that require close to real-time responsiveness. The subscribed actions
        /// are executed in order of their subscription priority.
        /// </summary>
        /// <param name="updateAction">The action to be executed on each update.
        /// This action should contain the logic that needs to be executed on a roughly per-second basis.</param>
        /// <param name="priority">The priority of the action. Lower numbers indicate higher priority.</param>
        protected void SubscribeToUpdatePerSecond(Action updateAction, int priority = 0)
        {
            EventSystemMain.Instance.UpdateManager.AddUpdateSubscriberPerSecond(updateAction, priority);
        }

        /// <summary>
        /// Unsubscribes a previously subscribed method from the per-second update calls.
        /// Use this method to clean up any subscriptions when they are no longer needed,
        /// such as when the event ends or specific conditions are met.
        /// </summary>
        /// <param name="updateAction">The action to unsubscribe from the per-second update calls.
        /// This should be the same action that was previously subscribed using SubscribeToUpdatePerSecond.</param>
        protected void UnsubscribeFromUpdatePerSecond(Action updateAction)
        {
            EventSystemMain.Instance.UpdateManager.RemoveUpdateSubscriberPerSecond(updateAction);
        }


        //WarZone Event

        /// <summary>
        /// Defines the type of coordinate randomization used in war zone events, specifying how coordinates are randomized within space.
        /// </summary>
        public enum CoordinateRandomizationType
        {
            Line,
            Sphere,
            Cube
        }

        /// <summary>
        /// Specifies the shape of a zone used in war zone events, defining the physical area where player interactions can occur.
        /// </summary>
        public enum ZoneShape
        {
            Sphere,
            Cube
        }



        /// <summary>
        /// Represents a set of coordinates within a three-dimensional space.
        /// </summary>
        public class AreaCoords
        {
            /// <summary>
            /// Gets or sets the X-coordinate.
            /// </summary>
            public double X { get; set; }

            /// <summary>
            /// Gets or sets the Y-coordinate.
            /// </summary>
            public double Y { get; set; }

            /// <summary>
            /// Gets or sets the Z-coordinate.
            /// </summary>
            public double Z { get; set; }

            /// <summary>
            /// Initializes a new instance of the <see cref="AreaCoords"/> class.
            /// </summary>
            public AreaCoords() { }

            /// <summary>
            /// Initializes a new instance of the <see cref="AreaCoords"/> class with specific coordinates.
            /// </summary>
            /// <param name="x">The X-coordinate.</param>
            /// <param name="y">The Y-coordinate.</param>
            /// <param name="z">The Z-coordinate.</param>
            public AreaCoords(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }

        private string _configPath = Path.Combine("EventSystem", "Config", "EntityIDs.json");

        private void SaveEntityIds()
        {
            var ids = new
            {
                SpawnedGrids = SpawnedGridsEntityIds.Keys.ToList(),
                SafeZones = safezoneEntityIds.Keys.ToList()
            };

            var json = JsonConvert.SerializeObject(ids, Formatting.Indented);
            File.WriteAllText(_configPath, json);
        }

        private void LoadEntityIds()
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var ids = JsonConvert.DeserializeObject<dynamic>(json);
                foreach (long id in ids.SpawnedGrids)
                {
                    SpawnedGridsEntityIds.TryAdd(id, true);
                }
                foreach (long id in ids.SafeZones)
                {
                    safezoneEntityIds.TryAdd(id, true);
                }
            }
        }

        public async Task ServerStartCleanup()
        {
            LoadEntityIds();
            await CleanupGrids();
            RemoveSafeZone();
        }
    }
}
