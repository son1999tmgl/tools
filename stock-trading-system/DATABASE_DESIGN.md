# THIẾT KẾ CƠ SỞ DỮ LIỆU & KIẾN TRÚC LƯU TRỮ HỆ THỐNG GIAO DỊCH
*(Hệ thống Đa tài sản: Cổ phiếu, Chứng quyền & Phái sinh 2 chiều Long/Short - Chuẩn Định lượng & Backtest)*

---

## 1. Triết Lý Thiết Kế: Kiến Trúc Lai (Hybrid Storage Architecture)

Để giải quyết bài toán **Big Data tài chính** (hàng triệu tick/ngày, nến 1 phút đa khung, sổ lệnh vi mô, đa cấp độ tín hiệu Thị trường - Ngành - Cổ phiếu, và vòng đời lệnh 2 chiều LONG / SHORT):

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                           TẦNG DATABASE QUẢN LÝ (SQL)                           │
│     (Dữ liệu quan hệ, Danh mục, Thông số CW/Phái sinh, Nến ngày, Tín hiệu)      │
├─────────────────────────────────────────────────────────────────────────────────┤
│ 1. asset_categories            : Phân cấp Ngành, Rổ chỉ số (Market, Sector, Basket)│
│ 2. symbols                     : Danh mục toàn bộ Cổ phiếu, CW, Phái sinh VN30  │
│ 3. covered_warrants            : Thông số Chứng quyền (Black-Scholes, Greeks)   │
│ 4. derivative_contracts        : Thông số Hợp đồng Phái sinh (VN30F, Ký quỹ, Hạn)│
│ 5. market_daily_bars           : Nến ngày EOD, Khối ngoại, Khối tự doanh, Room  │
│ 6. algorithmic_trading_signals : Tín hiệu đa cấp (Thị trường, Ngành, Mã, Long/Short)│
│ 7. backtest_runs               : Lịch sử và thông số các lần chạy Backtest      │
│ 8. simulated_trades (Positions): Lịch sử vị thế 2 chiều (LONG/SHORT, Entry/Exit, PnL)│
└─────────────────────────────────────────────────────────────────────────────────┘
                                         │
                                         ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                    TẦNG TIME-SERIES BIG DATA (PARQUET STORAGE)                  │
│       (Lưu trữ cột, nén 5x-10x, nạp trực tiếp vào RAM/GPU phục vụ thuật toán)   │
├─────────────────────────────────────────────────────────────────────────────────┤
│ 9.  market_ticks (Tick-by-tick): Khớp lệnh từng mili-giây, giá, vol, chiều Mua/Bán│
│ 10. orderbook_depth (L2/L3)    : 3-10 bước giá Mua/Bán & Order Book Imbalance   │
│ 11. bars_1m (Nến 1 phút)       : Chuỗi nến 1m liên tục cho Cổ phiếu, CW, VN30F  │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Chi Tiết Tầng Database Quản Lý (SQL Schema)

### 2.1. Bảng `asset_categories` (Nhóm ngành & Rổ danh mục)
* **Mục đích**: Hỗ trợ các thuật toán **Luân chuyển dòng tiền ngành (Sector Rotation)**, **Độ rộng thị trường (Market Breadth)** và **Độ lệch chỉ số (Index Arbitrage)**.

| Tên trường | Kiểu dữ liệu | Khóa | Ý nghĩa |
| :--- | :--- | :---: | :--- |
| `category_code` | `VARCHAR(30)` | **PK** | Mã nhóm (VD: `MARKET_ALL`, `BANKING`, `STEEL`, `REAL_ESTATE`, `VN30`, `VNFINLEAD`) |
| `category_name` | `VARCHAR(100)` | | Tên nhóm (VD: `Ngành Thép`, `Nhóm Ngân hàng`, `Rổ Chỉ số VN30`) |
| `category_type` | `VARCHAR(20)` | INDEX | Phân loại: `'MARKET'`, `'SECTOR'`, `'INDEX_BASKET'` |
| `description` | `TEXT` | | Mô tả chi tiết |

