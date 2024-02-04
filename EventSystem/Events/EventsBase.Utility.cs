using EventSystem.Utils;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Threading.Tasks;

namespace EventSystem.Events
{
    public abstract partial class EventsBase
    {
        //Removal of nets from the world via event
        private Task RemoveEntityAsync(long gridId)
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
    }
}
