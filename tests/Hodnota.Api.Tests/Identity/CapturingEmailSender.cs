using System.Net;

using Hodnota.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;

namespace Hodnota.Api.Tests.Identity;

// Test double for IEmailSender<ApplicationUser> — captures the exact link/code MapIdentityApi
// builds instead of a test trying to reimplement its internal token encoding by hand (that
// encoding is an internal implementation detail, not something to reverse-engineer in tests).
public sealed class CapturingEmailSender : IEmailSender<ApplicationUser>
{
    public string? LastConfirmationLink { get; private set; }

    public string? LastPasswordResetCode { get; private set; }

    public string? LastPasswordResetLink { get; private set; }

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        // MapIdentityApi HTML-encodes this link (it's meant for an HTML email body), turning "&"
        // between query params into "&amp;" — decode it, matching what NoOpEmailSender itself does,
        // so a consumer of this captured value gets a link that's actually usable as-is.
        LastConfirmationLink = WebUtility.HtmlDecode(confirmationLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        LastPasswordResetLink = resetLink;
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        LastPasswordResetCode = resetCode;
        return Task.CompletedTask;
    }
}
