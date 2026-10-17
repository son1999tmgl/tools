import os
import json
from typing import List, Optional
from dotenv import load_dotenv
from pydantic import BaseModel, Field
from google import genai
from google.genai import types

load_dotenv()

class AccountOffer(BaseModel):
    platform: str = Field(
        description="Tên chung của nền tảng/dịch vụ (viết chuẩn, ngắn gọn). Ví dụ: Gemini, ChatGPT, Netflix, Canva, Spotify, Youtube, Telegram, CapCut, Office 365, Adobe, Zoom, Facebook, v.v."
    )
    account_type: str = Field(
        description="Loại tài khoản cụ thể / phiên bản (Ví dụ: Gemini Pro, Gemini Ultra, ChatGPT Plus 4o, Netflix 4K, Canva Pro, Youtube Premium Family...)"
    )
    duration_or_plan: str = Field(
        description="Gói hoặc thời hạn (Ví dụ: 1 tháng, 1 năm, slot family, vĩnh viễn, 6 tháng...)"
    )
    price_vnd: Optional[int] = Field(
        default=None,
        description="Giá bán quy đổi chính xác ra số nguyên VNĐ (integer). Ví dụ: '35 cành' hoặc '35k' -> 35000; '1 lít' -> 100000; '1.5 củ' -> 1500000. Nếu là 'thương lượng' hoặc không rõ giá thì để null."
    )
    price_raw: str = Field(
        default="",
        description="Chuỗi giá hiển thị gốc hoặc mô tả giá (Ví dụ: '35 cành', '50k/slot', 'Thương lượng', '120k')"
    )
    warranty_or_notes: str = Field(
        default="",
        description="Chính sách bảo hành, cấp tài khoản hay nâng chính chủ, số lượng tồn kho..."
    )

class MessageAnalysisResult(BaseModel):
    is_selling_account: bool = Field(
        description="True nếu tin nhắn là người rao BÁN / PASS / CUNG CẤP tài khoản dịch vụ. False nếu là người hỏi mua, spam, tâm sự, cảnh báo scam, không liên quan."
    )
    offers: List[AccountOffer] = Field(
        default_factory=list,
        description="Danh sách các sản phẩm/tài khoản kèm giá được rao bán trong tin nhắn"
    )
    contact_info: Optional[str] = Field(
        default="",
        description="SĐT, Zalo, STK hoặc username liên hệ có trong bài"
    )

SYSTEM_INSTRUCTION = """
Bạn là một trợ lý AI chuyên gia phân tích tin nhắn mua bán tài khoản trên Telegram tại Việt Nam.
Nhiệm vụ của bạn là:
1. Xác định xem tin nhắn có phải là ĐĂNG BÁN tài khoản dịch vụ hay không:
   - Là người BÁN: Dùng các từ như "pass", "bán", "đẩy", "còn slot", "sẵn acc", "xả kho", "cung cấp", "giá hạt dẻ"... -> is_selling_account = True.
   - Là người MUA hoặc HỎI XIN: "cần mua", "ai có acc... không", "cần tìm", "tìm người bán", "xin slot"... -> is_selling_account = False.
   - Spam / Không liên quan / Quảng cáo cờ bạc, 18+: -> is_selling_account = False.
2. Phân loại hai cấp nền tảng:
   - `platform`: Tên nền tảng chung chuẩn hóa (ví dụ: Gemini, ChatGPT, Netflix, Canva, Spotify, Youtube, Telegram, Office 365, Adobe, CapCut...)
   - `account_type`: Chi tiết phiên bản (ví dụ: Gemini Pro, Gemini Ultra, ChatGPT Plus, Canva Pro, Netflix 4K UHD...)
3. Hiểu tiếng lóng tiền tệ Việt Nam và quy đổi sang số nguyên `price_vnd`:
   - "30k", "30 cành", "30 khoai" = 30000
   - "1 lít", "1 lốp" = 100000; "2 lít" = 200000
   - "1 củ", "1 chai" = 1000000; "5 loét" = 500000
   - Nếu ghi rõ "50.000đ" hoặc "50k" -> 50000
   - Nếu không có giá hoặc "thương lượng", "ib" -> price_vnd = null, price_raw = "Thương lượng / IB"
4. Trích xuất chính xác từng món hàng kèm giá và gói.
"""

class AIExtractor:
    def __init__(self, api_key: Optional[str] = None):
        self.api_key = api_key or os.getenv("GEMINI_API_KEY")
        if not self.api_key:
            raise ValueError("Chưa cấu hình GEMINI_API_KEY! Hãy thêm GEMINI_API_KEY vào file .env")
        
        self.client = genai.Client(api_key=self.api_key)
        self.model = os.getenv("GEMINI_MODEL", "gemini-3.8-flash")

    def analyze_message(self, text: str) -> Optional[MessageAnalysisResult]:
        """Phân tích tin nhắn Telegram và trích xuất thông tin bán tài khoản."""
        if not text or len(text.strip()) < 5:
            return None

        prompt = f"Phân tích tin nhắn Telegram sau đây:\n\"\"\"\n{text}\n\"\"\""

        try:
            response = self.client.models.generate_content(
                model=self.model,
                contents=prompt,
                config=types.GenerateContentConfig(
                    system_instruction=SYSTEM_INSTRUCTION,
                    response_mime_type="application/json",
                    response_json_schema=MessageAnalysisResult.model_json_schema(),
                    temperature=0.1
                )
            )

            if response.text:
                data = json.loads(response.text)
                return MessageAnalysisResult(**data)
        except Exception as e:
            print(f"[AIExtractor Error] Lỗi khi phân tích AI: {e}")
            return None
        return None
