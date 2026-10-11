using System.Collections.Generic;
using TradingSystem.Core.Entities;
using TradingSystem.Core.Models.TimeSeries;

namespace TradingSystem.Backtest.Core;

/// <summary>
/// Giao diện chuẩn cho tất cả các mô hình toán học & thuật toán giao dịch.
/// Thiết kế mở rộng để hỗ trợ hàng trăm mô hình khác nhau.
/// </summary>
public interface ITradingStrategy
{
    /// <summary>
    /// Mã định danh duy nhất của mô hình (VD: BUY_MOMENTUM_PULLBACK, SELL_MACD_BREAKDOWN)
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Tên mô hình thân thiện với con người
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Giải thích nguyên lý toán học và logic vào lệnh
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Phân loại mô hình: 'BUY_MODEL' (Dự đoán điểm Mua), 'SELL_MODEL' (Dự đoán điểm Bán), 'TWO_WAY' (2 chiều Long/Short)
    /// </summary>
    string StrategyType { get; }

    /// <summary>
    /// Thị trường áp dụng: 'STOCK', 'DERIVATIVE_VN30', 'COVERED_WARRANT', 'ALL'
    /// </summary>
    string TargetAssetClass { get; }

    /// <summary>
    /// Tham số cấu hình toán học của mô hình (Period, Tỷ lệ đòn bẩy, Ngưỡng kích hoạt)
    /// </summary>
    Dictionary<string, object> Parameters { get; set; }

    /// <summary>
    /// Hàm cốt lõi: Quét qua chuỗi nến lịch sử từ Ngày A -> Ngày B và sinh tín hiệu giao dịch
    /// </summary>
    List<AlgorithmicTradingSignal> GenerateSignals(string symbol, IReadOnlyList<Bar1m> historyBars);
}
