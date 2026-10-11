using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradingSystem.Core.Entities;
using TradingSystem.Core.Enums;
using TradingSystem.Core.Models.TimeSeries;
using TradingSystem.Infrastructure.Feeds;
using TradingSystem.Infrastructure.Storage;

namespace TradingSystem.Backtest.Core;

/// <summary>
/// Báo cáo tổng kết chỉ số thống kê của một đợt chạy Backtest
/// </summary>
public class BacktestReport
{
    public Guid RunId { get; set; } = Guid.NewGuid();
    public string StrategyId { get; set; } = string.Empty;
    public string StrategyName { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal InitialCapital { get; set; }
    public decimal FinalEquity { get; set; }
    public decimal TotalNetPnl { get; set; }
    public double WinRate { get; set; }               // Tỷ lệ thắng %
    public double ProfitFactor { get; set; }          // Hệ số Lời/Lỗ
    public double MaxDrawdownPct { get; set; }        // Sụt giảm tối đa %
    public double SharpeRatio { get; set; }
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public List<SimulatedTrade> Trades { get; set; } = new();
}

/// <summary>
/// ĐỘNG CƠ BACKTEST ĐA TÀI SẢN (BACKTEST ENGINE):
/// - Nhận mô hình toán học và khoảng thời gian từ Ngày A -> Ngày B.
/// - Tự động tải nến Parquet / Fallback DNSE.
/// - Khớp lệnh mô phỏng 2 chiều LONG / SHORT.
/// - Tính đòn bẩy ký quỹ 17% phái sinh (hệ số nhân 100.000) và đo lường MFE / MAE.
/// </summary>
public class BacktestEngine
{
    private readonly ParquetStorageEngine _parquetEngine;
    private readonly DnseDataFeed _dnseFeed;

    public BacktestEngine(ParquetStorageEngine parquetEngine, DnseDataFeed dnseFeed)
    {
        _parquetEngine = parquetEngine;
        _dnseFeed = dnseFeed;
    }

