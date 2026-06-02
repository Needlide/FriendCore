using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FriendCore.Infrastructure.Persistence.Interceptors;

/// <summary>
///     Converts hard deletes into soft deletes by setting <see cref="ISoftDeletable.DeletedAt" />.
///     Global query filters for excluding soft-deleted rows are configured per entity in EF configurations.
/// </summary>
public sealed class SoftDeleteInterceptor(IDateTimeProvider dateTimeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplySoftDelete(eventData.Context, dateTimeProvider);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplySoftDelete(eventData.Context, dateTimeProvider);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ApplySoftDelete(DbContext? context, IDateTimeProvider dateTimeProvider)
    {
        if (context is null)
            return;

        var now = dateTimeProvider.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Deleted || entry.Entity is not ISoftDeletable softDeletable)
                continue;

            entry.State = EntityState.Modified;
            softDeletable.DeletedAt = now;
        }
    }
}