---

### 2.2. Bảng `symbols` (Danh mục toàn bộ mã tài sản)
* **Mục đích**: Định danh tất cả các loại tài sản giao dịch trên thị trường.

| Tên trường | Kiểu dữ liệu | Khóa | Ý nghĩa & Nguồn lấy |
| :--- | :--- | :---: | :--- |
| `symbol` | `VARCHAR(15)` | **PK** | Mã giao dịch (VD: `HPG`, `CACB2603`, `VN30F1M`, `41I1GA000`). *(VPS/SSI)* |
| `exchange` | `VARCHAR(10)` | INDEX | Sàn: `HOSE`, `HNX`, `UPCOM`. *(VPS)* |
| **`asset_type`** | **`VARCHAR(5)`** | INDEX | **`'S'`** (Cổ phiếu - Stock)<br>**`'W'`** (Chứng quyền - Warrant)<br>**`'D'`** (Phái sinh - Derivative)<br>**`'I'`** (Chỉ số Index: VNINDEX, VN30) |
| `category_code` | `VARCHAR(30)` | FK | Ngành/Rổ chỉ số trực thuộc (FK -> `asset_categories.category_code`) |
| `company_name_vi`| `VARCHAR(255)` | | Tên tiếng Việt đầy đủ |
| `company_name_en`| `VARCHAR(255)` | | Tên tiếng Anh |
| `short_name` | `VARCHAR(50)` | | Tên viết tắt bảng điện |
| `isin` | `VARCHAR(20)` | | Mã định danh quốc tế ISIN (VD: `VN000000HPG4`, `VN0CACB26031`) |
| `lot_size` | `INT` | | Lô giao dịch tối thiểu (CP: 100, Phái sinh: 1 hợp đồng) |
| `par_value` | `DECIMAL(18,2)` | | Mệnh giá (Mặc định 10.000 VNĐ với CP) |
| `is_trading` | `BOOLEAN` | | Còn đang được phép giao dịch hay đã hủy niêm yết/đáo hạn |
| `created_at` | `TIMESTAMP` | | Ngày tạo |
| `updated_at` | `TIMESTAMP` | | Ngày cập nhật |

---

### 2.3. Bảng `covered_warrants` (Thông số Chứng Quyền Chuyên Sâu)
* **Mục đích**: Đầu vào cho mô hình **Black-Scholes**, tính hệ số Greeks (Delta $\Delta$, Gamma $\Gamma$, Theta $\Theta$, Vega $\nu$) và đòn bẩy hiệu dụng (Effective Gearing).

| Tên trường | Kiểu dữ liệu | Khóa | Ý nghĩa phục vụ mô hình toán | Nguồn |
| :--- | :--- | :---: | :--- | :--- |
| `symbol` | `VARCHAR(15)` | **PK** | Mã chứng quyền (VD: `CACB2603`). *(FK -> symbols)* | VPS/SSI |
| `underlying_symbol`| `VARCHAR(10)`| INDEX | Mã cổ phiếu mẹ cơ sở (VD: `ACB`). *(FK -> symbols)* | SSI/VPS |
| `issuer_name` | `VARCHAR(50)` | INDEX | CTCK phát hành (VD: `TCBS`, `SSI`, `VND`, `HSC`) | SSI/VPS |
| `cw_type` | `VARCHAR(5)` | | Loại chứng quyền: `'C'` (Call - Quyền Mua) / `'P'` (Put - Quyền Bán) | SSI/VPS |
| `exercise_price` | `DECIMAL(18,2)`| | **Giá thực hiện** (Strike Price $K$ trong Black-Scholes) | VPS/SSI |
| `exercise_ratio` | `VARCHAR(20)` | | Chuỗi tỷ lệ chuyển đổi gốc (VD: `1.7245:1`, `2:1`) | VPS/SSI |
| `ratio_multiplier`| `DOUBLE` | | Tỷ lệ số thập phân để chia toán học (VD: `1.7245`) | Tự tính |
| **`first_trading_date`**| **`DATE`** | | **Ngày bắt đầu niêm yết/giao dịch** | **SSI** |
| `last_trading_date` | `DATE` | | **Ngày giao dịch cuối cùng** trước khi hủy NY | VPS/SSI |
| `maturity_date` | `DATE` | INDEX | **Ngày đáo hạn chính thức** ($T$ trong Black-Scholes) | VPS/SSI |
| `listed_shares` | `BIGINT` | | Tổng khối lượng chứng quyền đang lưu hành | VPS |
| `settlement_type` | `VARCHAR(10)` | | Hình thức thanh toán: `'CASH'` (Tiền mặt) | SSI |
| `is_active` | `BOOLEAN` | INDEX | Còn hạn hay đã đáo hạn | Tự tính |

