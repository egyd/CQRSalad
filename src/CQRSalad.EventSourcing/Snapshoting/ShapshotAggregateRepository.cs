using System;
using System.Threading.Tasks;

namespace CQRSalad.EventSourcing
{
    //todo validate snapshot <--> event stream 
    public class ShapshotAggregateRepository<TAggregate> : AggregateRepository<TAggregate>
        where TAggregate : class, IAggregateRoot, new()
    {
        private readonly IEventStoreAdapter _eventStore;
        private readonly ISnapshotStore _snapshotStore;
        private readonly int _interval;

        public ShapshotAggregateRepository(IEventStoreAdapter eventStore, ISnapshotStore snapshotStore, int interval) 
            : base(eventStore)
        {
            Argument.IsNotNull(eventStore, nameof(eventStore));
            Argument.IsNotNull(snapshotStore, nameof(snapshotStore));
            Argument.NotNegative(interval, nameof(interval));

            _eventStore = eventStore;
            _snapshotStore = snapshotStore;
            _interval = interval;
        }

        public override async Task<TAggregate> LoadById(string aggregateId)
        {
            Argument.StringNotEmpty(aggregateId, nameof(aggregateId));
            
            AggregateSnapshot snapshot = await _snapshotStore.GetSnapshot(aggregateId);
            if (snapshot == null)
            {
                return await base.LoadById(aggregateId);
            }

            var aggregate = new TAggregate { Id = aggregateId};
            aggregate.Restore(snapshot);
            
            var stream = await _eventStore.GetStreamAsync(aggregateId, snapshot.Version + 1); //todo

            aggregate.Version = stream.Version;
            aggregate.Reel(stream.Events);
            aggregate.SetStatus(_eventStore.FirstEventIndex, stream.IsClosed);

            return aggregate;
        }

        public override async Task Save(TAggregate aggregate)
        {
            await base.Save(aggregate);

            int currentVersion = aggregate.Version;
            if (currentVersion > 0 && currentVersion % _interval == 0)
            {
                AggregateSnapshot snapshot = aggregate.MakeSnapshot();
                await _snapshotStore.SaveSnapshot(snapshot);
            }
        }
    }
}