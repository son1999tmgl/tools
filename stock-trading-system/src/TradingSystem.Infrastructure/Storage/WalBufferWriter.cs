using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TradingSystem.Core.Models.TimeSeries;

namespace TradingSystem.Infrastructure.Storage;

/// <summary>
/// Quản lý bộ đệm Write-Ahead Log (WAL) phi khóa sử dụng System.Threading.Channels.
/// Nhận tick real-time không làm nghẽn luồng socket và định kỳ flush nối tiếp xuống đĩa staging.
/// </summary>
public class WalBufferWriter : IAsyncDisposable
{
    private readonly Channel<MarketTick> _channel;
    private readonly string _stagingDir;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _flushTask;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public WalBufferWriter(string stagingDir, int capacity = 50000)
    {
        _stagingDir = stagingDir;
        Directory.CreateDirectory(_stagingDir);

        _channel = Channel.CreateBounded<MarketTick>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

        _flushTask = Task.Run(FlushLoopAsync);
    }

    /// <summary>
    /// Đẩy tick vào hàng đợi RAM phi khóa (không làm nghẽn kết nối mạng)
    /// </summary>
    public bool TryEnqueue(MarketTick tick)
    {
        return _channel.Writer.TryWrite(tick);
    }

    public async ValueTask EnqueueAsync(MarketTick tick, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(tick, ct);
    }

    /// <summary>
    /// Ép buộc xả toàn bộ dữ liệu đang chờ trong hàng đợi xuống đĩa ngay lập tức
    /// </summary>
    public async Task FlushAsync()
    {
        var buffer = new System.Collections.Generic.List<MarketTick>();
        while (_channel.Reader.TryRead(out var tick))
        {
            buffer.Add(tick);
        }
        if (buffer.Count > 0)
        {
            await AppendToDiskAsync(buffer);
        }
    }

    private async Task FlushLoopAsync()
    {
        var buffer = new System.Collections.Generic.List<MarketTick>(1000);
        var lastFlush = DateTime.UtcNow;

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                // Đọc có timeout để định kỳ flush dù ít lệnh
                while (_channel.Reader.TryRead(out var tick))
                {
                    buffer.Add(tick);
                    if (buffer.Count >= 1000) break;
                }

                bool shouldFlush = buffer.Count >= 1000 || 
                                   (buffer.Count > 0 && (DateTime.UtcNow - lastFlush).TotalSeconds >= 5);

                if (shouldFlush)
                {
                    await AppendToDiskAsync(buffer);
                    buffer.Clear();
                    lastFlush = DateTime.UtcNow;
                }

                if (buffer.Count == 0)
                {
                    // Chờ bản ghi tiếp theo tối đa 1 giây
                    if (await _channel.Reader.WaitToReadAsync(_cts.Token))
                    {
                        continue;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WAL ERROR] Lỗi ghi đệm WAL: {ex.Message}");
                await Task.Delay(1000, _cts.Token);
            }
        }

        // Flush hết những gì còn sót lại khi shutdown
        while (_channel.Reader.TryRead(out var remaining))
        {
            buffer.Add(remaining);
        }
        if (buffer.Count > 0)
        {
            await AppendToDiskAsync(buffer);
        }
    }

    private async Task AppendToDiskAsync(System.Collections.Generic.List<MarketTick> batch)
    {
        if (batch.Count == 0) return;
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var walFile = Path.Combine(_stagingDir, $"{today}.wal");

        using var stream = new FileStream(walFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        using var writer = new StreamWriter(stream, System.Text.Encoding.UTF8);

        foreach (var tick in batch)
        {
            var line = JsonSerializer.Serialize(tick, JsonOpts);
            await writer.WriteLineAsync(line);
        }
        await writer.FlushAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.Complete();
        _cts.Cancel();
        try
        {
            await _flushTask;
        }
        catch { }
        _cts.Dispose();
    }
}
