using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TradeVault.Services;

// ── Extraction DTOs ───────────────────────────────────────────────────────────

public record BeforeTradeExtraction(
    string? Pair,
    string? Timeframe,
    string? Direction,
    decimal? Entry,
    decimal? StopLoss,
    decimal? TakeProfit,
    int EntryConfidence,
    int SlConfidence,
    int TpConfidence,
    string? Pattern,
    string? Summary,
    string? RawResponse,
    bool Success,
    string? ErrorMessage);

public record AfterTradeAnalysis(
    string? Outcome,
    decimal? ExitPrice,
    decimal? PipsGained,
    decimal? ActualRR,
    string? Analysis,
    int Confidence,
    bool Success,
    string? ErrorMessage);

// ── Service ───────────────────────────────────────────────────────────────────

public class GeminiAiService(IConfiguration config, HttpClient httpClient, ILogger<GeminiAiService> logger)
{
    private const string Model = "gemini-1.5-flash";
    
    public string? CustomApiKey { get; set; }
    
    private string ApiKey => !string.IsNullOrWhiteSpace(CustomApiKey) ? CustomApiKey :
                             (!string.IsNullOrWhiteSpace(config["GeminiApiKey"]) ? config["GeminiApiKey"]! :
                             (!string.IsNullOrWhiteSpace(config["GEMINI_API_KEY"]) ? config["GEMINI_API_KEY"]! :
                             Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? ""));

    public bool IsKeyConfigured => !string.IsNullOrWhiteSpace(ApiKey) && ApiKey != "YOUR_GEMINI_API_KEY_HERE";

    private string BaseUrl => $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={ApiKey}";

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // ── Before-Trade Extraction ───────────────────────────────────────────────

