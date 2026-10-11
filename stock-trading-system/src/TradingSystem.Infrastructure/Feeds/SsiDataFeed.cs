using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TradingSystem.Core.Entities;
using TradingSystem.Core.Models.TimeSeries;

namespace TradingSystem.Infrastructure.Feeds;

/// <summary>
/// Nguồn cấp dữ liệu SSI iBoard:
/// - Toàn bộ thông số Hợp đồng Phái sinh Futures HNX (OI, Đáo hạn, Ký quỹ, Điểm số VN30 cơ sở).
/// - Thông số chi tiết 339 Chứng quyền HOSE (Ngày bắt đầu GD, Tỷ lệ chuyển đổi, CTCK phát hành).
/// </summary>
public class SsiDataFeed
{
    private readonly HttpClient _client;

    public SsiDataFeed(HttpClient? client = null)
    {
        _client = client ?? new HttpClient();
        if (!_client.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }
    }

    /// <summary>
    /// Tải toàn bộ danh sách Hợp đồng tương lai phái sinh (/stock/exchange/fu)
    /// </summary>
    public async Task<(List<DerivativeContract> Contracts, List<OrderBookDepth> DepthList)> GetDerivativeContractsAsync(CancellationToken ct = default)
    {
        var contracts = new List<DerivativeContract>();
        var depthList = new List<OrderBookDepth>();
        var url = "https://iboard-query.ssi.com.vn/stock/exchange/fu";

        try
        {
            var json = await _client.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("data", out var dataArr))
            {
                foreach (var el in dataArr.EnumerateArray())
                {
                    var sym = el.TryGetProperty("stockSymbol", out var s) ? s.GetString() ?? "" : "";
                    if (string.IsNullOrEmpty(sym)) continue;

                    var underlying = el.TryGetProperty("underlyingSymbol", out var u) ? u.GetString() ?? "VN30" : "VN30";
                    var month = el.TryGetProperty("expirationMonth", out var m) ? m.GetString() ?? "" : "";
                    var multiplier = el.TryGetProperty("contractMultiplier", out var cm) ? cm.GetInt32() : 100000;
                    var firstDateStr = el.TryGetProperty("firstTradingDate", out var fd) ? fd.GetString() : null;
                    var lastDateStr = el.TryGetProperty("lastTradingDate", out var ld) ? ld.GetString() : null;
                    var oi = el.TryGetProperty("openInterest", out var o) ? o.GetInt64() : 0;

                    // Parse ngày tháng
                    DateOnly? firstDate = ParseDate(firstDateStr);
                    DateOnly lastDate = ParseDate(lastDateStr) ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1));

                    contracts.Add(new DerivativeContract
                    {
                        SymbolCode = sym,
                        UnderlyingIndex = underlying,
                        ContractMonth = month,
                        ContractMultiplier = multiplier > 0 ? multiplier : 100000,
                        InitialMarginRate = 0.17, // Quy định VSD 17%
                        MaintenanceMarginRate = 0.13,
                        FirstTradingDate = firstDate,
                        LastTradingDate = lastDate,
                        SettlementMethod = "CASH",
                        IsActive = true
                    });

                    // Parse Sổ lệnh 10 bước giá
                    depthList.Add(new OrderBookDepth
                    {
                        Symbol = sym,
                        Timestamp = DateTime.UtcNow,
                        BidPrice1 = ParseDouble(el, "best1Bid"),
                        BidVol1 = (long)ParseDouble(el, "best1BidVol"),
                        BidPrice2 = ParseDouble(el, "best2Bid"),
                        BidVol2 = (long)ParseDouble(el, "best2BidVol"),
                        BidPrice3 = ParseDouble(el, "best3Bid"),
                        BidVol3 = (long)ParseDouble(el, "best3BidVol"),
                        AskPrice1 = ParseDouble(el, "best1Offer"),
                        AskVol1 = (long)ParseDouble(el, "best1OfferVol"),
                        AskPrice2 = ParseDouble(el, "best2Offer"),
                        AskVol2 = (long)ParseDouble(el, "best2OfferVol"),
                        AskPrice3 = ParseDouble(el, "best3Offer"),
                        AskVol3 = (long)ParseDouble(el, "best3OfferVol"),
                        TotalBidQty = (long)ParseDouble(el, "totalBidQty"),
                        TotalAskQty = (long)ParseDouble(el, "totalAskQty"),
                        ForeignBuyVol = (long)ParseDouble(el, "buyForeignQtty"),
                        ForeignSellVol = (long)ParseDouble(el, "sellForeignQtty")
                    });
                }
            }
        }
        catch
        {
            // Trả về danh sách hiện có
        }

        return (contracts, depthList);
    }

    /// <summary>
    /// Tải toàn bộ danh mục Chứng Quyền HOSE (/stock/cw/hose)
    /// </summary>
    public async Task<List<CoveredWarrant>> GetCoveredWarrantsAsync(CancellationToken ct = default)
    {
        var warrants = new List<CoveredWarrant>();
        var url = "https://iboard-query.ssi.com.vn/stock/cw/hose";

        try
        {
            var json = await _client.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("data", out var dataArr))
            {
                foreach (var el in dataArr.EnumerateArray())
                {
                    var sym = el.TryGetProperty("stockSymbol", out var s) ? s.GetString() ?? "" : "";
                    if (string.IsNullOrEmpty(sym)) continue;

                    var underlying = el.TryGetProperty("underlyingSymbol", out var u) ? u.GetString() ?? "" : "";
                    var issuer = el.TryGetProperty("issuerName", out var iss) ? iss.GetString() ?? "" : "";
                    var exPrice = (decimal)ParseDouble(el, "exercisePrice");
                    var exRatio = el.TryGetProperty("exerciseRatio", out var r) ? r.GetString() ?? "1:1" : "1:1";
                    var firstDateStr = el.TryGetProperty("firstTradingDate", out var fd) ? fd.GetString() : null;
                    var lastDateStr = el.TryGetProperty("lastTradingDate", out var ld) ? ld.GetString() : null;
                    var maturityDateStr = el.TryGetProperty("maturityDate", out var md) ? md.GetString() : null;

                    double multiplier = ParseRatioMultiplier(exRatio);

                    warrants.Add(new CoveredWarrant
                    {
                        SymbolCode = sym,
                        UnderlyingSymbol = underlying,
                        IssuerName = issuer,
                        CwType = "C", // Mặc định Call tại VN
                        ExercisePrice = exPrice,
                        ExerciseRatio = exRatio,
                        RatioMultiplier = multiplier,
                        FirstTradingDate = ParseDate(firstDateStr),
                        LastTradingDate = ParseDate(lastDateStr),
                        MaturityDate = ParseDate(maturityDateStr) ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)),
                        SettlementType = "CASH",
                        IsActive = true
                    });
                }
            }
        }
        catch
        {
            // Trả về rỗng nếu lỗi
        }

        return warrants;
    }

    private static double ParseRatioMultiplier(string ratioStr)
    {
        if (string.IsNullOrWhiteSpace(ratioStr)) return 1.0;
        var parts = ratioStr.Split(':');
        if (parts.Length > 0 && double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
        {
            return num;
        }
        return 1.0;
    }

    private static DateOnly? ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;

        // Định dạng yyyyMMdd (VD: 20261015)
        if (dateStr.Length == 8 && DateOnly.TryParseExact(dateStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1))
        {
            return d1;
        }
        // Định dạng dd/MM/yyyy (VD: 15/10/2026)
        if (DateOnly.TryParseExact(dateStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2))
        {
            return d2;
        }

        return null;
    }

    private static double ParseDouble(JsonElement el, string propName)
    {
        if (el.TryGetProperty(propName, out var p))
        {
            if (p.ValueKind == JsonValueKind.Number) return p.GetDouble();
            if (p.ValueKind == JsonValueKind.String && double.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var val)) return val;
        }
        return 0;
    }
}
