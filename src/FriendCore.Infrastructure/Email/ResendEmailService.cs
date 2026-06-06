using System.Net.Http.Json;
using System.Text.Json;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FriendCore.Infrastructure.Email;

public sealed class ResendEmailService(
    HttpClient httpClient,
    IOptions<EmailOptions> emailOptions,
    ILogger<ResendEmailService> logger) : IEmailService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var options = emailOptions.Value;
        if (string.IsNullOrWhiteSpace(options.ResendApiKey))
        {
            logger.LogWarning(
                "Email not sent (Resend API key missing). To={To}, Subject={Subject}",
                message.To,
                message.Subject);
            return;
        }

        var payload = new
        {
            from = $"{options.FromName} <{options.FromAddress}>",
            to = new[] { message.To },
            subject = message.Subject,
            html = message.HtmlBody,
            text = message.PlainTextBody
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Add("Authorization", $"Bearer {options.ResendApiKey}");
        request.Content = JsonContent.Create(payload, options: JsonOptions);

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "Resend API failed with status {StatusCode}: {Body}",
                (int)response.StatusCode,
                body);
            throw new InvalidOperationException("Failed to send email via Resend.");
        }

        logger.LogInformation("Email sent via Resend to {To} with subject {Subject}", message.To, message.Subject);
    }
}
