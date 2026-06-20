import requests
import time
import math

def get_historical_data(symbol, days=90):
    t = int(time.time())
    # Lấy dư ra ngày lịch để đảm bảo có đủ 90 phiên giao dịch
    from_t = t - (days + 60) * 24 * 3600
    
    url = f'https://services.entrade.com.vn/chart-api/v2/ohlcs/stock?from={from_t}&to={t}&symbol={symbol}&resolution=1D'
    headers = {'User-Agent': 'Mozilla/5.0'}
    
    print(f"Đang tải dữ liệu cho {symbol} từ DNSE API...")
    r = requests.get(url, headers=headers)
    
    if r.status_code == 200:
        data = r.json()
        closes = data.get('c', [])
        timestamps = data.get('t', [])
        volumes = data.get('v', [])
        
        if not closes:
            print("Không có dữ liệu.")
            return [], []
            
        # Chỉ lấy đúng N phiên giao dịch gần nhất
        if len(closes) > days:
            closes = closes[-days:]
            timestamps = timestamps[-days:]
            volumes = volumes[-days:]
            
        print(f"Đã tải thành công {len(closes)} phiên giao dịch gần nhất.")
        return closes, volumes
    else:
        print(f"Lỗi khi gọi API: {r.status_code}")
        return [], []

def calculate_historical_volatility(closes):
    if len(closes) < 2:
        return 0
        
    returns = []
    # Tính tỷ suất sinh lời hàng ngày (Log return)
    for i in range(1, len(closes)):
        returns.append(math.log(closes[i] / closes[i-1]))
        
    # Tính độ lệch chuẩn
    mean_return = sum(returns) / len(returns)
    variance = sum((r - mean_return)**2 for r in returns) / (len(returns) - 1)
    daily_vol = math.sqrt(variance)
    
    # Nhân hóa năm (252 phiên giao dịch/năm)
    annual_vol = daily_vol * math.sqrt(252)
    return annual_vol

if __name__ == "__main__":
    symbol = "FPT"
    closes, volumes = get_historical_data(symbol, days=90)
    
    if closes:
        print(f"\n--- Dữ liệu lịch sử khớp lệnh {symbol} (5 phiên gần nhất) ---")
        for i in range(1, 6):
            print(f"Phiên {-i}: Giá đóng cửa = {closes[-i]} | Khối lượng = {volumes[-i]:,}")
            
        hv = calculate_historical_volatility(closes)
        print(f"\n=> Độ biến động lịch sử (HV) 90 ngày của {symbol} là: {hv*100:.2f}%")
        print("=> Con số này đã sẵn sàng để đưa vào công thức Black-Scholes định giá Chứng Quyền!")
