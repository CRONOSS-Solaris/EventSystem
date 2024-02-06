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
        /// Subscribes a method to be called on each frame update. This method should be used to perform
        /// regular updates needed by the event, such as checking conditions or updating state.
        /// </summary>
        /// <param name="updateAction">The action to be called on each update. This action should contain
        /// the logic that needs to be executed regularly.</param>
        protected void SubscribeToUpdate(Action updateAction)
        {
            EventSystemMain.Instance.AddUpdateSubscriber(updateAction);
        }

        /// <summary>
        /// Unsubscribes a previously subscribed method from being called on each frame update. Use this method
        /// to clean up any subscriptions when they are no longer needed, such as when the event ends or specific
        /// conditions are met.
        /// </summary>
        /// <param name="updateAction">The action to be unsubscribed from the update calls. This should be the same
        /// action that was previously subscribed using SubscribeToUpdate.</param>
        protected void UnsubscribeFromUpdate(Action updateAction)
        {
            EventSystemMain.Instance.RemoveUpdateSubscriber(updateAction);
        }

    }
}
