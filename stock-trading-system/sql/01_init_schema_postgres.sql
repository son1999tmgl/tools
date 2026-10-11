-- ==============================================================================
-- DATABASE SCHEMA: HỆ THỐNG GIAO DỊCH CHỨNG KHOÁN & PHÁI SINH ĐA TÀI SẢN
-- HỆ QUẢN TRỊ CSDL: PostgreSQL 14+ / 16+
-- ==============================================================================

-- 1. BẢNG NHÓM NGÀNH & RỔ DANH MỤC
CREATE TABLE IF NOT EXISTS asset_categories (
    category_code VARCHAR(30) PRIMARY KEY,
    category_name VARCHAR(100) NOT NULL,
    category_type VARCHAR(20) NOT NULL, -- 'MARKET', 'SECTOR', 'INDEX_BASKET'
    description TEXT
);

CREATE INDEX IF NOT EXISTS idx_asset_categories_type ON asset_categories(category_type);

-- 2. BẢNG DANH MỤC TOÀN BỘ MÃ TÀI SẢN (CỔ PHIẾU, CHỨNG QUYỀN, PHÁI SINH, CHỈ SỐ)
CREATE TABLE IF NOT EXISTS symbols (
    symbol VARCHAR(15) PRIMARY KEY,
    exchange VARCHAR(10) NOT NULL, -- 'HOSE', 'HNX', 'UPCOM'
    asset_type VARCHAR(5) NOT NULL, -- 'S' (Stock), 'W' (CW), 'D' (Derivative), 'I' (Index)
    category_code VARCHAR(30) REFERENCES asset_categories(category_code) ON DELETE SET NULL,
    company_name_vi VARCHAR(255),
    company_name_en VARCHAR(255),
    short_name VARCHAR(50),
    isin VARCHAR(20),
    lot_size INT NOT NULL DEFAULT 100, -- CP: 100, Phái sinh: 1 hợp đồng
    par_value NUMERIC(18, 2) DEFAULT 10000.00,
    is_trading BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_symbols_exchange ON symbols(exchange);
CREATE INDEX IF NOT EXISTS idx_symbols_asset_type ON symbols(asset_type);
CREATE INDEX IF NOT EXISTS idx_symbols_category ON symbols(category_code);

-- 3. BẢNG THÔNG SỐ CHỨNG QUYỀN (COVERED WARRANTS)
CREATE TABLE IF NOT EXISTS covered_warrants (
    symbol VARCHAR(15) PRIMARY KEY REFERENCES symbols(symbol) ON DELETE CASCADE,
    underlying_symbol VARCHAR(15) NOT NULL REFERENCES symbols(symbol),
    issuer_name VARCHAR(50) NOT NULL,
    cw_type VARCHAR(5) NOT NULL DEFAULT 'C', -- 'C' (Call) / 'P' (Put)
    exercise_price NUMERIC(18, 2) NOT NULL,
    exercise_ratio VARCHAR(20) NOT NULL,
    ratio_multiplier DOUBLE PRECISION NOT NULL,
    first_trading_date DATE,
    last_trading_date DATE,
    maturity_date DATE NOT NULL,
    listed_shares BIGINT,
    settlement_type VARCHAR(10) DEFAULT 'CASH',
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE INDEX IF NOT EXISTS idx_cw_underlying ON covered_warrants(underlying_symbol);
CREATE INDEX IF NOT EXISTS idx_cw_issuer ON covered_warrants(issuer_name);
CREATE INDEX IF NOT EXISTS idx_cw_maturity ON covered_warrants(maturity_date);
CREATE INDEX IF NOT EXISTS idx_cw_active ON covered_warrants(is_active);

-- 4. BẢNG THÔNG SỐ HỢP ĐỒNG PHÁI SINH (DERIVATIVE CONTRACTS)
CREATE TABLE IF NOT EXISTS derivative_contracts (
    symbol VARCHAR(15) PRIMARY KEY REFERENCES symbols(symbol) ON DELETE CASCADE,
    underlying_index VARCHAR(15) NOT NULL, -- 'VN30', 'VN100'
    contract_month VARCHAR(10) NOT NULL, -- '202610', '202611'
    contract_multiplier INT NOT NULL DEFAULT 100000, -- 100.000 VNĐ / điểm
    initial_margin_rate DOUBLE PRECISION NOT NULL DEFAULT 0.17, -- 17% (VSD)
    maintenance_margin_rate DOUBLE PRECISION NOT NULL DEFAULT 0.13, -- 13%
    first_trading_date DATE,
    last_trading_date DATE NOT NULL,
    settlement_method VARCHAR(20) DEFAULT 'CASH',
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE INDEX IF NOT EXISTS idx_deriv_underlying ON derivative_contracts(underlying_index);
CREATE INDEX IF NOT EXISTS idx_deriv_last_trading ON derivative_contracts(last_trading_date);
CREATE INDEX IF NOT EXISTS idx_deriv_active ON derivative_contracts(is_active);

-- 5. BẢNG NẾN NGÀY EOD & TỔNG KẾT PHIÊN (DÙNG CHUNG CP, CW, PHÁI SINH, INDEX)
CREATE TABLE IF NOT EXISTS market_daily_bars (
    symbol VARCHAR(15) NOT NULL REFERENCES symbols(symbol) ON DELETE CASCADE,
    trading_date DATE NOT NULL,
    open NUMERIC(18, 2),
    high NUMERIC(18, 2),
    low NUMERIC(18, 2),
    close NUMERIC(18, 2),
    ref_price NUMERIC(18, 2),
    ceiling_price NUMERIC(18, 2),
    floor_price NUMERIC(18, 2),
    total_volume BIGINT NOT NULL DEFAULT 0,
    total_value NUMERIC(18, 2) NOT NULL DEFAULT 0,
    buy_active_volume BIGINT,
    sell_active_volume BIGINT,
    open_interest BIGINT, -- Khối lượng vị thế mở qua đêm (cho Phái sinh)
    foreign_buy_vol BIGINT,
    foreign_sell_vol BIGINT,
    foreign_room BIGINT,
    PRIMARY KEY (symbol, trading_date)
);

CREATE INDEX IF NOT EXISTS idx_daily_bars_date ON market_daily_bars(trading_date);

-- 6. BẢNG TÍN HIỆU THUẬT TOÁN ĐA CẤP ĐỘ & 2 CHIỀU LONG/SHORT
CREATE TABLE IF NOT EXISTS algorithmic_trading_signals (
    id BIGSERIAL PRIMARY KEY,
    strategy_name VARCHAR(100) NOT NULL,
    target_scope VARCHAR(20) NOT NULL, -- 'MARKET', 'SECTOR', 'BASKET', 'SYMBOL'
    target_code VARCHAR(30) NOT NULL,
    action_type VARCHAR(20) NOT NULL, -- 'OPEN_LONG', 'CLOSE_LONG', 'OPEN_SHORT', 'CLOSE_SHORT', 'RISK_ON', 'RISK_OFF'
    timeframe VARCHAR(10) NOT NULL, -- '1M', '5M', '15M', 'DAILY'
    signal_time TIMESTAMPTZ NOT NULL,
    target_price NUMERIC(18, 2),
    stop_loss_price NUMERIC(18, 2),
    take_profit_price NUMERIC(18, 2),
    confidence_score DOUBLE PRECISION,
    affected_symbols JSONB,
    metrics_snapshot JSONB
);

CREATE INDEX IF NOT EXISTS idx_signals_strategy ON algorithmic_trading_signals(strategy_name);
CREATE INDEX IF NOT EXISTS idx_signals_target ON algorithmic_trading_signals(target_scope, target_code);
CREATE INDEX IF NOT EXISTS idx_signals_time ON algorithmic_trading_signals(signal_time);
CREATE INDEX IF NOT EXISTS idx_signals_action ON algorithmic_trading_signals(action_type);

-- 7. BẢNG QUẢN LÝ CÁC ĐỢT CHẠY BACKTEST
CREATE TABLE IF NOT EXISTS backtest_runs (
    run_id UUID PRIMARY KEY,
    strategy_name VARCHAR(100) NOT NULL,
    asset_class VARCHAR(20) NOT NULL, -- 'STOCK', 'COVERED_WARRANT', 'DERIVATIVE_VN30'
    symbols_tested TEXT,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    initial_capital NUMERIC(18, 2) NOT NULL,
    final_equity NUMERIC(18, 2) NOT NULL,
    total_net_pnl NUMERIC(18, 2) NOT NULL,
    win_rate DOUBLE PRECISION NOT NULL,
    profit_factor DOUBLE PRECISION,
    max_drawdown DOUBLE PRECISION,
    sharpe_ratio DOUBLE PRECISION,
    total_trades INT NOT NULL DEFAULT 0,
    winning_trades INT NOT NULL DEFAULT 0,
    losing_trades INT NOT NULL DEFAULT 0,
    parameters JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_backtest_strategy ON backtest_runs(strategy_name);
CREATE INDEX IF NOT EXISTS idx_backtest_asset_class ON backtest_runs(asset_class);

-- 8. BẢNG LỊCH SỬ VỊ THẾ 2 CHIỀU (SIMULATED TRADES / POSITIONS)
CREATE TABLE IF NOT EXISTS simulated_trades (
    trade_id UUID PRIMARY KEY,
    run_id UUID NOT NULL REFERENCES backtest_runs(run_id) ON DELETE CASCADE,
    symbol VARCHAR(15) NOT NULL,
    position_side VARCHAR(10) NOT NULL, -- 'LONG' hoặc 'SHORT'
    entry_time TIMESTAMPTZ NOT NULL,
    entry_price NUMERIC(18, 2) NOT NULL,
    exit_time TIMESTAMPTZ,
    exit_price NUMERIC(18, 2),
    quantity INT NOT NULL,
    contract_multiplier INT NOT NULL DEFAULT 1, -- CP: 1, Phái sinh: 100.000
    gross_pnl NUMERIC(18, 2),
    fee_and_tax NUMERIC(18, 2),
    net_pnl NUMERIC(18, 2),
    return_pct DOUBLE PRECISION,
    exit_reason VARCHAR(30), -- 'TAKE_PROFIT', 'STOP_LOSS', 'TRAILING_STOP', 'TIMEOUT', 'ATC'
    mfe_max_runup NUMERIC(18, 2),
    mae_max_drawdown NUMERIC(18, 2)
);

CREATE INDEX IF NOT EXISTS idx_trades_run_id ON simulated_trades(run_id);
CREATE INDEX IF NOT EXISTS idx_trades_symbol ON simulated_trades(symbol);
CREATE INDEX IF NOT EXISTS idx_trades_side ON simulated_trades(position_side);
CREATE INDEX IF NOT EXISTS idx_trades_entry_time ON simulated_trades(entry_time);
