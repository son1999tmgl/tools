using System;

namespace TradingSystem.Core.Models.TimeSeries;

/// <summary>
/// Định dạng Parquet: market_ticks (Khớp lệnh Tick-by-tick vi mô)
/// </summary>
public class MarketTick
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double Price { get; set; }                 // Giá khớp (hoặc Điểm số Phái sinh)
    public long Volume { get; set; }                  // Khối lượng lệnh này
    public string Side { get; set; } = string.Empty;  // 'B' (Mua CĐ), 'S' (Bán CĐ), '' (ATO/ATC)
    public long AccumulatedVolume { get; set; }       // Khối lượng lũy kế phiên
    public double AccumulatedValue { get; set; }      // Giá trị lũy kế phiên (VNĐ)
}

/// <summary>
/// Định dạng Parquet: orderbook_depth (Sổ lệnh Level 2 / Level 3 và Order Book Imbalance)
/// </summary>
public class OrderBookDepth
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }

    // 3 - 10 bước giá Mua tốt nhất
    public double BidPrice1 { get; set; }
    public long BidVol1 { get; set; }
    public double BidPrice2 { get; set; }
    public long BidVol2 { get; set; }
    public double BidPrice3 { get; set; }
    public long BidVol3 { get; set; }

    // 3 - 10 bước giá Bán tốt nhất
    public double AskPrice1 { get; set; }
    public long AskVol1 { get; set; }
    public double AskPrice2 { get; set; }
    public long AskVol2 { get; set; }
    public double AskPrice3 { get; set; }
    public long AskVol3 { get; set; }

    public long TotalBidQty { get; set; }             // Tổng dư mua
    public long TotalAskQty { get; set; }             // Tổng dư bán
    public long ForeignBuyVol { get; set; }           // Nước ngoài mua
    public long ForeignSellVol { get; set; }          // Nước ngoài bán

    /// <summary>
    /// Chỉ số mất cân bằng sổ lệnh (Order Book Imbalance ratio: -1.0 đến +1.0)
    /// </summary>
    public double OrderBookImbalance => (TotalBidQty + TotalAskQty) == 0 ? 0 :
        (double)(TotalBidQty - TotalAskQty) / (TotalBidQty + TotalAskQty);
}

/// <summary>
/// Định dạng Parquet: bars_1m (Nến 1 phút liên tục nạp RAM phục vụ Quant/Backtest)
/// </summary>
public class Bar1m
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public long Volume { get; set; }
    public double Value { get; set; }
    public long BuyVolume { get; set; }               // Khối lượng Mua chủ động
    public long SellVolume { get; set; }              // Khối lượng Bán chủ động
}
