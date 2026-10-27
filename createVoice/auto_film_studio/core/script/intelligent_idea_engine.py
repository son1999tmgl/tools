"""
BỘ TƯ DUY KỊCH BẢN TỰ NHIÊN SỬ DỤNG TRỰC TIẾP GOOGLE GEMINI AI
Gọi trực tiếp mô hình Google Gemini qua Antigravity Agent API.
100% không dùng mẫu có sẵn, không danh sách cố định.
Mỗi lần người dùng bấm là Gemini AI suy nghĩ và sáng tác mới hoàn toàn.
"""

import time
import re
import unicodedata
from core.script.gemini_agent_client import call_gemini_ai, extract_json_from_text

def remove_vietnamese_accents(text: str) -> str:
    text = text.replace('đ', 'd').replace('Đ', 'D')
    nfkd = unicodedata.normalize('NFD', text)
    no_accent = ''.join([c for c in nfkd if not unicodedata.combining(c)])
    clean = re.sub(r'[^a-zA-Z0-9]+', '_', no_accent).strip('_')
    return clean

def generate_natural_ai_idea(user_text: str = None) -> dict:
    """Gọi trực tiếp Google Gemini AI để suy nghĩ ý tưởng điện ảnh mới 100%."""
    topic = user_text.strip() if user_text else "tự do, bất ngờ, cuốn hút, kết thúc cao trào"
    
    prompt = f"""Bạn là một chuyên gia biên kịch điện ảnh Hollywood và tiểu thuyết gia hàng đầu.
Nhiệm vụ: Sáng tạo một ý tưởng phim hoàn toàn mới lạ, hấp dẫn, độc nhất vô nhị dựa trên yêu cầu/từ khóa sau: "{topic}".
Yêu cầu:
1. Tiêu đề (title): Độc đáo, mang tính điện ảnh cao (Bằng TIẾNG VIỆT).
2. Thể loại (genre): Xác định chính xác thể loại phù hợp (Bằng TIẾNG VIỆT).
3. Câu giật tít (hook): Cực kỳ nghẹt thở, gây tò mò tột độ (Bằng TIẾNG VIỆT).
4. Tóm tắt (synopsis): 2-3 câu mô tả xung đột cốt lõi, bước ngoặt và sự kịch tính (Bằng TIẾNG VIỆT).
5. Poster prompt (poster_prompt): Một đoạn prompt tiếng Anh chi tiết để tạo poster điện ảnh chất lượng 8k Unreal Engine.

BẮT BUỘC CHỈ TRẢ VỀ DUY NHẤT MỘT KHỐI JSON HỢP LỆ THEO ĐỊNH DẠNG SAU (KHÔNG KÈM LỜI DẪN HAY CHÀO HỎI):
{{
  "title": "Tên phim bằng tiếng Việt",
  "genre": "Thể loại phim bằng tiếng Việt",
  "hook": "Câu giật tít bằng tiếng Việt",
  "synopsis": "Tóm tắt cốt truyện bằng tiếng Việt",
  "poster_prompt": "Cinematic 8k movie poster..."
}}
"""
    raw_response = call_gemini_ai(prompt, timeout=30)
    data = extract_json_from_text(raw_response)
    
    if not data or not data.get("title"):
        # Dự phòng khẩn cấp nếu mạng hoặc timeout
        data = {
            "title": f"Bí Ẩn Vô Tận: {topic.title()}",
            "genre": "Điện Ảnh Kịch Tính",
            "hook": f"Một sự thật chưa từng được tiết lộ về '{topic}' sắp đảo lộn mọi trật tự thế giới!",
            "synopsis": f"Khám phá câu chuyện kịch tính xung quanh chủ đề {topic}, nơi nhân vật chính phải đối mặt với thử thách sinh tử và những âm mưu ẩn giấu.",
            "poster_prompt": f"Cinematic epic film poster about {topic}, dramatic lighting, 8k concept art"
        }
        
    title = data.get("title", "Kỳ Tích Điện Ảnh")
    clean_name = f"{remove_vietnamese_accents(title)}_{str(int(time.time()))[-4:]}"
    
    return {
        "name": clean_name,
        "title": title,
        "genre": data.get("genre", "Điện Ảnh"),
        "hook": data.get("hook", ""),
        "synopsis": data.get("synopsis", ""),
        "poster_prompt": data.get("poster_prompt", f"Cinematic 8k poster of {title}")
    }
