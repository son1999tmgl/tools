using System;
using System.Collections.Generic;

namespace TradingSystem.Backtest.Core;

/// <summary>
/// Thư viện tính toán các chỉ báo phân tích kỹ thuật & vi cấu trúc định lượng:
/// - EMA (Exponential Moving Average)
/// - SMA (Simple Moving Average)
/// - RSI (Relative Strength Index)
/// - MACD (Moving Average Convergence Divergence)
/// - ATR (Average True Range)
/// - Bollinger Bands
/// </summary>
public static class Indicators
{
    /// <summary>
    /// Đường trung bình hàm mũ (EMA - Exponential Moving Average)
    /// </summary>
    /// <remarks>
    /// BẢN CHẤT TOÁN HỌC:
    /// - Khác với SMA (trọng số phẳng cào bằng), EMA gán trọng số suy giảm theo hàm mũ cho các phiên cũ.
    /// - Hệ số làm mịn (Multiplier / Weight): k = 2 / (period + 1).
    ///   Ví dụ với EMA(20): k = 2 / 21 ≈ 0.0952 (nến hiện tại chiếm 9.52% trọng số, 90.48% dồn cho quá khứ).
    /// - Công thức truy hồi: EMA_hôm_nay = (Giá_hôm_nay - EMA_hôm_qua) * k + EMA_hôm_qua.
    /// - Ứng dụng: Nhạy cảm với biến động giá mới nhất, giảm độ trễ (lag) so với SMA, cực kỳ phù hợp bắt sóng ngắn và làm đường hỗ trợ động.
    /// </remarks>
    public static double[] CalculateEma(IReadOnlyList<double> values, int period)
    {
        int count = values.Count;
        var ema = new double[count];
        if (count < period || period <= 0) return ema;

        // BƯỚC 1: Khởi tạo giá trị EMA đầu tiên bằng trung bình cộng đơn giản (SMA) của 'period' nến đầu tiên
        double sum = 0;
        for (int i = 0; i < period; i++) sum += values[i];
        ema[period - 1] = sum / period;

        // BƯỚC 2: Tính lũy kế theo công thức hàm mũ cho tất cả các nến tiếp theo
        double multiplier = 2.0 / (period + 1);
        for (int i = period; i < count; i++)
        {
            ema[i] = (values[i] - ema[i - 1]) * multiplier + ema[i - 1];
        }

        // BƯỚC 3: Xử lý dữ liệu biên (Padding): Gán các nến chưa đủ chu kỳ bằng giá trị EMA ban đầu để mảng không bị 0/NaN
        for (int i = 0; i < period - 1; i++) ema[i] = ema[period - 1];

        return ema;
    }

    /// <summary>
    /// Đường trung bình động đơn giản (SMA - Simple Moving Average)
    /// </summary>
    /// <remarks>
    /// BẢN CHẤT TOÁN HỌC & TỐI ƯU HIỆU NĂNG:
    /// - Giá trị trung bình cộng của N phiên gần nhất: SMA = (P1 + P2 + ... + Pn) / N.
    /// - Tối ưu O(1) qua kỹ thuật Cửa sổ trượt (Sliding Window):
    ///   Thay vì cộng lại N phần tử ở mỗi bước (O(N)), ta chỉ lấy Tổng_cũ + Phần_tử_mới - Phần_tử_rời_cửa_sổ.
    ///   Giúp tốc độ tính toán hàng triệu nến chỉ mất vài mili-giây.
    /// </remarks>
    public static double[] CalculateSma(IReadOnlyList<double> values, int period)
    {
        int count = values.Count;
        var sma = new double[count];
        if (count < period || period <= 0) return sma;

        // Khởi tạo cửa sổ trượt ban đầu
        double sum = 0;
        for (int i = 0; i < period; i++) sum += values[i];
        sma[period - 1] = sum / period;

        // Kỹ thuật Sliding Window O(1): Cộng thêm nến mới vào, trừ đi nến cũ nhất bị đẩy ra khỏi khung
        for (int i = period; i < count; i++)
        {
            sum += values[i] - values[i - period];
            sma[i] = sum / period;
        }

        // Điền giá trị biên cho các nến đầu chu kỳ
        for (int i = 0; i < period - 1; i++) sma[i] = sma[period - 1];

        return sma;
    }

    /// <summary>
    /// Chỉ số sức mạnh tương đối (RSI - Relative Strength Index) của J. Welles Wilder
    /// </summary>
    /// <remarks>
    /// BẢN CHẤT TOÁN HỌC:
    /// - Đo lường vận tốc và sự thay đổi biên độ giá trong khoảng từ 0 đến 100.
    /// - RS (Relative Strength) = Trung_bình_Lãi / Trung_bình_Lỗ.
    /// - RSI = 100 - (100 / (1 + RS)).
    /// - Wilder's Smoothing: Sau chu kỳ đầu tiên, trung bình lãi/lỗ được làm mịn theo trọng số Wilder:
    ///   AvgGain_mới = (AvgGain_cũ * (N - 1) + Gain_hiện_tại) / N.
    /// - Ý nghĩa thực chiến:
    ///   + RSI > 70: Vùng Quá mua (Overbought) -> Rủi ro đảo chiều giảm.
    ///   + RSI < 30: Vùng Quá bán (Oversold) -> Cơ hội phục hồi ngắn hạn.
    ///   + RSI cắt lên 45-50: Tín hiệu động lượng bên mua bắt đầu áp đảo bên bán.
    /// </remarks>
    public static double[] CalculateRsi(IReadOnlyList<double> closes, int period = 14)
    {
        int count = closes.Count;
        var rsi = new double[count];
        if (count <= period) return rsi;

        double gainSum = 0;
        double lossSum = 0;

        // Giai đoạn 1: Tính tổng Lãi và Lỗ cho chu kỳ đầu tiên
        for (int i = 1; i <= period; i++)
        {
            double diff = closes[i] - closes[i - 1];
            if (diff >= 0) gainSum += diff;
            else lossSum -= diff;
        }

        double avgGain = gainSum / period;
        double avgLoss = lossSum / period;

        // Giá trị RSI đầu tiên tại vị trí nến 'period'
        rsi[period] = avgLoss == 0 ? 100 : 100 - (100 / (1 + (avgGain / avgLoss)));

        // Giai đoạn 2: Áp dụng công thức làm mịn Wilder cho các nến tiếp theo
        for (int i = period + 1; i < count; i++)
        {
            double diff = closes[i] - closes[i - 1];
            double gain = diff > 0 ? diff : 0;
            double loss = diff < 0 ? -diff : 0;

            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;

            rsi[i] = avgLoss == 0 ? 100 : 100 - (100 / (1 + (avgGain / avgLoss)));
        }

        // Với các nến đầu chưa đủ dữ liệu tính RSI, gán giá trị 50 (vùng cân bằng/trung lập)
        for (int i = 0; i < period; i++) rsi[i] = 50;

        return rsi;
    }

