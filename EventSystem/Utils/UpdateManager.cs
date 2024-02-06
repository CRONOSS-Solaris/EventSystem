using NLog;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace EventSystem.Utils
{
    public class UpdateManager
    {
        public static readonly Logger Log = LogManager.GetLogger("EventSystem/UpdateManager");
        private class SubscriberInfo
        {
            public Action Action { get; }
            public int Priority { get; }
            public Stopwatch Stopwatch { get; }

            public SubscriberInfo(Action action, int priority)
            {
                Action = action;
                Priority = priority;
                Stopwatch = new Stopwatch();
            }
        }

        private readonly ConcurrentDictionary<Action, SubscriberInfo> updateSubscribers = new ConcurrentDictionary<Action, SubscriberInfo>();
        private readonly ConcurrentDictionary<Action, SubscriberInfo> updateSubscribersPerSecond = new ConcurrentDictionary<Action, SubscriberInfo>();
        private Timer minuteTimer;
        private Timer secondTimer;

        public UpdateManager()
        {
            // Zainicjowanie Timerów z Timeout.Infinite zapobiega ich automatycznemu uruchomieniu
            minuteTimer = new Timer(MinuteUpdate, null, Timeout.Infinite, Timeout.Infinite);
            secondTimer = new Timer(SecondUpdate, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void StartTimers()
        {
            // Uruchomienie timerów
            minuteTimer.Change(TimeSpan.Zero, TimeSpan.FromMinutes(1));
            secondTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }

        public void StopTimers()
        {
            // Zatrzymanie timerów
            minuteTimer.Change(Timeout.Infinite, Timeout.Infinite);
            secondTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void ExecuteActions(ConcurrentDictionary<Action, SubscriberInfo> subscribers)
        {
            foreach (var subscriber in subscribers.Values.OrderBy(s => s.Priority))
            {
                subscriber.Stopwatch.Restart();
                subscriber.Action();
                subscriber.Stopwatch.Stop();

                // Logowanie długotrwałych akcji
                if (subscriber.Stopwatch.ElapsedMilliseconds > 100)
                {
                    Log.Info($"Action {subscriber.Action.Method.Name} took {subscriber.Stopwatch.ElapsedMilliseconds} ms.");
                }
            }
        }

        private void MinuteUpdate(object state)
        {
            ExecuteActions(updateSubscribers);
        }

        private void SecondUpdate(object state)
        {
            ExecuteActions(updateSubscribersPerSecond);
        }

        public void AddUpdateSubscriber(Action updateAction, int priority = 0)
        {
            var info = new SubscriberInfo(updateAction, priority);
            updateSubscribers.TryAdd(updateAction, info);
        }

        public void RemoveUpdateSubscriber(Action updateAction)
        {
            updateSubscribers.TryRemove(updateAction, out _);
        }

        public void AddUpdateSubscriberPerSecond(Action updateAction, int priority = 0)
        {
            var info = new SubscriberInfo(updateAction, priority);
            updateSubscribersPerSecond.TryAdd(updateAction, info);
        }

        public void RemoveUpdateSubscriberPerSecond(Action updateAction)
        {
            updateSubscribersPerSecond.TryRemove(updateAction, out _);
        }
    }
}
