namespace FriendCore.Application.Common.Options;

/// <summary>
///     Bootstrap configuration. <see cref="AdminEmails" /> lists email addresses that are granted
///     the Admin role when they register — the only way to obtain the first admin on a fresh
///     database without manual SQL. Empty by default (dev/test): registration behaves normally.
/// </summary>
public sealed class SeedingOptions
{
    public const string SectionName = "Seeding";

    public string[] AdminEmails { get; set; } = [];
}
