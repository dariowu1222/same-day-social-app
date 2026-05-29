namespace SameDaySocialApp.Infrastructure.Email;

public interface IEmailSender
{
    Task SendRegistrationVerificationCodeAsync(string toEmail, string code, CancellationToken cancellationToken);
    Task SendPasswordResetCodeAsync(string toEmail, string code, CancellationToken cancellationToken);
}
