import math
import time
import requests
from datetime import datetime
from scipy.stats import norm

def compute_historical_volatility(symbol, days=90):
    """Lấy dữ liệu giá đóng cửa 90 ngày của mã cơ sở từ DNSE và tính Volatility."""
    t = int(time.time())
    from_t = t - (days + 60) * 24 * 3600
    url = f'https://services.entrade.com.vn/chart-api/v2/ohlcs/stock?from={from_t}&to={t}&symbol={symbol}&resolution=1D'
    headers = {'User-Agent': 'Mozilla/5.0'}
    
    try:
        r = requests.get(url, headers=headers, timeout=5)
        if r.status_code == 200:
            data = r.json()
            closes = data.get('c', [])
            if len(closes) > days:
                closes = closes[-days:]
            
            if len(closes) < 2:
                return 0.3 # Fallback volatility 30%
                
            returns = [math.log(closes[i] / closes[i-1]) for i in range(1, len(closes))]
            mean_return = sum(returns) / len(returns)
            variance = sum((r - mean_return)**2 for r in returns) / (len(returns) - 1)
            daily_vol = math.sqrt(variance)
            annual_vol = daily_vol * math.sqrt(252)
            return annual_vol
    except Exception as e:
        print(f"Error fetching HV for {symbol}: {e}")
    return 0.3 # Default 30% if API fails

# Cache for Volatility to avoid fetching the same underlying stock multiple times
VOLATILITY_CACHE = {}

def get_volatility(symbol):
    if symbol not in VOLATILITY_CACHE:
        VOLATILITY_CACHE[symbol] = compute_historical_volatility(symbol)
    return VOLATILITY_CACHE[symbol]

def black_scholes_call(S, X, T, r, sigma):
    """Tính giá Call Option theo Black-Scholes."""
    if T <= 0 or sigma <= 0:
        return max(0, S - X)
    d1 = (math.log(S / X) + (r + (sigma**2) / 2) * T) / (sigma * math.sqrt(T))
    d2 = d1 - sigma * math.sqrt(T)
    return S * norm.cdf(d1) - X * math.exp(-r * T) * norm.cdf(d2)

def calculate_theoretical_price(cw_data):
    """Tính giá lý thuyết cho 1 CW."""
    try:
        underlying = cw_data.get('underlying', '')
        ratio = cw_data.get('ratio', 1.0)
        conversion_ratio = ratio
        
        S = cw_data.get('underlying_price', 0)
        X = cw_data.get('strike_price', 0)
        
        days_to_maturity = cw_data.get('days_to_maturity', 0)
        if days_to_maturity <= 0:
            return 0
        T = days_to_maturity / 365.0
        
        r_rate = 0.05 # Lãi suất phi rủi ro 5%
        sigma = get_volatility(underlying)
        
        call_price = black_scholes_call(S, X, T, r_rate, sigma)
        cw_theoretical_price = call_price / conversion_ratio
        
        return cw_theoretical_price
    except Exception as e:
        print(f"Error calculating BS for {cw_data.get('cw_symbol', '')}: {e}")
        return 0

def apply_model_1(cw_list):
    """Mô hình 1: Lọc An Toàn (Smart Filter)"""
    filtered = []
    for cw in cw_list:
        best_ask_vol = cw.get('best_ask_vol', 0)
        days_to_maturity = cw.get('days_to_maturity', 0)
        premium = cw.get('premium', 100)
        moneyness = cw.get('moneyness', -100)
        
        if best_ask_vol >= 1000 and days_to_maturity >= 30 and premium <= 5 and moneyness >= 0:
            filtered.append(cw)
    return filtered

def apply_model_2(cw_list):
    """Mô hình 2: Chấm Điểm Đòn Bẩy (Scoring Model)"""
    scored = []
    for cw in cw_list:
        moneyness = cw.get('moneyness', 0)
        gearing = cw.get('gearing', 0)
        best_ask_vol = cw.get('best_ask_vol', 0)
        
        # Chuẩn hóa điểm thanh khoản (tối đa 100 điểm cho 100k vol)
        liq_score = min(100, best_ask_vol / 1000) 
        
        # Điểm tổng hợp
        score = (moneyness * 0.4) + (gearing * 0.4) + (liq_score * 0.2)
        cw['score'] = score
        scored.append(cw)
        
    # Sắp xếp giảm dần theo điểm
    scored.sort(key=lambda x: x.get('score', 0), reverse=True)
    return scored

def apply_model_3(cw_list):
    """Mô hình 3: Định Giá Black-Scholes"""
    valued = []
    for cw in cw_list:
        theo_price = calculate_theoretical_price(cw)
        cw['theoretical_price'] = theo_price
        
        current_price = cw.get('cw_price', 0)
        best_ask = cw.get('best_ask_price', current_price)
        market_price = best_ask if (isinstance(best_ask, (int, float)) and best_ask > 0) else current_price
        
        # Định giá Rẻ/Đắt: (Giá LT - Giá TT) / Giá TT
        if market_price > 0:
            undervalued_pct = ((theo_price - market_price) / market_price) * 100
        else:
            undervalued_pct = 0
            
        cw['undervalued_pct'] = undervalued_pct
        valued.append(cw)
        
    # Lọc những con có undervalued_pct > 0 (đang bị định giá rẻ) và sắp xếp giảm dần
    valued = [cw for cw in valued if cw['undervalued_pct'] > 0]
    valued.sort(key=lambda x: x['undervalued_pct'], reverse=True)
    return valued
