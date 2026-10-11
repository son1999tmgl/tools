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
/// MÔ HÌNH TOÁN HỌC 1: DỰ ĐOÁN ĐIỂM MUA (BUY POINT PREDICTION)
/// - Nguyên lý: Bắt sóng tăng Pullback khi giá hồi về đường xu hướng EMA20 kết hợp nổ thanh khoản và RSI phục hồi.
/// - Áp dụng: Cổ phiếu cơ sở, Chứng quyền (Mua Call) và Phái sinh VN30 (Mở vị thế Long).
/// </summary>
public class BuyPointPredictionModel : ITradingStrategy
{
    public string Id => "BUY_TREND_PULLBACK_V1";
    public string Name => "Dự Đoán Điểm Mua (Trend Pullback & Volume Surge)";
    public string Description => "Mua khi giá hồi về đường EMA20 trong xu hướng tăng (EMA20 > EMA50), RSI phục hồi vượt 45 và Volume cao hơn 1.3x trung bình.";
    public string StrategyType => "BUY_MODEL";
    public string TargetAssetClass => "ALL";

    public Dictionary<string, object> Parameters { get; set; } = new()
    {
        { "FastEmaPeriod", 20 },
        { "SlowEmaPeriod", 50 },
        { "RsiPeriod", 14 },
        { "RsiThreshold", 45.0 },
        { "VolumeMultiplier", 1.3 },
        { "TakeProfitAtrMultiplier", 2.0 },
        { "StopLossAtrMultiplier", 1.0 }
    };

    public List<AlgorithmicTradingSignal> GenerateSignals(string symbol, IReadOnlyList<Bar1m> bars)
    {
        var signals = new List<AlgorithmicTradingSignal>();
        int count = bars.Count;
        if (count < 60) return signals;

        // Trích xuất các mảng giá trị
        var closes = bars.Select(b => b.Close).ToList();
        var highs = bars.Select(b => b.High).ToList();
        var lows = bars.Select(b => b.Low).ToList();
        var volumes = bars.Select(b => (double)b.Volume).ToList();

        // 1. Tính toán các chuỗi chỉ báo toán học
        int fastPeriod = Convert.ToInt32(Parameters["FastEmaPeriod"]);
        int slowPeriod = Convert.ToInt32(Parameters["SlowEmaPeriod"]);
        int rsiPeriod = Convert.ToInt32(Parameters["RsiPeriod"]);
        double rsiThreshold = Convert.ToDouble(Parameters["RsiThreshold"]);
        double volMultiplier = Convert.ToDouble(Parameters["VolumeMultiplier"]);
        double tpMultiplier = Convert.ToDouble(Parameters["TakeProfitAtrMultiplier"]);
        double slMultiplier = Convert.ToDouble(Parameters["StopLossAtrMultiplier"]);

        var emaFast = Indicators.CalculateEma(closes, fastPeriod);
        var emaSlow = Indicators.CalculateEma(closes, slowPeriod);
        var rsi = Indicators.CalculateRsi(closes, rsiPeriod);
        var atr = Indicators.CalculateAtr(highs, lows, closes, 14);
        var volSma = Indicators.CalculateSma(volumes, 20);

        // 2. Quét qua từng mốc thời gian để dò điểm Mua thỏa mãn hệ điều kiện
        int startIndex = Math.Max(slowPeriod, 25);
        for (int i = startIndex; i < count; i++)
        {
            var bar = bars[i];
            double price = bar.Close;

            // Điều kiện 1: Xu hướng chính đang tăng (EMA nhanh nằm trên EMA chậm)
            bool isUptrend = emaFast[i] > emaSlow[i];

            // Điều kiện 2: Giá lùi về gần đường hỗ trợ EMA nhanh (trong phạm vi 0.4%)
            double distToEma = Math.Abs(price - emaFast[i]) / price;
            bool isPullback = distToEma <= 0.004 && price >= emaSlow[i];

            // Điều kiện 3: RSI phục hồi vượt lên trên ngưỡng xác nhận (chấm dứt đà chỉnh)
            bool isRsiRebounding = rsi[i - 1] < rsiThreshold && rsi[i] >= rsiThreshold;

            // Điều kiện 4: Khối lượng xác nhận dòng tiền vào (cao hơn trung bình 20 phiên)
            bool isVolumeSurging = volumes[i] >= volSma[i] * volMultiplier;

            if (isUptrend && isPullback && isRsiRebounding && isVolumeSurging)
            {
                double currentAtr = atr[i] > 0 ? atr[i] : (price * 0.005);
                decimal entryPrice = (decimal)price;
                decimal takeProfit = entryPrice + (decimal)(currentAtr * tpMultiplier);
                decimal stopLoss = entryPrice - (decimal)(currentAtr * slMultiplier);

                var metrics = new Dictionary<string, object>
                {
                    { "EMA20", Math.Round(emaFast[i], 2) },
                    { "EMA50", Math.Round(emaSlow[i], 2) },
                    { "RSI", Math.Round(rsi[i], 1) },
                    { "VolumeRatio", Math.Round(volumes[i] / Math.Max(1, volSma[i]), 2) },
                    { "ATR", Math.Round(currentAtr, 2) }
                };

                signals.Add(new AlgorithmicTradingSignal
                {
                    StrategyName = Id,
                    TargetScope = "SYMBOL",
                    TargetCode = symbol,
                    ActionType = SignalActionType.OpenLong,
                    Timeframe = "1M",
                    SignalTime = bar.Timestamp,
                    TargetPrice = entryPrice,
                    TakeProfitPrice = takeProfit,
                    StopLossPrice = stopLoss,
                    ConfidenceScore = 0.85,
                    MetricsSnapshotJson = JsonSerializer.Serialize(metrics)
                });

                // Bỏ qua tối thiểu 5 nến tiếp theo để tránh spam tín hiệu liên tiếp
                i += 5;
            }
        }

        return signals;
    }
}
