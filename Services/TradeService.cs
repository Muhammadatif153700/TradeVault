using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using TradeVault.Data;
using TradeVault.Models;

namespace TradeVault.Services;

public class DashboardStats
{
    public int TotalTrades { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Breakevens { get; set; }
    public double WinRate { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal TotalLoss { get; set; }
    public decimal NetPnL { get; set; }
    public decimal NetRR { get; set; }
    public decimal WeeklyRR { get; set; }
    public decimal MonthlyRR { get; set; }
    public decimal AvgRR { get; set; }
    public decimal AvgWin { get; set; }
    public decimal AvgLoss { get; set; }
    public decimal ProfitFactor { get; set; }
    public decimal Expectancy { get; set; }
    public decimal LargestWin { get; set; }
    public decimal LargestLoss { get; set; }
    public int CurrentWinStreak { get; set; }
    public int CurrentLoseStreak { get; set; }
    public decimal MaxDrawdown { get; set; }
    public List<EquityPoint> EquityCurve { get; set; } = [];
    public List<DailyPnL> DailyPnLData { get; set; } = [];
    public List<MonthlyPerformance> MonthlyData { get; set; } = [];
    // Long vs Short
    public int LongTrades { get; set; }
    public int LongWins { get; set; }
    public int ShortTrades { get; set; }
    public int ShortWins { get; set; }
    public double LongWinRate { get; set; }
    public double ShortWinRate { get; set; }
    // Pattern performance
    public List<PatternStat> PatternStats { get; set; } = [];
}

public class EquityPoint { public string Date { get; set; } = ""; public decimal Equity { get; set; } }
public class DailyPnL { public string Date { get; set; } = ""; public decimal PnL { get; set; } }
public class MonthlyPerformance { public string Month { get; set; } = ""; public decimal PnL { get; set; } public double WinRate { get; set; } public int Trades { get; set; } }
public class PatternStat { public string Pattern { get; set; } = ""; public int Total { get; set; } public int Wins { get; set; } public double WinRate { get; set; } }

public class TradeService(IDbContextFactory<ApplicationDbContext> dbFactory, IWebHostEnvironment env)
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void AutoCalcRR(Trade trade)
    {
        // Priority 1: from price levels (Entry / SL / TP)
        if (trade.EntryPrice > 0 && trade.StopLoss > 0 && trade.TakeProfit > 0)
        {
            var risk   = Math.Abs(trade.EntryPrice - trade.StopLoss!.Value);
            var reward = Math.Abs(trade.TakeProfit!.Value - trade.EntryPrice);
            if (risk > 0)
            {
                trade.RRRatio = Math.Round(reward / risk, 2);
                return;
            }
        }
        // Priority 2: from risk/reward percent
        if (trade.RiskPercent > 0 && trade.RewardPercent > 0)
            trade.RRRatio = Math.Round(trade.RewardPercent!.Value / trade.RiskPercent!.Value, 2);
    }

    // ── Trades ────────────────────────────────────────────────────────────────

    public async Task<List<Trade>> GetTradesAsync(string userId, int? limit = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.Trades
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .Include(t => t.Images)
            .Include(t => t.Timeline)
            .OrderByDescending(t => t.Date)
            .AsQueryable();
        if (limit.HasValue) query = query.Take(limit.Value);
        return await query.ToListAsync();
    }

    public async Task<Trade?> GetTradeAsync(Guid id, string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Trades
            .AsNoTracking()
            .Include(t => t.Images)
            .Include(t => t.Timeline)
            .Include(t => t.AiExtractions)
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
    }

    public async Task<Trade> CreateTradeAsync(Trade trade)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        trade.Id = Guid.NewGuid();
        trade.CreatedAt = DateTime.UtcNow;
        trade.UpdatedAt = DateTime.UtcNow;
        AutoCalcRR(trade);
        db.Trades.Add(trade);
        await db.SaveChangesAsync();
        // Add timeline event
        db.TradeTimelineEvents.Add(new TradeTimelineEvent
        {
            TradeId = trade.Id,
            Event = "Trade Logged",
            Description = $"Trade opened at {trade.EntryPrice}",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return trade;
    }

    public async Task UpdateTradeAsync(Trade trade)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        trade.UpdatedAt = DateTime.UtcNow;
        AutoCalcRR(trade);
        db.Trades.Update(trade);
        await db.SaveChangesAsync();
    }

    public async Task DeleteTradeAsync(Guid id, string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var trade = await db.Trades.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (trade != null) { db.Trades.Remove(trade); await db.SaveChangesAsync(); }
    }

    // ── AI Methods ────────────────────────────────────────────────

    public async Task SaveAiExtractionAsync(TradeAiExtraction extraction)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.TradeAiExtractions.Add(extraction);
        await db.SaveChangesAsync();
    }

