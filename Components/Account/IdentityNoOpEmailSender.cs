using Microsoft.AspNetCore.Identity;
using TradeVault.Data;

namespace TradeVault.Components.Account;

public static class SimulatedMailbox
{
    public record MailMessage(string To, string Subject, string Body, DateTime SentAt);
    
    private static readonly List<MailMessage> _messages = new();

    public static IReadOnlyList<MailMessage> GetMessages()
    {
        lock (_messages)
        {
            return _messages.ToList();
        }
    }

    public static void Send(string to, string subject, string body)
    {
        lock (_messages)
        {
            _messages.Add(new MailMessage(to, subject, body, DateTime.UtcNow));
            if (_messages.Count > 50)
            {
                _messages.RemoveAt(0);
            }
        }
    }

    public static MailMessage? GetLatestMessageFor(string email)
    {
        lock (_messages)
        {
            return _messages.LastOrDefault(m => m.To.Equals(email, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static MailMessage? GetLatestMessage()
    {
        lock (_messages)
        {
            return _messages.LastOrDefault();
        }
    }
}

internal sealed class IdentityNoOpEmailSender : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var body = $@"
            <div style='font-family: system-ui, sans-serif; color: #d4d4d8; line-height: 1.6;'>
                <p style='margin-bottom: 16px;'>Welcome to <strong>TradeVault</strong>! To get started tracking your confluences and mastering your trading psychology, please verify your email address:</p>
                <div style='margin: 20px 0;'>
                    <a href='{confirmationLink}' style='display: inline-block; background: linear-gradient(135deg, #ffd700, #b8860b); color: #000000; font-weight: 700; padding: 12px 24px; border-radius: 8px; text-decoration: none; text-align: center; box-shadow: 0 4px 15px rgba(212,175,55,0.2);'>Verify Email Address</a>
                </div>
                <p style='font-size: 12px; color: #a1a1aa;'>If you did not create a TradeVault account, you can safely ignore this email.</p>
            </div>";
        SimulatedMailbox.Send(email, "Verify Your TradeVault Account", body);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var body = $@"
            <div style='font-family: system-ui, sans-serif; color: #d4d4d8; line-height: 1.6;'>
                <p style='margin-bottom: 16px;'>We received a request to reset the password for your TradeVault account. Click the button below to set a new password:</p>
                <div style='margin: 20px 0;'>
                    <a href='{resetLink}' style='display: inline-block; background: linear-gradient(135deg, #ffd700, #b8860b); color: #000000; font-weight: 700; padding: 12px 24px; border-radius: 8px; text-decoration: none; text-align: center; box-shadow: 0 4px 15px rgba(212,175,55,0.2);'>Reset Password</a>
                </div>
                <p style='font-size: 12px; color: #a1a1aa;'>If you did not request this, please verify your security settings.</p>
            </div>";
        SimulatedMailbox.Send(email, "Reset Your TradeVault Password", body);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var body = $@"
            <div style='font-family: system-ui, sans-serif; color: #d4d4d8; line-height: 1.6;'>
                <p style='margin-bottom: 16px;'>Your password reset verification code is:</p>
                <div style='margin: 20px 0; background: rgba(255,255,255,0.05); border: 1px solid rgba(212,175,55,0.2); padding: 16px; border-radius: 8px; text-align: center; font-size: 24px; font-weight: 800; font-family: monospace; letter-spacing: 4px; color: #d4af37;'>
                    {resetCode}
                </div>
                <p style='font-size: 12px; color: #a1a1aa;'>Enter this code on the verification page to continue.</p>
            </div>";
        SimulatedMailbox.Send(email, "Reset Your TradeVault Password", body);
        return Task.CompletedTask;
    }
}
