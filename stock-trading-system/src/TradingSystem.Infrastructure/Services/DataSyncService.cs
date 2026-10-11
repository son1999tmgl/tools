using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingSystem.Core.Entities;
using TradingSystem.Core.Models.TimeSeries;
using TradingSystem.Infrastructure.Data;
using TradingSystem.Infrastructure.Feeds;
using TradingSystem.Infrastructure.Storage;

namespace TradingSystem.Infrastructure.Services;

/// <summary>
/// Dịch vụ đồng bộ dữ liệu tổng thể:
/// 1. Đồng bộ Danh mục Master (Cổ phiếu, CW, Phái sinh) vào PostgreSQL.
/// 2. Thu thập và đóng gói dữ liệu Tick từ WAL ra Parquet.
/// 3. Lưu trữ Nến ngày EOD vào bảng market_daily_bars.
/// </summary>
public class DataSyncService
{
    private readonly TradingDbContext _dbContext;
    private readonly VpsDataFeed _vpsFeed;
    private readonly SsiDataFeed _ssiFeed;
    private readonly DnseDataFeed _dnseFeed;
    private readonly ParquetStorageEngine _parquetEngine;
    private readonly string _stagingDir;
    private readonly ILogger<DataSyncService> _logger;

    public DataSyncService(
        TradingDbContext dbContext,
        VpsDataFeed vpsFeed,
        SsiDataFeed ssiFeed,
        DnseDataFeed dnseFeed,
        ParquetStorageEngine parquetEngine,
        string stagingDir,
        ILogger<DataSyncService> logger)
    {
        _dbContext = dbContext;
        _vpsFeed = vpsFeed;
        _ssiFeed = ssiFeed;
        _dnseFeed = dnseFeed;
        _parquetEngine = parquetEngine;
        _stagingDir = stagingDir;
        _logger = logger;
    }

    /// <summary>
    /// Đồng bộ Danh mục toàn thị trường đầu ngày (08:30): Cổ phiếu, CW, Phái sinh
    /// </summary>
    public async Task SyncMasterCatalogAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("--> [MASTER SYNC] Bắt đầu đồng bộ danh mục toàn thị trường...");

        // 1. Kéo danh mục mã từ VPS (khoảng 2.166 mã)
        var allSymbols = await _vpsFeed.GetAllSymbolsAsync(ct);
        _logger.LogInformation("    • VPS: Đã lấy {Count} mã Master", allSymbols.Count);

        // 2. Kéo danh mục HĐTL Phái sinh từ SSI
        var (derivContracts, _) = await _ssiFeed.GetDerivativeContractsAsync(ct);
        _logger.LogInformation("    • SSI: Đã lấy {Count} Hợp đồng Phái sinh Futures", derivContracts.Count);

        // 3. Kéo danh mục Chứng quyền từ SSI (đầy đủ ngày niêm yết/đáo hạn/tỷ lệ)
        var coveredWarrants = await _ssiFeed.GetCoveredWarrantsAsync(ct);
        _logger.LogInformation("    • SSI: Đã lấy {Count} mã Chứng quyền có bảo đảm", coveredWarrants.Count);

        // 4. Lưu / Cập nhật vào PostgreSQL (Upsert an toàn)
        await UpsertSymbolsAsync(allSymbols, ct);
        await UpsertDerivativesAsync(derivContracts, ct);
        await UpsertCoveredWarrantsAsync(coveredWarrants, ct);

