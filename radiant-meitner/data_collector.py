import sqlite3
import time
import requests
import threading
import os
import concurrent.futures
from datetime import datetime
from sync_to_hf import sync_database_to_hf

class DataCollector:
    def __init__(self, db_path='data/intraday_data.db', stocks_file='top_300_stocks.txt'):
        self.db_path = db_path
        self.stocks_file = stocks_file
        self.running = False
        self.db_lock = threading.Lock()
        
        # Ensure data directory exists
        os.makedirs(os.path.dirname(self.db_path), exist_ok=True)
        self.init_db()

    def init_db(self):
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        # Create table with UNIQUE constraint on symbol and timestamp to prevent duplicates
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS intraday (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                symbol TEXT,
                timestamp INTEGER,
                open REAL,
                high REAL,
                low REAL,
                close REAL,
                volume INTEGER,
                UNIQUE(symbol, timestamp)
            )
        ''')
        conn.commit()
        conn.close()

    def get_stocks(self):
        if not os.path.exists(self.stocks_file):
            return []
        with open(self.stocks_file, 'r', encoding='utf-8') as f:
            return [line.strip() for line in f if line.strip()]

    def fetch_data_for_symbol(self, symbol):
        t = int(time.time())
        # Lấy dữ liệu 5 ngày gần nhất (đảm bảo lấy được cả ngày thứ 6 nếu mở app vào cuối tuần/lễ)
        from_t = t - 5 * 24 * 3600 
        url = f'https://services.entrade.com.vn/chart-api/v2/ohlcs/stock?from={from_t}&to={t}&symbol={symbol}&resolution=1'
        headers = {'User-Agent': 'Mozilla/5.0'}
        
        try:
            r = requests.get(url, headers=headers, timeout=10)
            if r.status_code == 200:
                data = r.json()
                timestamps = data.get('t', [])
                opens = data.get('o', [])
                highs = data.get('h', [])
                lows = data.get('l', [])
                closes = data.get('c', [])
                volumes = data.get('v', [])
                
                if timestamps:
                    # Package data for insertion
                    records = []
                    for i in range(len(timestamps)):
                        records.append((
                            symbol, timestamps[i], opens[i], highs[i], lows[i], closes[i], volumes[i]
                        ))
                    return records
        except Exception as e:
            print(f"Error fetching data for {symbol}: {e}")
        return []

    def save_records(self, records):
        if not records:
            return
        with self.db_lock:
            conn = sqlite3.connect(self.db_path)
            cursor = conn.cursor()
            # INSERT OR IGNORE will skip rows where (symbol, timestamp) already exists
            cursor.executemany('''
                INSERT OR IGNORE INTO intraday (symbol, timestamp, open, high, low, close, volume)
                VALUES (?, ?, ?, ?, ?, ?, ?)
            ''', records)
            conn.commit()
            conn.close()

    def collection_task(self):
        print("Starting intraday data collection (multi-threaded)...")
        stocks = self.get_stocks()
        total = len(stocks)
        
        def process_symbol(symbol):
            if not self.running:
                return 0, symbol
            try:
                records = self.fetch_data_for_symbol(symbol)
                if records:
                    self.save_records(records)
                    return len(records), symbol
            except Exception as e:
                print(f"Error processing {symbol}: {e}")
            return 0, symbol

        max_workers = 10
        completed_count = 0
        
        with concurrent.futures.ThreadPoolExecutor(max_workers=max_workers) as executor:
            futures = {executor.submit(process_symbol, symbol): symbol for symbol in stocks}
            
            for future in concurrent.futures.as_completed(futures):
                if not self.running:
                    executor.shutdown(wait=False, cancel_futures=True)
                    break
                
                try:
                    count, symbol = future.result()
                    completed_count += 1
                    if count > 0:
                        print(f"[{completed_count}/{total}] Saved {count} records for {symbol}")
                    else:
                        print(f"[{completed_count}/{total}] No data for {symbol}")
                except Exception as e:
                    completed_count += 1
                    symbol = futures[future]
                    print(f"[{completed_count}/{total}] Exception for {symbol}: {e}")

        print("Intraday data collection finished!")
        
        # Đồng bộ lên Hugging Face nếu có cấu hình
        sync_database_to_hf()

    def start(self):
        if not self.running:
            self.running = True
            thread = threading.Thread(target=self.collection_task, daemon=True)
            thread.start()

    def stop(self):
        self.running = False
