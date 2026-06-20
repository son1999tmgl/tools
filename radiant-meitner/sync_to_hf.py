import os
import sqlite3
import pandas as pd
from huggingface_hub import HfApi
from dotenv import load_dotenv

# Nạp biến môi trường từ file .env
load_dotenv()

def sync_database_to_hf():
    """
    Kết nối tới SQLite, xuất dữ liệu ra file Parquet,
    và tự động upload lên Hugging Face Dataset.
    """
    token = os.getenv("HF_TOKEN")
    repo_id = os.getenv("HF_REPO_ID")
    db_path = 'data/intraday_data.db'
    parquet_path = 'data/intraday_data.parquet'

    if not token or not repo_id:
        print("Skip HF sync: HF_TOKEN or HF_REPO_ID not configured in .env")
        return

    if not os.path.exists(db_path):
        print(f"Skip HF sync: Database not found at {db_path}")
        return

    print("Starting sync to Hugging Face...")
    try:
        # 1. Đọc dữ liệu từ SQLite
        conn = sqlite3.connect(db_path)
        # Sắp xếp theo mã chứng khoán và thời gian để data gọn gàng
        query = "SELECT * FROM intraday ORDER BY symbol ASC, timestamp ASC"
        df = pd.read_sql_query(query, conn)
        conn.close()

        if df.empty:
            print("Database empty, nothing to sync.")
            return

        # Thêm cột date (dd/mm/yyyy) và time (HH:MM) theo giờ Việt Nam để dễ nhìn và lọc
        df_datetime = pd.to_datetime(df['timestamp'], unit='s').dt.tz_localize('UTC').dt.tz_convert('Asia/Ho_Chi_Minh')
        df['date'] = df_datetime.dt.strftime('%d/%m/%Y')
        df['time'] = df_datetime.dt.strftime('%H:%M')

        # 2. Lưu thành file Parquet
        # Parquet nén dữ liệu tốt hơn CSV rất nhiều và cực kỳ tối ưu cho Pandas/HuggingFace
        df.to_parquet(parquet_path, engine='pyarrow', index=False)
        print(f"Exported Parquet successfully: {parquet_path} ({os.path.getsize(parquet_path)/1024:.2f} KB)")

        # 3. Upload lên Hugging Face
        api = HfApi(token=token)
        
        # Tạo repo nếu chưa tồn tại (chỉ chạy lần đầu)
        try:
            api.create_repo(repo_id=repo_id, repo_type="dataset", private=True, exist_ok=True)
        except Exception as e:
            # Nếu báo lỗi không có quyền tạo repo thì bỏ qua vì repo có thể đã tồn tại
            pass

        # Upload file
        api.upload_file(
            path_or_fileobj=parquet_path,
            path_in_repo="intraday_data.parquet",
            repo_id=repo_id,
            repo_type="dataset"
        )
        print(f"Sync success to HF Repo: {repo_id}")

    except Exception as e:
        print(f"Error syncing to Hugging Face: {e}")

if __name__ == "__main__":
    # Test chạy thử độc lập
    sync_database_to_hf()