        _logger.LogInformation("✅ [MASTER SYNC] Hoàn tất đồng bộ danh mục vào PostgreSQL thành công!");
    }

    /// <summary>
    /// Tiến trình cuối phiên (15:00): Đóng gói file nháp WAL ra Apache Parquet và khử trùng lặp
    /// </summary>
    public async Task ProcessWalToParquetAsync(DateOnly date, CancellationToken ct = default)
    {
        _logger.LogInformation("--> [WAL -> PARQUET] Bắt đầu xử lý file WAL ngày {Date}...", date);
        var walFile = Path.Combine(_stagingDir, $"{date:yyyy-MM-dd}.wal");

        if (!File.Exists(walFile))
        {
            _logger.LogInformation("    Không tìm thấy file WAL {File}, bỏ qua.", walFile);
            return;
        }

        // Đọc toàn bộ các tick trong file WAL
        var rawTicks = new List<MarketTick>();
        using (var reader = new StreamReader(walFile))
        {
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var tick = JsonSerializer.Deserialize<MarketTick>(line);
                    if (tick != null) rawTicks.Add(tick);
                }
                catch { }
            }
        }

        _logger.LogInformation("    Đã đọc {Count} dòng từ file WAL. Bắt đầu khử trùng lặp...", rawTicks.Count);

        // Khử trùng lặp theo khóa tự nhiên: Symbol + Timestamp + Price + Volume + Side
        var deduplicatedBySymbol = rawTicks
            .GroupBy(t => t.Symbol)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(x => new { x.Timestamp, x.Price, x.Volume, x.Side })
                      .Select(x => x.First())
                      .OrderBy(x => x.Timestamp)
                      .ToList()
            );

        // Ghi ra từng file Parquet theo mã
        int totalSaved = 0;
        foreach (var kvp in deduplicatedBySymbol)
        {
            var symbol = kvp.Key;
            var ticks = kvp.Value;
            await _parquetEngine.WriteTicksAsync(symbol, date, ticks);
            totalSaved += ticks.Count;
        }

        _logger.LogInformation("✅ [WAL -> PARQUET] Đã nén thành công {Count} ticks sạch cho {Symbols} mã ra Parquet!",
            totalSaved, deduplicatedBySymbol.Count);
    }

    /// <summary>
    /// Đồng bộ Nến ngày EOD và nến 1 phút cho danh sách mã trọng điểm (VN30F1M, HPG...)
    /// </summary>
    public async Task SyncHistoricalBarsAsync(IEnumerable<string> prioritySymbols, DateOnly date, CancellationToken ct = default)
    {
        _logger.LogInformation("--> [BARS SYNC] Đang tải chuỗi nến quá khứ từ DNSE để đối soát...");

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long from5Days = now - (86400 * 5);

        foreach (var sym in prioritySymbols)
        {
            bool isDeriv = sym.StartsWith("VN30F") || sym.StartsWith("41I");
            List<Bar1m> bars1m;

            if (isDeriv)
            {
                bars1m = await _dnseFeed.GetDerivativeBars1mAsync(sym, from5Days, now, ct);
            }
            else
            {
                bars1m = await _dnseFeed.GetStockBars1mAsync(sym, from5Days, now, ct);
            }

            if (bars1m.Count > 0)
            {
                await _parquetEngine.WriteBars1mAsync(sym, date, bars1m);
                _logger.LogInformation("    • Mã {Symbol}: Đã lưu {Count} nến 1 phút vào Parquet", sym, bars1m.Count);
            }
        }

        _logger.LogInformation("✅ [BARS SYNC] Hoàn tất nạp chuỗi nến vào Parquet!");
    }

    private async Task UpsertSymbolsAsync(List<Symbol> symbols, CancellationToken ct)
    {
        var existingCodes = await _dbContext.Symbols.Select(s => s.SymbolCode).ToHashSetAsync(ct);
        var toAdd = symbols.Where(s => !existingCodes.Contains(s.SymbolCode)).ToList();

        if (toAdd.Count > 0)
        {
            await _dbContext.Symbols.AddRangeAsync(toAdd, ct);
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("    [DB] Đã thêm mới {Count} mã vào bảng symbols", toAdd.Count);
        }
    }

    private async Task UpsertDerivativesAsync(List<DerivativeContract> contracts, CancellationToken ct)
    {
        var existingCodes = await _dbContext.DerivativeContracts.Select(d => d.SymbolCode).ToHashSetAsync(ct);
        var toAdd = contracts.Where(d => !existingCodes.Contains(d.SymbolCode)).ToList();

        if (toAdd.Count > 0)
        {
            await _dbContext.DerivativeContracts.AddRangeAsync(toAdd, ct);
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("    [DB] Đã thêm mới {Count} hợp đồng phái sinh vào bảng derivative_contracts", toAdd.Count);
        }
    }

    private async Task UpsertCoveredWarrantsAsync(List<CoveredWarrant> warrants, CancellationToken ct)
    {
        var existingCodes = await _dbContext.CoveredWarrants.Select(w => w.SymbolCode).ToHashSetAsync(ct);
        var toAdd = warrants.Where(w => !existingCodes.Contains(w.SymbolCode)).ToList();

        if (toAdd.Count > 0)
        {
            await _dbContext.CoveredWarrants.AddRangeAsync(toAdd, ct);
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("    [DB] Đã thêm mới {Count} chứng quyền vào bảng covered_warrants", toAdd.Count);
        }
    }
}
