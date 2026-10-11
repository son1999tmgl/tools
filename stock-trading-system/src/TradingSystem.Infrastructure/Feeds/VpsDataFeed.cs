using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TradingSystem.Core.Entities;
using TradingSystem.Core.Enums;
using TradingSystem.Core.Models.TimeSeries;

namespace TradingSystem.Infrastructure.Feeds;

/// <summary>
/// Nguồn cấp dữ liệu VPS Datafeed (Nguồn chính 85%):
/// - Danh mục toàn bộ 2.166 mã HOSE, HNX, UPCOM.
/// - Sổ lệnh Level 2 realtime toàn thị trường (bước giá, giá trần/sàn/tham chiếu).
/// - Khớp lệnh trong ngày có cờ Mua/Bán chủ động.
/// </summary>
public class VpsDataFeed
{
    private readonly HttpClient _client;

    public VpsDataFeed(HttpClient? client = null)
    {
        _client = client ?? new HttpClient();
        if (!_client.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }
    }

    /// <summary>
    /// Tải toàn bộ danh mục mã niêm yết trên 3 sàn từ VPS (/getlistallstock)
    /// </summary>
    public async Task<List<Symbol>> GetAllSymbolsAsync(CancellationToken ct = default)
    {
        var result = new List<Symbol>();
        var url = "https://bgapidatafeed.vps.com.vn/getlistallstock";

        var response = await _client.GetStringAsync(url, ct);
        using var doc = JsonDocument.Parse(response);

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var code = item.GetProperty("stock_code").GetString()?.Trim().ToUpper() ?? "";
            var exchange = item.GetProperty("post_to").GetString()?.Trim().ToUpper() ?? "HOSE";
            var type = item.GetProperty("type").GetString()?.Trim().ToUpper() ?? "S";
            var nameVn = item.TryGetProperty("name_vn", out var nVn) ? nVn.GetString() : "";
            var nameEn = item.TryGetProperty("name_en", out var nEn) ? nEn.GetString() : "";
            var shortName = item.TryGetProperty("name_short", out var sN) ? sN.GetString() : "";

            if (string.IsNullOrEmpty(code)) continue;

            result.Add(new Symbol
            {
                SymbolCode = code,
                Exchange = exchange,
                AssetType = type,
                CompanyNameVi = nameVn,
                CompanyNameEn = nameEn,
                ShortName = shortName,
                LotSize = type == AssetType.Derivative ? 1 : 100,
                ParValue = 10000m,
                IsTrading = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        return result;
    }

    /// <summary>
    /// Kéo snapshot sổ lệnh và bước giá cho danh sách mã (batch tối đa 100 mã/lần)
    /// </summary>
    public async Task<List<OrderBookDepth>> GetOrderBookSnapshotAsync(IEnumerable<string> symbols, CancellationToken ct = default)
    {
        var result = new List<OrderBookDepth>();
        var symbolList = symbols.ToList();
        if (symbolList.Count == 0) return result;

        int batchSize = 100;
        for (int i = 0; i < symbolList.Count; i += batchSize)
        {
            var batch = symbolList.Skip(i).Take(batchSize);
            var url = "https://bgapidatafeed.vps.com.vn/getliststockdata/" + string.Join(",", batch);

            try
            {
                var json = await _client.GetStringAsync(url, ct);
                using var doc = JsonDocument.Parse(json);

                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var sym = el.TryGetProperty("sym", out var s) ? s.GetString() ?? "" : "";
                    if (string.IsNullOrEmpty(sym)) continue;

                    // Parse các bước giá Mua (g1, g2, g3) và Bán (g4, g5, g6)
                    double b1 = ParseDouble(el, "g1"), bv1 = ParseDouble(el, "g2");
                    double b2 = ParseDouble(el, "g3"), bv2 = ParseDouble(el, "g4");
                    double a1 = ParseDouble(el, "g5"), av1 = ParseDouble(el, "g6");

                    result.Add(new OrderBookDepth
                    {
                        Symbol = sym,
                        Timestamp = DateTime.UtcNow,
                        BidPrice1 = b1,
                        BidVol1 = (long)bv1,
                        BidPrice2 = b2,
                        BidVol2 = (long)bv2,
                        AskPrice1 = a1,
                        AskVol1 = (long)av1,
                        TotalBidQty = (long)ParseDouble(el, "totalBidVol"),
                        TotalAskQty = (long)ParseDouble(el, "totalAskVol"),
                        ForeignBuyVol = (long)ParseDouble(el, "fBVol"),
                        ForeignSellVol = (long)ParseDouble(el, "fSVolume")
                    });
                }
            }
            catch
            {
                // Bỏ qua lỗi batch để tiếp tục các batch khác
            }
        }

        return result;
    }

    /// <summary>
    /// Tải toàn bộ danh sách khớp lệnh từ đầu phiên của một mã (/getliststocktrade)
    /// </summary>
    public async Task<List<MarketTick>> GetIntradayTradesAsync(string symbol, CancellationToken ct = default)
    {
        var result = new List<MarketTick>();
        var url = $"https://bgapidatafeed.vps.com.vn/getliststocktrade/{symbol}";

        try
        {
            var json = await _client.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);

            var now = DateTime.UtcNow;
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var timeStr = item.TryGetProperty("time", out var t) ? t.GetString() ?? "" : "";
                var price = item.TryGetProperty("lastPrice", out var p) ? p.GetDouble() : 0;
                var vol = item.TryGetProperty("lastVol", out var v) ? v.GetInt64() : 0;
                var side = item.TryGetProperty("side", out var s) ? s.GetString() ?? "" : "";
                var accVol = item.TryGetProperty("totalVol", out var av) ? av.GetInt64() : 0;

                // Chuẩn hóa thời gian HH:mm:ss thành DateTime
                DateTime tickTime = now;
                if (TimeSpan.TryParse(timeStr, out var timeSpan))
                {
                    tickTime = now.Date.Add(timeSpan);
                }

                result.Add(new MarketTick
                {
                    Symbol = symbol,
                    Timestamp = tickTime,
                    Price = price,
                    Volume = vol,
                    Side = side,
                    AccumulatedVolume = accVol,
                    AccumulatedValue = 0
                });
            }
        }
        catch
        {
            // Trả về rỗng nếu chưa có dữ liệu phiên
        }

        return result;
    }

    private static double ParseDouble(JsonElement el, string propName)
    {
        if (el.TryGetProperty(propName, out var p))
        {
            if (p.ValueKind == JsonValueKind.Number) return p.GetDouble();
            if (p.ValueKind == JsonValueKind.String && double.TryParse(p.GetString(), out var val)) return val;
        }
        return 0;
    }
}
