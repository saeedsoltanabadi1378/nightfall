using Microsoft.EntityFrameworkCore;

namespace Nightfall.Infrastructure.History;

public sealed class NightfallDbContext : DbContext
{
    public NightfallDbContext(DbContextOptions<NightfallDbContext> options) : base(options)
    {
    }

    public DbSet<GameRecord> Games => Set<GameRecord>();
    public DbSet<GamePlayerRecord> GamePlayers => Set<GamePlayerRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameRecord>(b =>
        {
            b.ToTable("games");
            b.HasKey(g => g.Id);
            b.HasIndex(g => g.TelegramChatId);
            b.Property(g => g.Result).HasConversion<string>().HasMaxLength(32);
            b.HasMany(g => g.Players)
                .WithOne(p => p.GameRecord)
                .HasForeignKey(p => p.GameRecordId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GamePlayerRecord>(b =>
        {
            b.ToTable("game_players");
            b.HasKey(p => p.Id);
            b.Property(p => p.TelegramUsername).HasMaxLength(256);
            b.Property(p => p.Role).HasConversion<string>().HasMaxLength(32);
        });
    }
}
