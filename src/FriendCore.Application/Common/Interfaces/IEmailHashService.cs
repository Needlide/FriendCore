namespace FriendCore.Application.Common.Interfaces;

/// <summary>
///     Deterministic email hasher: the same normalized email always yields the same hash,
///     so accounts remain findable by email without storing any address. This must NOT be
///     bcrypt (random salt per hash is unverifiable without the original); HMAC with a
///     server-side pepper gives lookup ability while staying useless without the secret.
/// </summary>
public interface IEmailHashService
{
    string Hash(string normalizedEmail);
}
