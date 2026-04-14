namespace Nexus_backend.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string to, string subject, string body);
        Task<string> GenerateOtpAsync(string userId);
        bool VerifyOtp(string userId, string otp);
    }
}