using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TradingSystem.Core.Entities;
using TradingSystem.Core.Models.TimeSeries;

namespace TradingSystem.Infrastructure.Feeds;

/// <summary>
/// Nguồn cấp dữ liệu DNSE Entrade (Nguồn phụ trợ 15%):
/// - Nạp chuỗi nến lịch sử quá khứ 1 phút (1m OHLCV).
/// - Nạp nến ngày (1D OHLCV) phục vụ đối soát và nạp bảng market_daily_bars.
/// - Hỗ trợ Cổ phiếu, Chứng quyền, Phái sinh VN30F1M và Chỉ số VN30.
/// </summary>
public class DnseDataFeed
{
    private readonly HttpClient _client;

    public DnseDataFeed(HttpClient? client = null)
    {
        _client = client ?? new HttpClient();
        if (!_client.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }
    }

    /// <summary>
    /// Lấy nến 1 phút (1m) cho Cổ phiếu hoặc Chứng quyền
    /// </summary>
    public async Task<List<Bar1m>> GetStockBars1mAsync(string symbol, long fromUnix, long toUnix, CancellationToken ct = default)
    {
        var url = $"https://services.entrade.com.vn/chart-api/v2/ohlcs/stock?from={fromUnix}&to={toUnix}&symbol={symbol}&resolution=1";
        return await FetchBars1mInternalAsync(symbol, url, ct);
    }

    /// <summary>
    /// Lấy nến 1 phút (1m) cho HĐTL Phái sinh VN30F1M
    /// </summary>
    public async Task<List<Bar1m>> GetDerivativeBars1mAsync(string symbol, long fromUnix, long toUnix, CancellationToken ct = default)
    {
        var url = $"https://services.entrade.com.vn/chart-api/v2/ohlcs/derivative?from={fromUnix}&to={toUnix}&symbol={symbol}&resolution=1";
        return await FetchBars1mInternalAsync(symbol, url, ct);
    }

    /// <summary>
    /// Lấy nến 1 phút (1m) cho Chỉ số cơ sở VN30 (để tính Basis)
    /// </summary>
    public async Task<List<Bar1m>> GetIndexBars1mAsync(string indexSymbol, long fromUnix, long toUnix, CancellationToken ct = default)
    {
        var url = $"https://services.entrade.com.vn/chart-api/v2/ohlcs/index?from={fromUnix}&to={toUnix}&symbol={indexSymbol}&resolution=1";
        return await FetchBars1mInternalAsync(indexSymbol, url, ct);
    }

    /// <summary>
    /// Lấy nến ngày (1D) phục vụ nạp bảng market_daily_bars
    /// </summary>
    public async Task<List<MarketDailyBar>> GetDailyBarsAsync(string symbol, bool isDerivative, long fromUnix, long toUnix, CancellationToken ct = default)
    {
        var result = new List<MarketDailyBar>();
        var endpoint = isDerivative ? "derivative" : "stock";
        var url = $"https://services.entrade.com.vn/chart-api/v2/ohlcs/{endpoint}?from={fromUnix}&to={toUnix}&symbol={symbol}&resolution=1D";

        try
        {
            var json = await _client.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("t", out var tArr))
            {
                var oArr = root.GetProperty("o");
                var hArr = root.GetProperty("h");
                var lArr = root.GetProperty("l");
                var cArr = root.GetProperty("c");
                var vArr = root.GetProperty("v");

                int count = tArr.GetArrayLength();
                for (int i = 0; i < count; i++)
                {
                    long unixTime = tArr[i].GetInt64();
                    var date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime);

                    result.Add(new MarketDailyBar
                    {
                        SymbolCode = symbol,
                        TradingDate = date,
                        Open = (decimal)oArr[i].GetDouble(),
                        High = (decimal)hArr[i].GetDouble(),
                        Low = (decimal)lArr[i].GetDouble(),
                        Close = (decimal)cArr[i].GetDouble(),
                        RefPrice = (decimal)oArr[i].GetDouble(),
                        TotalVolume = (long)vArr[i].GetDouble(),
                        TotalValue = (decimal)(vArr[i].GetDouble() * cArr[i].GetDouble() * (isDerivative ? 100000 : 1000))
                    });
                }
            }
        }
        catch
        {
            // Trả về danh sách hiện có nếu lỗi
        }

        return result;
    }

    private async Task<List<Bar1m>> FetchBars1mInternalAsync(string symbol, string url, CancellationToken ct)
    {
        var result = new List<Bar1m>();

        try
        {
            var json = await _client.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("t", out var tArr))
            {
                var oArr = root.GetProperty("o");
                var hArr = root.GetProperty("h");
                var lArr = root.GetProperty("l");
                var cArr = root.GetProperty("c");
                var vArr = root.GetProperty("v");

                int count = tArr.GetArrayLength();
                for (int i = 0; i < count; i++)
                {
                    long unixTime = tArr[i].GetInt64();
                    var time = DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;

                    result.Add(new Bar1m
                    {
                        Symbol = symbol,
                        Timestamp = time,
                        Open = oArr[i].GetDouble(),
                        High = hArr[i].GetDouble(),
                        Low = lArr[i].GetDouble(),
                        Close = cArr[i].GetDouble(),
                        Volume = (long)vArr[i].GetDouble(),
                        Value = vArr[i].GetDouble() * cArr[i].GetDouble(),
                        BuyVolume = 0,
                        SellVolume = 0
                    });
                }
            }
        }
        catch
        {
            // Trả về rỗng nếu lỗi
        }

        return result;
    }
}
