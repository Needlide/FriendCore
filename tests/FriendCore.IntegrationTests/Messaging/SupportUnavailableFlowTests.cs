using System.Net;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Messaging;

/// <summary>
///     Own class (own database) so no Admin account exists: the support endpoint must fail loudly
///     (500 + Messaging.SupportUnavailable) instead of inventing a conversation.
/// </summary>
public class SupportUnavailableFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    [Fact]
    public async Task Support_WithoutAdmin_ReturnsServerError()
    {
        var scenarios = new AuthScenarios(factory);
        var (user, _) = await scenarios.RegisterVerifiedClientAsync();

        var response = await user.PostAsync("/api/v1/conversations/support", content: null);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Messaging.SupportUnavailable", await response.Content.ReadAsStringAsync());
    }
}