---

### 2.4. Bảng `derivative_contracts` (Thông số Hợp Đồng Phái Sinh)
* **Mục đích**: Lưu thông số các hợp đồng tương lai chỉ số VN30 / VN100 / TPCP để tính ký quỹ, đòn bẩy và đáo hạn.

| Tên trường | Kiểu dữ liệu | Khóa | Ý nghĩa & Công thức toán |
| :--- | :--- | :---: | :--- |
| `symbol` | `VARCHAR(15)` | **PK** | Mã HĐTL (VD: `VN30F1M`, `41I1GA000`, `VN30F2610`). *(FK -> symbols)* |
| `underlying_index` | `VARCHAR(15)` | INDEX | Chỉ số cơ sở tham chiếu (VD: `VN30`, `VN100`) |
| `contract_month` | `VARCHAR(10)` | | Tháng đáo hạn của hợp đồng (VD: `2026-10`, `102026`) |
| **`contract_multiplier`**| **`INT`** | | **Hệ số nhân hợp đồng** (Mặc định: **100.000 VNĐ** / 1 điểm chỉ số) |
| **`initial_margin_rate`**| **`DOUBLE`** | | **Tỷ lệ ký quỹ ban đầu (IM)** (Quy định VSD: thường là **17%** $\sim$ Đòn bẩy ~5.88x) |
| `maintenance_margin_rate`| `DOUBLE` | | Tỷ lệ ký quỹ duy trì (MM) (Thường là **13%** - dưới mức này bị Call Margin) |
| `first_trading_date`| `DATE` | | Ngày hợp đồng bắt đầu được giao dịch |
| `last_trading_date` | `DATE` | INDEX | **Ngày đáo hạn / Ngày giao dịch cuối cùng** (Thứ 5 tuần thứ 3 của tháng) |
| `settlement_method`| `VARCHAR(20)` | | Phương thức thanh toán: `'CASH'` (Chênh lệch tiền mặt theo điểm VN30 phiên ATC) |
| `is_active` | `BOOLEAN` | INDEX | Hợp đồng đang chạy hay đã đáo hạn |

---

### 2.5. Bảng `market_daily_bars` (Nến Ngày EOD & Tổng kết thị trường)
* **Mục đích**: Lưu trữ dữ liệu phiên ngày cho tất cả các mã (Cổ phiếu, CW, Phái sinh, Index).

