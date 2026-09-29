using System.Threading.Channels;

namespace AuthUserService.Services;

// Delivery happens outside the request so public recovery response time does not
// disclose whether an account exists. The bounded queue contains no durable secrets.
public sealed class PasswordEmailQueue(IHttpClientFactory clients, IConfiguration config,
    ILogger<PasswordEmailQueue> logger) : BackgroundService, IPasswordEmailSender
{
    private readonly Channel<(string Email, string Code, DateTime Expires)> queue =
        Channel.CreateBounded<(string, string, DateTime)>(100);
    public bool IsConfigured => !string.IsNullOrWhiteSpace(config["PasswordEmail:ApiKey"])
        && !string.IsNullOrWhiteSpace(config["PasswordEmail:SenderEmail"]);
    public Task SendCodeAsync(string email, string code, CancellationToken cancellationToken)
    {
        if (!queue.Writer.TryWrite((email, code, DateTime.UtcNow.AddMinutes(10))))
            throw new HttpRequestException("Email queue unavailable.");
        return Task.CompletedTask;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
        {
            if (message.Expires < DateTime.UtcNow) continue;
            try
            {
                var sender = new PasswordEmailSender(clients.CreateClient("PasswordEmail"), config);
                await sender.SendCodeAsync(message.Email, message.Code, stoppingToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                logger.LogWarning("Password verification email delivery failed. Check the email provider configuration and quota.");
            }
        }
    }
}
