using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    private static readonly HttpClient client = new HttpClient();

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

        Console.WriteLine("==================================================================");
        Console.WriteLine(" BẮT ĐẦU TEST CÁC NGUỒN DỮ LIỆU CHỨNG KHOÁN & CHỨNG QUYỀN (C# .NET)");
        Console.WriteLine("==================================================================\n");

        await TestVpsMasterList();
        await TestKbsecIntradayTrades("HPG");
        await TestKbsecIntradayTrades("CACB2515"); // Mã chứng quyền (CW)
        await TestDnseIntradayCandles("HPG");
        await TestDnseIntradayCandles("CACB2515");
        await TestSsiExchangeSnapshot();

        Console.WriteLine("\n[DONE] Hoàn tất quá trình kiểm thử các nguồn dữ liệu!");
    }

    // 1. Test VPS: Lấy danh mục toàn bộ cổ phiếu và chứng quyền
    static async Task TestVpsMasterList()
    {
        Console.WriteLine("--> [NGUỒN 1: VPS] Lấy danh mục toàn bộ mã niêm yết (Master Symbols)...");
        try
        {
            var url = "https://bgapidatafeed.vps.com.vn/getlistallstock";
            var json = await client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            int total = root.GetArrayLength();

            int cwCount = 0;
            string sampleCw = "";
            foreach (var item in root.EnumerateArray())
            {
                var code = item.GetProperty("stock_code").GetString() ?? "";
                if (code.StartsWith("C") && code.Length == 8)
                {
                    cwCount++;
                    if (string.IsNullOrEmpty(sampleCw)) sampleCw = code;
                }
            }

            Console.WriteLine($"    [OK] Lấy thành công {total} mã giao dịch.");
            Console.WriteLine($"    [OK] Trong đó có {cwCount} mã Chứng quyền (CW). Mã mẫu: {sampleCw}\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [FAIL] Lỗi: {ex.Message}\n");
        }
    }

    // 2. Test KBSec: Lấy từng lệnh khớp (Tick-by-tick)
    static async Task TestKbsecIntradayTrades(string symbol)
    {
        Console.WriteLine($"--> [NGUỒN 2: KBSec] Lấy dữ liệu khớp lệnh Intraday (Tick) cho mã '{symbol}'...");
        try
        {
            var url = $"https://kbbuddywts.kbsec.com.vn/iis-server/investment/trade/history/{symbol}?page=1&limit=50";
            var json = await client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            int count = data.GetArrayLength();

            Console.WriteLine($"    [OK] Lấy thành công {count} lệnh khớp gần nhất.");
            if (count > 0)
            {
                var first = data[0];
                var time = first.GetProperty("t").GetString();
                var price = first.GetProperty("FMP").GetDecimal();
                var vol = first.GetProperty("FV").GetInt64();
                var side = first.GetProperty("LC").GetString();
                string sideStr = side == "B" ? "Mua chủ động" : (side == "S" ? "Bán chủ động" : "Khớp định kỳ ATO/ATC");
                Console.WriteLine($"    [MẪU LỆNH] Thời gian: {time} | Giá: {price} | KL: {vol:N0} | Chiều: {sideStr} ({side})");
            }
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [FAIL] Lỗi: {ex.Message}\n");
        }
    }

    // 3. Test DNSE: Lấy nến 1 phút (1-minute OHLCV)
    static async Task TestDnseIntradayCandles(string symbol)
    {
        Console.WriteLine($"--> [NGUỒN 3: DNSE Entrade] Lấy nến 1 phút (1m OHLCV) cho mã '{symbol}'...");
        try
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long from = now - (86400 * 5); // 5 ngày trước
            var url = $"https://services.entrade.com.vn/chart-api/v2/ohlcs/stock?from={from}&to={now}&symbol={symbol}&resolution=1";
            var json = await client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("t", out var tProp) && tProp.ValueKind == JsonValueKind.Array)
            {
                int bars = tProp.GetArrayLength();
                var cProp = root.GetProperty("c");
                var vProp = root.GetProperty("v");

                Console.WriteLine($"    [OK] Lấy thành công {bars} nến 1 phút.");
                if (bars > 0)
                {
                    decimal lastClose = cProp[bars - 1].GetDecimal();
                    long lastVol = vProp[bars - 1].GetInt64();
                    Console.WriteLine($"    [NẾN GẦN NHẤT] Giá đóng cửa: {lastClose} | Khối lượng: {lastVol:N0}");
                }
            }
            else
            {
                Console.WriteLine("    [WARN] Không có nến trả về.");
            }
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [FAIL] Lỗi: {ex.Message}\n");
        }
    }

    // 4. Test SSI: Lấy Snapshot bảng giá toàn sàn HOSE
    static async Task TestSsiExchangeSnapshot()
    {
        Console.WriteLine("--> [NGUỒN 4: SSI iBoard] Lấy Snapshot bảng giá sàn HOSE...");
        try
        {
            var url = "https://iboard-query.ssi.com.vn/stock/exchange/hose";
            var json = await client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            int count = data.GetArrayLength();
            Console.WriteLine($"    [OK] Lấy thành công Snapshot {count} mã sàn HOSE kèm 3 bước giá Mua/Bán.\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [FAIL] Lỗi: {ex.Message}\n");
        }
    }
}
