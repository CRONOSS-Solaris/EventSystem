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

namespace EventSystem.Events
{
    public abstract partial class EventsBase
    {
        protected ConcurrentDictionary<long, bool> safezoneEntityIds = new ConcurrentDictionary<long, bool>();

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

        /// <summary>
        /// Creates a safe zone with customizable settings around a specified center point. This method can be used
        /// to dynamically generate safe zones during events, offering protection or restrictions in designated areas.
        /// </summary>
        /// <param name="center">The center point of the safe zone.</param>
        /// <param name="radius">The radius of the safe zone if it's a sphere, or half the edge length if it's a cube.</param>
        /// <param name="shape">The shape of the safe zone, either Sphere or Cube.</param>
        /// <param name="isEnabled">Determines whether the safe zone is enabled upon creation. Default is true.</param>
        /// <param name="accessTypePlayers">Defines the access type for players. Default is Blacklist.</param>
        /// <param name="accessTypeFactions">Defines the access type for factions. Default is Blacklist.</param>
        /// <param name="accessTypeGrids">Defines the access type for grids. Default is Whitelist.</param>
        /// <param name="accessTypeFloatingObjects">Defines the access type for floating objects. Default is Blacklist.</param>
        /// <param name="allowedActions">Specifies the actions that are allowed within the safe zone. Default is All.</param>
        /// <param name="modelColor">The color of the safe zone model. Pass null to use the default red color.</param>
        /// <param name="texture">The texture of the safe zone. Default is "SafeZone_Texture_Restricted".</param>
        /// <param name="isVisible">Determines whether the safe zone is visible. Default is true.</param>
        /// <param name="displayName">The display name of the safe zone. If empty or null, a generic name will be used.</param>
        /// Example:
        /// ZoneShape shape = _config.WarZoneGridSettings.Shape == EventsBase.ZoneShape.Sphere ? ZoneShape.Sphere : ZoneShape.Cube;
        /// CreateSafeZone(sphereCenter, Radius, shape, true, MySafeZoneAccess.Blacklist, MySafeZoneAccess.Blacklist, MySafeZoneAccess.Whitelist, MySafeZoneAccess.Blacklist, MySafeZoneAction.Damage | MySafeZoneAction.Shooting, new SerializableVector3(0f, 0f, 1f), "SafeZone_Texture_Restricted", true, $"{EventName}SafeZone");
        protected void CreateSafeZone(Vector3D center, double radius, ZoneShape shape, bool isEnabled = true, MySafeZoneAccess accessTypePlayers = MySafeZoneAccess.Blacklist, MySafeZoneAccess accessTypeFactions = MySafeZoneAccess.Blacklist, MySafeZoneAccess accessTypeGrids = MySafeZoneAccess.Whitelist, MySafeZoneAccess accessTypeFloatingObjects = MySafeZoneAccess.Blacklist, MySafeZoneAction allowedActions = MySafeZoneAction.All, SerializableVector3? modelColor = null, string texture = "SafeZone_Texture_Default", bool isVisible = true, string displayName = "")
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                try
                {
                    SerializableVector3 color = modelColor ?? new SerializableVector3(0f, 0f, 0f);

                    var safezoneDefinition = new MyObjectBuilder_SafeZone
                    {
                        PositionAndOrientation = new MyPositionAndOrientation(center, Vector3.Forward, Vector3.Up),
                        Enabled = isEnabled,
                        AccessTypePlayers = accessTypePlayers,
                        AccessTypeFactions = accessTypeFactions,
                        AccessTypeGrids = accessTypeGrids,
                        AccessTypeFloatingObjects = accessTypeFloatingObjects,
                        AllowedActions = allowedActions,
                        ModelColor = color,
                        Texture = texture,
                        IsVisible = isVisible,
                        DisplayName = displayName
                    };

                    // Ustawienie rozmiaru strefy
                    if (shape == ZoneShape.Sphere)
                    {
                        safezoneDefinition.Shape = MySafeZoneShape.Sphere;
                        safezoneDefinition.Radius = (float)radius;
                    }
                    else // Dla boxa
                    {
                        safezoneDefinition.Shape = MySafeZoneShape.Box;
                        safezoneDefinition.Size = new Vector3((float)radius, (float)radius, (float)radius);
                    }

                    var safezoneEntity = MyAPIGateway.Entities.CreateFromObjectBuilder(safezoneDefinition);
                    if (safezoneEntity != null)
                    {
                        MyAPIGateway.Entities.AddEntity(safezoneEntity, true);
                        safezoneEntityIds.TryAdd(safezoneEntity.EntityId, true);
                    }
                    else
                    {
                        Log.Error("Failed to create safezone. Entity creation returned null.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Exception occurred while creating safezone: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Removes all safe zones previously created by this event. This method should be called
        /// when the event ends or when the safe zones are no longer needed, to clean up all
        /// safe zone entities and release resources.
        /// </summary>
        protected void RemoveSafeZone()
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                var safezoneIds = safezoneEntityIds.Keys.ToList();

                foreach (var safezoneId in safezoneIds)
                {
                    if (MyAPIGateway.Entities.TryGetEntityById(safezoneId, out var safezoneEntity))
                    {
                        safezoneEntity.Close();
                        MyAPIGateway.Entities.RemoveEntity(safezoneEntity);

                        safezoneEntityIds.TryRemove(safezoneId, out _);
                    }
                }
            });
        }
    }
}
