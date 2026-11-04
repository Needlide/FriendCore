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

    /// <summary>
    ///     When true, the host seeds the demo dataset on startup. Intended for preview/staging
    ///     deployments only; the seeder itself refuses to run against a non-empty database.
    ///     A one-off <c>--seed-demo</c> command-line flag does the same without the config var.
    /// </summary>
    public bool DemoData { get; set; }

    /// <summary>Shared password for the seeded demo accounts. Defaults to <c>Preview123!</c>.</summary>
    public string DemoPassword { get; set; } = "Preview123!";
}
