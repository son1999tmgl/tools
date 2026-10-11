using Microsoft.EntityFrameworkCore;
using TradingSystem.Core.Entities;

namespace TradingSystem.Infrastructure.Data;

public class TradingDbContext : DbContext
{
    public TradingDbContext(DbContextOptions<TradingDbContext> options) : base(options)
    {
    }

    public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();
    public DbSet<Symbol> Symbols => Set<Symbol>();
    public DbSet<CoveredWarrant> CoveredWarrants => Set<CoveredWarrant>();
    public DbSet<DerivativeContract> DerivativeContracts => Set<DerivativeContract>();
    public DbSet<MarketDailyBar> MarketDailyBars => Set<MarketDailyBar>();
    public DbSet<AlgorithmicTradingSignal> AlgorithmicTradingSignals => Set<AlgorithmicTradingSignal>();
    public DbSet<BacktestRun> BacktestRuns => Set<BacktestRun>();
    public DbSet<SimulatedTrade> SimulatedTrades => Set<SimulatedTrade>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. asset_categories
        modelBuilder.Entity<AssetCategory>(entity =>
        {
            entity.ToTable("asset_categories");
            entity.HasKey(e => e.CategoryCode);
            entity.Property(e => e.CategoryCode).HasColumnName("category_code").HasMaxLength(30);
            entity.Property(e => e.CategoryName).HasColumnName("category_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.CategoryType).HasColumnName("category_type").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description");
        });

