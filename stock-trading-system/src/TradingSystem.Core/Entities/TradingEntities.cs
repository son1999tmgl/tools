using System;
using System.Collections.Generic;

namespace TradingSystem.Core.Entities;

/// <summary>
/// Bảng asset_categories: Nhóm ngành & Rổ chỉ số (Market, Sector, Basket)
/// </summary>
public class AssetCategory
{
    public string CategoryCode { get; set; } = string.Empty; // PK (VD: MARKET_ALL, BANKING, STEEL, VN30)
    public string CategoryName { get; set; } = string.Empty; // Tên nhóm (VD: Ngành Thép, Rổ VN30)
    public string CategoryType { get; set; } = string.Empty; // 'MARKET', 'SECTOR', 'INDEX_BASKET'
    public string? Description { get; set; }

    // Navigation
    public ICollection<Symbol> Symbols { get; set; } = new List<Symbol>();
}

/// <summary>
/// Bảng symbols: Danh mục toàn bộ mã tài sản (Cổ phiếu, CW, Phái sinh, Index)
/// </summary>
public class Symbol
{
    public string SymbolCode { get; set; } = string.Empty; // PK (VD: HPG, CACB2603, VN30F1M, 41I1GA000)
    public string Exchange { get; set; } = string.Empty;   // 'HOSE', 'HNX', 'UPCOM'
    public string AssetType { get; set; } = string.Empty;  // 'S' (Stock), 'W' (CW), 'D' (Derivative), 'I' (Index)
    public string? CategoryCode { get; set; }              // FK -> asset_categories
    public string? CompanyNameVi { get; set; }
    public string? CompanyNameEn { get; set; }
    public string? ShortName { get; set; }
    public string? Isin { get; set; }                      // Mã quốc tế ISIN (VD: VN41I1GA0001)
    public int LotSize { get; set; } = 100;                // CP: 100, Phái sinh: 1 hợp đồng
    public decimal ParValue { get; set; } = 10000m;        // Mệnh giá (CP: 10.000)
    public bool IsTrading { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigations
    public AssetCategory? Category { get; set; }
    public CoveredWarrant? CoveredWarrantSpec { get; set; }
    public DerivativeContract? DerivativeContractSpec { get; set; }
    public ICollection<MarketDailyBar> DailyBars { get; set; } = new List<MarketDailyBar>();
}

/// <summary>
/// Bảng covered_warrants: Thông số Chứng quyền (Black-Scholes, Greeks)
/// </summary>
public class CoveredWarrant
{
    public string SymbolCode { get; set; } = string.Empty;       // PK, FK -> symbols
    public string UnderlyingSymbol { get; set; } = string.Empty; // Mã CP cơ sở (VD: ACB, HPG)
    public string IssuerName { get; set; } = string.Empty;       // CTCK phát hành (TCBS, SSI, VND...)
    public string CwType { get; set; } = "C";                   // 'C' (Call) / 'P' (Put)
    public decimal ExercisePrice { get; set; }                   // Giá thực hiện (Strike Price K)
    public string ExerciseRatio { get; set; } = string.Empty;    // Chuỗi tỷ lệ (VD: "2:1")
    public double RatioMultiplier { get; set; }                 // Tỷ lệ chia số thực (VD: 2.0)
    public DateOnly? FirstTradingDate { get; set; }              // Ngày bắt đầu giao dịch
    public DateOnly? LastTradingDate { get; set; }               // Ngày GD cuối cùng
    public DateOnly MaturityDate { get; set; }                   // Ngày đáo hạn
    public long? ListedShares { get; set; }                      // Khối lượng lưu hành
    public string SettlementType { get; set; } = "CASH";
    public bool IsActive { get; set; } = true;

    // Navigations
    public Symbol? Symbol { get; set; }
    public Symbol? Underlying { get; set; }
}

/// <summary>
/// Bảng derivative_contracts: Thông số Hợp đồng Phái sinh (VN30F, Ký quỹ, Hạn)
/// </summary>
public class DerivativeContract
{
    public string SymbolCode { get; set; } = string.Empty;       // PK, FK -> symbols (VD: 41I1GA000, VN30F1M)
    public string UnderlyingIndex { get; set; } = "VN30";        // Chỉ số tham chiếu (VN30, VN100)
    public string ContractMonth { get; set; } = string.Empty;    // Tháng đáo hạn (VD: 202610)
    public int ContractMultiplier { get; set; } = 100000;        // Hệ số nhân (100.000 VNĐ / 1 điểm)
    public double InitialMarginRate { get; set; } = 0.17;        // Ký quỹ ban đầu IM (17%)
    public double MaintenanceMarginRate { get; set; } = 0.13;    // Ký quỹ duy trì MM (13%)
    public DateOnly? FirstTradingDate { get; set; }              // Ngày bắt đầu niêm yết
    public DateOnly LastTradingDate { get; set; }                // Ngày đáo hạn (Thứ 5 tuần thứ 3)
    public string SettlementMethod { get; set; } = "CASH";
    public bool IsActive { get; set; } = true;

