import tkinter as tk
from tkinter import ttk, messagebox
import threading
import time
import requests
from datetime import datetime
from tkcalendar import DateEntry

from data_collector import DataCollector
from valuation_models import apply_model_1, apply_model_2, apply_model_3

class CWTrackerApp(tk.Tk):
    def __init__(self):
        super().__init__()

        self.title("Ứng Dụng Theo Dõi Chứng Quyền (Covered Warrants)")
        self.geometry("1100x600")
        self.minsize(800, 400)

        # Style cấu hình
        style = ttk.Style()
        style.theme_use('clam')
        style.configure("Treeview.Heading", font=('Arial', 10, 'bold'))
        style.configure("Treeview", font=('Arial', 10), rowheight=25)

        # Dữ liệu & Trạng thái
        self.raw_data = [] # Stores dictionaries instead of tuples now
        self.sort_col = "percent_to_breakeven"
        self.sort_reverse = False
        self.static_cw_info = {} # Lưu thông tin chứng quyền
        self.is_fetching = False
        self.current_model = tk.IntVar(value=0) # 0: None, 1: Model 1, 2: Model 2, 3: Model 3

        # Cấu hình UI chính
        self.top_frame = ttk.Frame(self)
        self.top_frame.pack(side=tk.TOP, fill=tk.X, padx=10, pady=5)

        self.lbl_status = ttk.Label(self.top_frame, text="Đang khởi tạo...", font=('Arial', 11, 'bold'), foreground="blue")
        self.lbl_status.pack(side=tk.LEFT)

        self.btn_refresh = ttk.Button(self.top_frame, text="Làm mới (Refresh)", command=self.manual_refresh)
        self.btn_refresh.pack(side=tk.RIGHT)

        self.lbl_time = ttk.Label(self.top_frame, text="", font=('Arial', 10))
        self.lbl_time.pack(side=tk.RIGHT, padx=15)

        # Cấu hình UI điều khiển (Lọc, Hiện Cột)
        self.control_frame = ttk.Frame(self)
        self.control_frame.pack(side=tk.TOP, fill=tk.X, padx=10, pady=5)
        
        ttk.Label(self.control_frame, text="Lọc Mã Cơ Sở:", font=('Arial', 10, 'bold')).pack(side=tk.LEFT, padx=(0, 5))
        self.filter_var = tk.StringVar(value="Tất cả")
        self.cb_filter = ttk.Combobox(self.control_frame, textvariable=self.filter_var, state="readonly", width=10)
        self.cb_filter['values'] = ("Tất cả",)
        self.cb_filter.pack(side=tk.LEFT)
        self.cb_filter.bind("<<ComboboxSelected>>", lambda e: self.render_table())
        ttk.Label(self.control_frame, text="Đáo hạn >=", font=('Arial', 10, 'bold')).pack(side=tk.LEFT, padx=(20, 5))
        self.maturity_filter_var = tk.StringVar(value="")
        self.entry_maturity = DateEntry(self.control_frame, textvariable=self.maturity_filter_var, width=12, 
                                        date_pattern='dd/mm/yyyy', background='darkblue', foreground='white', borderwidth=2)
        self.entry_maturity.delete(0, 'end')
        self.entry_maturity.pack(side=tk.LEFT)
        self.entry_maturity.bind("<<DateEntrySelected>>", lambda e: self.render_table())
        self.entry_maturity.bind("<Return>", lambda e: self.render_table())
        
        self.btn_clear_date = ttk.Button(self.control_frame, text="X", width=2, command=self.clear_date_filter)
        self.btn_clear_date.pack(side=tk.LEFT, padx=(2, 0))

        # Khung chọn Mô Hình
        self.model_frame = ttk.LabelFrame(self.control_frame, text="Chọn Mô Hình Lọc")
        self.model_frame.pack(side=tk.LEFT, padx=(20, 0))
        
        ttk.Radiobutton(self.model_frame, text="Mặc định", variable=self.current_model, value=0, command=self.render_table).pack(side=tk.LEFT, padx=5)
        ttk.Radiobutton(self.model_frame, text="Mô hình 1: Lọc An Toàn", variable=self.current_model, value=1, command=self.render_table).pack(side=tk.LEFT, padx=5)
        ttk.Radiobutton(self.model_frame, text="Mô hình 2: Chấm Điểm", variable=self.current_model, value=2, command=self.render_table).pack(side=tk.LEFT, padx=5)
        ttk.Radiobutton(self.model_frame, text="Mô hình 3: Black-Scholes", variable=self.current_model, value=3, command=self.render_table).pack(side=tk.LEFT, padx=5)

        self.btn_cols = ttk.Menubutton(self.control_frame, text="Hiển thị Cột")
        self.btn_cols.pack(side=tk.RIGHT)
        self.col_menu = tk.Menu(self.btn_cols, tearoff=0)
        self.btn_cols["menu"] = self.col_menu
        
        self.col_vars = {}

        # Bảng dữ liệu (Treeview)
        self.columns = ("cw_symbol", "underlying", "maturity", "ratio", "strike_price", 
                   "cw_price", "best_ask_price", "best_ask_vol", "underlying_price", "breakeven", "percent_to_breakeven", "moneyness", "score", "theoretical_price", "undervalued_pct")
        self.col_names = {
            "cw_symbol": "Mã CW", "underlying": "Mã Cơ Sở", "maturity": "Ngày Đáo Hạn", 
            "ratio": "Tỷ lệ (1:X)", "strike_price": "Giá Thực Hiện", "cw_price": "Giá CW Hiện Tại", 
            "best_ask_price": "Giá Bán 1", "best_ask_vol": "KL Bán 1",
            "underlying_price": "Giá Cổ Phiếu", "breakeven": "Điểm Hòa Vốn", "percent_to_breakeven": "% Tăng Hòa Vốn",
            "moneyness": "Hệ số An Toàn (%)", "score": "Điểm Đòn Bẩy", "theoretical_price": "Giá Lý Thuyết (BS)", "undervalued_pct": "Mức Rẻ (%)"
        }
        
        self.tree = ttk.Treeview(self, columns=self.columns, show="headings")
        
        # Thiết lập cột và Menu chọn cột
        for col in self.columns:
            self.tree.heading(col, text=self.col_names[col], command=lambda c=col: self.treeview_sort_column(c))
            anchor = tk.CENTER if col in ("cw_symbol", "underlying", "maturity", "ratio") else tk.E
            width = 80 if col in ("cw_symbol", "underlying", "ratio") else (100 if col == "maturity" else 120)
            self.tree.column(col, width=width, anchor=anchor)
            
            # Khởi tạo Checkbutton cho Menu
            var = tk.BooleanVar(value=True)
            self.col_vars[col] = var
            self.col_menu.add_checkbutton(label=self.col_names[col], variable=var, command=self.update_display_columns)

        # Scrollbar
        scrollbar = ttk.Scrollbar(self, orient=tk.VERTICAL, command=self.tree.yview)
        self.tree.configure(yscroll=scrollbar.set)
        
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        self.tree.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)

        # Định dạng màu sắc
        self.tree.tag_configure("ceil_odd", foreground="#ff00ff", background="#f9f9f9")
        self.tree.tag_configure("ceil_even", foreground="#ff00ff", background="#ffffff")
        self.tree.tag_configure("floor_odd", foreground="#00ccff", background="#f9f9f9")
        self.tree.tag_configure("floor_even", foreground="#00ccff", background="#ffffff")
        self.tree.tag_configure("up_odd", foreground="#008000", background="#f9f9f9")
        self.tree.tag_configure("up_even", foreground="#008000", background="#ffffff")
        self.tree.tag_configure("down_odd", foreground="#cc0000", background="#f9f9f9")
        self.tree.tag_configure("down_even", foreground="#cc0000", background="#ffffff")
        self.tree.tag_configure("ref_odd", foreground="#b8860b", background="#f9f9f9")
        self.tree.tag_configure("ref_even", foreground="#b8860b", background="#ffffff")
        self.tree.tag_configure("normal_odd", foreground="black", background="#f9f9f9")
        self.tree.tag_configure("normal_even", foreground="black", background="#ffffff")

        # Khởi chạy Data Collector ngầm
        self.data_collector = DataCollector()
        self.data_collector.start()

        # Tự động tải dữ liệu lần đầu
        self.after(100, self.start_fetch_thread)

        # Vòng lặp refresh 2 phút
        self.refresh_interval_ms = 120000 
        self.after(self.refresh_interval_ms, self.auto_refresh)

    def manual_refresh(self):
        self.start_fetch_thread()

    def auto_refresh(self):
        self.start_fetch_thread()
        self.after(self.refresh_interval_ms, self.auto_refresh)

    def clear_date_filter(self):
        self.entry_maturity.delete(0, 'end')
        self.render_table()

    def start_fetch_thread(self):
        if self.is_fetching: return
        self.is_fetching = True
        self.btn_refresh.config(state=tk.DISABLED)
        self.lbl_status.config(text="Đang lấy dữ liệu...", foreground="blue")
        threading.Thread(target=self.fetch_data, daemon=True).start()

    def fetch_data(self):
        try:
            headers = {
                'User-Agent': 'Mozilla/5.0'
            }
            # Danh sách tĩnh các CW
            cw_symbols_str = "CACB2510,CACB2511,CACB2514,CACB2515,CACB2516,CACB2517,CACB2601,CACB2602,CACB2603,CACB2604,CACB2605,CACB2606,CACB2607,CACB2608,CDGC2601,CFPT2517,CFPT2518,CFPT2520,CFPT2521,CFPT2524,CFPT2526,CFPT2528,CFPT2529,CFPT2532,CFPT2533,CFPT2601,CFPT2602,CFPT2603,CFPT2604,CFPT2605,CFPT2606,CFPT2607,CFPT2608,CFPT2609,CFPT2610,CFPT2611,CFPT2612,CFPT2613,CFPT2614,CHDB2508,CHDB2509,CHDB2601,CHDB2602,CHDB2603,CHDB2604,CHDB2605,CHPG2523,CHPG2524,CHPG2525,CHPG2530,CHPG2531,CHPG2532,CHPG2534,CHPG2536,CHPG2538,CHPG2539,CHPG2540,CHPG2541,CHPG2601,CHPG2602,CHPG2603,CHPG2604,CHPG2605,CHPG2606,CHPG2607,CHPG2608,CHPG2609,CHPG2610,CHPG2612,CHPG2613,CHPG2614,CHPG2615,CLPB2503,CLPB2509,CLPB2602,CMBB2516,CMBB2517,CMBB2520,CMBB2521,CMBB2522,CMBB2523,CMBB2601,CMBB2602,CMBB2603,CMBB2604,CMBB2605,CMBB2606,CMBB2607,CMBB2608,CMBB2609,CMBB2610,CMSN2516,CMSN2520,CMSN2522,CMSN2601,CMSN2603,CMSN2604,CMSN2605,CMSN2606,CMSN2608,CMSN2609,CMSN2610,CMWG2515,CMWG2516,CMWG2518,CMWG2522,CMWG2524,CMWG2525,CMWG2526,CMWG2527,CMWG2601,CMWG2602,CMWG2603,CMWG2604,CMWG2605,CMWG2607,CMWG2608,CMWG2609,CMWG2610,CMWG2611,CSHB2514,CSHB2601,CSHB2603,CSHB2605,CSHB2606,CSHB2607,CSHB2608,CSSB2509,CSSB2602,CSSB2603,CSTB2519,CSTB2521,CSTB2524,CSTB2525,CSTB2527,CSTB2530,CSTB2532,CSTB2533,CSTB2536,CSTB2537,CSTB2601,CSTB2602,CSTB2603,CSTB2604,CSTB2605,CSTB2606,CSTB2607,CSTB2608,CSTB2609,CTCB2512,CTCB2517,CTCB2520,CTCB2521,CTCB2522,CTCB2523,CTCB2601,CTCB2602,CTCB2603,CTCB2604,CTCB2605,CTCB2606,CTCB2607,CTPB2506,CTPB2510,CTPB2602,CTPB2603,CTPB2604,CTPB2605,CTPB2606,CVHM2516,CVHM2520,CVHM2522,CVHM2523,CVHM2524,CVHM2601,CVHM2602,CVHM2603,CVHM2604,CVHM2605,CVHM2606,CVHM2607,CVHM2608,CVHM2609,CVIB2508,CVIB2513,CVIB2601,CVIB2603,CVIB2604,CVIB2605,CVIC2514,CVIC2515,CVIC2516,CVIC2601,CVJC2506,CVJC2601,CVNM2515,CVNM2520,CVNM2521,CVNM2523,CVNM2601,CVNM2602,CVNM2603,CVNM2604,CVNM2605,CVNM2606,CVNM2607,CVPB2516,CVPB2521,CVPB2522,CVPB2524,CVPB2526,CVPB2528,CVPB2531,CVPB2532,CVPB2601,CVPB2602,CVPB2603,CVPB2604,CVPB2605,CVPB2606,CVPB2607,CVPB2608,CVPB2609,CVPB2610,CVRE2516,CVRE2520,CVRE2521,CVRE2524,CVRE2525,CVRE2526,CVRE2601,CVRE2602,CVRE2603"
            cw_series = cw_symbols_str.split(",")
            all_underlyings = list(set([sym[1:4] for sym in cw_series if len(sym) >= 4]))
            
            # Nếu chưa có thông tin, khởi tạo mặc định
            if not self.static_cw_info:
                for sym in cw_series:
                    self.static_cw_info[sym] = {
                        'underlying': sym[1:4] if len(sym) >= 4 else "",
                        'strike_price': 0,
                        'ratio': 1.0,
                        'maturity': 'N/A'
                    }

            all_symbols = cw_series + all_underlyings
            prices = {}
            best_asks = {}
            refs = {}
            ceils = {}
            floors = {}
            
            # Gửi request lên VPS API
            chunk_size = 50
            for i in range(0, len(all_symbols), chunk_size):
                chunk = all_symbols[i:i+chunk_size]
                chunk_str = ",".join(chunk)
                url = f"https://bgapidatafeed.vps.com.vn/getliststockdata/{chunk_str}"
                
                try:
                    r = requests.get(url, headers=headers, timeout=5)
                    if r.status_code == 200:
                        data = r.json()
                        for item in data:
                            sym = item.get('sym')
                            last_price = float(item.get('lastPrice', 0))
                            prices[sym] = last_price * 1000 # CW price ở VPS thường là 1.25 -> 1250đ
                            refs[sym] = float(item.get('r', 0)) * 1000
                            ceils[sym] = float(item.get('c', 0)) * 1000
                            floors[sym] = float(item.get('f', 0)) * 1000
                            
                            best_ask_price = 0
                            best_ask_vol = 0
                            g4 = item.get('g4', '')
                            if g4:
                                parts = g4.split('|')
                                if len(parts) >= 2:
                                    try:
                                        best_ask_price = float(parts[0]) * 1000
                                        best_ask_vol = int(parts[1]) * 10
                                    except:
                                        pass
                            best_asks[sym] = (best_ask_price, best_ask_vol)
                            
                            # Cập nhật thông tin tỷ lệ, giá thực hiện, ngày đáo hạn nếu là CW
                            if sym in self.static_cw_info:
                                strike = item.get('CWExcersisePrice', '')
                                ratio_str = str(item.get('CWExerciseRatio', '1:1'))
                                maturity_raw = str(item.get('CWMaturityDate', ''))
                                
                                try:
                                    if strike:
                                        self.static_cw_info[sym]['strike_price'] = float(strike) * 1000
                                except: pass
                                
                                try:
                                    if ':' in ratio_str:
                                        p1, p2 = ratio_str.split(':')
                                        self.static_cw_info[sym]['ratio'] = float(p1) / float(p2)
                                    else:
                                        self.static_cw_info[sym]['ratio'] = float(ratio_str)
                                except: pass
                                
                                if maturity_raw and len(maturity_raw) == 8:
                                    self.static_cw_info[sym]['maturity'] = f"{maturity_raw[:4]}-{maturity_raw[4:6]}-{maturity_raw[6:]}"
                except:
                    pass

            # Prepare rows
            rows_data = []
            for cw_sym, info in self.static_cw_info.items():
                underlying = info['underlying']
                cw_price = prices.get(cw_sym, 0)
                underlying_price = prices.get(underlying, 0)
                best_ask = best_asks.get(cw_sym, (0, 0))
                best_ask_price = best_ask[0]
                best_ask_vol = best_ask[1]
                strike_price = info['strike_price']
                ratio = info['ratio']

                # Bỏ qua CW hết hạn hoặc không có giá giao dịch
                if cw_price == 0:
                    continue

                if strike_price == 0:
                    breakeven = 0
                    percent_to_breakeven = 0
                    moneyness = 0
                else:
                    breakeven = strike_price + (cw_price * ratio)
                    if underlying_price > 0:
                        percent_to_breakeven = ((breakeven - underlying_price) / underlying_price) * 100
                        moneyness = ((underlying_price - strike_price) / strike_price) * 100
                    else:
                        percent_to_breakeven = 0
                        moneyness = 0

                cw_ref = refs.get(cw_sym, 0)
                cw_ceil = ceils.get(cw_sym, 0)
                cw_floor = floors.get(cw_sym, 0)

                days_to_maturity = 0
                if info['maturity'] != 'N/A':
                    try:
                        days_to_maturity = (datetime.strptime(info['maturity'], "%Y-%m-%d") - datetime.now()).days
                    except: pass
                    
                rows_data.append({
                    'cw_symbol': cw_sym,
                    'underlying': underlying,
                    'maturity': info['maturity'],
                    'ratio': ratio,
                    'strike_price': strike_price,
                    'cw_price': cw_price,
                    'best_ask_price': best_ask_price,
                    'best_ask_vol': best_ask_vol,
                    'underlying_price': underlying_price,
                    'breakeven': breakeven,
                    'percent_to_breakeven': percent_to_breakeven,
                    'moneyness': moneyness,
                    'cw_ref': cw_ref,
                    'cw_ceil': cw_ceil,
                    'cw_floor': cw_floor,
                    'days_to_maturity': days_to_maturity,
                    'premium': percent_to_breakeven,
                    'gearing': (underlying_price / cw_price) * ratio if cw_price > 0 else 0,
                    'score': "N/A",
                    'theoretical_price': "N/A",
                    'undervalued_pct': "N/A"
                })

            self.raw_data = rows_data
            self.update_ui_success()

        except Exception as e:
            err_str = str(e).encode('ascii', 'ignore').decode('ascii')
            self.update_ui_error(f"Lỗi mạng: {err_str}")
        finally:
            self.is_fetching = False

    def update_ui_success(self):
        def _update():
            # Update combobox filter values
            underlyings = sorted(list(set([row['underlying'] for row in self.raw_data if row['underlying']])))
            self.cb_filter['values'] = ["Tất cả"] + underlyings
            
            # Update status
            msg = f"Hoàn thành cập nhật. Tổng số CW: {len(self.raw_data)}"
            self.lbl_status.config(text=msg, foreground="green")
            self.lbl_time.config(text=f"Cập nhật lúc: {datetime.now().strftime('%H:%M:%S')}")
            self.btn_refresh.config(state=tk.NORMAL)
            
            # Trigger render table
            self.render_table()

        self.after(0, _update)

    def render_table(self):
        # 1. Lọc dữ liệu
        selected_underlying = self.filter_var.get()
        maturity_filter_str = self.maturity_filter_var.get().strip()
        
        filtered_data = []
        for row in self.raw_data:
            if selected_underlying != "Tất cả" and row['underlying'] != selected_underlying:
                continue
                
            if maturity_filter_str:
                parts = maturity_filter_str.split('/')
                filter_ymd = "-".join(reversed(parts))
                
                row_maturity = str(row['maturity'])
                if row_maturity != "N/A" and row_maturity < filter_ymd:
                    continue
                    
            filtered_data.append(dict(row)) # create a copy

        # Áp dụng mô hình lọc
        selected_model = self.current_model.get()
        if selected_model == 1:
            filtered_data = apply_model_1(filtered_data)
            # Default sort for Model 1: percent_to_breakeven ASC
            if self.sort_col == "percent_to_breakeven":
                self.sort_reverse = False
        elif selected_model == 2:
            filtered_data = apply_model_2(filtered_data)
            # Default sort for Model 2: score DESC
            self.sort_col = "score"
            self.sort_reverse = True
        elif selected_model == 3:
            filtered_data = apply_model_3(filtered_data)
            # Default sort for Model 3: undervalued_pct DESC
            self.sort_col = "undervalued_pct"
            self.sort_reverse = True

        # 2. Sắp xếp dữ liệu
        def sort_key(row):
            val = row.get(self.sort_col, "N/A")
            if val == "N/A":
                return (1, 0) if not self.sort_reverse else (-1, 0)
            if isinstance(val, (int, float)):
                return (0, float(val))
            return (0, str(val))
            
        filtered_data.sort(key=sort_key, reverse=self.sort_reverse)

        # Cập nhật hiển thị cột tùy theo mô hình
        if selected_model == 2:
            self.col_vars["score"].set(True)
            self.col_vars["theoretical_price"].set(False)
            self.col_vars["undervalued_pct"].set(False)
        elif selected_model == 3:
            self.col_vars["score"].set(False)
            self.col_vars["theoretical_price"].set(True)
            self.col_vars["undervalued_pct"].set(True)
        else:
            self.col_vars["score"].set(False)
            self.col_vars["theoretical_price"].set(False)
            self.col_vars["undervalued_pct"].set(False)
        self.update_display_columns()

        # 3. Cập nhật Treeview
        for item in self.tree.get_children():
            self.tree.delete(item)

        for index, row in enumerate(filtered_data):
            # Chuyển đổi định dạng hiển thị ngày đáo hạn sang dd/mm/yyyy
            maturity_display = row['maturity']
            if maturity_display != "N/A":
                m_parts = maturity_display.split('-')
                if len(m_parts) == 3:
                    maturity_display = f"{m_parts[2]}/{m_parts[1]}/{m_parts[0]}"
                    
            fmt_row = (
                row['cw_symbol'],
                row['underlying'],
                maturity_display,
                f"{row['ratio']:g}" if row['strike_price'] > 0 else "N/A",
                f"{row['strike_price']:,.0f}" if row['strike_price'] > 0 else "N/A",
                f"{row['cw_price']:,.0f}" if row['cw_price'] > 0 else "N/A",
                f"{row['best_ask_price']:,.0f}" if row['best_ask_price'] > 0 else "N/A",
                f"{row['best_ask_vol']:,.0f}" if row['best_ask_vol'] > 0 else "N/A",
                f"{row['underlying_price']:,.0f}" if row['underlying_price'] > 0 else "N/A",
                f"{row['breakeven']:,.0f}" if row['breakeven'] > 0 else "N/A",
                f"{row['percent_to_breakeven']:.2f}%" if row['strike_price'] > 0 else "N/A",
                f"{row['moneyness']:.2f}%" if row['strike_price'] > 0 else "N/A",
                f"{row['score']:.2f}" if isinstance(row['score'], (int, float)) else "N/A",
                f"{row['theoretical_price']:,.0f}" if isinstance(row['theoretical_price'], (int, float)) else "N/A",
                f"{row['undervalued_pct']:.2f}%" if isinstance(row['undervalued_pct'], (int, float)) else "N/A",
            )
            
            # Xác định màu sắc theo giá tham chiếu
            cw_price = row['cw_price']
            cw_ref = row['cw_ref']
            cw_ceil = row['cw_ceil']
            cw_floor = row['cw_floor']
            state = "normal"
            
            if isinstance(cw_price, (int, float)) and cw_price > 0 and cw_ref > 0:
                if cw_price >= cw_ceil and cw_ceil > 0:
                    state = "ceil"
                elif cw_price <= cw_floor and cw_floor > 0:
                    state = "floor"
                elif cw_price > cw_ref:
                    state = "up"
                elif cw_price < cw_ref:
                    state = "down"
                else:
                    state = "ref"
            
            stripe = "even" if index % 2 == 0 else "odd"
            tag = f"{state}_{stripe}"
            
            self.tree.insert("", tk.END, values=fmt_row, tags=(tag,))
            
        # 4. Đánh dấu cột đang được sắp xếp
        for col in self.columns:
            text = self.col_names[col]
            if col == self.sort_col:
                text += " ▼" if self.sort_reverse else " ▲"
            self.tree.heading(col, text=text)

    def treeview_sort_column(self, col):
        if self.sort_col == col:
            self.sort_reverse = not self.sort_reverse
        else:
            self.sort_reverse = False
            self.sort_col = col
        self.render_table()

    def update_display_columns(self):
        display_cols = [col for col in self.columns if self.col_vars[col].get()]
        self.tree["displaycolumns"] = display_cols

    def update_ui_error(self, err_msg):
        def _update():
            self.lbl_status.config(text="Lỗi", foreground="red")
            self.btn_refresh.config(state=tk.NORMAL)
            messagebox.showwarning("Cảnh báo", err_msg)
        self.after(0, _update)

if __name__ == "__main__":
    app = CWTrackerApp()
    app.mainloop()