        // 2. symbols
        modelBuilder.Entity<Symbol>(entity =>
        {
            entity.ToTable("symbols");
            entity.HasKey(e => e.SymbolCode);
            entity.Property(e => e.SymbolCode).HasColumnName("symbol").HasMaxLength(15);
            entity.Property(e => e.Exchange).HasColumnName("exchange").HasMaxLength(10).IsRequired();
            entity.Property(e => e.AssetType).HasColumnName("asset_type").HasMaxLength(5).IsRequired();
            entity.Property(e => e.CategoryCode).HasColumnName("category_code").HasMaxLength(30);
            entity.Property(e => e.CompanyNameVi).HasColumnName("company_name_vi").HasMaxLength(255);
            entity.Property(e => e.CompanyNameEn).HasColumnName("company_name_en").HasMaxLength(255);
            entity.Property(e => e.ShortName).HasColumnName("short_name").HasMaxLength(50);
            entity.Property(e => e.Isin).HasColumnName("isin").HasMaxLength(20);
            entity.Property(e => e.LotSize).HasColumnName("lot_size").HasDefaultValue(100);
            entity.Property(e => e.ParValue).HasColumnName("par_value").HasColumnType("numeric(18,2)").HasDefaultValue(10000m);
            entity.Property(e => e.IsTrading).HasColumnName("is_trading").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Symbols)
                  .HasForeignKey(e => e.CategoryCode)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // 3. covered_warrants
        modelBuilder.Entity<CoveredWarrant>(entity =>
        {
            entity.ToTable("covered_warrants");
            entity.HasKey(e => e.SymbolCode);
            entity.Property(e => e.SymbolCode).HasColumnName("symbol").HasMaxLength(15);
            entity.Property(e => e.UnderlyingSymbol).HasColumnName("underlying_symbol").HasMaxLength(15).IsRequired();
            entity.Property(e => e.IssuerName).HasColumnName("issuer_name").HasMaxLength(50).IsRequired();
            entity.Property(e => e.CwType).HasColumnName("cw_type").HasMaxLength(5).HasDefaultValue("C");
            entity.Property(e => e.ExercisePrice).HasColumnName("exercise_price").HasColumnType("numeric(18,2)");
            entity.Property(e => e.ExerciseRatio).HasColumnName("exercise_ratio").HasMaxLength(20);
            entity.Property(e => e.RatioMultiplier).HasColumnName("ratio_multiplier");
            entity.Property(e => e.FirstTradingDate).HasColumnName("first_trading_date");
            entity.Property(e => e.LastTradingDate).HasColumnName("last_trading_date");
            entity.Property(e => e.MaturityDate).HasColumnName("maturity_date");
            entity.Property(e => e.ListedShares).HasColumnName("listed_shares");
            entity.Property(e => e.SettlementType).HasColumnName("settlement_type").HasMaxLength(10).HasDefaultValue("CASH");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);

            entity.HasOne(e => e.Symbol)
                  .WithOne(s => s.CoveredWarrantSpec)
                  .HasForeignKey<CoveredWarrant>(e => e.SymbolCode)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 4. derivative_contracts
        modelBuilder.Entity<DerivativeContract>(entity =>
        {
            entity.ToTable("derivative_contracts");
            entity.HasKey(e => e.SymbolCode);
            entity.Property(e => e.SymbolCode).HasColumnName("symbol").HasMaxLength(15);
            entity.Property(e => e.UnderlyingIndex).HasColumnName("underlying_index").HasMaxLength(15).HasDefaultValue("VN30");
            entity.Property(e => e.ContractMonth).HasColumnName("contract_month").HasMaxLength(10);
            entity.Property(e => e.ContractMultiplier).HasColumnName("contract_multiplier").HasDefaultValue(100000);
            entity.Property(e => e.InitialMarginRate).HasColumnName("initial_margin_rate").HasDefaultValue(0.17);
            entity.Property(e => e.MaintenanceMarginRate).HasColumnName("maintenance_margin_rate").HasDefaultValue(0.13);
            entity.Property(e => e.FirstTradingDate).HasColumnName("first_trading_date");
            entity.Property(e => e.LastTradingDate).HasColumnName("last_trading_date");
            entity.Property(e => e.SettlementMethod).HasColumnName("settlement_method").HasMaxLength(20).HasDefaultValue("CASH");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);

            entity.HasOne(e => e.Symbol)
                  .WithOne(s => s.DerivativeContractSpec)
                  .HasForeignKey<DerivativeContract>(e => e.SymbolCode)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 5. market_daily_bars
        modelBuilder.Entity<MarketDailyBar>(entity =>
        {
            entity.ToTable("market_daily_bars");
            entity.HasKey(e => new { e.SymbolCode, e.TradingDate });
            entity.Property(e => e.SymbolCode).HasColumnName("symbol").HasMaxLength(15);
            entity.Property(e => e.TradingDate).HasColumnName("trading_date");
            entity.Property(e => e.Open).HasColumnName("open").HasColumnType("numeric(18,2)");
            entity.Property(e => e.High).HasColumnName("high").HasColumnType("numeric(18,2)");
            entity.Property(e => e.Low).HasColumnName("low").HasColumnType("numeric(18,2)");
            entity.Property(e => e.Close).HasColumnName("close").HasColumnType("numeric(18,2)");
            entity.Property(e => e.RefPrice).HasColumnName("ref_price").HasColumnType("numeric(18,2)");
            entity.Property(e => e.CeilingPrice).HasColumnName("ceiling_price").HasColumnType("numeric(18,2)");
            entity.Property(e => e.FloorPrice).HasColumnName("floor_price").HasColumnType("numeric(18,2)");
            entity.Property(e => e.TotalVolume).HasColumnName("total_volume").HasDefaultValue(0);
            entity.Property(e => e.TotalValue).HasColumnName("total_value").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            entity.Property(e => e.BuyActiveVolume).HasColumnName("buy_active_volume");
            entity.Property(e => e.SellActiveVolume).HasColumnName("sell_active_volume");
            entity.Property(e => e.OpenInterest).HasColumnName("open_interest");
            entity.Property(e => e.ForeignBuyVol).HasColumnName("foreign_buy_vol");
            entity.Property(e => e.ForeignSellVol).HasColumnName("foreign_sell_vol");
            entity.Property(e => e.ForeignRoom).HasColumnName("foreign_room");

            entity.HasOne(e => e.Symbol)
                  .WithMany(s => s.DailyBars)
                  .HasForeignKey(e => e.SymbolCode)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 6. algorithmic_trading_signals
        modelBuilder.Entity<AlgorithmicTradingSignal>(entity =>
        {
            entity.ToTable("algorithmic_trading_signals");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.StrategyName).HasColumnName("strategy_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.TargetScope).HasColumnName("target_scope").HasMaxLength(20).IsRequired();
            entity.Property(e => e.TargetCode).HasColumnName("target_code").HasMaxLength(30).IsRequired();
            entity.Property(e => e.ActionType).HasColumnName("action_type").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Timeframe).HasColumnName("timeframe").HasMaxLength(10).HasDefaultValue("1M");
            entity.Property(e => e.SignalTime).HasColumnName("signal_time").HasColumnType("timestamptz");
            entity.Property(e => e.TargetPrice).HasColumnName("target_price").HasColumnType("numeric(18,2)");
            entity.Property(e => e.StopLossPrice).HasColumnName("stop_loss_price").HasColumnType("numeric(18,2)");
            entity.Property(e => e.TakeProfitPrice).HasColumnName("take_profit_price").HasColumnType("numeric(18,2)");
            entity.Property(e => e.ConfidenceScore).HasColumnName("confidence_score");
            entity.Property(e => e.AffectedSymbolsJson).HasColumnName("affected_symbols").HasColumnType("jsonb");
            entity.Property(e => e.MetricsSnapshotJson).HasColumnName("metrics_snapshot").HasColumnType("jsonb");
        });

        // 7. backtest_runs
        modelBuilder.Entity<BacktestRun>(entity =>
        {
            entity.ToTable("backtest_runs");
            entity.HasKey(e => e.RunId);
            entity.Property(e => e.RunId).HasColumnName("run_id");
            entity.Property(e => e.StrategyName).HasColumnName("strategy_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.AssetClass).HasColumnName("asset_class").HasMaxLength(20).IsRequired();
            entity.Property(e => e.SymbolsTested).HasColumnName("symbols_tested");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.InitialCapital).HasColumnName("initial_capital").HasColumnType("numeric(18,2)");
            entity.Property(e => e.FinalEquity).HasColumnName("final_equity").HasColumnType("numeric(18,2)");
            entity.Property(e => e.TotalNetPnl).HasColumnName("total_net_pnl").HasColumnType("numeric(18,2)");
            entity.Property(e => e.WinRate).HasColumnName("win_rate");
            entity.Property(e => e.ProfitFactor).HasColumnName("profit_factor");
            entity.Property(e => e.MaxDrawdown).HasColumnName("max_drawdown");
            entity.Property(e => e.SharpeRatio).HasColumnName("sharpe_ratio");
            entity.Property(e => e.TotalTrades).HasColumnName("total_trades").HasDefaultValue(0);
            entity.Property(e => e.WinningTrades).HasColumnName("winning_trades").HasDefaultValue(0);
            entity.Property(e => e.LosingTrades).HasColumnName("losing_trades").HasDefaultValue(0);
            entity.Property(e => e.ParametersJson).HasColumnName("parameters").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
        });

        // 8. simulated_trades
        modelBuilder.Entity<SimulatedTrade>(entity =>
        {
            entity.ToTable("simulated_trades");
            entity.HasKey(e => e.TradeId);
            entity.Property(e => e.TradeId).HasColumnName("trade_id");
            entity.Property(e => e.RunId).HasColumnName("run_id");
            entity.Property(e => e.SymbolCode).HasColumnName("symbol").HasMaxLength(15).IsRequired();
            entity.Property(e => e.PositionSide).HasColumnName("position_side").HasMaxLength(10).HasDefaultValue("LONG");
            entity.Property(e => e.EntryTime).HasColumnName("entry_time").HasColumnType("timestamptz");
            entity.Property(e => e.EntryPrice).HasColumnName("entry_price").HasColumnType("numeric(18,2)");
            entity.Property(e => e.ExitTime).HasColumnName("exit_time").HasColumnType("timestamptz");
            entity.Property(e => e.ExitPrice).HasColumnName("exit_price").HasColumnType("numeric(18,2)");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.ContractMultiplier).HasColumnName("contract_multiplier").HasDefaultValue(1);
            entity.Property(e => e.GrossPnl).HasColumnName("gross_pnl").HasColumnType("numeric(18,2)");
            entity.Property(e => e.FeeAndTax).HasColumnName("fee_and_tax").HasColumnType("numeric(18,2)");
            entity.Property(e => e.NetPnl).HasColumnName("net_pnl").HasColumnType("numeric(18,2)");
            entity.Property(e => e.ReturnPct).HasColumnName("return_pct");
            entity.Property(e => e.ExitReason).HasColumnName("exit_reason").HasMaxLength(30);
            entity.Property(e => e.MfeMaxRunup).HasColumnName("mfe_max_runup").HasColumnType("numeric(18,2)");
            entity.Property(e => e.MaeMaxDrawdown).HasColumnName("mae_max_drawdown").HasColumnType("numeric(18,2)");

            entity.HasOne(e => e.BacktestRun)
                  .WithMany(r => r.Trades)
                  .HasForeignKey(e => e.RunId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
