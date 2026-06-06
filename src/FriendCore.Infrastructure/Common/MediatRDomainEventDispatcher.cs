using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using FriendCore.Infrastructure.Persistence;
using MediatR;

namespace FriendCore.Infrastructure.Common;

public sealed class MediatRDomainEventDispatcher(AppDbContext context, IPublisher publisher)
    : IDomainEventDispatcher
{
    public async Task DispatchEventsAsync(CancellationToken cancellationToken = default)
    {
        var aggregates = context.ChangeTracker.Entries()
            .Where(entry => entry.Entity is IHasDomainEvents)
            .Select(entry => (IHasDomainEvents)entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        if (aggregates.Count == 0)
            return;

        var domainEvents = aggregates.SelectMany(aggregate => aggregate.DomainEvents).ToList();

        foreach (var domainEvent in domainEvents)
            await publisher.Publish(domainEvent, cancellationToken);

        foreach (var aggregate in aggregates)
            aggregate.ClearDomainEvents();
    }
}
