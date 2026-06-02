namespace FriendCore.Domain.Common;

public interface IAuditable
{
    DateTimeOffset UpdatedAt { get; set; }
}