    /// <summary>
    /// Chạy Backtest một mô hình toán học trên một mã cụ thể trong khoảng từ Ngày A -> Ngày B
    /// </summary>
    public async Task<BacktestReport> RunAsync(
        ITradingStrategy strategy,
        string symbol,
        DateOnly startDate,
        DateOnly endDate,
        decimal initialCapital = 100_000_000m,
        CancellationToken ct = default)
    {
        bool isDerivative = symbol.StartsWith("VN30F") || symbol.StartsWith("41I");
        int contractMultiplier = isDerivative ? 100000 : 1;
        decimal priceMultiplier = isDerivative ? 100000m : 1000m;

        // 1. Tải chuỗi nến lịch sử từ Ngày A -> Ngày B
        var allBars = await LoadBarsRangeAsync(symbol, isDerivative, startDate, endDate, ct);

        var report = new BacktestReport
        {
            StrategyId = strategy.Id,
            StrategyName = strategy.Name,
            Symbol = symbol,
            StartDate = startDate,
            EndDate = endDate,
            InitialCapital = initialCapital,
            FinalEquity = initialCapital
        };

        if (allBars.Count < 30)
        {
            return report;
        }

        // 2. Chạy mô hình toán học để sinh toàn bộ tín hiệu
        var signals = strategy.GenerateSignals(symbol, allBars);

        // 3. Khớp lệnh mô phỏng từng vòng đời vị thế (Round-trip)
        var trades = new List<SimulatedTrade>();
        decimal currentEquity = initialCapital;
        decimal peakEquity = initialCapital;
        decimal maxDrawdown = 0;

        decimal totalWinAmount = 0;
        decimal totalLossAmount = 0;
        var returnsList = new List<double>();

        var barDict = allBars.Select((b, idx) => new { b, idx }).ToDictionary(x => x.b.Timestamp, x => x.idx);

        foreach (var signal in signals)
        {
            if (!barDict.TryGetValue(signal.SignalTime, out int entryIdx)) continue;
            if (entryIdx >= allBars.Count - 2) continue;

            decimal entryPrice = signal.TargetPrice ?? (decimal)allBars[entryIdx].Close;
            decimal takeProfit = signal.TakeProfitPrice ?? (entryPrice * 1.02m);
            decimal stopLoss = signal.StopLossPrice ?? (entryPrice * 0.99m);
            bool isLong = signal.ActionType == SignalActionType.OpenLong;
            bool isShort = signal.ActionType == SignalActionType.OpenShort;

            if (!isLong && !isShort) continue;

            // =========================================================================
            // BƯỚC 1: QUẢN TRỊ VỐN & TÍNH KHỐI LƯỢNG VÀO LỆNH (POSITION SIZING)
            // =========================================================================
            int quantity;
            if (isDerivative)
            {
                // PHÁI SINH VIỆT NAM (VN30F1M):
                // - Giá trị 1 Hợp đồng (Notional Value) = Điểm_số * 100.000 VNĐ.
                // - Tỷ lệ Ký quỹ Ban Đầu (Initial Margin - IM) quy định bởi VSD: 17% (~ Đòn bẩy 5.88x).
                // - Tiền ký quỹ tối thiểu cho 1 HĐ = Điểm_số * 100.000 * 0.17.
                // - Quản trị rủi ro: Chỉ phân bổ tối đa 40% vốn tổng (currentEquity) để duy trì vùng đệm an toàn, tránh Force Margin / Call Margin.
                decimal marginPerContract = entryPrice * 100000m * 0.17m;
                quantity = Math.Max(1, (int)((currentEquity * 0.4m) / marginPerContract));
            }
            else
            {
                // CỔ PHIẾU CƠ SỞ (HOSE/HNX):
                // - Quy định sàn HOSE: Khớp lệnh theo Lô chẵn 100 cổ phiếu.
                // - Giá nến trên sàn DNSE/HOSE niêm yết theo nghìn đồng (VD: 20.2 tức 20.200 VNĐ).
                // - Quản trị rủi ro: Phân bổ 25% vốn tổng cho mỗi vị thế (tối đa nắm 4 mã), làm tròn xuống bội số 100.
                int rawShares = (int)((currentEquity * 0.25m) / (entryPrice * 1000m));
                quantity = Math.Max(100, (rawShares / 100) * 100);
            }

            // =========================================================================
            // BƯỚC 2: MÔ PHỎNG VÒNG ĐỜI LỆNH & ĐO LƯỜNG TÂM LÝ GỒNG LÃI/LỖ (MFE & MAE)
            // =========================================================================
            decimal? exitPrice = null;
            DateTime? exitTime = null;
            string exitReason = "TIMEOUT";
            decimal mfe = 0; // MFE (Maximum Favorable Excursion): Số tiền LÃI CỰC ĐẠI từng đạt được khi vị thế đang mở
            decimal mae = 0; // MAE (Maximum Adverse Excursion): Số tiền LỖ SÂU NHẤT từng phải chịu đựng (áp lực tâm lý gồng lỗ)

            int maxHoldingBars = 120; // Giới hạn giữ lệnh tối đa 120 phút (2 tiếng) để tránh rủi ro qua phiên/qua đêm
            int endCheck = Math.Min(allBars.Count, entryIdx + maxHoldingBars);

            for (int j = entryIdx + 1; j < endCheck; j++)
            {
                var curBar = allBars[j];
                decimal high = (decimal)curBar.High;
                decimal low = (decimal)curBar.Low;
                decimal close = (decimal)curBar.Close;

                // Đo lường MFE & MAE trong từng nhịp nến để đánh giá hiệu quả điểm vào (Entry Efficiency)
                if (isLong)
                {
                    decimal runup = (high - entryPrice) * quantity * priceMultiplier;
                    decimal drawdown = (entryPrice - low) * quantity * priceMultiplier;
                    if (runup > mfe) mfe = runup;
                    if (drawdown > mae) mae = drawdown;

                    // Kiểm tra chạm ngưỡng Chốt lời (Take Profit)
                    if (high >= takeProfit)
                    {
                        exitPrice = takeProfit;
                        exitTime = curBar.Timestamp;
                        exitReason = "TAKE_PROFIT";
                        break;
                    }
                    // Kiểm tra chạm ngưỡng Cắt lỗ (Stop Loss)
                    if (low <= stopLoss)
                    {
                        exitPrice = stopLoss;
                        exitTime = curBar.Timestamp;
                        exitReason = "STOP_LOSS";
                        break;
                    }
                }
                else // Vị thế SHORT (Bán khống phái sinh)
                {
                    decimal runup = (entryPrice - low) * quantity * priceMultiplier;
                    decimal drawdown = (high - entryPrice) * quantity * priceMultiplier;
                    if (runup > mfe) mfe = runup;
                    if (drawdown > mae) mae = drawdown;

                    if (low <= takeProfit)
                    {
                        exitPrice = takeProfit;
                        exitTime = curBar.Timestamp;
                        exitReason = "TAKE_PROFIT";
                        break;
                    }
                    if (high >= stopLoss)
                    {
                        exitPrice = stopLoss;
                        exitTime = curBar.Timestamp;
                        exitReason = "STOP_LOSS";
                        break;
                    }
                }
            }

            // Nếu hết thời gian nắm giữ tối đa mà chưa dính TP/SL -> Tự động đóng vị thế theo giá thị trường (Market Exit)
            if (!exitPrice.HasValue)
            {
                var lastBar = allBars[endCheck - 1];
                exitPrice = (decimal)lastBar.Close;
                exitTime = lastBar.Timestamp;
                exitReason = "TIMEOUT";
            }

            // =========================================================================
            // BƯỚC 3: TÍNH TOÁN LỜI/LỖ THỰC TẾ TRỪ ĐẦY ĐỦ THUẾ & PHÍ GIAO DỊCH
            // =========================================================================
            // Lãi/Lỗ gộp trước thuế phí:
            decimal grossPnl = isLong 
                ? (exitPrice.Value - entryPrice) * quantity * priceMultiplier
                : (entryPrice - exitPrice.Value) * quantity * priceMultiplier;

            // Chi phí giao dịch tại Việt Nam:
            decimal feeAndTax;
            if (isDerivative)
            {
                // Phái sinh: Phí công ty chứng khoán + Phí quản lý vị thế VSD + Thuế TNCN ≈ 15.000 VNĐ / HĐ cho cả 2 chiều
                feeAndTax = quantity * 15000m;
            }
            else
            {
                // Cổ phiếu: Phí môi giới 0.15% cả 2 chiều (mua+bán) + Thuế chuyển nhượng 0.1% chiều bán (tổng ~ 0.25% - 0.2%)
                feeAndTax = (entryPrice + exitPrice.Value) * 1000m * quantity * 0.002m;
            }

            // Lợi nhuận ròng (Net PnL) bỏ túi thực tế:
            decimal netPnl = grossPnl - feeAndTax;
            currentEquity += netPnl;

            // Tính Drawdown tổng của danh mục tài khoản
            if (currentEquity > peakEquity) peakEquity = currentEquity;
            decimal dd = (peakEquity - currentEquity) / peakEquity;
            if (dd > maxDrawdown) maxDrawdown = dd;

            // Tính tỷ suất lợi nhuận (%) trên số vốn ký quỹ thực bỏ ra cho lệnh đó
            double returnPct = (double)(netPnl / Math.Max(1, isDerivative ? (entryPrice * quantity * 100000m * 0.17m) : (entryPrice * quantity * 1000m))) * 100;
            returnsList.Add(returnPct);

            if (netPnl > 0) totalWinAmount += netPnl;
            else totalLossAmount += Math.Abs(netPnl);

            trades.Add(new SimulatedTrade
            {
                TradeId = Guid.NewGuid(),
                RunId = report.RunId,
                SymbolCode = symbol,
                PositionSide = isLong ? PositionSide.Long : PositionSide.Short,
                EntryTime = signal.SignalTime,
                EntryPrice = entryPrice,
                ExitTime = exitTime,
                ExitPrice = exitPrice,
                Quantity = quantity,
                ContractMultiplier = contractMultiplier,
                GrossPnl = grossPnl,
                FeeAndTax = feeAndTax,
                NetPnl = netPnl,
                ReturnPct = returnPct,
                ExitReason = exitReason,
                MfeMaxRunup = mfe,
                MaeMaxDrawdown = mae
            });
        }

        // 4. Tổng hợp các chỉ số định lượng
        int total = trades.Count;
        int winCount = trades.Count(t => t.NetPnl > 0);
        int lossCount = trades.Count(t => t.NetPnl <= 0);

        report.TotalTrades = total;
        report.WinningTrades = winCount;
        report.LosingTrades = lossCount;
        report.WinRate = total > 0 ? ((double)winCount / total) * 100 : 0;
        report.TotalNetPnl = currentEquity - initialCapital;
        report.FinalEquity = currentEquity;
        report.ProfitFactor = totalLossAmount > 0 ? (double)(totalWinAmount / totalLossAmount) : (totalWinAmount > 0 ? 99.0 : 0);
        report.MaxDrawdownPct = (double)(maxDrawdown * 100);
        report.SharpeRatio = CalculateSharpeRatio(returnsList);
        report.Trades = trades;

        return report;
    }

