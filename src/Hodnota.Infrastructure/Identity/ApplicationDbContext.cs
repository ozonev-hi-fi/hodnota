using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Hodnota.Infrastructure.Identity;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Artist> Artists => Set<Artist>();

    public DbSet<Release> Releases => Set<Release>();

    public DbSet<Track> Tracks => Set<Track>();

    public DbSet<ReleaseTrack> ReleaseTracks => Set<ReleaseTrack>();

    public DbSet<ArtistCredit> ArtistCredits => Set<ArtistCredit>();

    public DbSet<Platform> Platforms => Set<Platform>();

    public DbSet<ProviderLink> ProviderLinks => Set<ProviderLink>();

    public DbSet<Genre> Genres => Set<Genre>();

    public DbSet<EntityGenre> EntityGenres => Set<EntityGenre>();

    public DbSet<RecordLabel> RecordLabels => Set<RecordLabel>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();
        configurationBuilder.Properties<Uri>().HaveConversion<UriValueConverter>();
    }
}
