using System.Net;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Hodnota.Infrastructure.Identity;

// Logs the link instead of sending it, until the "Integrate email service" roadmap item lands.
public sealed partial class NoOpEmailSender(ILogger<NoOpEmailSender> logger) : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        // MapIdentityApi HTML-encodes this link (it's meant for an HTML email body), turning "&" between
        // query params into "&amp;" — copy-pasted as-is, the query-string parser reads a param literally
        // named "amp;code" instead of "code". Decode it since this sender's whole point is copy-paste testing.
        LogConfirmationLink(logger, email, WebUtility.HtmlDecode(confirmationLink));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        LogPasswordResetLink(logger, email, WebUtility.HtmlDecode(resetLink));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        LogPasswordResetCode(logger, email, resetCode);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Confirmation link for {Email}: {ConfirmationLink}")]
    private static partial void LogConfirmationLink(ILogger logger, string email, string confirmationLink);

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset link for {Email}: {ResetLink}")]
    private static partial void LogPasswordResetLink(ILogger logger, string email, string resetLink);

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset code for {Email}: {ResetCode}")]
    private static partial void LogPasswordResetCode(ILogger logger, string email, string resetCode);
}
