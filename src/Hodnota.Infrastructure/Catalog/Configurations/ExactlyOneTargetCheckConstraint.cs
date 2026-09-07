namespace Hodnota.Infrastructure.Catalog.Configurations;

// Shared by every polymorphic nullable-FK entity configuration (ProviderLink, EntityGenre,
// ArtistCredit, SharePage) so the "exactly one of N" SQL is written once, not copy-pasted per entity.
internal static class ExactlyOneTargetCheckConstraint
{
    public static string Sql(params string[] columnNames) =>
        string.Join(" +\n", columnNames.Select(c => $"(CASE WHEN \"{c}\" IS NOT NULL THEN 1 ELSE 0 END)")) + " = 1";
}
