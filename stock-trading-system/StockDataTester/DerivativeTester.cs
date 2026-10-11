using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

public static class DerivativeTester
{
    private static readonly HttpClient client = new HttpClient();
    private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    static DerivativeTester()
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public static async Task Run(string logDir)
    {
        Console.WriteLine("\n==========================================================================");
        Console.WriteLine(" [KIỂM THỬ CHUYÊN SÂU DỮ LIỆU PHÁI SINH (DERIVATIVES - VN30F)]");
        Console.WriteLine($" Thời gian thực hiện: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine("==========================================================================\n");

        var results = new Dictionary<string, object>();

        // 1. DNSE: Nến 1 phút và nến ngày của VN30F1M & Chỉ số VN30
        await TestDnse(results);

        // 2. SSI: Bảng giá Phái sinh
        await TestSsi(results);

        // 3. KBSEC: Khớp lệnh Tick-by-tick của VN30F1M / 41I1GA000
        await TestKbsec(results);

        // 4. VPS: Điều tra sâu endpoint Phái sinh
        await TestVps(results);

        // Ghi file log riêng cho phái sinh
        var logFile = Path.Combine(logDir, "log_derivative_test.json");
        var json = JsonSerializer.Serialize(results, jsonOptions);
        await File.WriteAllTextAsync(logFile, json, System.Text.Encoding.UTF8);
        Console.WriteLine($"\n==> [DONE] ĐÃ LƯU TOÀN BỘ KẾT QUẢ KIỂM THỬ PHÁI SINH VÀO: {logFile}\n");
    }

    private static async Task TestDnse(Dictionary<string, object> results)
    {
        Console.WriteLine("--> [1. KIỂM THỬ DNSE - PHÁI SINH & CHỈ SỐ CƠ SỞ]");
        var dnseRes = new Dictionary<string, object>();

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long from5Days = now - (86400 * 5);
        long from1Year = now - (86400 * 365);

        // 1.1 VN30F1M 1 phút
        string urlF1m_1m = $"https://services.entrade.com.vn/chart-api/v2/ohlcs/derivative?from={from5Days}&to={now}&symbol=VN30F1M&resolution=1";
        try
        {
            var json = await client.GetStringAsync(urlF1m_1m);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("t", out var tArr))
            {
                int count = tArr.GetArrayLength();
                var cArr = doc.RootElement.GetProperty("c");
                var vArr = doc.RootElement.GetProperty("v");
                double lastClose = cArr[count - 1].GetDouble();
                double lastVol = vArr[count - 1].GetDouble();
                Console.WriteLine($"    [DNSE] VN30F1M Nến 1m (5 ngày): {count} nến OK! (Nến gần nhất: Close={lastClose:F1}, Vol={lastVol})");
                dnseRes["vn30f1m_1m_count"] = count;
                dnseRes["vn30f1m_1m_latest"] = new { close = lastClose, volume = lastVol };
                dnseRes["vn30f1m_1m_sample"] = doc.RootElement.Clone();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [DNSE] VN30F1M 1m Lỗi: {ex.Message}");
            dnseRes["vn30f1m_1m_error"] = ex.Message;
        }

        // 1.2 VN30F1M Nến ngày (1D)
        string urlF1m_1d = $"https://services.entrade.com.vn/chart-api/v2/ohlcs/derivative?from={from1Year}&to={now}&symbol=VN30F1M&resolution=1D";
        try
        {
            var json = await client.GetStringAsync(urlF1m_1d);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("t", out var tArr))
            {
                int count = tArr.GetArrayLength();
                var cArr = doc.RootElement.GetProperty("c");
                double lastClose = cArr[count - 1].GetDouble();
                Console.WriteLine($"    [DNSE] VN30F1M Nến 1D (1 năm): {count} nến EOD OK! (Điểm đóng cửa gần nhất: {lastClose:F1})");
                dnseRes["vn30f1m_1d_count"] = count;
                dnseRes["vn30f1m_1d_sample"] = doc.RootElement.Clone();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [DNSE] VN30F1M 1D Lỗi: {ex.Message}");
            dnseRes["vn30f1m_1d_error"] = ex.Message;
        }

        // 1.3 Chỉ số VN30 cơ sở (để tính Basis realtime)
        string urlIndex = $"https://services.entrade.com.vn/chart-api/v2/ohlcs/index?from={from5Days}&to={now}&symbol=VN30&resolution=1";
        try
        {
            var json = await client.GetStringAsync(urlIndex);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("t", out var tArr))
            {
                int count = tArr.GetArrayLength();
                var cArr = doc.RootElement.GetProperty("c");
                double lastClose = cArr[count - 1].GetDouble();
                Console.WriteLine($"    [DNSE] Chỉ số cơ sở VN30 1m: {count} nến OK! (Điểm số VN30 gần nhất: {lastClose:F2})");
                dnseRes["vn30_index_1m_count"] = count;
                dnseRes["vn30_index_1m_sample"] = doc.RootElement.Clone();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [DNSE] VN30 Index 1m Lỗi: {ex.Message}");
        }

        results["dnse"] = dnseRes;
    }

    private static async Task TestSsi(Dictionary<string, object> results)
    {
        Console.WriteLine("\n--> [2. KIỂM THỬ SSI - BẢNG GIÁ PHÁI SINH & HỢP ĐỒNG]");
        var ssiRes = new Dictionary<string, object>();

        // 2.1 Bảng giá phái sinh exchange/derivative
        string derivUrl = "https://iboard-query.ssi.com.vn/stock/exchange/derivative";
        try
        {
            var json = await client.GetStringAsync(derivUrl);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var dataArr))
            {
                int count = dataArr.GetArrayLength();
                Console.WriteLine($"    [SSI] Endpoint /stock/exchange/derivative: {count} hợp đồng phái sinh niêm yết!");
                foreach (var item in dataArr.EnumerateArray())
                {
                    string sym = item.TryGetProperty("stockSymbol", out var s) ? s.GetString() ?? "" : "";
                    double refP = item.TryGetProperty("refPrice", out var r) ? r.GetDouble() : 0;
                    double ceilP = item.TryGetProperty("ceiling", out var c) ? c.GetDouble() : 0;
                    double floorP = item.TryGetProperty("floor", out var f) ? f.GetDouble() : 0;
                    double lastP = item.TryGetProperty("matchedPrice", out var mp) ? mp.GetDouble() : 0;
                    long totalVol = item.TryGetProperty("totalMatchVol", out var mv) ? mv.GetInt64() : 0;
                    long oi = item.TryGetProperty("openInterest", out var o) ? o.GetInt64() : 0;
                    Console.WriteLine($"       • HĐ: {sym,-12} | Giá khớp: {lastP,7:F1} | Trần: {ceilP,7:F1} | Sàn: {floorP,7:F1} | Vol: {totalVol,8:N0} | OI: {oi,6:N0}");
                }
                ssiRes["exchange_derivative_contracts"] = dataArr.Clone();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [SSI] exchange/derivative Lỗi: {ex.Message}");
            ssiRes["exchange_derivative_error"] = ex.Message;
        }

        // 2.2 Bảng giá HĐTL Futures: exchange/fu
        string fuUrl = "https://iboard-query.ssi.com.vn/stock/exchange/fu";
        try
        {
            var json = await client.GetStringAsync(fuUrl);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var dataArr))
            {
                int count = dataArr.GetArrayLength();
                Console.WriteLine($"    [SSI] Endpoint /stock/exchange/fu: {count} mã HĐTL!");
                ssiRes["fu_data"] = dataArr.Clone();
            }
        }
        catch { }

        results["ssi"] = ssiRes;
    }

    private static async Task TestKbsec(Dictionary<string, object> results)
    {
        Console.WriteLine("\n--> [3. KIỂM THỬ KBSEC - TICK-BY-TICK KHỚP LỆNH PHÁI SINH]");
        var kbRes = new Dictionary<string, object>();

        // Dùng mã HĐ tháng 10/2026: 41I1GA000
        string code = "41I1GA000";
        string url = $"https://kbbuddywts.kbsec.com.vn/iis-server/investment/trade/history/{code}?page=1&limit=100";
        try
        {
            var json = await client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var dataArr))
            {
                int count = dataArr.GetArrayLength();
                Console.WriteLine($"    [KBSEC] Lấy thành công {count} ticks khớp lệnh gần nhất của '{code}'!");
                if (count > 0)
                {
                    var sample = dataArr[0];
                    string t = sample.GetProperty("t").GetString() ?? "";
                    string side = sample.GetProperty("LC").GetString() ?? "";
                    double price = sample.GetProperty("FMP").GetDouble();
                    int vol = sample.GetProperty("FV").GetInt32();
                    long accVol = sample.GetProperty("AVO").GetInt64();
                    Console.WriteLine($"       [TICK MẪU]: Thời gian: {t} | Chiều: {(side == "B" ? "MUA/LONG" : "BÁN/SHORT")} | Điểm: {price:F1} | Khối lượng: {vol} HĐ | Tổng lũy kế phiên: {accVol:N0} HĐ");
                }
                kbRes[$"ticks_{code}"] = dataArr.Clone();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [KBSEC] '{code}' Lỗi: {ex.Message}");
            kbRes["error"] = ex.Message;
        }

        results["kbsec"] = kbRes;
    }

    private static async Task TestVps(Dictionary<string, object> results)
    {
        Console.WriteLine("\n--> [4. ĐIỀU TRA VPS - DATA FEED PHÁI SINH]");
        var vpsRes = new Dictionary<string, object>();

        // Kiểm tra danh mục phái sinh HNX từ VPS Master
        try
        {
            var masterUrl = "https://bgapidatafeed.vps.com.vn/getlistallstock";
            var masterJson = await client.GetStringAsync(masterUrl);
            using var doc = JsonDocument.Parse(masterJson);
            var derivSymbols = doc.RootElement.EnumerateArray()
                .Where(x => x.TryGetProperty("type", out var tp) && tp.GetString() == "D")
                .Select(x => new
                {
                    code = x.GetProperty("stock_code").GetString(),
                    name = x.GetProperty("name_vn").GetString(),
                    exchange = x.GetProperty("post_to").GetString()
                })
                .ToList();

            Console.WriteLine($"    [VPS] Tổng cộng {derivSymbols.Count} mã Phái sinh trong Master List của VPS:");
            var vn30Derivs = derivSymbols.Where(x => x.code != null && x.code.StartsWith("41I1")).ToList();
            var vn100Derivs = derivSymbols.Where(x => x.code != null && x.code.StartsWith("41I2")).ToList();
            var bondDerivs = derivSymbols.Where(x => x.code != null && (x.code.StartsWith("41B5") || x.code.StartsWith("41BA"))).ToList();
            Console.WriteLine($"       • HĐTL Chỉ số VN30: {vn30Derivs.Count} mã ({string.Join(", ", vn30Derivs.Take(4).Select(x => x.code))})");
            Console.WriteLine($"       • HĐTL Chỉ số VN100: {vn100Derivs.Count} mã");
            Console.WriteLine($"       • HĐTL Trái phiếu CP: {bondDerivs.Count} mã");

            vpsRes["total_derivatives"] = derivSymbols.Count;
            vpsRes["vn30_derivatives"] = vn30Derivs;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [VPS] Master List Lỗi: {ex.Message}");
        }

        results["vps"] = vpsRes;
    }
}
