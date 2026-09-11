using System.Net;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Hodnota.Infrastructure.Identity;

// Logs the link instead of sending it, until the "Integrate email service" roadmap item lands.
public sealed partial class NoOpEmailSender(ILogger<NoOpEmailSender> logger, IConfiguration configuration) : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var decodedLink = WebUtility.HtmlDecode(confirmationLink);
        LogConfirmationLink(logger, email, ToWebAppConfirmationLink(decodedLink));
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

    private string ToWebAppConfirmationLink(string apiConfirmationLink)
    {
        var query = QueryHelpers.ParseQuery(new Uri(apiConfirmationLink).Query);
        var parameters = new Dictionary<string, string?>
        {
            ["userId"] = query["userId"].ToString(),
            ["code"] = query["code"].ToString(),
        };
        if (query.TryGetValue("changedEmail", out var changedEmail))
        {
            parameters["changedEmail"] = changedEmail.ToString();
        }

        var webAppBaseUrl = configuration[WebAppConfiguration.BaseUrlConfigKey] ?? WebAppConfiguration.DefaultBaseUrl;
        return QueryHelpers.AddQueryString($"{webAppBaseUrl.TrimEnd('/')}/confirm-email", parameters);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Confirmation link for {Email}: {ConfirmationLink}")]
    private static partial void LogConfirmationLink(ILogger logger, string email, string confirmationLink);

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset link for {Email}: {ResetLink}")]
    private static partial void LogPasswordResetLink(ILogger logger, string email, string resetLink);

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset code for {Email}: {ResetCode}")]
    private static partial void LogPasswordResetCode(ILogger logger, string email, string resetCode);
}
