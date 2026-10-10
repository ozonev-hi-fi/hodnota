namespace Hodnota.Infrastructure.Providers.AppleMusic;

// No credentials — only the iTunes storefront to search. See ADR 0016.
public sealed record AppleMusicSettings(string Country);
