using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TradeVault.Data;
using TradeVault.Models;

namespace TradeVault.Services;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        // Ensure database directory exists before SQLite tries to create/open file
        var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "Data");
        Directory.CreateDirectory(dbPath);

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await db.Database.EnsureCreatedAsync();

        // Skip if already seeded
        if (await db.Trades.AnyAsync()) return;

        // Create demo user
        var user = new ApplicationUser { UserName = "demo@tradevault.com", Email = "demo@tradevault.com", EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, "Demo@1234!");
        if (!result.Succeeded) return;

        var userId = user.Id;
        var rng = new Random(42);

        // Achievements
        await db.UserAchievements.AddRangeAsync(new[]
        {
            new UserAchievement { UserId = userId, Badge = "🚀 First Trade", Description = "Logged your first trade", EarnedAt = DateTime.UtcNow.AddDays(-30) },
            new UserAchievement { UserId = userId, Badge = "🔥 10 RR Month", Description = "Achieved 10+ RR in a single month", EarnedAt = DateTime.UtcNow.AddDays(-15) },
            new UserAchievement { UserId = userId, Badge = "🎯 Streak Master", Description = "5 consecutive winning trades", EarnedAt = DateTime.UtcNow.AddDays(-10) },
            new UserAchievement { UserId = userId, Badge = "🧘 Disciplined", Description = "No rule breaks for 10 trades", EarnedAt = DateTime.UtcNow.AddDays(-5) },
        });

        // Goals
        await db.TradingGoals.AddRangeAsync(new[]
        {
            new TradingGoal { UserId = userId, Title = "15 RR This Month", Description = "Target 15 cumulative RR", TargetValue = 15, CurrentValue = 18.2m, Metric = "RR", TargetDate = DateTime.UtcNow.AddDays(20), Status = "COMPLETED" },
            new TradingGoal { UserId = userId, Title = "90% Discipline Score", Description = "Max 10% rule breaks", TargetValue = 90, CurrentValue = 92.5m, Metric = "Discipline", TargetDate = DateTime.UtcNow.AddDays(20), Status = "ACTIVE" },
            new TradingGoal { UserId = userId, Title = "Max 2 Trades Per Day", Description = "Avoid overtrading", TargetValue = 2, CurrentValue = 1.1m, Metric = "TradesLimit", TargetDate = DateTime.UtcNow.AddDays(20), Status = "ACTIVE" },
        });

        // Journal Entries
        await db.JournalEntries.AddRangeAsync(new[]
        {
            new JournalEntry { UserId = userId, Type = "DAILY", Date = DateTime.UtcNow.AddDays(-3), Notes = "London session was choppy. Waited for NY open — EURUSD gave a clean expansion setup.", Psychology = "Felt calm and patient. No FOMO today.", Mistakes = "None, followed rules perfectly.", Lessons = "Patience pays every time.", Wins = "Caught the EURUSD expansion leg perfectly." },
            new JournalEntry { UserId = userId, Type = "DAILY", Date = DateTime.UtcNow.AddDays(-1), Notes = "CPI day — XAUUSD swept the liquidity pool and reversed sharply. Closed early due to momentum stalling.", Psychology = "Slight anxiety from news volatility but stayed disciplined.", Mistakes = "Closed slightly before target due to fear.", Lessons = "Trust the setup. Stop watching M1 chart." },
            new JournalEntry { UserId = userId, Type = "WEEKLY", Date = DateTime.UtcNow.AddDays(-7), Notes = "Weekly: +4.2% / +5.5R. Excellent execution week. Avoided trading after 12PM EST.", Psychology = "Very stable. Taking breaks after losses works.", Mistakes = "1 revenge trade on Thursday — minor loss.", Lessons = "Shut down the terminal after a loss.", Wins = "Great EURUSD swing setup execution." },
        });

        // Trades
        string[] pairs = ["EURUSD", "XAUUSD", "BTCUSDT", "NAS100", "US30"];
        string[] markets = ["Forex", "Commodities", "Crypto", "Indices", "Indices"];
        string[] sessions = ["London", "New York", "Asian"];
        string[] timeframes = ["M15", "H1", "H4"];
        string[] strategies = ["SMC Order Block", "Liquidity Grab Sweep", "Fair Value Gap Fill", "Support/Resistance Bounce", "ICT Killzone Entry"];
        string[] emotions = ["Calm", "Focused", "Anxious", "Confident", "Neutral"];
        string[] statuses = ["WIN", "WIN", "WIN", "LOSS", "LOSS", "BREAKEVEN", "PARTIAL"];

        var tradesData = new List<Trade>();
        for (int i = 35; i >= 1; i--)
        {
            var date = DateTime.UtcNow.AddDays(-i).AddHours(rng.Next(8, 17));
            int pairIdx = rng.Next(pairs.Length);
            var pair = pairs[pairIdx];
            var market = markets[pairIdx];
            var direction = rng.Next(2) == 0 ? "BUY" : "SELL";
            var session = sessions[rng.Next(sessions.Length)];
            var tf = timeframes[rng.Next(timeframes.Length)];
            var strategy = strategies[rng.Next(strategies.Length)];
            var emotion = emotions[rng.Next(emotions.Length)];
            var status = statuses[rng.Next(statuses.Length)];
            var confidence = rng.Next(6, 11);

            decimal entryPrice = pair switch { "EURUSD" => 1.0800m + (decimal)rng.NextDouble() * 0.02m, "XAUUSD" => 2300m + (decimal)rng.NextDouble() * 80m, "BTCUSDT" => 60000m + (decimal)rng.NextDouble() * 5000m, "NAS100" => 19000m + (decimal)rng.NextDouble() * 500m, _ => 38000m + (decimal)rng.NextDouble() * 400m };
            decimal rrRatio = status switch { "WIN" => 2.0m + (decimal)rng.NextDouble() * 2.5m, "LOSS" => -1.0m, "BREAKEVEN" => 0m, "PARTIAL" => 0.5m + (decimal)rng.NextDouble(), _ => 0m };
            decimal pnl = rrRatio * 100m; // $100 risk per trade

            var trade = new Trade
            {
                UserId = userId, Title = $"{strategy} on {pair}", Pair = pair, MarketType = market, Direction = direction,
                EntryPrice = Math.Round(entryPrice, 4), StopLoss = Math.Round(entryPrice - (direction == "BUY" ? 0.001m : -0.001m) * entryPrice / 10, 4),
                TakeProfit = Math.Round(entryPrice + (direction == "BUY" ? 0.002m : -0.002m) * entryPrice / 10, 4),
                ExitPrice = Math.Round(entryPrice + (decimal)rng.NextDouble() * 0.01m, 4),
                LotSize = pair == "BTCUSDT" ? 0.5m : pair == "XAUUSD" ? 2m : 5m,
                RiskPercent = 1.0m, RewardPercent = Math.Abs(rrRatio), RRRatio = rrRatio, PnL = Math.Round(pnl, 2), Status = status,
                Date = date, Session = session, NewsTrade = rng.Next(5) == 0, Strategy = strategy, Timeframe = tf,
                EmotionsBefore = emotion, Confidence = confidence,
                Mistakes = status == "LOSS" ? "Entered too early before structure confirmation" : "None",
                Lessons = status == "LOSS" ? "Wait for candle close before entry" : "Trust the setup, stay patient",
                Notes = $"Executed during {session} session. Clear displacement on {tf} with bullish/bearish order flow confirmed.",
                Tags = $"{strategy.Split(' ')[0]},{direction},{tf}",
                CreatedAt = date, UpdatedAt = date
            };
            tradesData.Add(trade);
        }

        await db.Trades.AddRangeAsync(tradesData);
        await db.SaveChangesAsync();

        // Add images and timeline events
        foreach (var trade in tradesData)
        {
            await db.TradeImages.AddRangeAsync(
                new TradeImage { TradeId = trade.Id, Url = "https://images.unsplash.com/photo-1611974789855-9c2a0a7236a3?w=800", Category = "BEFORE" },
                new TradeImage { TradeId = trade.Id, Url = "https://images.unsplash.com/photo-1642543492481-44e81e3914a7?w=800", Category = "AFTER" }
            );
            await db.TradeTimelineEvents.AddRangeAsync(
                new TradeTimelineEvent { TradeId = trade.Id, Event = "Analysis Uploaded", Description = "Chart markup uploaded", CreatedAt = trade.Date.AddMinutes(-15) },
                new TradeTimelineEvent { TradeId = trade.Id, Event = "Trade Opened", Description = $"Position filled at {trade.EntryPrice}", CreatedAt = trade.Date },
                new TradeTimelineEvent { TradeId = trade.Id, Event = "Trade Closed", Description = $"Closed at {trade.ExitPrice} — {trade.Status}", CreatedAt = trade.Date.AddHours(2) },
                new TradeTimelineEvent { TradeId = trade.Id, Event = "Review Added", Description = "Post-trade journaling done", CreatedAt = trade.Date.AddHours(2.5) }
            );
        }
        await db.SaveChangesAsync();
    }
}