| Tên trường | Kiểu dữ liệu | Khóa | Ý nghĩa |
| :--- | :--- | :---: | :--- |
| `symbol` | `VARCHAR(15)` | **PK (1)** | Mã CK / CW / Phái sinh |
| `trading_date` | `DATE` | **PK (2)** | Ngày giao dịch (VD: `2026-10-09`) |
| `open` | `DECIMAL(18,2)` | | Giá mở cửa (hoặc Điểm mở cửa với Phái sinh) |
| `high` | `DECIMAL(18,2)` | | Giá cao nhất |
| `low` | `DECIMAL(18,2)` | | Giá thấp nhất |
| `close` | `DECIMAL(18,2)` | | Giá đóng cửa |
| `ref_price` | `DECIMAL(18,2)` | | Giá tham chiếu đầu ngày |
| `ceiling_price`| `DECIMAL(18,2)` | | Giá trần |
| `floor_price` | `DECIMAL(18,2)` | | Giá sàn |
| `total_volume` | `BIGINT` | | Tổng khối lượng giao dịch cả ngày (CP: cổ phiếu, PS: hợp đồng) |
| `total_value` | `DECIMAL(18,2)` | | Tổng giá trị giao dịch cả ngày (VNĐ) |
| `buy_active_volume`| `BIGINT` | | **Tổng khối lượng MUA chủ động** (Khớp tại giá Ask) |
| `sell_active_volume`| `BIGINT` | | **Tổng khối lượng BÁN chủ động** (Khớp tại giá Bid) |
| **`open_interest`**| **`BIGINT`** (NULLABLE)| | **Khối lượng vị thế mở qua đêm (OI)** *(Cực quan trọng cho Phái sinh)* |
| `foreign_buy_vol`| `BIGINT` | | Khối ngoại mua |
| `foreign_sell_vol`| `BIGINT` | | Khối ngoại bán |
| `foreign_room` | `BIGINT` (NULLABLE)| | Room ngoại còn lại |

---

### 2.6. Bảng `algorithmic_trading_signals` (Tín hiệu Đa cấp độ & Đa chiều)
* **Mục đích**: Lưu trữ mọi tín hiệu sinh ra từ các mô hình toán học: từ cấp Vĩ mô/Thị trường, Cấp Ngành, đến Cấp Mã đơn lẻ, hỗ trợ đầy đủ lệnh 2 chiều LONG / SHORT.

| Tên trường | Kiểu dữ liệu | Khóa | Ý nghĩa & Mô tả |
| :--- | :--- | :---: | :--- |
| `id` | `BIGINT AUTO_INCREMENT`| **PK** | Định danh tín hiệu |
| `strategy_name` | `VARCHAR(100)` | INDEX | Tên mô hình (VD: `Sector_Rotation_V1`, `VN30F_Momentum_Breakout`, `OrderFlow_Imbalance`) |
| **`target_scope`** | **`VARCHAR(20)`** | INDEX | **Phạm vi áp dụng**: `'MARKET'`, `'SECTOR'`, `'BASKET'`, `'SYMBOL'` |
| **`target_code`** | **`VARCHAR(30)`** | INDEX | **Mã đối tượng**:<br>• `'VNINDEX'` / `'MARKET_ALL'` *(nếu là MARKET)*<br>• `'STEEL'`, `'BANKING'` *(nếu là SECTOR)*<br>• `'VN30'`, `'VNFINLEAD'` *(nếu là BASKET)*<br>• `'HPG'`, `'VN30F1M'`, `'CACB2603'` *(nếu là SYMBOL)* |
| **`action_type`** | **`VARCHAR(20)`** | INDEX | **Loại hành động 2 chiều**:<br>• **`OPEN_LONG`** (Mở vị thế Mua / Kỳ vọng tăng)<br>• **`CLOSE_LONG`** (Đóng vị thế Long / Chốt lời / Cắt lỗ)<br>• **`OPEN_SHORT`** (Mở vị thế Bán khống / Kỳ vọng giảm)<br>• **`CLOSE_SHORT`** (Đóng vị thế Short / Mua trả hàng)<br>• `'RISK_ON'`, `'RISK_OFF'` *(Cho cấp thị trường)*<br>• `'OVERWEIGHT'`, `'UNDERWEIGHT'` *(Cho cấp ngành)* |
| `timeframe` | `VARCHAR(10)` | | Khung thời gian: `'1M'`, `'5M'`, `'15M'`, `'INTRADAY'`, `'DAILY'`, `'SWING'` |
| `signal_time` | `TIMESTAMP` | INDEX | Thời điểm chính xác sinh tín hiệu |
| `target_price` | `DECIMAL(18,2)` | | Giá / Điểm số kích hoạt tín hiệu |
| `stop_loss_price`| `DECIMAL(18,2)` | | Mức cắt lỗ đề xuất |
| `take_profit_price`| `DECIMAL(18,2)` | | Mức chốt lời đề xuất |
| `confidence_score`| `DOUBLE` | | Độ tin cậy của thuật toán (0.0 đến 1.0) |
| **`affected_symbols`**| **`JSON / TEXT`**| | Danh sách các mã hưởng lợi khi tín hiệu ở cấp Ngành (VD: `["HPG", "HSG", "NKG"]`) |
| `metrics_snapshot`| `JSON / TEXT` | | Toàn bộ biến số toán lúc đó (RSI, Vol, Imbalance ratio, Basis phái sinh...) |