    // Navigation
    public Symbol? Symbol { get; set; }
}

/// <summary>
/// Bảng market_daily_bars: Nến ngày EOD, Khối ngoại, Tự doanh, Room, Vị thế mở OI
/// </summary>
public class MarketDailyBar
{
    public string SymbolCode { get; set; } = string.Empty; // PK part 1
    public DateOnly TradingDate { get; set; }              // PK part 2
    public decimal? Open { get; set; }
    public decimal? High { get; set; }
    public decimal? Low { get; set; }
    public decimal? Close { get; set; }
    public decimal? RefPrice { get; set; }
    public decimal? CeilingPrice { get; set; }
    public decimal? FloorPrice { get; set; }
    public long TotalVolume { get; set; }                  // Cổ phiếu hoặc Hợp đồng
    public decimal TotalValue { get; set; }                // Giá trị GD (VNĐ)
    public long? BuyActiveVolume { get; set; }             // Khối lượng Mua chủ động
    public long? SellActiveVolume { get; set; }            // Khối lượng Bán chủ động
    public long? OpenInterest { get; set; }                // Vị thế mở qua đêm OI (Phái sinh)
    public long? ForeignBuyVol { get; set; }
    public long? ForeignSellVol { get; set; }
    public long? ForeignRoom { get; set; }

    // Navigation
    public Symbol? Symbol { get; set; }
}

/// <summary>
/// Bảng algorithmic_trading_signals: Tín hiệu đa cấp (Thị trường, Ngành, Mã, Long/Short)
/// </summary>
public class AlgorithmicTradingSignal
{
    public long Id { get; set; } // PK Auto Increment
    public string StrategyName { get; set; } = string.Empty;
    public string TargetScope { get; set; } = string.Empty;  // 'MARKET', 'SECTOR', 'BASKET', 'SYMBOL'
    public string TargetCode { get; set; } = string.Empty;   // 'VNINDEX', 'STEEL', 'VN30F1M', 'HPG'
    public string ActionType { get; set; } = string.Empty;   // 'OPEN_LONG', 'CLOSE_LONG', 'OPEN_SHORT', 'CLOSE_SHORT'
    public string Timeframe { get; set; } = "1M";            // '1M', '5M', '15M', 'DAILY'
    public DateTime SignalTime { get; set; } = DateTime.UtcNow;
    public decimal? TargetPrice { get; set; }
    public decimal? StopLossPrice { get; set; }
    public decimal? TakeProfitPrice { get; set; }
    public double? ConfidenceScore { get; set; }             // 0.0 -> 1.0
    public string? AffectedSymbolsJson { get; set; }         // JSON array danh sách mã ảnh hưởng
    public string? MetricsSnapshotJson { get; set; }         // JSON snapshot các chỉ báo (RSI, Basis, OBI...)
}

/// <summary>
/// Bảng backtest_runs: Lịch sử và thông số các lần chạy Backtest
/// </summary>
public class BacktestRun
{
    public Guid RunId { get; set; } = Guid.NewGuid(); // PK UUID
    public string StrategyName { get; set; } = string.Empty;
    public string AssetClass { get; set; } = string.Empty; // 'STOCK', 'COVERED_WARRANT', 'DERIVATIVE_VN30'
    public string? SymbolsTested { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal InitialCapital { get; set; }
    public decimal FinalEquity { get; set; }
    public decimal TotalNetPnl { get; set; }
    public double WinRate { get; set; }                    // Tỷ lệ thắng %
    public double? ProfitFactor { get; set; }
    public double? MaxDrawdown { get; set; }               // % Sụt giảm sâu nhất
    public double? SharpeRatio { get; set; }
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public string? ParametersJson { get; set; }            // Tham số config thuật toán
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<SimulatedTrade> Trades { get; set; } = new List<SimulatedTrade>();
}

/// <summary>
/// Bảng simulated_trades: Lịch sử vị thế 2 chiều (LONG/SHORT, Entry/Exit, PnL, MFE, MAE)
/// </summary>
public class SimulatedTrade
{
    public Guid TradeId { get; set; } = Guid.NewGuid(); // PK UUID
    public Guid RunId { get; set; }                     // FK -> backtest_runs
    public string SymbolCode { get; set; } = string.Empty;
    public string PositionSide { get; set; } = "LONG";  // 'LONG' hoặc 'SHORT'
    public DateTime EntryTime { get; set; }
    public decimal EntryPrice { get; set; }
    public DateTime? ExitTime { get; set; }
    public decimal? ExitPrice { get; set; }
    public int Quantity { get; set; }                   // Số CP hoặc Số hợp đồng phái sinh
    public int ContractMultiplier { get; set; } = 1;    // CP/CW: 1, Phái sinh: 100.000
    public decimal? GrossPnl { get; set; }
    public decimal? FeeAndTax { get; set; }             // Phí + Thuế + Phí vị thế VSD
    public decimal? NetPnl { get; set; }
    public double? ReturnPct { get; set; }              // % ROI trên vốn ký quỹ
    public string? ExitReason { get; set; }             // TAKE_PROFIT, STOP_LOSS, TIMEOUT...
    public decimal? MfeMaxRunup { get; set; }           // Lãi cực đại lúc gồng (Max Favorable Excursion)
    public decimal? MaeMaxDrawdown { get; set; }        // Lỗ sâu nhất lúc gồng (Max Adverse Excursion)

    // Navigation
    public BacktestRun? BacktestRun { get; set; }
}
