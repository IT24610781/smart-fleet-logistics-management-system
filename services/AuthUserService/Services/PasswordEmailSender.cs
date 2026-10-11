using System.Net.Http.Json;

namespace AuthUserService.Services;

public interface IPasswordEmailSender
{
    bool IsConfigured { get; }
    Task SendCodeAsync(string email, string code, CancellationToken cancellationToken);
}

public sealed class PasswordEmailSender(HttpClient client, IConfiguration configuration) : IPasswordEmailSender
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["PasswordEmail:ApiKey"])
        && !string.IsNullOrWhiteSpace(configuration["PasswordEmail:SenderEmail"]);

    public async Task SendCodeAsync(string email, string code, CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new InvalidOperationException("Email verification is not configured.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
        request.Headers.Add("api-key", configuration["PasswordEmail:ApiKey"]);
        request.Content = JsonContent.Create(new
        {
            sender = new { name = "Smart Fleet", email = configuration["PasswordEmail:SenderEmail"] },
            to = new[] { new { email } },
            subject = "Your Smart Fleet password verification code",
            textContent = $"Your Smart Fleet verification code is {code}. It expires in 10 minutes and can only be used once. Never share this code. If you did not request a password change, ignore this email."
        });
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
