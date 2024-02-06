using EventSystem.Utils;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Threading.Tasks;

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
        /// such as checking conditions or updating states less frequently.
        /// </summary>
        /// <param name="updateAction">The action to be executed on each update. 
        /// This action should contain the logic that needs to be executed on a roughly per-minute basis.</param>
        protected void SubscribeToUpdate(Action updateAction)
        {
            EventSystemMain.Instance.AddUpdateSubscriber(updateAction);
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
            EventSystemMain.Instance.RemoveUpdateSubscriber(updateAction);
        }

        /// <summary>
        /// Subscribes a method to be executed on each update, approximately once per second. 
        /// This method is suitable for more frequent updates, such as real-time checks or 
        /// rapid state changes that require close to real-time responsiveness.
        /// </summary>
        /// <param name="updateAction">The action to be executed on each update. 
        /// This action should contain the logic that needs to be executed on a roughly per-second basis.</param>
        protected void SubscribeToUpdatePerSecond(Action updateAction)
        {
            EventSystemMain.Instance.AddUpdateSubscriberPerSecond(updateAction);
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
            EventSystemMain.Instance.RemoveUpdateSubscriberPerSecond(updateAction);
        }


    }
}
