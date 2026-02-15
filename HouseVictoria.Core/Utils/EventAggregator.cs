using System.Collections.Concurrent;
using HouseVictoria.Core.Interfaces;

namespace HouseVictoria.Core.Utils
{
    /// <summary>
    /// Central event aggregator for decoupled communication between components
    /// </summary>
    public class EventAggregator : IEventAggregator
    {
        private readonly ConcurrentDictionary<Type, List<Delegate>> _subscribers = new();

        public void Subscribe<TEvent>(Action<TEvent> handler)
        {
            var eventType = typeof(TEvent);
            _subscribers.AddOrUpdate(eventType,
                _ => new List<Delegate> { handler },
                (_, list) =>
                {
                    lock (list)
                    {
                        list.Add(handler);
                    }
                    return list;
                });
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler)
        {
            var eventType = typeof(TEvent);
            if (_subscribers.TryGetValue(eventType, out var list))
            {
                lock (list)
                {
                    list.Remove(handler);
                }
            }
        }

        public void Publish<TEvent>(TEvent eventData)
        {
            var eventType = typeof(TEvent);
            if (_subscribers.TryGetValue(eventType, out var list))
            {
                Delegate[] handlers;
                lock (list)
                {
                    handlers = list.ToArray();
                }

                foreach (var handler in handlers)
                {
                    try
                    {
                        ((Action<TEvent>)handler)(eventData);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in event handler: {ex.Message}");
                    }
                }
            }
        }

        public void Clear()
        {
            _subscribers.Clear();
        }
    }
}