---

### 2.7. Bảng `backtest_runs` (Quản lý các đợt chạy thử nghiệm)
* **Mục đích**: Lưu thông số và chỉ số đánh giá của từng lần chạy thuật toán Backtest.

| Tên trường | Kiểu dữ liệu | Khóa | Ý nghĩa |
| :--- | :--- | :---: | :--- |
| `run_id` | `VARCHAR(36)` | **PK** | Định danh UUID của lần chạy |
| `strategy_name` | `VARCHAR(100)` | INDEX | Tên thuật toán thử nghiệm |
| `asset_class` | `VARCHAR(20)` | | Loại tài sản: `'STOCK'`, `'COVERED_WARRANT'`, `'DERIVATIVE_VN30'` |
| `symbols_tested`| `TEXT` | | Danh sách mã được quét (VD: `VN30F1M` hoặc `HPG,SSI,FPT`) |
| `start_date` | `DATE` | | Ngày bắt đầu khoảng backtest |
| `end_date` | `DATE` | | Ngày kết thúc khoảng backtest |
| `initial_capital`| `DECIMAL(18,2)` | | Vốn ban đầu mô phỏng (VD: `100.000.000 VNĐ`) |
| **`final_equity`** | **`DECIMAL(18,2)`** | | Tổng vốn sau khi chạy xong |
| **`total_net_pnl`**| **`DECIMAL(18,2)`** | | Tổng tiền lời/lỗ ròng thực nhận |
| **`win_rate`** | **`DOUBLE`** | | **Tỷ lệ thắng (% Win)** = Số lệnh thắng / Tổng số lệnh |
| **`profit_factor`**| **`DOUBLE`** | | **Profit Factor** = Tổng lãi / Tổng lỗ |
| **`max_drawdown`** | **`DOUBLE`** | | **Max Drawdown (%)** (Mức sụt giảm tài khoản sâu nhất) |
| **`sharpe_ratio`** | **`DOUBLE`** | | Chỉ số Sharpe (Tỷ suất sinh lời trên mỗi đơn vị rủi ro) |
| `total_trades` | `INT` | | Tổng số lệnh giao dịch phát sinh |
| `winning_trades`| `INT` | | Số lệnh thắng |
| `losing_trades` | `INT` | | Số lệnh thua |
| `parameters` | `JSON / TEXT` | | Các tham số cấu hình của thuật toán (VD: `{ "stop_loss_pct": 2.0, "take_profit_pct": 5.0 }`) |
| `created_at` | `TIMESTAMP` | | Thời điểm chạy |

---

### 2.8. Bảng `simulated_trades` (Chi Tiết Vị Thế 2 Chiều LONG / SHORT)
* **Mục đích**: Ghi nhận toàn bộ từng vòng đời lệnh (Round-trip Position) của Bot khi chạy Backtest hoặc Chạy Thật.

