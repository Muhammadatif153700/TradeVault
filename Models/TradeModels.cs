using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradeVault.Models;

public class Trade
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    public string Title { get; set; } = "";

    [Required, MaxLength(20)]
    public string Pair { get; set; } = "";

    [Required]
    public string MarketType { get; set; } = "Forex"; // Forex, Crypto, Stocks, Indices, Commodities, Options

    [Required]
    public string Direction { get; set; } = "BUY"; // BUY, SELL

    public decimal EntryPrice { get; set; }
    public decimal? StopLoss { get; set; }
    public decimal? TakeProfit { get; set; }
    public decimal? ExitPrice { get; set; }
    public decimal? LotSize { get; set; }
    public decimal? RiskPercent { get; set; }
    public decimal? RewardPercent { get; set; }
    public decimal? RRRatio { get; set; }
    public decimal? PnL { get; set; }

    public string Status { get; set; } = "PENDING"; // WIN, LOSS, BREAKEVEN, PARTIAL, PENDING

    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string? Session { get; set; } // London, New York, Asian
    public bool NewsTrade { get; set; } = false;
    public string? Strategy { get; set; }
    public string? Timeframe { get; set; } // M1, M5, M15, H1, H4, Daily, Weekly

    public string? EmotionsBefore { get; set; }
    public int? Confidence { get; set; } // 1-10
    public string? Mistakes { get; set; }
    public string? Lessons { get; set; }
    public string? Notes { get; set; }
    public string? Tags { get; set; } // comma-separated

    // ── AI Extraction Fields ──────────────────────────────────────────────
    // Populated when user uploads a Before screenshot and AI Vision runs
    public string? AiExtractedPair { get; set; }
    public string? AiExtractedTimeframe { get; set; }
    public string? AiExtractedDirection { get; set; } // BUY or SELL
    public decimal? AiExtractedEntry { get; set; }
    public decimal? AiExtractedSL { get; set; }
    public decimal? AiExtractedTP { get; set; }
    public int? AiEntryConfidence { get; set; } // 0-100
    public int? AiSlConfidence { get; set; }    // 0-100
    public int? AiTpConfidence { get; set; }    // 0-100
    public string? AiDetectedPattern { get; set; } // e.g. "Order Block", "FVG", "BOS"
    public string? AiSummary { get; set; }          // AI trade setup narrative
    public string? AiAfterAnalysis { get; set; }    // AI post-trade analysis
    public string? AiOutcomeDetected { get; set; }  // WIN/LOSS/RUNNING/INVALID from After chart
    public int? AiOutcomeConfidence { get; set; }   // 0-100
    public bool AiFieldsApplied { get; set; } = false; // true when user confirmed AI values

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // FK to Identity User
    public string UserId { get; set; } = "";

    public ICollection<TradeImage> Images { get; set; } = new List<TradeImage>();
    public ICollection<TradeTimelineEvent> Timeline { get; set; } = new List<TradeTimelineEvent>();
    public ICollection<TradeAiExtraction> AiExtractions { get; set; } = new List<TradeAiExtraction>();
}

public class TradeImage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string Url { get; set; } = "";

    public string Category { get; set; } = "BEFORE"; // BEFORE, AFTER

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid TradeId { get; set; }
    public Trade Trade { get; set; } = null!;
}

public class TradeTimelineEvent
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string Event { get; set; } = "";

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid TradeId { get; set; }
    public Trade Trade { get; set; } = null!;
}

public class TradingGoal
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    public string Title { get; set; } = "";

    public string? Description { get; set; }
    public decimal TargetValue { get; set; }
    public decimal CurrentValue { get; set; } = 0;
    public string Metric { get; set; } = "RR"; // RR, WinRate, TradesLimit, Discipline
    public DateTime TargetDate { get; set; }
    public string Status { get; set; } = "ACTIVE"; // ACTIVE, COMPLETED, FAILED

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string UserId { get; set; } = "";
}

public class JournalEntry
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Type { get; set; } = "DAILY"; // DAILY, WEEKLY, MONTHLY
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public string? Psychology { get; set; }
    public string? Mistakes { get; set; }
    public string? Lessons { get; set; }
    public string? Wins { get; set; }
    public string? Goals { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string UserId { get; set; } = "";
}

public class UserAchievement
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string Badge { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;

    public string UserId { get; set; } = "";
}

/// <summary>Stores raw Gemini API responses for auditing and re-use.</summary>
public class TradeAiExtraction
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TradeId { get; set; }
    public Trade Trade { get; set; } = null!;

    /// <summary>BEFORE or AFTER</summary>
    public string ImageCategory { get; set; } = "BEFORE";

    /// <summary>The full raw JSON response from Gemini.</summary>
    public string? RawResponse { get; set; }

    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
}
