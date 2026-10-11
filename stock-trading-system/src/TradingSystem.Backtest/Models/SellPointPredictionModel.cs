using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TradingSystem.Backtest.Core;
using TradingSystem.Core.Entities;
using TradingSystem.Core.Enums;
using TradingSystem.Core.Models.TimeSeries;

namespace TradingSystem.Backtest.Models;

/// <summary>
/// MÔ HÌNH TOÁN HỌC 2: DỰ ĐOÁN ĐIỂM BÁN & SHORT (SELL & SHORT POINT PREDICTION)
/// - Nguyên lý: Bắt đảo chiều giảm khi giá gãy kênh EMA, xung lượng MACD cắt xuống và RSI suy kiệt.
/// - Áp dụng: 
///   + Với Cổ phiếu / CW: Báo điểm CHỐT LỜI / THOÁT HÀNG (CLOSE_LONG).
///   + Với Phái sinh VN30: Báo điểm MỞ VỊ THẾ BÁN KHỐNG (OPEN_SHORT).
/// </summary>
public class SellPointPredictionModel : ITradingStrategy
{
    public string Id => "SELL_MACD_BREAKDOWN_V1";
    public string Name => "Dự Đoán Điểm Bán & Short (Trend Breakdown & MACD Divergence)";
    public string Description => "Bán/Short khi giá thủng EMA20 & EMA50, đường MACD cắt xuống dưới Signal line và RSI giảm xuống dưới 50.";
    public string StrategyType => "SELL_MODEL";
    public string TargetAssetClass => "ALL";

    public Dictionary<string, object> Parameters { get; set; } = new()
    {
        { "FastEmaPeriod", 20 },
        { "SlowEmaPeriod", 50 },
        { "RsiPeriod", 14 },
        { "RsiThreshold", 50.0 },
        { "TakeProfitAtrMultiplier", 2.0 },
        { "StopLossAtrMultiplier", 1.0 }
    };

    public List<AlgorithmicTradingSignal> GenerateSignals(string symbol, IReadOnlyList<Bar1m> bars)
    {
        var signals = new List<AlgorithmicTradingSignal>();
        int count = bars.Count;
        if (count < 60) return signals;

        bool isDerivative = symbol.StartsWith("VN30F") || symbol.StartsWith("41I");

        var closes = bars.Select(b => b.Close).ToList();
        var highs = bars.Select(b => b.High).ToList();
        var lows = bars.Select(b => b.Low).ToList();
        var volumes = bars.Select(b => (double)b.Volume).ToList();

        // 1. Tính toán các chuỗi chỉ báo toán học
        int fastPeriod = Convert.ToInt32(Parameters["FastEmaPeriod"]);
        int slowPeriod = Convert.ToInt32(Parameters["SlowEmaPeriod"]);
        int rsiPeriod = Convert.ToInt32(Parameters["RsiPeriod"]);
        double rsiThreshold = Convert.ToDouble(Parameters["RsiThreshold"]);
        double tpMultiplier = Convert.ToDouble(Parameters["TakeProfitAtrMultiplier"]);
        double slMultiplier = Convert.ToDouble(Parameters["StopLossAtrMultiplier"]);

        var emaFast = Indicators.CalculateEma(closes, fastPeriod);
        var emaSlow = Indicators.CalculateEma(closes, slowPeriod);
        var rsi = Indicators.CalculateRsi(closes, rsiPeriod);
        var atr = Indicators.CalculateAtr(highs, lows, closes, 14);
        var (macdLine, signalLine, histogram) = Indicators.CalculateMacd(closes, 12, 26, 9);
        var volSma = Indicators.CalculateSma(volumes, 20);

        // 2. Quét chuỗi nến dò tìm điểm Bán / Short
        int startIndex = Math.Max(slowPeriod, 30);
        for (int i = startIndex; i < count; i++)
        {
            var bar = bars[i];
            double price = bar.Close;

            // Điều kiện 1: Giá gãy xuống dưới đường EMA trung hạn (thủng hỗ trợ)
            bool isBreakdown = price < emaFast[i] && emaFast[i] < emaSlow[i];

            // Điều kiện 2: MACD cắt xuống đường tín hiệu (Bearish Cross)
            bool isMacdCrossDown = (macdLine[i - 1] >= signalLine[i - 1] && macdLine[i] < signalLine[i]) || histogram[i] < 0;

            // Điều kiện 3: RSI rơi xuống dưới ngưỡng trung tính 50 (bước vào pha phe gấu kiểm soát)
            bool isRsiWeakening = rsi[i - 1] >= rsiThreshold && rsi[i] < rsiThreshold;

            if (isBreakdown && isMacdCrossDown && isRsiWeakening)
            {
                double currentAtr = atr[i] > 0 ? atr[i] : (price * 0.005);
                decimal entryPrice = (decimal)price;
                // Nếu SHORT: Lãi khi giá giảm (TP thấp hơn), Lỗ khi giá tăng (SL cao hơn)
                decimal takeProfit = entryPrice - (decimal)(currentAtr * tpMultiplier);
                decimal stopLoss = entryPrice + (decimal)(currentAtr * slMultiplier);

                var metrics = new Dictionary<string, object>
                {
                    { "EMA20", Math.Round(emaFast[i], 2) },
                    { "EMA50", Math.Round(emaSlow[i], 2) },
                    { "MACD", Math.Round(macdLine[i], 2) },
                    { "Signal", Math.Round(signalLine[i], 2) },
                    { "RSI", Math.Round(rsi[i], 1) },
                    { "ATR", Math.Round(currentAtr, 2) }
                };

                signals.Add(new AlgorithmicTradingSignal
                {
                    StrategyName = Id,
                    TargetScope = "SYMBOL",
                    TargetCode = symbol,
                    ActionType = isDerivative ? SignalActionType.OpenShort : SignalActionType.CloseLong,
                    Timeframe = "1M",
                    SignalTime = bar.Timestamp,
                    TargetPrice = entryPrice,
                    TakeProfitPrice = takeProfit,
                    StopLossPrice = stopLoss,
                    ConfidenceScore = 0.82,
                    MetricsSnapshotJson = JsonSerializer.Serialize(metrics)
                });

                // Bỏ qua tối thiểu 5 nến tiếp theo
                i += 5;
            }
        }

        return signals;
    }
}