| Tên trường | Kiểu dữ liệu | Khóa | Ý nghĩa & Quy tắc tính toán |
| :--- | :--- | :---: | :--- |
| `trade_id` | `VARCHAR(36)` | **PK** | UUID định danh lệnh |
| `run_id` | `VARCHAR(36)` | FK | Liên kết tới lần chạy (`backtest_runs.run_id`) |
| `symbol` | `VARCHAR(15)` | INDEX | Mã tài sản giao dịch (`HPG`, `VN30F1M`, `CACB2603`) |
| **`position_side`** | **`VARCHAR(10)`** | INDEX | **Chiều vị thế: `'LONG'` hoặc `'SHORT'`** |
| `entry_time` | `TIMESTAMP` | INDEX | Thời điểm mở vị thế |
| `entry_price` | `DECIMAL(18,2)` | | Giá khớp mở vị thế (hoặc Điểm số VN30F) |
| `exit_time` | `TIMESTAMP` | | Thời điểm đóng vị thế |
| `exit_price` | `DECIMAL(18,2)` | | Giá khớp đóng vị thế |
| `quantity` | `INT` | | Khối lượng (Số cổ phiếu hoặc Số hợp đồng phái sinh) |
| **`contract_multiplier`**| **`INT`** | | **Hệ số nhân**: `1` (Cổ phiếu, CW), **`100.000`** (Phái sinh VN30) |
| **`gross_pnl`** | **`DECIMAL(18,2)`** | | **Lãi/Lỗ trước phí**:<br>• Nếu LONG: `(exit - entry) * qty * multiplier`<br>• Nếu SHORT: `(entry - exit) * qty * multiplier` |
| `fee_and_tax` | `DECIMAL(18,2)` | | Tổng phí sàn + Thuế thu nhập + Phí quản lý vị thế VSD |
| **`net_pnl`** | **`DECIMAL(18,2)`** | | **Lãi/Lỗ ròng** = `gross_pnl - fee_and_tax` |
| **`return_pct`** | **`DOUBLE`** | | **% Lợi nhuận** trên vốn ký quỹ hoặc giá trị vào lệnh |
| `exit_reason` | `VARCHAR(30)` | | Lý do thoát lệnh: `'TAKE_PROFIT'`, `'STOP_LOSS'`, `'TRAILING_STOP'`, `'TIMEOUT'`, `'ATC'` |
| **`mfe_max_runup`**| **`DECIMAL(18,2)`** | | **Maximum Favorable Excursion (MFE)**: Mức lãi cực đại lệnh từng đạt được trong lúc gồng |
| **`mae_max_drawdown`**|**`DECIMAL(18,2)`**| | **Maximum Adverse Excursion (MAE)**: Mức lỗ sâu nhất lệnh từng bị âm trước khi đóng |

---

## 3. Chi Tiết Tầng Time-Series Big Data (Định Dạng Parquet)

Được lưu trữ dạng file phân vùng (Partitioned Parquet Files):
`data/ticks/{YYYY-MM-DD}/{symbol}.parquet` (hoặc nén chung file ngày).

### 3.1. File Parquet Khớp Lệnh: `market_ticks`
* **Mục đích**: Phục vụ các mô hình vi cấu trúc thị trường (Market Microstructure), Dòng tiền chủ động (Order Flow Cumulative Volume Delta - CVD), Dò quét lệnh lớn (Whale / Block Trades).

| Tên trường | Kiểu dữ liệu Parquet | Ý nghĩa |
| :--- | :--- | :--- |
| `symbol` | `STRING` | Mã CK / CW / Phái sinh |
| `timestamp` | `TIMESTAMP_MICROS` | Thời gian khớp lệnh chính xác đến mili/micro-giây |
| `price` | `DOUBLE` | Giá khớp (hoặc Điểm số phái sinh) |
| `volume` | `INT64` | Khối lượng khớp của lệnh này |
| **`side`** | **`STRING`** | **Chiều lệnh**: `'B'` (Mua chủ động), `'S'` (Bán chủ động), `''` (Khớp định kỳ ATO/ATC) |
| `accumulated_volume`| `INT64` | Khối lượng khớp tích lũy từ đầu phiên |
| `accumulated_value` | `DOUBLE` | Giá trị khớp tích lũy từ đầu phiên (VNĐ) |

