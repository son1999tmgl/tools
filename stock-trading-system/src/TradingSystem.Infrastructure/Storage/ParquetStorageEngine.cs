using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Parquet.Serialization;
using TradingSystem.Core.Models.TimeSeries;

namespace TradingSystem.Infrastructure.Storage;

public class ParquetStorageEngine
{
    private readonly string _baseStorageDir;

    public ParquetStorageEngine(string baseStorageDir)
    {
        _baseStorageDir = baseStorageDir;
        Directory.CreateDirectory(_baseStorageDir);
    }

    /// <summary>
    /// Ghi danh sách Nến 1 phút ra file Parquet: data/bars_1m/{YYYY-MM-DD}/{symbol}.parquet
    /// </summary>
    public async Task WriteBars1mAsync(string symbol, DateOnly date, IList<Bar1m> bars)
    {
        if (bars.Count == 0) return;

        var dir = Path.Combine(_baseStorageDir, "bars_1m", date.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, $"{symbol}.parquet");

        var tempPath = filePath + ".tmp";
        var backupPath = filePath + ".bak";

        // Ghi vào file tạm trước
        using (var fileStream = File.Create(tempPath))
        {
            await ParquetSerializer.SerializeAsync(bars, fileStream);
        }

        // Nếu file đích đã có sẵn, sao lưu sang .bak trước khi ghi đè
        if (File.Exists(filePath))
        {
            File.Copy(filePath, backupPath, true);
        }

        File.Move(tempPath, filePath, true);
    }

    /// <summary>
    /// Ghi danh sách Khớp lệnh Tick ra file Parquet: data/ticks/{YYYY-MM-DD}/{symbol}.parquet
    /// Hỗ trợ cơ chế Atomic Write (.tmp) và sao lưu an toàn (.bak) chống lỗi hỏng file
    /// </summary>
    public async Task WriteTicksAsync(string symbol, DateOnly date, IList<MarketTick> ticks)
    {
        if (ticks.Count == 0) return;

        var dir = Path.Combine(_baseStorageDir, "ticks", date.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, $"{symbol}.parquet");
        var tempPath = filePath + ".tmp";
        var backupPath = filePath + ".bak";

        // Ghi vào file tạm trước
        using (var fileStream = File.Create(tempPath))
        {
            await ParquetSerializer.SerializeAsync(ticks, fileStream);
        }

        // Nếu file đích đã có sẵn, sao lưu sang .bak trước khi ghi đè
        if (File.Exists(filePath))
        {
            File.Copy(filePath, backupPath, true);
        }

        File.Move(tempPath, filePath, true);
    }

    /// <summary>
    /// Đọc nến 1 phút từ file Parquet nạp vào RAM siêu tốc để tính toán chỉ báo / Backtest
    /// </summary>
    public async Task<IList<Bar1m>> ReadBars1mAsync(string symbol, DateOnly date)
    {
        var filePath = Path.Combine(_baseStorageDir, "bars_1m", date.ToString("yyyy-MM-dd"), $"{symbol}.parquet");
        if (!File.Exists(filePath)) return new List<Bar1m>();

        using var fileStream = File.OpenRead(filePath);
        var res = await ParquetSerializer.DeserializeAsync<Bar1m>(fileStream);
        return (IList<Bar1m>)res;
    }

    /// <summary>
    /// Đọc Khớp lệnh Tick từ file Parquet
    /// </summary>
    public async Task<IList<MarketTick>> ReadTicksAsync(string symbol, DateOnly date)
    {
        var filePath = Path.Combine(_baseStorageDir, "ticks", date.ToString("yyyy-MM-dd"), $"{symbol}.parquet");
        if (!File.Exists(filePath)) return new List<MarketTick>();

        using var fileStream = File.OpenRead(filePath);
        var res = await ParquetSerializer.DeserializeAsync<MarketTick>(fileStream);
        return (IList<MarketTick>)res;
    }
}
