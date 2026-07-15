using Microsoft.EntityFrameworkCore;
using Nightfall.Infrastructure.Admin;

namespace Nightfall.Infrastructure.History;

public sealed class NightfallDbContext : DbContext
{
    public NightfallDbContext(DbContextOptions<NightfallDbContext> options) : base(options)
    {
    }

    public DbSet<GameRecord> Games => Set<GameRecord>();
    public DbSet<GamePlayerRecord> GamePlayers => Set<GamePlayerRecord>();
    public DbSet<BotSettingsRecord> BotSettings => Set<BotSettingsRecord>();
    public DbSet<UserProfileRecord> UserProfiles => Set<UserProfileRecord>();
    public DbSet<ChatProfileRecord> ChatProfiles => Set<ChatProfileRecord>();
    public DbSet<OperationalEventRecord> OperationalEvents => Set<OperationalEventRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameRecord>(b =>
        {
            b.ToTable("games");
            b.HasKey(g => g.Id);
            b.HasIndex(g => g.TelegramChatId);
            b.Property(g => g.Result).HasConversion<string>().HasMaxLength(32);
            b.Property(g => g.Status).HasMaxLength(32);
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

        modelBuilder.Entity<BotSettingsRecord>(b => { b.ToTable("bot_settings"); b.HasKey(x => x.Id); });
        modelBuilder.Entity<UserProfileRecord>(b => { b.ToTable("user_profiles"); b.HasKey(x => x.TelegramUserId); b.HasIndex(x => x.Username); });
        modelBuilder.Entity<ChatProfileRecord>(b => { b.ToTable("chat_profiles"); b.HasKey(x => x.TelegramChatId); });
        modelBuilder.Entity<OperationalEventRecord>(b => { b.ToTable("operational_events"); b.HasKey(x => x.Id); b.HasIndex(x => x.CreatedAt); });
    }
}
