using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TradeVault.Models;

namespace TradeVault.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<TradeImage> TradeImages => Set<TradeImage>();
    public DbSet<TradeTimelineEvent> TradeTimelineEvents => Set<TradeTimelineEvent>();
    public DbSet<TradingGoal> TradingGoals => Set<TradingGoal>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<TradeAiExtraction> TradeAiExtractions => Set<TradeAiExtraction>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Trade>(e =>
        {
            e.HasMany(t => t.Images)
             .WithOne(i => i.Trade)
             .HasForeignKey(i => i.TradeId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(t => t.Timeline)
             .WithOne(ev => ev.Trade)
             .HasForeignKey(ev => ev.TradeId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(t => t.AiExtractions)
             .WithOne(a => a.Trade)
             .HasForeignKey(a => a.TradeId)
             .OnDelete(DeleteBehavior.Cascade);

            // Price level columns
            e.Property(t => t.EntryPrice).HasColumnType("TEXT");
            e.Property(t => t.StopLoss).HasColumnType("TEXT");
            e.Property(t => t.TakeProfit).HasColumnType("TEXT");
            e.Property(t => t.ExitPrice).HasColumnType("TEXT");
            e.Property(t => t.LotSize).HasColumnType("TEXT");
            e.Property(t => t.RiskPercent).HasColumnType("TEXT");
            e.Property(t => t.RewardPercent).HasColumnType("TEXT");
            e.Property(t => t.RRRatio).HasColumnType("TEXT");
            e.Property(t => t.PnL).HasColumnType("TEXT");

            // AI extraction decimal columns
            e.Property(t => t.AiExtractedEntry).HasColumnType("TEXT");
            e.Property(t => t.AiExtractedSL).HasColumnType("TEXT");
            e.Property(t => t.AiExtractedTP).HasColumnType("TEXT");
        });

        builder.Entity<TradingGoal>(e =>
        {
            e.Property(g => g.TargetValue).HasColumnType("TEXT");
            e.Property(g => g.CurrentValue).HasColumnType("TEXT");
        });
    }
}
