using System.Collections.Concurrent;

namespace Nexus_backend.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private static readonly ConcurrentDictionary<string, (string Otp, DateTime Expiry)> _otpStore = new();

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> SendEmailAsync(string to, string subject, string body)
        {
            // Mock email service - In production, use SendGrid, SMTP, etc.
            // For demo, just log to console
            Console.WriteLine($"=== EMAIL SENT ===");
            Console.WriteLine($"To: {to}");
            Console.WriteLine($"Subject: {subject}");
            Console.WriteLine($"Body: {body}");
            Console.WriteLine($"=================");

            await Task.CompletedTask;
            return true;
        }

        public async Task<string> GenerateOtpAsync(string userId)
        {
            var otp = new Random().Next(100000, 999999).ToString();
            var expiry = DateTime.UtcNow.AddMinutes(10);

            _otpStore[userId] = (otp, expiry);

            await Task.CompletedTask;
            return otp;
        }

        public bool VerifyOtp(string userId, string otp)
        {
            if (_otpStore.TryGetValue(userId, out var stored))
            {
                if (stored.Otp == otp && stored.Expiry > DateTime.UtcNow)
                {
                    _otpStore.TryRemove(userId, out _);
                    return true;
                }
            }
            return false;
        }
    }
}