    public async Task<BeforeTradeExtraction> ExtractBeforeTradeAsync(byte[] imageBytes, string contentType)
    {
        if (!IsKeyConfigured)
            return Fail<BeforeTradeExtraction>("Gemini API key is not configured. Please add your key to appsettings.json or enter it in the AI Key input box.");

        const string prompt = """
            You are an expert trading chart analyst. Analyze this trading chart screenshot carefully.

            Extract the following information and respond ONLY with a valid JSON object (no markdown, no extra text):
            {
              "pair": "trading symbol e.g. XAUUSD, EURUSD, BTCUSD or null if not visible",
              "timeframe": "e.g. M1, M5, M15, M30, H1, H4, Daily, Weekly or null",
              "direction": "BUY or SELL based on chart annotations, arrows, or labels",
              "entry": 0.00000,
              "stopLoss": 0.00000,
              "takeProfit": 0.00000,
              "entryConfidence": 85,
              "slConfidence": 80,
              "tpConfidence": 75,
              "pattern": "e.g. Order Block, Fair Value Gap, BOS, CHoCH, Liquidity Sweep, Breakout, Support/Resistance, Trend Following or null",
              "summary": "A concise 2-3 sentence description of the trade setup, including: direction bias, key levels identified, and the strategy or pattern being traded."
            }

            Rules for extraction:
            - Look for horizontal lines, text labels (Entry, Buy, Sell, SL, Stop Loss, TP, Target, Take Profit) drawn on the chart
            - If explicit labels are not present, infer from chart annotations, arrows, price boxes, and line positions
            - For BUY trades: Entry < TakeProfit and Entry > StopLoss
            - For SELL trades: Entry > TakeProfit and Entry < StopLoss
            - confidence values must be integers between 0-100
            - If a value cannot be determined, use null for strings and 0 for numbers
            - entry, stopLoss, takeProfit must be the actual price values shown on the chart axes or labels
            """;

        try
        {
            var (raw, error) = await CallGeminiAsync(imageBytes, contentType, prompt);
            if (raw == null)
                return Fail<BeforeTradeExtraction>(error ?? "No response from Gemini API.");

            var json = ExtractJson(raw);
            if (json == null)
                return FallbackBeforeExtraction(raw);

            var node = JsonNode.Parse(json);
            if (node == null)
                return FallbackBeforeExtraction(raw);

            return new BeforeTradeExtraction(
                Pair:             node["pair"]?.GetValue<string>(),
                Timeframe:        node["timeframe"]?.GetValue<string>(),
                Direction:        NormalizeDirection(node["direction"]?.GetValue<string>()),
                Entry:            SafeDecimal(node["entry"]),
                StopLoss:         SafeDecimal(node["stopLoss"]),
                TakeProfit:       SafeDecimal(node["takeProfit"]),
                EntryConfidence:  SafeInt(node["entryConfidence"], 0),
                SlConfidence:     SafeInt(node["slConfidence"], 0),
                TpConfidence:     SafeInt(node["tpConfidence"], 0),
                Pattern:          node["pattern"]?.GetValue<string>(),
                Summary:          node["summary"]?.GetValue<string>(),
                RawResponse:      raw,
                Success:          true,
                ErrorMessage:     null
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gemini BeforeTrade extraction failed");
            return Fail<BeforeTradeExtraction>($"AI extraction error: {ex.Message}");
        }
    }

    // ── After-Trade Analysis ──────────────────────────────────────────────────

    public async Task<AfterTradeAnalysis> AnalyzeAfterTradeAsync(
        byte[] beforeBytes, string beforeContentType,
        byte[] afterBytes,  string afterContentType,
        decimal? entryPrice, decimal? stopLoss, decimal? takeProfit, string direction)
    {
        if (!IsKeyConfigured)
            return FailAfter("Gemini API key is not configured. Please add your key to appsettings.json or enter it in the AI Key box.");

        var prompt = $$"""
            You are an expert trading journal analyst. You are given TWO chart screenshots:
            - Image 1 (BEFORE): The setup chart before the trade played out
            - Image 2 (AFTER): The result chart showing what actually happened

            Known trade parameters:
            - Direction: {{direction}}
            - Entry Price: {{entryPrice?.ToString("G") ?? "unknown"}}
            - Stop Loss: {{stopLoss?.ToString("G") ?? "unknown"}}
            - Take Profit: {{takeProfit?.ToString("G") ?? "unknown"}}

            Analyze both charts carefully and respond ONLY with a valid JSON object (no markdown, no extra text):
            {
              "outcome": "WIN or LOSS or RUNNING or INVALID",
              "exitPrice": 0.00000,
              "pipsGained": 0.0,
              "actualRR": 0.00,
              "confidence": 85,
              "analysis": "A concise 2-3 sentence post-trade analysis: what happened, whether TP or SL was hit, price behavior, and any key observations."
            }

            Rules:
            - outcome: WIN if price reached Take Profit, LOSS if price hit Stop Loss, RUNNING if trade appears still open, INVALID if the setup was invalidated before entry
            - exitPrice: the price where the trade closed (TP level if WIN, SL level if LOSS)
            - pipsGained: positive for wins (pips from entry to exit), negative for losses
            - actualRR: the actual risk-reward achieved (negative if loss)
            - confidence must be integer 0-100
            - Be conservative - if you cannot clearly determine outcome, set outcome to RUNNING
            """;

        try
        {
            var (raw, error) = await CallGeminiMultiImageAsync(
                new[] { (beforeBytes, beforeContentType), (afterBytes, afterContentType) }, prompt);

            if (raw == null)
                return FailAfter(error ?? "No response from Gemini API.");

            var json = ExtractJson(raw);
            if (json == null)
                return FailAfter($"Could not parse AI response. Raw: {raw[..Math.Min(200, raw.Length)]}");

            var node = JsonNode.Parse(json);
            if (node == null)
                return FailAfter("JSON parse error.");

            return new AfterTradeAnalysis(
                Outcome:    NormalizeOutcome(node["outcome"]?.GetValue<string>()),
                ExitPrice:  SafeDecimal(node["exitPrice"]),
                PipsGained: SafeDecimal(node["pipsGained"]),
                ActualRR:   SafeDecimal(node["actualRR"]),
                Analysis:   node["analysis"]?.GetValue<string>(),
                Confidence: SafeInt(node["confidence"], 0),
                Success:    true,
                ErrorMessage: null
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gemini AfterTrade analysis failed");
            return FailAfter($"AI analysis error: {ex.Message}");
        }
    }

    // ── Internal: Single-image Gemini call ────────────────────────────────────

    private async Task<(string? Text, string? Error)> CallGeminiAsync(byte[] imageBytes, string contentType, string prompt)
    {
        return await CallGeminiWithModelsAsync((baseUrl) =>
        {
            var base64 = Convert.ToBase64String(imageBytes);
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { inlineData = new { mimeType = contentType, data = base64 } },
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    topP = 0.9,
                    maxOutputTokens = 1024
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            return new StringContent(json, Encoding.UTF8, "application/json");
        });
    }

    // ── Internal: Multi-image Gemini call ─────────────────────────────────────

    private async Task<(string? Text, string? Error)> CallGeminiMultiImageAsync(
        IEnumerable<(byte[] bytes, string contentType)> images, string prompt)
    {
        return await CallGeminiWithModelsAsync((baseUrl) =>
        {
            var parts = new List<object>();
            foreach (var (bytes, ct) in images)
            {
                parts.Add(new { inlineData = new { mimeType = ct, data = Convert.ToBase64String(bytes) } });
            }
            parts.Add(new { text = prompt });

            var requestBody = new
            {
                contents = new[] { new { parts = parts.ToArray() } },
                generationConfig = new { temperature = 0.1, topP = 0.9, maxOutputTokens = 1024 }
            };

            var json = JsonSerializer.Serialize(requestBody);
            return new StringContent(json, Encoding.UTF8, "application/json");
        });
    }

    private async Task<(string? Text, string? Error)> CallGeminiWithModelsAsync(Func<string, HttpContent> contentBuilder)
    {
        string[] modelsToTry = ["gemini-2.0-flash", "gemini-1.5-flash", "gemini-1.5-pro"];
        string lastError = "";

        foreach (var model in modelsToTry)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={ApiKey}";
            try
            {
                var content = contentBuilder(url);
                var response = await httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var text = await ParseGeminiResponse(response);
                    return (text, null);
                }

                var errBody = await response.Content.ReadAsStringAsync();
                logger.LogError("Gemini API error ({Model}) {Status}: {Body}", model, response.StatusCode, errBody);

                // Try to parse error message from Gemini JSON error response
                string formattedError = $"Gemini API error ({response.StatusCode}): ";
                try
                {
                    using var doc = JsonDocument.Parse(errBody);
                    if (doc.RootElement.TryGetProperty("error", out var errObj) &&
                        errObj.TryGetProperty("message", out var msgElem))
                    {
                        formattedError += msgElem.GetString();
                    }
                    else
                    {
                        formattedError += errBody;
                    }
                }
                catch
                {
                    formattedError += errBody;
                }

                lastError = formattedError;

                // If 404 (model not found), try next model in loop. If 400/403 (invalid key), stop early.
                if ((int)response.StatusCode == 400 || (int)response.StatusCode == 403)
                {
                    return (null, lastError);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception calling Gemini API with model {Model}", model);
                lastError = $"Connection error: {ex.Message}";
            }
        }

        return (null, lastError);
    }

    // ── Internal: Parse Gemini response text ──────────────────────────────────

    private static async Task<string?> ParseGeminiResponse(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        var doc = await JsonDocument.ParseAsync(stream);

        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();
    }

    // ── Internal: OCR fallback — extract JSON block from messy text ───────────

    private static string? ExtractJson(string text)
    {
        // Try to find a JSON object in the response (handles markdown code blocks too)
        var match = Regex.Match(text, @"\{[\s\S]*\}", RegexOptions.Multiline);
        return match.Success ? match.Value : null;
    }

    // ── OCR fallback: try to extract price numbers from raw text ─────────────

    private static BeforeTradeExtraction FallbackBeforeExtraction(string rawText)
    {
        // Very basic regex to find numbers near price keywords
        var numbers = Regex.Matches(rawText, @"\b(\d{1,6}(?:\.\d{1,6})?)\b")
            .Select(m => decimal.TryParse(m.Value, out var v) ? v : (decimal?)null)
            .Where(v => v.HasValue && v.Value > 0)
            .Select(v => v!.Value)
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        return new BeforeTradeExtraction(
            Pair: null, Timeframe: null, Direction: null,
            Entry: numbers.Count > 1 ? numbers[numbers.Count / 2] : null,
            StopLoss: null, TakeProfit: null,
            EntryConfidence: 10, SlConfidence: 0, TpConfidence: 0,
            Pattern: null,
            Summary: "AI could not parse a structured response. Please enter values manually.",
            RawResponse: rawText,
            Success: false,
            ErrorMessage: "Could not parse structured JSON from AI response. OCR fallback used."
        );
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BeforeTradeExtraction Fail<T>(string message) =>
        new(null, null, null, null, null, null, 0, 0, 0, null, null, null, false, message);

    private static AfterTradeAnalysis FailAfter(string message) =>
        new(null, null, null, null, null, 0, false, message);

    private static decimal? SafeDecimal(JsonNode? node)
    {
        if (node == null) return null;
        try
        {
            var val = node.GetValue<double>();
            return val == 0 ? null : (decimal)val;
        }
        catch { return null; }
    }

    private static int SafeInt(JsonNode? node, int fallback)
    {
        if (node == null) return fallback;
        try { return node.GetValue<int>(); }
        catch { return fallback; }
    }

    private static string? NormalizeDirection(string? dir) =>
        dir?.ToUpperInvariant() switch
        {
            "BUY" or "LONG" or "BULLISH" => "BUY",
            "SELL" or "SHORT" or "BEARISH" => "SELL",
            _ => dir
        };

    private static string? NormalizeOutcome(string? outcome) =>
        outcome?.ToUpperInvariant() switch
        {
            "WIN" or "TP" or "TARGET HIT" or "TAKE PROFIT" => "WIN",
            "LOSS" or "SL" or "STOP LOSS" or "STOPPED OUT" => "LOSS",
            "RUNNING" or "OPEN" or "ACTIVE" => "RUNNING",
            "INVALID" or "INVALIDATED" => "INVALID",
            _ => outcome?.ToUpperInvariant()
        };
}
