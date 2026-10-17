import os
import json
from typing import List, Optional
from dotenv import load_dotenv
from pydantic import BaseModel, Field
from google import genai
from google.genai import types

load_dotenv()

class AccountOffer(BaseModel):
    account_type: str = Field(description="Tên loại tài khoản hoặc dịch vụ (ví dụ: Netflix, Spotify, ChatGPT Plus, Canva, Telegram Premium, Youtube Premium, Mail, Game...)")
    duration_or_plan: str = Field(description="Gói hoặc thời hạn (ví dụ: 1 tháng, 1 năm, slot family, vĩnh viễn...)")
    price: str = Field(description="Giá bán quy đổi ra VNĐ hoặc ghi rõ (ví dụ: '50.000 VNĐ', '30.000 VNĐ', 'Thương lượng')")
    warranty_or_notes: str = Field(description="Chính sách bảo hành, tài khoản cấp sẵn hay nâng chính chủ, lưu ý đặc biệt nếu có")

class MessageAnalysisResult(BaseModel):
    is_selling_account: bool = Field(description="True nếu tin nhắn là người rao BÁN / PASS / CUNG CẤP tài khoản. False nếu là người hỏi mua, spam, tâm sự, cảnh báo scam, không liên quan.")
    offers: List[AccountOffer] = Field(default_factory=list, description="Danh sách các sản phẩm/tài khoản kèm giá được rao bán trong tin nhắn")
    contact_info: Optional[str] = Field(default="", description="SĐT, Zalo, STK hoặc thông tin liên hệ khác có trong bài")

SYSTEM_INSTRUCTION = """
Bạn là một trợ lý AI chuyên gia phân tích tin nhắn mua bán tài khoản trên Telegram tại Việt Nam.
Nhiệm vụ của bạn là:
1. Xác định xem tin nhắn có phải là ĐĂNG BÁN tài khoản dịch vụ hay không:
   - Là người BÁN: Dùng các từ như "pass", "bán", "đẩy", "còn slot", "sẵn acc", "xả kho", "cung cấp", "giá hạt dẻ"... -> is_selling_account = True.
   - Là người MUA hoặc HỎI XIN: "cần mua", "ai có acc... không", "cần tìm", "tìm người bán", "xin slot"... -> is_selling_account = False.
   - Spam / Không liên quan / Quảng cáo web sex / Cờ bạc: -> is_selling_account = False.
2. Hiểu tiếng lóng tiền tệ Việt Nam:
   - "30k", "30 cành", "30 khoai" = 30.000 VNĐ
   - "1 lít", "1 lốp" = 100.000 VNĐ
   - "1 củ", "1 chai" = 1.000.000 VNĐ
   - "1m" trong ngữ cảnh thời gian là 1 month (1 tháng), trong ngữ cảnh tiền là 1 triệu tuỳ câu từ.
3. Trích xuất chính xác từng món hàng kèm giá và gói.
"""

class AIExtractor:
    def __init__(self, api_key: Optional[str] = None):
        self.api_key = api_key or os.getenv("GEMINI_API_KEY")
        if not self.api_key:
            raise ValueError("Chưa cấu hình GEMINI_API_KEY! Hãy thêm GEMINI_API_KEY vào file .env")
        
        self.client = genai.Client(api_key=self.api_key)
        # Sử dụng model gemini-3.8-flash (hoặc gemini-3.5-flash-lite)
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
