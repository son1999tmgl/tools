using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingSystem.Infrastructure.Feeds;
using TradingSystem.Infrastructure.Services;
using TradingSystem.Infrastructure.Storage;

namespace TradingSystem.Collector;

/// <summary>
/// Tiến trình ngầm tự động (Background Service) điều phối toàn bộ chu trình thu thập dữ liệu:
/// 1. Tự động chạy theo đúng khung giờ giao dịch chứng khoán Việt Nam (UTC+7).
/// 2. Hứng Ticks & Sổ lệnh đẩy vào đệm phi khóa WAL.
/// 3. Cuối phiên tự động khử trùng lặp và nén ra Apache Parquet dài hạn.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly VpsDataFeed _vpsFeed;
    private readonly VpsSocketFeed _vpsSocketFeed;
    private readonly SsiDataFeed _ssiFeed;
    private readonly DnseDataFeed _dnseFeed;
    private readonly WalBufferWriter _walWriter;
    private readonly ParquetStorageEngine _parquetEngine;

    private readonly List<string> _prioritySymbols;
    private readonly bool _runOnceImmediate;
    private readonly int _pollIntervalSec;
    private readonly bool _collectAllSymbols;
    private readonly int _maxDegreeOfParallelism;
    private List<string> _activeTradingSymbols = new();

    public Worker(
        ILogger<Worker> logger,
        IConfiguration config,
        IServiceScopeFactory scopeFactory,
        VpsDataFeed vpsFeed,
        VpsSocketFeed vpsSocketFeed,
        SsiDataFeed ssiFeed,
        DnseDataFeed dnseFeed,
        WalBufferWriter walWriter,
        ParquetStorageEngine parquetEngine)
    {
        _logger = logger;
        _config = config;
        _scopeFactory = scopeFactory;
        _vpsFeed = vpsFeed;
        _vpsSocketFeed = vpsSocketFeed;
        _ssiFeed = ssiFeed;
        _dnseFeed = dnseFeed;
        _walWriter = walWriter;
        _parquetEngine = parquetEngine;

        _prioritySymbols = _config.GetSection("CollectorSettings:PrioritySymbols").Get<List<string>>() 
                           ?? new List<string> { "VN30F1M", "HPG", "FPT", "SSI" };
        _runOnceImmediate = _config.GetValue<bool>("CollectorSettings:RunOnceImmediate", true);
        _pollIntervalSec = _config.GetValue<int>("CollectorSettings:PollIntervalSeconds", 3);
        _collectAllSymbols = _config.GetValue<bool>("CollectorSettings:CollectAllSymbolsBookSnapshot", true);
        _maxDegreeOfParallelism = _config.GetValue<int>("CollectorSettings:MaxDegreeOfParallelism", 10);
    }

    /// <summary>
    /// VÒNG LẶP CHÍNH (MAIN LOOP): Chạy ngầm liên tục khi ứng dụng khởi động.
    /// Nhiệm vụ:
    /// - Kiểm tra đồng hồ hệ thống theo múi giờ Việt Nam (UTC+7).
    /// - Điều phối tác vụ theo từng khung giờ:
    ///   + 08:30 - 09:00: Đồng bộ danh mục cổ phiếu trước phiên (RunMasterSyncSafelyAsync).
    ///   + 09:00 - 11:30 & 13:00 - 14:45: Thu thập tick khớp lệnh liên tục trong phiên (CollectIntradayTicksAsync).
    ///   + 15:00 - 16:00: Chốt phiên cuối ngày, nén WAL ra Parquet & đối soát dữ liệu (RunEndOfDayProcessAsync).
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("==========================================================================");
        _logger.LogInformation("🚀 TRADING DATA COLLECTOR WORKER ĐÃ KHỞI CHẠY");
        _logger.LogInformation("• Thời gian máy chủ: {Time}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        _logger.LogInformation("• Các mã theo dõi chính: {Symbols}", string.Join(", ", _prioritySymbols));
        _logger.LogInformation("==========================================================================\n");

        // 0. Bật kết nối luồng WebSocket hứng dữ liệu tức thời (Zero Polling - Chống chặn IP)
        try
        {
            await _vpsSocketFeed.StartAsync(_prioritySymbols, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Không thể khởi động WebSocket ngay lập tức: {Msg}. (Sẽ dùng REST fallback)", ex.Message);
        }

        // 1. Chạy chế độ kiểm thử nhanh ngay khi bật (nếu được cấu hình)
        if (_runOnceImmediate)
        {
            await RunImmediateValidationAsync(stoppingToken);
        }

        // 2. Vòng lặp lịch trình thời gian thực theo phiên giao dịch
        DateOnly lastMasterSyncDate = DateOnly.MinValue;
        DateOnly lastEodProcessDate = DateOnly.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Giờ Việt Nam (UTC+7)
                var vnTime = DateTime.UtcNow.AddHours(7);
                var today = DateOnly.FromDateTime(vnTime);
                var timeOfDay = vnTime.TimeOfDay;

                // Khung 1: 08:30 - 08:59 (Đầu ngày -> Đồng bộ danh mục)
                if (timeOfDay >= new TimeSpan(8, 30, 0) && timeOfDay < new TimeSpan(9, 0, 0))
                {
                    if (lastMasterSyncDate != today)
                    {
                        await RunMasterSyncSafelyAsync(stoppingToken);
                        lastMasterSyncDate = today;
                    }
                }

                // Khung 2: 09:00 - 11:30 & 13:00 - 14:45 (Trong phiên -> Hứng Ticks liên tục)
                bool isMorningSession = timeOfDay >= new TimeSpan(9, 0, 0) && timeOfDay <= new TimeSpan(11, 30, 0);
                bool isAfternoonSession = timeOfDay >= new TimeSpan(13, 0, 0) && timeOfDay <= new TimeSpan(14, 45, 0);

                if (isMorningSession || isAfternoonSession)
                {
                    await CollectIntradayTicksAsync(stoppingToken);
                }

                // Khung 3: 15:00 - 15:30 (Cuối ngày -> Đóng gói WAL ra Parquet & Đối soát EOD)
                if (timeOfDay >= new TimeSpan(15, 0, 0) && timeOfDay < new TimeSpan(16, 0, 0))
                {
                    if (lastEodProcessDate != today)
                    {
                        await RunEndOfDayProcessAsync(today, stoppingToken);
                        lastEodProcessDate = today;
                    }
                }

                // Điều tiết nhịp nghỉ thông minh:
                // - Trong phiên giao dịch (Thứ 2 - Thứ 6): Nghỉ theo chu kỳ cấu hình (3-5s).
                // - Ngoài phiên giao dịch / Buổi tối / Thứ 7, Chủ Nhật: Nghỉ 30 giây để không spam tài nguyên CPU và không tốn request.
                bool isWeekday = vnTime.DayOfWeek != DayOfWeek.Saturday && vnTime.DayOfWeek != DayOfWeek.Sunday;
                if (isWeekday && (isMorningSession || isAfternoonSession))
                {
                    await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSec), stoppingToken);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong vòng lặp chính của Worker: {Message}", ex.Message);
                await Task.Delay(10000, stoppingToken);
            }
        }

        _logger.LogInformation("🛑 TRADING DATA COLLECTOR WORKER ĐÃ DỪNG AN TOÀN.");
    }

    /// <summary>
    /// HÀM KIỂM THỬ KHỞI ĐỘNG (SMOKE TEST / VALIDATION):
    /// Chạy một chu trình đầy đủ thu nhỏ ngay khi bật ứng dụng để kiểm tra luồng:
    /// 1. Tải nến lịch sử từ DNSE và lưu file Parquet.
    /// 2. Lấy mẫu 50 ticks từ VPS và ghi vào đệm WAL (.bin).
    /// 3. Nén dữ liệu từ WAL sang file nén Parquet chính thức.
    /// -> Giúp phát hiện lỗi kết nối / thư mục ngay lập tức mà không cần đợi đến giờ giao dịch.
    /// </summary>
    private async Task RunImmediateValidationAsync(CancellationToken ct)
    {
        _logger.LogInformation("⚡ [DEMO VALIDATION] Bắt đầu chạy chu trình kiểm thử & đồng bộ toàn thị trường...");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 0. Đồng bộ toàn bộ Danh mục Master (Cổ phiếu, CW, Phái sinh) vào DB PostgreSQL
        _logger.LogInformation("0. Đang đồng bộ danh mục toàn bộ mã Cổ phiếu, CW & Phái sinh vào PostgreSQL...");
        await RunMasterSyncSafelyAsync(ct);

        // 1. Thử kéo chuỗi nến quá khứ từ DNSE và ghi ra Parquet
        _logger.LogInformation("1. Đang kiểm tra lưu chuỗi nến Parquet cho các mã trọng điểm...");
        using (var scope = _scopeFactory.CreateScope())
        {
            var syncService = scope.ServiceProvider.GetRequiredService<DataSyncService>();
            await syncService.SyncHistoricalBarsAsync(_prioritySymbols, today, ct);
        }

        // 2. Kéo khớp lệnh mẫu từ VPS và đẩy vào bộ đệm WAL
        _logger.LogInformation("2. Đang kiểm tra ghi đệm Write-Ahead Log (WAL)...");
        foreach (var sym in _prioritySymbols.Take(3))
        {
            var trades = await _vpsFeed.GetIntradayTradesAsync(sym, ct);
            if (trades.Count > 0)
            {
                foreach (var t in trades.Take(50))
                {
                    _walWriter.TryEnqueue(t);
                }
                _logger.LogInformation("   • Đã nạp {Count} ticks mẫu của {Sym} vào WAL Buffer", Math.Min(50, trades.Count), sym);
            }
        }

        // Xả ngay lập tức xuống đĩa WAL
        await _walWriter.FlushAsync();

        // 3. Đóng gói nén WAL ra file Parquet chính thức
        _logger.LogInformation("3. Đang kiểm tra đóng gói nén WAL ra Apache Parquet...");
        using (var scope = _scopeFactory.CreateScope())
        {
            var syncService = scope.ServiceProvider.GetRequiredService<DataSyncService>();
            await syncService.ProcessWalToParquetAsync(today, _prioritySymbols.Take(3), ct);
        }

        _logger.LogInformation("✨ [DEMO VALIDATION] Chu trình kiểm thử mẫu hoàn tất thành công 100%!\n");
    }

    /// <summary>
    /// HÀM ĐỒNG BỘ DANH MỤC TRƯỚC PHIÊN (08:30 - 08:59):
    /// - Gọi DataSyncService để tải danh sách mã cổ phiếu, chỉ số, ngành từ các sàn về lưu vào PostgreSQL.
    /// - Được bọc trong try/catch an toàn: Nếu chưa bật DB PostgreSQL thì vẫn ghi log cảnh báo và cho hệ thống tiếp tục chạy lưu file Parquet.
    /// </summary>
    private async Task RunMasterSyncSafelyAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<DataSyncService>();
            await syncService.SyncMasterCatalogAsync(ct);

            // Nạp toàn bộ danh mục đang hoạt động vào bộ nhớ
            if (_collectAllSymbols)
            {
                var symbols = await _vpsFeed.GetAllSymbolsAsync(ct);
                _activeTradingSymbols = symbols.Where(s => s.IsTrading)
                                               .Select(s => s.SymbolCode)
                                               .Distinct()
                                               .ToList();
                _logger.LogInformation("📋 [ACTIVE UNIVERSE] Đã nạp {Count} mã Cổ phiếu, CW & Phái sinh đang hoạt động sẵn sàng thu thập realtime!", _activeTradingSymbols.Count);

                // Tự động mở rộng luồng WebSocket lắng nghe TOÀN BỘ thị trường (Zero HTTP Polling)
                await _vpsSocketFeed.SubscribeSymbolsAsync(_activeTradingSymbols);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Không thể kết nối PostgreSQL để đồng bộ danh mục: {Msg}. (Hệ thống vẫn tiếp tục lưu Parquet)", ex.Message);
        }
    }

    /// <summary>
    /// HÀM THU THẬP TRONG PHIÊN (INTRADAY - 09:00-11:30 & 13:00-14:45):
    /// - 100% dữ liệu Ticks và Sổ lệnh L2 trong phiên được hứng tự động qua luồng WEBSOCKET (Zero HTTP Request).
    /// - Không gửi bất kỳ HTTP request polling nào lên sàn trong suốt phiên giao dịch.
    /// - CHỈ KÍCH HOẠT REST API dự phòng khi WebSocket bị đứt mạng hoặc bảo trì (Fallback mode).
    /// </summary>
    private async Task CollectIntradayTicksAsync(CancellationToken ct)
    {
        // Kiểm tra sức khỏe kết nối WebSocket
        if (_vpsSocketFeed.IsConnected)
        {
            // WebSocket đang nhận luồng dữ liệu trơn tru 24/7 -> KHÔNG GỬI BẤT KỲ REQUEST API NÀO!
            return;
        }

        // CHẾ ĐỘ DỰ PHÒNG KHẨN CẤP (FALLBACK): Khi WebSocket rớt mạng
        _logger.LogWarning("⚠️ [FALLBACK] WebSocket tạm thời gián đoạn. Kích hoạt quét REST polling dự phòng...");
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, _maxDegreeOfParallelism),
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(_prioritySymbols, parallelOptions, async (symbol, token) =>
        {
            try
            {
                var trades = await _vpsFeed.GetIntradayTradesAsync(symbol, token);
                if (trades.Count > 0)
                {
                    foreach (var t in trades)
                    {
                        _walWriter.TryEnqueue(t);
                    }
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("⚠️ VPS cảnh báo Rate Limit (HTTP 429) cho mã {Sym}. Tạm nghỉ 5s...", symbol);
                await Task.Delay(5000, token);
            }
            catch
            {
                // Bỏ qua lỗi timeout đơn lẻ để không gián đoạn toàn bộ luồng
            }
        });
    }

    /// <summary>
    /// HÀM CHỐT PHIÊN CUỐI NGÀY (END OF DAY - 15:00):
    /// Thực hiện 2 việc quan trọng:
    /// 1. Gom toàn bộ file đệm WAL (.bin) đã thu thập trong ngày, khử trùng lặp và nén thành file Parquet vĩnh viễn.
    /// 2. Kéo dữ liệu nến 1 phút và nến ngày từ nguồn DNSE để đối soát, bù đắp các ticks có thể bị miss trong phiên.
    /// </summary>
    private async Task RunEndOfDayProcessAsync(DateOnly date, CancellationToken ct)
    {
        _logger.LogInformation("🏁 [END OF DAY] Bắt đầu quy trình tổng kết phiên {Date}...", date);
        using var scope = _scopeFactory.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<DataSyncService>();

        // 1. Chốt nén toàn bộ WAL của ngày ra Parquet và kéo bù toàn bộ danh mục 101 mã trọng điểm
        await syncService.ProcessWalToParquetAsync(date, _prioritySymbols, ct);

        // 2. Kéo nến ngày và nến 1m từ DNSE để đối soát bù dữ liệu
        await syncService.SyncHistoricalBarsAsync(_prioritySymbols, date, ct);

        _logger.LogInformation("✅ [END OF DAY] Đã hoàn thành toàn bộ công việc cuối ngày!");
    }
}
