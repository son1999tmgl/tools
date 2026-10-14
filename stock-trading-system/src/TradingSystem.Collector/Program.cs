using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TradingSystem.Collector;
using TradingSystem.Infrastructure.Data;
using TradingSystem.Infrastructure.Feeds;
using TradingSystem.Infrastructure.Services;
using TradingSystem.Infrastructure.Storage;

var builder = Host.CreateApplicationBuilder(args);

// -------------------------------------------------------------------------------------------------
// 1. CẤU HÌNH POSTGRESQL (EF Core)
// Dùng lưu thông tin danh mục cổ phiếu, metadata, trạng thái đồng bộ
// -------------------------------------------------------------------------------------------------
var pgConnectionString = builder.Configuration["ConnectionStrings:PostgreSQL"]
    ?? "Host=localhost;Port=5432;Database=trading_system;Username=postgres;Password=postgres";

builder.Services.AddDbContext<TradingDbContext>(options =>
{
    options.UseNpgsql(pgConnectionString);
});

// -------------------------------------------------------------------------------------------------
// 2. CẤU HÌNH LƯU TRỮ TẬP TIN DỮ LIỆU (WAL Buffer & Parquet Big Data Engine)
// - storageDir: Nơi lưu nén Parquet dài hạn (data/storage)
// - stagingDir: Vùng đệm nhị phân WAL ghi nhanh trong phiên để chống mất dữ liệu (data/staging)
// -------------------------------------------------------------------------------------------------
var baseDir = builder.Environment.ContentRootPath;
var configuredStorage = builder.Configuration.GetValue<string>("CollectorSettings:StorageDir", "data/storage")!;
var configuredStaging = builder.Configuration.GetValue<string>("CollectorSettings:StagingDir", "data/staging")!;

var storageDir = Path.IsPathRooted(configuredStorage) ? configuredStorage : Path.Combine(baseDir, configuredStorage);
var stagingDir = Path.IsPathRooted(configuredStaging) ? configuredStaging : Path.Combine(baseDir, configuredStaging);

builder.Services.AddSingleton(new ParquetStorageEngine(storageDir));
builder.Services.AddSingleton(new WalBufferWriter(stagingDir));
builder.Services.AddSingleton<VpsSocketFeed>();

// -------------------------------------------------------------------------------------------------
// 3. ĐĂNG KÝ CÁC KÊNH DỮ LIỆU THỊ TRƯỜNG (MARKET DATA FEEDS)
// - VpsDataFeed: Kéo khớp lệnh và giá thời gian thực
// - SsiDataFeed: Kéo dữ liệu dự phòng từ SSI
// - DnseDataFeed: Kéo nến lịch sử (Historical OHLCV) và sổ lệnh từ DNSE
// -------------------------------------------------------------------------------------------------
builder.Services.AddHttpClient<VpsDataFeed>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
    client.DefaultRequestHeaders.Add("Referer", "https://banggia.vps.com.vn/");
    client.DefaultRequestHeaders.Add("Origin", "https://banggia.vps.com.vn");
});
builder.Services.AddHttpClient<SsiDataFeed>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
    client.DefaultRequestHeaders.Add("Referer", "https://iboard.ssi.com.vn/");
});
builder.Services.AddHttpClient<DnseDataFeed>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
    client.DefaultRequestHeaders.Add("Referer", "https://entrade.com.vn/");
});

// -------------------------------------------------------------------------------------------------
// 4. SERVICE ĐIỀU PHỐI ĐỒNG BỘ DỮ LIỆU (DataSyncService)
// Quản lý nghiệp vụ: tải danh mục vào PostgreSQL, nén WAL -> Parquet, đồng bộ nến lịch sử
// -------------------------------------------------------------------------------------------------
builder.Services.AddScoped<DataSyncService>(sp =>
{
    return new DataSyncService(
        sp.GetRequiredService<TradingDbContext>(),
        sp.GetRequiredService<VpsDataFeed>(),
        sp.GetRequiredService<SsiDataFeed>(),
        sp.GetRequiredService<DnseDataFeed>(),
        sp.GetRequiredService<ParquetStorageEngine>(),
        stagingDir,
        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DataSyncService>>()
    );
});

// -------------------------------------------------------------------------------------------------
// 5. WORKER NỀN HOẶC LỆNH CHẠY THỦ CÔNG ĐỘC LẬP (CLI COMMANDS)
// - Mặc định: Chạy Worker ngầm theo phiên (08:30 -> 16:00).
// - Nếu truyền tham số "sync-eod" (vd: dotnet run -- sync-eod [yyyy-MM-dd]):
//   -> Kích hoạt chạy bù dữ liệu độc lập ngay lập tức mà không cần đợi Worker.
// -------------------------------------------------------------------------------------------------
if (args.Length > 0 && args[0].Equals("sync-eod", StringComparison.OrdinalIgnoreCase))
{
    var host = builder.Build();
    using var scope = host.Services.CreateScope();
    var syncService = scope.ServiceProvider.GetRequiredService<DataSyncService>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    var prioritySymbols = config.GetSection("CollectorSettings:PrioritySymbols").Get<List<string>>()
                          ?? new List<string> { "VN30F1M", "HPG", "FPT", "SSI" };

    DateOnly targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
    if (args.Length > 1 && DateOnly.TryParse(args[1], out var parsedDate))
    {
        targetDate = parsedDate;
    }

    Console.WriteLine($"==========================================================================");
    Console.WriteLine($"🚀 [CLI ON-DEMAND] KÍCH HOẠT ĐỒNG BỘ BÙ DỮ LIỆU ĐỘC LẬP NGÀY: {targetDate:yyyy-MM-dd}");
    Console.WriteLine($"• Danh mục cần kéo bù: {prioritySymbols.Count} mã trọng điểm");
    Console.WriteLine($"==========================================================================\n");

    await syncService.ProcessWalToParquetAsync(targetDate, prioritySymbols);
    await syncService.SyncHistoricalBarsAsync(prioritySymbols, targetDate);

    Console.WriteLine("\n✨ [HOÀN TẤT] Dữ liệu ngày {0} đã được cập nhật sạch vào Parquet!", targetDate);
    return;
}

builder.Services.AddHostedService<Worker>();

// Khởi chạy ứng dụng chạy ngầm
var appHost = builder.Build();
await appHost.RunAsync();
