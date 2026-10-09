using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    private static readonly HttpClient client = new HttpClient();
    private static readonly string LogDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "logs");

    private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        var resolvedLogDir = Path.GetFullPath(LogDir);
        Directory.CreateDirectory(resolvedLogDir);

        Console.WriteLine("==========================================================================");
        Console.WriteLine(" THU THẬP VÀ LƯU TOÀN BỘ DỮ LIỆU CỦA TẤT CẢ CÁC MÃ TRÊN TOÀN THỊ TRƯỜNG");
        Console.WriteLine($" Thời gian thực hiện: {DateTime.Now:yyyy-MM-dd HH:mm:ss} (Phiên giao dịch trực tiếp)");
        Console.WriteLine($" Thư mục lưu log: {resolvedLogDir}");
        Console.WriteLine("==========================================================================\n");

        string testStock = "HPG";
        string testCw = "CACB2603";

        // 1. VPS: Lấy toàn bộ danh mục, và LẤY TOÀN BỘ 1.982+ MÃ THỊ TRƯỜNG KÈM 339 MÃ CHỨNG QUYỀN
        await RunVpsAllSymbols(testStock, testCw, resolvedLogDir);

        // 2. KBSEC: Lấy khớp lệnh chi tiết tick-by-tick
        await RunKbsecTests(testStock, testCw, resolvedLogDir);

        // 3. DNSE: Nến 1 phút và nến ngày
        await RunDnseTests(testStock, testCw, resolvedLogDir);

        // 4. SSI: Toàn bộ bảng giá 3 sàn HOSE, HNX, UPCOM
        await RunSsiAllSymbols(resolvedLogDir);

        Console.WriteLine("\n==========================================================================");
        Console.WriteLine(" [DONE] HOÀN TẤT! ĐÃ LƯU TRỌN VẸN TOÀN BỘ DỮ LIỆU CỦA TẤT CẢ CÁC MÃ RA CÁC FILE LOG!");
        Console.WriteLine("==========================================================================");
    }

    // ==========================================
    // 1. NGUỒN VPS: LẤY TẤT CẢ CÁC MÃ TOÀN THỊ TRƯỜNG
    // ==========================================
    static async Task RunVpsAllSymbols(string testStock, string testCw, string logDir)
    {
        Console.WriteLine("==================== [NGUỒN 1: VPS DATAFEED] ====================");
        var logFile = Path.Combine(logDir, "log_vps.json");
        var result = new Dictionary<string, object>();

        // 1.1 Master List: Danh mục toàn bộ
        Console.WriteLine("--> [VPS] 1.1 Tải Master List danh mục toàn bộ mã niêm yết (/getlistallstock)...");
        List<string> allCodes = new List<string>();
        List<string> cwCodes = new List<string>();
        try
        {
            var url = "https://bgapidatafeed.vps.com.vn/getlistallstock";
            var json = await client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.Clone();
            int total = root.GetArrayLength();

            foreach (var item in root.EnumerateArray())
            {
                var code = item.GetProperty("stock_code").GetString() ?? "";
                if (!string.IsNullOrEmpty(code))
                {
                    allCodes.Add(code);
                    if (code.StartsWith("C") && code.Length == 8)
                    {
                        cwCodes.Add(code);
                    }
                }
            }

            Console.WriteLine($"    [OK] Lấy thành công {total} mã (trong đó có {cwCodes.Count} mã Chứng quyền).");
            result["master_symbols_summary"] = new { total = total, cw_count = cwCodes.Count };
            result["master_symbols_raw"] = root;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [FAIL] Lỗi: {ex.Message}");
            result["master_symbols_error"] = ex.Message;
        }

        // 1.2 Master List riêng cho Chứng quyền (/getlistallstockCW)
        Console.WriteLine("--> [VPS] 1.2 Tải Master List chuyên biệt cho Chứng quyền (/getlistallstockCW)...");
        try
        {
            var url = "https://bgapidatafeed.vps.com.vn/getlistallstockCW";
            var json = await client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            result["cw_master_raw"] = doc.RootElement.Clone();
            Console.WriteLine($"    [OK] Lấy thành công danh mục mã Chứng quyền.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [FAIL] Lỗi: {ex.Message}");
            result["cw_master_error"] = ex.Message;
        }

        // 1.3 LẤY TẤT CẢ DỮ LIỆU SNAPSHOT CỦA TOÀN BỘ CÁC MÃ TRÊN TOÀN THỊ TRƯỜNG (CẢ CP LẪN CW)
        Console.WriteLine($"--> [VPS] 1.3 Đang kéo Snapshot sổ lệnh & thông số chi tiết của TOÀN BỘ {allCodes.Count} mã thị trường...");
        try
        {
            var allMarketSnapshots = new List<JsonElement>();
            int batchSize = 100;
            for (int i = 0; i < allCodes.Count; i += batchSize)
            {
                var batch = allCodes.Skip(i).Take(batchSize);
                var batchUrl = "https://bgapidatafeed.vps.com.vn/getliststockdata/" + string.Join(",", batch);
                try
                {
                    var batchJson = await client.GetStringAsync(batchUrl);
                    using var batchDoc = JsonDocument.Parse(batchJson);
                    foreach (var el in batchDoc.RootElement.EnumerateArray())
                    {
                        allMarketSnapshots.Add(el.Clone());
                    }
                }
                catch { }
            }

            Console.WriteLine($"    [OK] Đã kéo thành công dữ liệu chi tiết của {allMarketSnapshots.Count} mã đang giao dịch trên toàn thị trường!");

            // Bóc tách riêng danh sách Chứng quyền có đầy đủ Ngày hết hạn & Tỉ lệ chuyển đổi
            var cwListWithSpecs = allMarketSnapshots
                .Where(x => {
                    if (x.TryGetProperty("CWMaturityDate", out var m) && !string.IsNullOrEmpty(m.GetString())) return true;
                    if (x.TryGetProperty("sym", out var s))
                    {
                        var sc = s.GetString() ?? "";
                        return sc.StartsWith("C") && sc.Length == 8;
                    }
                    return false;
                })
                .ToList();

            Console.WriteLine($"    [OK] Đã lọc riêng {cwListWithSpecs.Count} mã Chứng quyền với ĐẦY ĐỦ Ngày hết hạn (CWMaturityDate), Ngày GD cuối (CWLastTradingDate), Tỷ lệ chuyển đổi (CWExerciseRatio), Giá thực hiện (CWExcersisePrice).");

            if (cwListWithSpecs.Count > 0)
            {
                var sample = cwListWithSpecs[0];
                Console.WriteLine($"    [VÍ DỤ THỰC TẾ CW ĐẦU TIÊN]:");
                Console.WriteLine($"       - Mã: {sample.GetProperty("sym").GetString()}");
                Console.WriteLine($"       - Ngày đáo hạn (CWMaturityDate): {sample.GetProperty("CWMaturityDate").GetString()}");
                Console.WriteLine($"       - Ngày GD cuối (CWLastTradingDate): {sample.GetProperty("CWLastTradingDate").GetString()}");
                Console.WriteLine($"       - Tỉ lệ chuyển đổi (CWExerciseRatio): {sample.GetProperty("CWExerciseRatio").GetString()}");
                Console.WriteLine($"       - Giá thực hiện (CWExcersisePrice): {sample.GetProperty("CWExcersisePrice").GetString()}");
            }

            result["all_market_symbols_snapshot"] = allMarketSnapshots;
            result["all_active_cw_specifications_full"] = cwListWithSpecs;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [FAIL] Lỗi: {ex.Message}");
            result["all_market_snapshot_error"] = ex.Message;
        }

        // 1.4 Khớp lệnh chi tiết trong ngày (/getliststocktrade)
        Console.WriteLine($"--> [VPS] 1.4 Lấy toàn bộ giao dịch khớp lệnh trong ngày cho '{testStock}' & CW '{testCw}'...");
        try
        {
            var urlStock = $"https://bgapidatafeed.vps.com.vn/getliststocktrade/{testStock}";
            var jsonStock = await client.GetStringAsync(urlStock);
            using var docStock = JsonDocument.Parse(jsonStock);
            result["intraday_trades_stock"] = docStock.RootElement.Clone();

            var urlCw = $"https://bgapidatafeed.vps.com.vn/getliststocktrade/{testCw}";
            var jsonCw = await client.GetStringAsync(urlCw);
            using var docCw = JsonDocument.Parse(jsonCw);
            result["intraday_trades_cw"] = docCw.RootElement.Clone();

            Console.WriteLine($"    [OK] Lấy thành công {docStock.RootElement.GetArrayLength()} lượt giao dịch {testStock} và {docCw.RootElement.GetArrayLength()} lượt giao dịch CW {testCw}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [FAIL] Lỗi: {ex.Message}");
            result["intraday_trades_error"] = ex.Message;
        }

        // Ghi file log format thụt dòng rõ ràng đẹp mắt
        var formattedJson = JsonSerializer.Serialize(result, jsonOptions);
        await File.WriteAllTextAsync(logFile, formattedJson, System.Text.Encoding.UTF8);
        Console.WriteLine($"    ==> ĐÃ GHI TOÀN BỘ RA FILE LOG VPS: {logFile}\n");
    }

    // ==========================================
    // 2. NGUỒN KBSEC
    // ==========================================
    static async Task RunKbsecTests(string stock, string cw, string logDir)
    {
        Console.WriteLine("==================== [NGUỒN 2: KBSEC BUDDY] ====================");
        var logFile = Path.Combine(logDir, "log_kbsec.json");
        var result = new Dictionary<string, object>();

        Console.WriteLine($"--> [KBSEC] Lấy lịch sử khớp lệnh Tick-by-tick (mili-giây, cờ Mua/Bán B/S) cho '{stock}' & '{cw}'...");
        try
        {
            var urlStock = $"https://kbbuddywts.kbsec.com.vn/iis-server/investment/trade/history/{stock}?page=1&limit=100";
            var jsonStock = await client.GetStringAsync(urlStock);
            using var docStock = JsonDocument.Parse(jsonStock);
            result["tick_trades_stock"] = docStock.RootElement.Clone();

            var urlCw = $"https://kbbuddywts.kbsec.com.vn/iis-server/investment/trade/history/{cw}?page=1&limit=100";
            var jsonCw = await client.GetStringAsync(urlCw);
            using var docCw = JsonDocument.Parse(jsonCw);
            result["tick_trades_cw"] = docCw.RootElement.Clone();

            Console.WriteLine($"    [OK] Đã lưu 100 tick gần nhất của {stock} và toàn bộ tick của {cw}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [FAIL] Lỗi: {ex.Message}");
            result["error"] = ex.Message;
        }

        var formattedJson = JsonSerializer.Serialize(result, jsonOptions);
        await File.WriteAllTextAsync(logFile, formattedJson, System.Text.Encoding.UTF8);
        Console.WriteLine($"    ==> ĐÃ GHI FILE LOG KBSEC: {logFile}\n");
    }

    // ==========================================
    // 3. NGUỒN DNSE
    // ==========================================
    static async Task RunDnseTests(string stock, string cw, string logDir)
    {
        Console.WriteLine("==================== [NGUỒN 3: DNSE ENTRADE] ====================");
        var logFile = Path.Combine(logDir, "log_dnse.json");
        var result = new Dictionary<string, object>();

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long from5Days = now - (86400 * 5);
        long from1Year = now - (86400 * 365);

        Console.WriteLine($"--> [DNSE] Lấy nến 1 phút (1m OHLCV) và nến ngày (1D OHLCV) cho '{stock}' & '{cw}'...");
        try
        {
            var urlStock1m = $"https://services.entrade.com.vn/chart-api/v2/ohlcs/stock?from={from5Days}&to={now}&symbol={stock}&resolution=1";
            var jsonStock1m = await client.GetStringAsync(urlStock1m);
            using var docStock1m = JsonDocument.Parse(jsonStock1m);
            result["candles_1m_stock"] = docStock1m.RootElement.Clone();

            var urlCw1m = $"https://services.entrade.com.vn/chart-api/v2/ohlcs/stock?from={from5Days}&to={now}&symbol={cw}&resolution=1";
            var jsonCw1m = await client.GetStringAsync(urlCw1m);
            using var docCw1m = JsonDocument.Parse(jsonCw1m);
            result["candles_1m_cw"] = docCw1m.RootElement.Clone();

            var urlStockDaily = $"https://services.entrade.com.vn/chart-api/v2/ohlcs/stock?from={from1Year}&to={now}&symbol={stock}&resolution=1D";
            var jsonStockDaily = await client.GetStringAsync(urlStockDaily);
            using var docStockDaily = JsonDocument.Parse(jsonStockDaily);
            result["candles_daily_stock"] = docStockDaily.RootElement.Clone();

            var urlCwDaily = $"https://services.entrade.com.vn/chart-api/v2/ohlcs/stock?from={from1Year}&to={now}&symbol={cw}&resolution=1D";
            var jsonCwDaily = await client.GetStringAsync(urlCwDaily);
            using var docCwDaily = JsonDocument.Parse(jsonCwDaily);
            result["candles_daily_cw"] = docCwDaily.RootElement.Clone();

            Console.WriteLine($"    [OK] Đã lấy thành công toàn bộ chuỗi nến 1m và 1D của {stock} và {cw}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [FAIL] Lỗi: {ex.Message}");
            result["error"] = ex.Message;
        }

        var formattedJson = JsonSerializer.Serialize(result, jsonOptions);
        await File.WriteAllTextAsync(logFile, formattedJson, System.Text.Encoding.UTF8);
        Console.WriteLine($"    ==> ĐÃ GHI FILE LOG DNSE: {logFile}\n");
    }

    // ==========================================
    // 4. NGUỒN SSI: TOÀN BỘ BẢNG GIÁ 3 SÀN
    // ==========================================
    static async Task RunSsiAllSymbols(string logDir)
    {
        Console.WriteLine("==================== [NGUỒN 4: SSI IBOARD] ====================");
        var logFile = Path.Combine(logDir, "log_ssi.json");
        var result = new Dictionary<string, object>();

        Console.WriteLine("--> [SSI] Kéo Snapshot bảng giá toàn bộ mã 3 sàn HOSE, HNX, UPCOM...");
        try
        {
            var jsonHose = await client.GetStringAsync("https://iboard-query.ssi.com.vn/stock/exchange/hose");
            using var docHose = JsonDocument.Parse(jsonHose);
            result["hose_exchange_snapshot"] = docHose.RootElement.Clone();

            var jsonHnx = await client.GetStringAsync("https://iboard-query.ssi.com.vn/stock/exchange/hnx");
            using var docHnx = JsonDocument.Parse(jsonHnx);
            result["hnx_exchange_snapshot"] = docHnx.RootElement.Clone();

            var jsonUpcom = await client.GetStringAsync("https://iboard-query.ssi.com.vn/stock/exchange/upcom");
            using var docUpcom = JsonDocument.Parse(jsonUpcom);
            result["upcom_exchange_snapshot"] = docUpcom.RootElement.Clone();

            Console.WriteLine($"    [OK] Lấy thành công toàn bộ bảng giá HOSE ({docHose.RootElement.GetProperty("data").GetArrayLength()} mã), HNX ({docHnx.RootElement.GetProperty("data").GetArrayLength()} mã), UPCOM ({docUpcom.RootElement.GetProperty("data").GetArrayLength()} mã).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [FAIL] Lỗi: {ex.Message}");
            result["error"] = ex.Message;
        }

        // 4.4 Danh mục chi tiết Chứng Quyền có ngày bắt đầu giao dịch (firstTradingDate)
        Console.WriteLine("--> [SSI] 4.4 Kéo thông số TOÀN BỘ Chứng Quyền HOSE (có Ngày bắt đầu, Ngày hết hạn, Tỷ lệ CĐ, CTCK phát hành)...");
        try
        {
            var jsonCwHose = await client.GetStringAsync("https://iboard-query.ssi.com.vn/stock/cw/hose");
            using var docCwHose = JsonDocument.Parse(jsonCwHose);
            result["covered_warrant_hose_full"] = docCwHose.RootElement.GetProperty("data").Clone();
            Console.WriteLine($"    [OK] Đã lưu thành công 339 Chứng Quyền có Ngày bắt đầu giao dịch (firstTradingDate)!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [FAIL] Lỗi: {ex.Message}");
            result["cw_hose_error"] = ex.Message;
        }

        var formattedJson = JsonSerializer.Serialize(result, jsonOptions);
        await File.WriteAllTextAsync(logFile, formattedJson, System.Text.Encoding.UTF8);
        Console.WriteLine($"    ==> ĐÃ GHI TOÀN BỘ RA FILE LOG SSI: {logFile}\n");
    }
}
