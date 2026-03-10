// Services/EmailService.cs
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ShopAPI.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        // ── Core send method (all emails go through here) ──
        private async Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            var emailConfig = _config.GetSection("Email");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                emailConfig["DisplayName"], emailConfig["Username"]));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();

            try
            {
                await client.ConnectAsync(
                    emailConfig["Host"]!,
                    int.Parse(emailConfig["Port"]!),
                    SecureSocketOptions.StartTls);

                await client.AuthenticateAsync(
                    emailConfig["Username"],
                    emailConfig["Password"]);

                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                // Log the error but don't crash the app
                Console.WriteLine($"❌ Email failed to {toEmail}: {ex.Message}");
                // In production → use ILogger instead of Console.WriteLine
                throw; // re-throw so controller knows email failed
            }
        }

        // ── 1. Email Confirmation ──
        public async Task SendConfirmationEmailAsync(string toEmail, string link)
        {
            var html = $@"
                <div style='font-family:Arial; max-width:600px; margin:auto'>
                    <h2 style='color:#2d86ab'>Welcome to ShopAPI! 🛍️</h2>
                    <p>Thanks for registering. Please confirm your email:</p>
                    <a href='{link}'
                       style='background:#2d86ab; color:white; padding:12px 24px;
                              text-decoration:none; border-radius:4px;
                              display:inline-block; margin:16px 0'>
                        Confirm Email
                    </a>
                    <p style='color:#999'>Link expires in 24 hours.</p>
                    <p style='color:#999'>If you didn't register, ignore this email.</p>
                </div>";

            await SendAsync(toEmail, "Confirm your ShopAPI email", html);
        }

        // ── 2. OTP / MFA Code ──
        public async Task SendOtpEmailAsync(string toEmail, string otp)
        {
            var html = $@"
                <div style='font-family:Arial; max-width:600px; margin:auto'>
                    <h2 style='color:#2d86ab'>Your Login Code 🔐</h2>
                    <p>Use this code to complete your login:</p>
                    <div style='font-size:36px; font-weight:bold;
                                letter-spacing:12px; color:#2d86ab;
                                padding:20px; background:#f0f8ff;
                                border-radius:8px; text-align:center;
                                margin:20px 0'>
                        {otp}
                    </div>
                    <p>⚠️ This code expires in <strong>5 minutes</strong>.</p>
                    <p style='color:#999'>If you didn't request this, 
                       please secure your account.</p>
                </div>";

            await SendAsync(toEmail, "ShopAPI Login Code", html);
        }

        // ── 3. Password Reset ──
        public async Task SendPasswordResetEmailAsync(string toEmail, string link)
        {
            var html = $@"
                <div style='font-family:Arial; max-width:600px; margin:auto'>
                    <h2 style='color:#e74c3c'>Password Reset Request 🔑</h2>
                    <p>Click the button below to reset your password:</p>
                    <a href='{link}'
                       style='background:#e74c3c; color:white; padding:12px 24px;
                              text-decoration:none; border-radius:4px;
                              display:inline-block; margin:16px 0'>
                        Reset Password
                    </a>
                    <p style='color:#999'>Link expires in 1 hour.</p>
                    <p style='color:#999'>If you didn't request this, 
                       ignore this email.</p>
                </div>";

            await SendAsync(toEmail, "ShopAPI Password Reset", html);
        }

        // ── 4. Welcome Email (after confirmation) ──
        public async Task SendWelcomeEmailAsync(string toEmail, string firstName)
        {
            var html = $@"
                <div style='font-family:Arial; max-width:600px; margin:auto'>
                    <h2 style='color:#27ae60'>Welcome aboard, {firstName}! 🎉</h2>
                    <p>Your account is confirmed and ready to use.</p>
                    <p>Start shopping at ShopAPI today!</p>
                </div>";

            await SendAsync(toEmail, "Welcome to ShopAPI!", html);
        }
    }
}