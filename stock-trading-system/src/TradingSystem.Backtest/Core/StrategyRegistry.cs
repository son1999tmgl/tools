using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TradingSystem.Backtest.Core;

/// <summary>
/// KHO QUẢN LÝ TẬP TRUNG CÁC THUẬT TOÁN & MÔ HÌNH TOÁN HỌC (STRATEGY REGISTRY)
/// - Hỗ trợ mở rộng không giới hạn lên tới hàng trăm mô hình.
/// - Tự động quét và phát hiện các class kế thừa ITradingStrategy trong hệ thống.
/// - Phân loại theo mục đích: Mô hình Điểm Mua, Mô hình Điểm Bán, Mô hình 2 Chiều.
/// </summary>
public class StrategyRegistry
{
    private readonly Dictionary<string, ITradingStrategy> _strategies = new(StringComparer.OrdinalIgnoreCase);

    public StrategyRegistry()
    {
        // Tự động quét và nạp toàn bộ các mô hình trong Assembly
        AutoDiscoverStrategies();
    }

    /// <summary>
    /// Tự động quét qua Assembly tìm tất cả các class hiện thực ITradingStrategy
    /// </summary>
    public void AutoDiscoverStrategies()
    {
        var strategyTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(ITradingStrategy).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in strategyTypes)
        {
            try
            {
                if (Activator.CreateInstance(type) is ITradingStrategy instance)
                {
                    _strategies[instance.Id] = instance;
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// Đăng ký thủ công một mô hình toán học mới
    /// </summary>
    public void Register(ITradingStrategy strategy)
    {
        _strategies[strategy.Id] = strategy;
    }

    /// <summary>
    /// Lấy một mô hình cụ thể theo Id
    /// </summary>
    public ITradingStrategy? GetById(string id)
    {
        return _strategies.GetValueOrDefault(id);
    }

    /// <summary>
    /// Lấy toàn bộ danh sách các mô hình trong kho
    /// </summary>
    public IReadOnlyList<ITradingStrategy> GetAll()
    {
        return _strategies.Values.ToList();
    }

    /// <summary>
    /// Lọc các mô hình chuyên biệt dự đoán ĐIỂM MUA (BUY_MODEL)
    /// </summary>
    public IReadOnlyList<ITradingStrategy> GetBuyModels()
    {
        return _strategies.Values.Where(s => s.StrategyType.Equals("BUY_MODEL", StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// Lọc các mô hình chuyên biệt dự đoán ĐIỂM BÁN & SHORT (SELL_MODEL)
    /// </summary>
    public IReadOnlyList<ITradingStrategy> GetSellModels()
    {
        return _strategies.Values.Where(s => s.StrategyType.Equals("SELL_MODEL", StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