    /// <summary>
    /// Độ biến động thực tế trung bình (ATR - Average True Range)
    /// </summary>
    /// <remarks>
    /// BẢN CHẤT TOÁN HỌC & QUẢN TRỊ RỦI RO ĐỘNG:
    /// - Không đo xu hướng (tăng/giảm) mà đo "ĐỘ CO GIÃN / BIẾN ĐỘNG" của thị trường.
    /// - True Range (TR) là giá trị lớn nhất trong 3 trường hợp:
    ///   1. Khoảng cách đỉnh - đáy nến hiện tại: High - Low.
    ///   2. Khoảng cách nhảy giá mở cửa (Gap) từ đỉnh: |High - Close_hôm_qua|.
    ///   3. Khoảng cách nhảy giá mở cửa (Gap) từ đáy: |Low - Close_hôm_qua|.
    /// - Ứng dụng sống còn trong Trading:
    ///   Đặt Stop Loss / Take Profit cố định (ví dụ 1%) rất dễ bị "quét râu nến" khi thị trường rung lắc mạnh.
    ///   Dùng ATR x 1.5 hoặc ATR x 2.0 giúp khoảng dừng lỗ tự động nới rộng khi thị trường biến động mạnh, và tự co lại khi thị trường đi ngang.
    /// </remarks>
    public static double[] CalculateAtr(IReadOnlyList<double> highs, IReadOnlyList<double> lows, IReadOnlyList<double> closes, int period = 14)
    {
        int count = closes.Count;
        var atr = new double[count];
        if (count <= period) return atr;

        // Tính True Range (TR) cho từng nến
        var tr = new double[count];
        tr[0] = highs[0] - lows[0];
        for (int i = 1; i < count; i++)
        {
            double hl = highs[i] - lows[i];
            double hc = Math.Abs(highs[i] - closes[i - 1]);
            double lc = Math.Abs(lows[i] - closes[i - 1]);
            tr[i] = Math.Max(hl, Math.Max(hc, lc));
        }

        // Giá trị ATR đầu tiên = trung bình cộng TR trong 'period' nến
        double trSum = 0;
        for (int i = 0; i < period; i++) trSum += tr[i];
        atr[period - 1] = trSum / period;

        // Làm mịn ATR theo công thức Wilder cho các nến sau
        for (int i = period; i < count; i++)
        {
            atr[i] = (atr[i - 1] * (period - 1) + tr[i]) / period;
        }

        // Điền giá trị biên
        for (int i = 0; i < period - 1; i++) atr[i] = atr[period - 1];

        return atr;
    }

    /// <summary>
    /// Chỉ báo MACD (Moving Average Convergence Divergence - Phân kỳ hội tụ đường trung bình động)
    /// </summary>
    /// <remarks>
    /// BẢN CHẤT TOÁN HỌC:
    /// 1. MacdLine (Đường MACD nhanh) = EMA(12) - EMA(26).
    ///    Đo lường độ dốc và khoảng cách phân kỳ giữa xu hướng ngắn hạn và trung hạn.
    /// 2. SignalLine (Đường tín hiệu chậm) = EMA(9) của MacdLine.
    /// 3. Histogram (Biểu đồ xung lượng) = MacdLine - SignalLine.
    ///    Histogram > 0: Phe bò (mua) đang gia tăng xung lượng.
    ///    Histogram < 0: Phe gấu (bán) đang gia tăng sức ép.
    /// - Giao cắt kinh điển: MacdLine cắt lên SignalLine là tín hiệu Mua; cắt xuống là tín hiệu Bán/Short.
    /// </remarks>
    public static (double[] MacdLine, double[] SignalLine, double[] Histogram) CalculateMacd(
        IReadOnlyList<double> closes, int fast = 12, int slow = 26, int signal = 9)
    {
        int count = closes.Count;
        var fastEma = CalculateEma(closes, fast);
        var slowEma = CalculateEma(closes, slow);

        // 1. Tính đường MACD chính = EMA(fast) - EMA(slow)
        var macdLine = new double[count];
        for (int i = 0; i < count; i++)
        {
            macdLine[i] = fastEma[i] - slowEma[i];
        }

        // 2. Tính đường tín hiệu Signal = EMA(signal) của đường MACD
        var signalLine = CalculateEma(macdLine, signal);

        // 3. Tính cột Histogram = Hiệu số giữa MACD và Signal
        var histogram = new double[count];
        for (int i = 0; i < count; i++)
        {
            histogram[i] = macdLine[i] - signalLine[i];
        }

        return (macdLine, signalLine, histogram);
    }
}