---

### 3.2. File Parquet Sổ Lệnh: `orderbook_depth`
* **Mục đích**: Phục vụ mô hình **Mất cân bằng Sổ lệnh (Order Book Imbalance - OBI)** và tính thanh khoản tức thời.

| Tên trường | Kiểu dữ liệu Parquet | Ý nghĩa |
| :--- | :--- | :--- |
| `symbol` | `STRING` | Mã CK / Phái sinh |
| `timestamp` | `TIMESTAMP_MICROS` | Thời điểm lấy mẫu sổ lệnh |
| `bid_price_1`, `bid_vol_1` | `DOUBLE`, `INT64` | Giá Mua 1 tốt nhất & Khối lượng chờ mua |
| `bid_price_2`, `bid_vol_2` | `DOUBLE`, `INT64` | Giá Mua 2 & Khối lượng chờ mua |
| `bid_price_3`, `bid_vol_3` | `DOUBLE`, `INT64` | Giá Mua 3 & Khối lượng chờ mua |
| `ask_price_1`, `ask_vol_1` | `DOUBLE`, `INT64` | Giá Bán 1 tốt nhất & Khối lượng chờ bán |
| `ask_price_2`, `ask_vol_2` | `DOUBLE`, `INT64` | Giá Bán 2 & Khối lượng chờ bán |
| `ask_price_3`, `ask_vol_3` | `DOUBLE`, `INT64` | Giá Bán 3 & Khối lượng chờ bán |
| `total_bid_qty` | `INT64` | Tổng khối lượng dư mua toàn bảng |
| `total_ask_qty` | `INT64` | Tổng khối lượng dư bán toàn bảng |
| `foreign_buy_vol`, `foreign_sell_vol`| `INT64`, `INT64` | Khối ngoại mua/bán tích lũy |

---

### 3.3. File Parquet Nến Phút: `bars_1m`
* **Mục đích**: Chuỗi nến 1 phút gộp cho tất cả Cổ phiếu, CW và Phái sinh VN30F1M để tính RSI, MACD, MA, VWAP tốc độ cao.
* **Các trường**: `symbol`, `timestamp`, `open`, `high`, `low`, `close`, `volume`, `value`, `buy_volume` (Khối lượng Mua CĐ), `sell_volume` (Khối lượng Bán CĐ).

---

## 4. Bảng Ánh Xạ Dữ Liệu Nguồn Vào Database (Data Mapping)

```
[SSI GET /stock/cw/hose] ──► Bảng `covered_warrants`:
                             • firstTradingDate  ──► first_trading_date (Ngày bắt đầu)
                             • maturityDate      ──► maturity_date (Ngày đáo hạn)
                             • lastTradingDate   ──► last_trading_date (Ngày GD cuối)
                             • exerciseRatio     ──► exercise_ratio, ratio_multiplier
                             • exercisePrice     ──► exercise_price
                             • issuerName        ──► issuer_name
                             • underlyingSymbol  ──► underlying_symbol

[VPS GET /getlistall]   ──► Bảng `symbols`:
                             • stock_code        ──► symbol
                             • post_to           ──► exchange
                             • type              ──► asset_type ('S', 'W', 'D')

[VPS /getliststocktrade]──► File Parquet `market_ticks`:
                             • time              ──► timestamp
                             • lastPrice         ──► price
                             • lastVol           ──► volume
                             • side              ──► side ('B' / 'S')

[VPS /getliststockdata] ──► File Parquet `orderbook_depth`:
                             • g1 -> g6          ──► bid/ask prices & volumes (3 bước giá)
                             • c, f, r           ──► ceiling, floor, ref_price
                             • fBVol, fSVolume   ──► foreign flows

[DNSE /ohlcs/derivative]──► File Parquet `bars_1m` (Phái sinh VN30F1M):
                             • t, o, h, l, c, v  ──► timestamp, open, high, low, close, volume
```
