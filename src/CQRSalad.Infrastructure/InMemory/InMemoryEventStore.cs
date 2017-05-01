using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CQRSalad.EventSourcing;

namespace CQRSalad.Infrastructure
{
    public class InMemoryEventStore : IEventStoreAdapter
    {
        private readonly ConcurrentDictionary<string, EventStream> _streams = new ConcurrentDictionary<string, EventStream>();
        
        public async Task<EventStream> GetStreamAsync(string streamId)
        {
            Argument.IsNotNull(streamId, nameof(streamId));

            var stream = _streams.GetOrAdd(streamId, GetEmptyStream);
            return await Task.FromResult(stream);
        }

        public async Task<EventStream> GetStreamAsync(string streamId, int fromVersion, int toVersion = -1)
        {
            Argument.IsNotNull(streamId, nameof(streamId));
            Argument.NotNegative(fromVersion, nameof(fromVersion));

            EventStream stream;
            bool isStreamExists = _streams.TryGetValue(streamId, out stream);
            if (!isStreamExists || stream == null)
            {
                return null;
            }

            int takeCount = toVersion > 0 ? toVersion : stream.Events.Count();
            List<IEvent> part = stream.Events.Skip(fromVersion - 1).Take(takeCount).ToList();
            stream.Events = part;
            return await Task.FromResult(stream);
        }

        public async Task AppendEventsAsync(string streamId, IEnumerable<IEvent> events, int expectedVersion, bool isEndOfStream = false)
        {
            Argument.IsNotNull(streamId, nameof(streamId));
            Argument.ElementsNotNull(events);

            var stream = _streams.GetOrAdd(streamId, GetEmptyStream);

            stream.Events.ToList().AddRange(events);
            stream.Version++;
            stream.IsEnded = isEndOfStream;

            await Task.CompletedTask;
        }

        private static EventStream GetEmptyStream(string key)
        {
            return new EventStream
            {
                StreamId = key,
                Version = 0,
                IsEnded = false,
                Events = new List<IEvent>()
            };
        }
    }
}