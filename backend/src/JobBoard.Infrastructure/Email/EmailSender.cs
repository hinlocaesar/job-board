using JobBoard.Domain.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace JobBoard.Infrastructure.Email;

/// <summary>
/// Email delivery. <c>Log</c> (default) appends every message to
/// <c>logs/outbox.log</c> so verification/reset links are readable in dev;
/// <c>Smtp</c> sends for real. No secrets are ever logged.
/// </summary>
public sealed class EmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailSender> _logger;
    private readonly string _outboxPath;

    public EmailSender(IConfiguration configuration, ILogger<EmailSender> logger, string outboxPath)
    {
        _configuration = configuration;
        _logger = logger;
        _outboxPath = outboxPath;
        Directory.CreateDirectory(Path.GetDirectoryName(_outboxPath)!);
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var provider = _configuration["Email:Provider"] ?? "Log";

        if (string.Equals(provider, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            await SendSmtpAsync(to, subject, htmlBody, cancellationToken);
            return;
        }

        var message = $"""
            ----- {DateTime.UtcNow:O} -----
            To: {to}
            Subject: {subject}

            {htmlBody}

            """;

        await File.AppendAllTextAsync(_outboxPath, message, cancellationToken);
        _logger.LogInformation("Email queued to {Recipient}: {Subject} (see {Outbox})", to, subject, _outboxPath);
    }

    private async Task SendSmtpAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var host = _configuration["Email:Smtp:Host"]
            ?? throw new InvalidOperationException("Email:Smtp:Host is required when Email:Provider=Smtp.");
        var port = _configuration.GetValue("Email:Smtp:Port", 587);
        var user = _configuration["Email:Smtp:Username"];
        var password = _configuration["Email:Smtp:Password"];
        var from = _configuration["Email:Smtp:From"] ?? "no-reply@jobboard.local";

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, SecureSocketOptions.Auto, cancellationToken);
        if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(password))
            await client.AuthenticateAsync(user, password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
