using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TradingSystem.Backtest.Core;
using TradingSystem.Infrastructure.Feeds;
using TradingSystem.Infrastructure.Storage;

Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("==========================================================================");
Console.WriteLine(" 📊 HỆ THỐNG KIỂM THỬ THUẬT TOÁN ĐỊNH LƯỢNG (QUANT BACKTEST ENGINE)");
Console.WriteLine("==========================================================================\n");

// 1. Khởi tạo kho quản lý thuật toán (Registry)
var registry = new StrategyRegistry();
var allStrategies = registry.GetAll();

Console.WriteLine($"🔍 [KHO THUẬT TOÁN]: Đã nạp thành công {allStrategies.Count} mô hình toán học:");
foreach (var s in allStrategies)
{
    string tag = s.StrategyType == "BUY_MODEL" ? "🟢 [DỰ ĐOÁN MUA]" : "🔴 [DỰ ĐOÁN BÁN/SHORT]";
    Console.WriteLine($"   • {tag,-20} [{s.Id}] {s.Name}");
    Console.WriteLine($"     Mô tả: {s.Description}");
}
Console.WriteLine();

// 2. Cấu hình Engine
var baseDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
var storageDir = Path.Combine(baseDir, "data", "storage");
var parquetEngine = new ParquetStorageEngine(storageDir);
var dnseFeed = new DnseDataFeed();
var engine = new BacktestEngine(parquetEngine, dnseFeed);

// 3. Phân tích tham số CLI (nếu có)
string strategyId = "BUY_TREND_PULLBACK_V1";
string symbol = "VN30F1M";
var startDate = new DateOnly(2026, 10, 1);
var endDate = new DateOnly(2026, 10, 11);
decimal capital = 100_000_000m;

// Hỗ trợ truyền tham số: --strategy X --symbol Y --from YYYY-MM-DD --to YYYY-MM-DD
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--strategy" && i + 1 < args.Length) strategyId = args[++i];
    if (args[i] == "--symbol" && i + 1 < args.Length) symbol = args[++i].ToUpper();
    if (args[i] == "--from" && i + 1 < args.Length) startDate = DateOnly.Parse(args[++i]);
    if (args[i] == "--to" && i + 1 < args.Length) endDate = DateOnly.Parse(args[++i]);
    if (args[i] == "--capital" && i + 1 < args.Length) capital = decimal.Parse(args[++i]);
}

// 4. Nếu chạy mặc định (không truyền flag cụ thể) -> Chạy demo cả 2 mô hình Mua & Bán trên Phái sinh VN30F1M
if (args.Length == 0)
{
    Console.WriteLine("--------------------------------------------------------------------------");
    Console.WriteLine($"▶️ CHẠY KIỂM THỬ TẤT CẢ MÔ HÌNH TRÊN '{symbol}' (TỪ {startDate} -> {endDate})");
    Console.WriteLine("--------------------------------------------------------------------------\n");

    foreach (var st in allStrategies)
    {
        await RunAndPrintReport(engine, st, symbol, startDate, endDate, capital);
    }
}
else
{
    var selectedStrategy = registry.GetById(strategyId);
    if (selectedStrategy == null)
    {
        Console.WriteLine($"❌ Không tìm thấy thuật toán với ID '{strategyId}'.");
        return;
    }

    await RunAndPrintReport(engine, selectedStrategy, symbol, startDate, endDate, capital);
}

static async Task RunAndPrintReport(
    BacktestEngine engine,
    ITradingStrategy strategy,
    string symbol,
    DateOnly start,
    DateOnly end,
    decimal capital)
{
    Console.WriteLine($"⏳ Đang chạy mô phỏng mô hình: [{strategy.Id}]...");
    var report = await engine.RunAsync(strategy, symbol, start, end, capital);

    Console.WriteLine("==========================================================================");
    Console.WriteLine($" 🏆 BÁO CÁO KẾT QUẢ: {report.StrategyName.ToUpper()}");
    Console.WriteLine($" • Mã tài sản: {report.Symbol} | Khoảng thời gian: {report.StartDate} -> {report.EndDate}");
    Console.WriteLine("--------------------------------------------------------------------------");
    Console.WriteLine($" Vốn khởi điểm:         {report.InitialCapital,18:N0} VNĐ");
    Console.WriteLine($" Vốn kết thúc:          {report.FinalEquity,18:N0} VNĐ");
    
    string pnlColor = report.TotalNetPnl >= 0 ? "+" : "";
    Console.WriteLine($" Lời/Lỗ ròng (Net PnL): {pnlColor + report.TotalNetPnl.ToString("N0"),18} VNĐ");
    Console.WriteLine($" Tỷ lệ thắng (Win Rate):{report.WinRate,17:F1} %");
    Console.WriteLine($" Hệ số Lời/Lỗ (Profit Factor):{report.ProfitFactor,12:F2}");
    Console.WriteLine($" Sụt giảm tối đa (Max Drawdown):{report.MaxDrawdownPct,10:F2} %");
    Console.WriteLine($" Chỉ số Sharpe Ratio:   {report.SharpeRatio,18:F2}");
    Console.WriteLine($" Tổng số lệnh:          {report.TotalTrades,18} (Thắng: {report.WinningTrades} | Thua: {report.LosingTrades})");
    Console.WriteLine("--------------------------------------------------------------------------");

    if (report.Trades.Count > 0)
    {
        Console.WriteLine(" 📜 CHI TIẾT 5 LỆNH ĐẦU TIÊN ĐƯỢC KHỚP:");
        Console.WriteLine($" {"Side",-6} | {"Entry Time",-19} | {"Entry",8} | {"Exit",8} | {"Net PnL (VNĐ)",14} | {"MFE (Lãi Max)",14} | {"Lý do thoát",-12}");
        foreach (var t in report.Trades.Take(5))
        {
            Console.WriteLine($" {t.PositionSide,-6} | {t.EntryTime:yyyy-MM-dd HH:mm:ss} | {t.EntryPrice,8:F1} | {t.ExitPrice,8:F1} | {t.NetPnl,14:N0} | {t.MfeMaxRunup,14:N0} | {t.ExitReason,-12}");
        }
    }
    else
    {
        Console.WriteLine(" (Không phát sinh lệnh giao dịch nào thỏa mãn hệ điều kiện trong khoảng thời gian này)");
    }

    Console.WriteLine("==========================================================================\n");
}
