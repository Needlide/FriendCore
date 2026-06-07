using Xunit;

// Each integration test class owns a Testcontainers-backed WebApplicationFactory host.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
