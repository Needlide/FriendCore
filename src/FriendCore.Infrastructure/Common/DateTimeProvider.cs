using FriendCore.Application.Common.Interfaces;

namespace FriendCore.Infrastructure.Common;

public sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}