    private async Task<List<Bar1m>> LoadBarsRangeAsync(string symbol, bool isDerivative, DateOnly startDate, DateOnly endDate, CancellationToken ct)
    {
        var result = new List<Bar1m>();

        // 1. Thử đọc từ Parquet Storage trước
        var curDate = startDate;
        while (curDate <= endDate)
        {
            var barsInDay = await _parquetEngine.ReadBars1mAsync(symbol, curDate);
            if (barsInDay.Count > 0)
            {
                result.AddRange(barsInDay);
            }
            curDate = curDate.AddDays(1);
        }

        // 2. Nếu Parquet chưa có đủ dữ liệu khoảng ngày này -> Kéo trực tiếp từ DNSE
        if (result.Count < 50)
        {
            long fromUnix = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUnixTimeSeconds();
            long toUnix = new DateTimeOffset(endDate.ToDateTime(new TimeOnly(15, 0)), TimeSpan.FromHours(7)).ToUnixTimeSeconds();

            if (isDerivative)
            {
                var dnseBars = await _dnseFeed.GetDerivativeBars1mAsync(symbol, fromUnix, toUnix, ct);
                result.AddRange(dnseBars);
            }
            else
            {
                var dnseBars = await _dnseFeed.GetStockBars1mAsync(symbol, fromUnix, toUnix, ct);
                result.AddRange(dnseBars);
            }
        }

        return result.OrderBy(b => b.Timestamp).ToList();
    }

    private static double CalculateSharpeRatio(List<double> returns)
    {
        if (returns.Count < 2) return 0;
        double avg = returns.Average();
        double sumSq = returns.Sum(r => Math.Pow(r - avg, 2));
        double stdDev = Math.Sqrt(sumSq / (returns.Count - 1));
        if (stdDev == 0) return 0;
        // Chuẩn hóa hàng năm (Annualized Sharpe)
        return Math.Round((avg / stdDev) * Math.Sqrt(250), 2);
    }
}
