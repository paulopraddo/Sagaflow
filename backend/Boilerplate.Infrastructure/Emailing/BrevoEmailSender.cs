using System.Net.Http.Json;
using Boilerplate.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Boilerplate.Infrastructure.Emailing;

public sealed class BrevoEmailSender(HttpClient httpClient, IOptions<BrevoOptions> options, ILogger<BrevoEmailSender> logger)
    : IEmailSender
{
    private readonly BrevoOptions _options = options.Value;

    public async Task SendAsync(
        string toEmail, string toName, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            sender = new { name = _options.SenderName, email = _options.SenderEmail },
            to = new[] { new { email = toEmail, name = toName } },
            subject,
            htmlContent = htmlBody,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "smtp/email")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("api-key", _options.ApiKey);
        request.Headers.Add("accept", "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "Falha ao enviar e-mail via Brevo para {ToEmail}: {StatusCode} {Body}",
                toEmail, response.StatusCode, body);
            throw new InvalidOperationException("Não foi possível enviar o e-mail de verificação. Tente novamente mais tarde.");
        }
    }
}