    public async Task ApplyAiBeforeExtractionAsync(Guid tradeId, BeforeTradeExtraction result)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var trade = await db.Trades.FirstOrDefaultAsync(t => t.Id == tradeId);
        if (trade == null) return;

        trade.AiExtractedPair      = result.Pair;
        trade.AiExtractedTimeframe = result.Timeframe;
        trade.AiExtractedDirection = result.Direction;
        trade.AiExtractedEntry     = result.Entry;
        trade.AiExtractedSL        = result.StopLoss;
        trade.AiExtractedTP        = result.TakeProfit;
        trade.AiEntryConfidence    = result.EntryConfidence;
        trade.AiSlConfidence       = result.SlConfidence;
        trade.AiTpConfidence       = result.TpConfidence;
        trade.AiDetectedPattern    = result.Pattern;
        trade.AiSummary            = result.Summary;
        trade.UpdatedAt            = DateTime.UtcNow;
        db.Trades.Update(trade);
        await db.SaveChangesAsync();
    }

    public async Task ApplyAiAfterAnalysisAsync(Guid tradeId, AfterTradeAnalysis result)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var trade = await db.Trades.FirstOrDefaultAsync(t => t.Id == tradeId);
        if (trade == null) return;

        trade.AiOutcomeDetected   = result.Outcome;
        trade.AiOutcomeConfidence = result.Confidence;
        trade.AiAfterAnalysis     = result.Analysis;
        if (result.ExitPrice.HasValue) trade.ExitPrice = result.ExitPrice;
        if (result.ActualRR.HasValue)  trade.RRRatio   = result.ActualRR;
        trade.UpdatedAt = DateTime.UtcNow;
        db.Trades.Update(trade);
        await db.SaveChangesAsync();
    }

    // ── Images ────────────────────────────────────────────────────────────────

    public async Task<TradeImage> SaveTradeImageAsync(Guid tradeId, string fileName, byte[] bytes, string category)
    {
        // Build directory: wwwroot/uploads/trades/{tradeId}/
        var uploadDir = Path.Combine(env.WebRootPath, "uploads", "trades", tradeId.ToString());
        Directory.CreateDirectory(uploadDir);

        var ext      = Path.GetExtension(fileName).ToLowerInvariant();
        var safeName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadDir, safeName);

        await File.WriteAllBytesAsync(filePath, bytes);

        var relativeUrl = $"/uploads/trades/{tradeId}/{safeName}";

        await using var db = await dbFactory.CreateDbContextAsync();
        var image = new TradeImage
        {
            TradeId    = tradeId,
            Url        = relativeUrl,
            Category   = category,
            CreatedAt  = DateTime.UtcNow
        };
        db.TradeImages.Add(image);
        await db.SaveChangesAsync();
        return image;
    }

    public async Task DeleteTradeImageAsync(Guid imageId, string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var image = await db.TradeImages
            .Include(i => i.Trade)
            .FirstOrDefaultAsync(i => i.Id == imageId && i.Trade.UserId == userId);

        if (image == null) return;

        // Remove file from disk
        var filePath = Path.Combine(env.WebRootPath,
            image.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(filePath)) File.Delete(filePath);

        db.TradeImages.Remove(image);
        await db.SaveChangesAsync();
    }

    // ── Dashboard Stats ───────────────────────────────────────────────────────

    public async Task<DashboardStats> GetDashboardStatsAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var trades = await db.Trades
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.Status != "PENDING")
            .OrderBy(t => t.Date)
            .ToListAsync();

        var stats = new DashboardStats();
        if (!trades.Any()) return stats;

        var wins   = trades.Where(t => t.Status == "WIN").ToList();
        var losses = trades.Where(t => t.Status == "LOSS").ToList();

        stats.TotalTrades = trades.Count;
        stats.Wins        = wins.Count;
        stats.Losses      = losses.Count;
        stats.Breakevens  = trades.Count(t => t.Status is "BREAKEVEN" or "PARTIAL");
        stats.WinRate     = trades.Count > 0 ? Math.Round((double)wins.Count / trades.Count * 100, 1) : 0;

        stats.TotalProfit = wins.Sum(t => t.PnL ?? 0);
        stats.TotalLoss   = Math.Abs(losses.Sum(t => t.PnL ?? 0));
        stats.NetPnL      = trades.Sum(t => t.PnL ?? 0);
        stats.NetRR       = trades.Sum(t => t.RRRatio ?? 0);
        stats.LargestWin  = wins.Any()   ? wins.Max(t => t.PnL ?? 0)               : 0;
        stats.LargestLoss = losses.Any() ? Math.Abs(losses.Min(t => t.PnL ?? 0))   : 0;
        stats.AvgWin      = wins.Any()   ? wins.Average(t => t.PnL ?? 0)            : 0;
        stats.AvgLoss     = losses.Any() ? Math.Abs(losses.Average(t => t.PnL ?? 0)) : 0;
        stats.ProfitFactor = stats.TotalLoss > 0 ? Math.Round(stats.TotalProfit / stats.TotalLoss, 2) : stats.TotalProfit;
        stats.AvgRR       = trades.Any(t => t.RRRatio.HasValue)
            ? trades.Where(t => t.RRRatio.HasValue).Average(t => t.RRRatio!.Value) : 0;
        stats.Expectancy  = stats.WinRate > 0
            ? Math.Round(((decimal)stats.WinRate / 100 * stats.AvgWin)
                       - ((decimal)(100 - stats.WinRate) / 100 * stats.AvgLoss), 2) : 0;

        // Streaks
        int winStreak = 0, loseStreak = 0;
        foreach (var t in trades.OrderByDescending(x => x.Date))
        {
            if      (t.Status == "WIN")  { if (loseStreak == 0) winStreak++;  else break; }
            else if (t.Status == "LOSS") { if (winStreak  == 0) loseStreak++; else break; }
            else break;
        }
        stats.CurrentWinStreak  = winStreak;
        stats.CurrentLoseStreak = loseStreak;

        // Weekly / Monthly RR
        var weekStart  = DateTime.UtcNow.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        stats.WeeklyRR  = trades.Where(t => t.Date >= weekStart).Sum(t => t.RRRatio ?? 0);
        stats.MonthlyRR = trades.Where(t => t.Date >= monthStart).Sum(t => t.RRRatio ?? 0);

        // Long vs Short breakdown
        var longs  = trades.Where(t => t.Direction == "BUY").ToList();
        var shorts = trades.Where(t => t.Direction == "SELL").ToList();
        stats.LongTrades   = longs.Count;
        stats.LongWins     = longs.Count(t => t.Status == "WIN");
        stats.ShortTrades  = shorts.Count;
        stats.ShortWins    = shorts.Count(t => t.Status == "WIN");
        stats.LongWinRate  = longs.Count  > 0 ? Math.Round((double)stats.LongWins  / longs.Count  * 100, 1) : 0;
        stats.ShortWinRate = shorts.Count > 0 ? Math.Round((double)stats.ShortWins / shorts.Count * 100, 1) : 0;

        // Pattern performance
        stats.PatternStats = trades
            .Where(t => !string.IsNullOrEmpty(t.AiDetectedPattern))
            .GroupBy(t => t.AiDetectedPattern!)
            .Select(g => new PatternStat
            {
                Pattern = g.Key,
                Total   = g.Count(),
                Wins    = g.Count(t => t.Status == "WIN"),
                WinRate = g.Count() > 0 ? Math.Round((double)g.Count(t => t.Status == "WIN") / g.Count() * 100, 1) : 0
            })
            .OrderByDescending(p => p.Total)
            .ToList();

        // Equity Curve
        decimal equity = 10000;
        stats.EquityCurve = trades.Select(t =>
        {
            equity += t.PnL ?? 0;
            return new EquityPoint { Date = t.Date.ToString("MMM dd"), Equity = equity };
        }).ToList();

        // Max Drawdown
        decimal peak = 10000, maxDD = 0; equity = 10000;
        foreach (var t in trades)
        {
            equity += t.PnL ?? 0;
            if (equity > peak) peak = equity;
            var dd = peak > 0 ? (peak - equity) / peak * 100 : 0;
            if (dd > maxDD) maxDD = dd;
        }
        stats.MaxDrawdown = Math.Round(maxDD, 2);

        // Daily PnL
        stats.DailyPnLData = trades
            .GroupBy(t => t.Date.Date)
            .Select(g => new DailyPnL { Date = g.Key.ToString("MMM dd"), PnL = g.Sum(t => t.PnL ?? 0) })
            .OrderBy(d => d.Date)
            .ToList();

        // Monthly Performance
        stats.MonthlyData = trades
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .Select(g => new MonthlyPerformance
            {
                Month   = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yy"),
                PnL     = g.Sum(t => t.PnL ?? 0),
                WinRate = g.Any() ? Math.Round((double)g.Count(t => t.Status == "WIN") / g.Count() * 100, 1) : 0,
                Trades  = g.Count()
            })
            .OrderBy(m => m.Month)
            .ToList();

        return stats;
    }

    // ── Goals ─────────────────────────────────────────────────────────────────

    public async Task<List<TradingGoal>> GetGoalsAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.TradingGoals.AsNoTracking().Where(g => g.UserId == userId).OrderBy(g => g.CreatedAt).ToListAsync();
    }

    public async Task<TradingGoal> CreateGoalAsync(TradingGoal goal)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        goal.Id = Guid.NewGuid();
        goal.CreatedAt = DateTime.UtcNow;
        db.TradingGoals.Add(goal);
        await db.SaveChangesAsync();
        return goal;
    }

    public async Task UpdateGoalAsync(TradingGoal goal)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.TradingGoals.Update(goal);
        await db.SaveChangesAsync();
    }

    public async Task DeleteGoalAsync(Guid id, string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var g = await db.TradingGoals.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (g != null) { db.TradingGoals.Remove(g); await db.SaveChangesAsync(); }
    }

    // ── Journal ───────────────────────────────────────────────────────────────

    public async Task<List<JournalEntry>> GetJournalEntriesAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.JournalEntries.AsNoTracking().Where(j => j.UserId == userId).OrderByDescending(j => j.Date).ToListAsync();
    }

    public async Task<JournalEntry> CreateJournalEntryAsync(JournalEntry entry)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        entry.Id = Guid.NewGuid();
        entry.CreatedAt = DateTime.UtcNow;
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    public async Task UpdateJournalEntryAsync(JournalEntry entry)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.JournalEntries.Update(entry);
        await db.SaveChangesAsync();
    }

    // ── Achievements ──────────────────────────────────────────────────────────

    public async Task<List<UserAchievement>> GetAchievementsAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.UserAchievements.AsNoTracking().Where(a => a.UserId == userId).ToListAsync();
    }
}
