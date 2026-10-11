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

// -------------------------------------------------------------------------------------------------
// 3. ĐĂNG KÝ CÁC KÊNH DỮ LIỆU THỊ TRƯỜNG (MARKET DATA FEEDS)
// - VpsDataFeed: Kéo khớp lệnh và giá thời gian thực
// - SsiDataFeed: Kéo dữ liệu dự phòng từ SSI
// - DnseDataFeed: Kéo nến lịch sử (Historical OHLCV) và sổ lệnh từ DNSE
// -------------------------------------------------------------------------------------------------
builder.Services.AddHttpClient<VpsDataFeed>();
builder.Services.AddHttpClient<SsiDataFeed>();
builder.Services.AddHttpClient<DnseDataFeed>();

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
// 5. WORKER NỀN (BACKGROUND SERVICE)
// Đăng ký tiến trình chạy ngầm theo chu kỳ phiên giao dịch (Worker.cs)
// -------------------------------------------------------------------------------------------------
builder.Services.AddHostedService<Worker>();

// Khởi chạy ứng dụng
var host = builder.Build();
await host.RunAsync();
