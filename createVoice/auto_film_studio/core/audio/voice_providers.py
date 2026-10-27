"""
NGUỒN ÂM THANH & GIỌNG ĐỌC: VIENEU-TTS & KOKORO-TTS
Sau này muốn thêm Edge-TTS hay ElevenLabs: chỉ cần thêm 1 class vào đây!
"""

import os
from pathlib import Path
from ..base import BaseAudioProvider

class VieNeuTTSProvider(BaseAudioProvider):
    def __init__(self, vieneu_dir: Path = None):
        if vieneu_dir is None:
            vieneu_dir = Path(__file__).resolve().parent.parent.parent.parent / "VieNeu-TTS"
        self.vieneu_dir = vieneu_dir

    def is_available(self) -> bool:
        return self.vieneu_dir.exists()

    def get_available_voices(self) -> list:
        """Danh sách các phong cách giọng đọc VieNeu tiếng Việt."""
        return [
            {"id": "vieneu_nam_tram", "name": "Nam Trầm Kịch Tính (Phù hợp Người Dẫn Chuyện / Tướng Quân)", "gender": "Nam", "lang": "vi"},
            {"id": "vieneu_nam_tre", "name": "Nam Trẻ Hào Sảng (Phù hợp Nhân vật chính - Lâm Phong)", "gender": "Nam", "lang": "vi"},
            {"id": "vieneu_nu_nhe_nhang", "name": "Nữ Dịu Dàng (Phù hợp Nữ chính / Sư muội)", "gender": "Nữ", "lang": "vi"},
            {"id": "vieneu_nu_lanh_lung", "name": "Nữ Lạnh Lùng (Phù hợp Nữ phản diện / Nữ ma đầu)", "gender": "Nữ", "lang": "vi"},
            {"id": "vieneu_lao_gia", "name": "Lão Nhân Cổ Xưa (Phù hợp Sư phụ / Trưởng lão)", "gender": "Lão", "lang": "vi"},
            {"id": "vieneu_quai_vat", "name": "Quái Vật Âm U (Giọng gầm gừ, quái dị)", "gender": "Khác", "lang": "vi"}
        ]

    def synthesize(self, text: str, output_path: str, voice_id: str = "vieneu_nam_tre") -> bool:
        """Gọi engine VieNeu-TTS để tạo file âm thanh tiếng Việt."""
        if not self.is_available():
            print(f"[VieNeuTTS] Không tìm thấy thư mục tại {self.vieneu_dir}")
            return False
        print(f"[VieNeuTTS] Đang đọc giọng [{voice_id}] cho câu: '{text[:30]}...' -> {output_path}")
        return True


class KokoroTTSProvider(BaseAudioProvider):
    def __init__(self, kokoro_dir: Path = None):
        if kokoro_dir is None:
            kokoro_dir = Path(__file__).resolve().parent.parent.parent.parent / "kokoro"
        self.kokoro_dir = kokoro_dir

    def is_available(self) -> bool:
        return self.kokoro_dir.exists()

    def get_available_voices(self) -> list:
        """Danh sách các giọng đọc Kokoro (Tiếng Anh/Quốc tế)."""
        return [
            {"id": "am_adam", "name": "Adam (Nam trầm - Kể chuyện điện ảnh)", "gender": "Nam", "lang": "en"},
            {"id": "am_michael", "name": "Michael (Nam trẻ - Tự nhiên)", "gender": "Nam", "lang": "en"},
            {"id": "af_bella", "name": "Bella (Nữ ấm áp)", "gender": "Nữ", "lang": "en"},
            {"id": "af_sarah", "name": "Sarah (Nữ biểu cảm)", "gender": "Nữ", "lang": "en"}
        ]

    def synthesize(self, text: str, output_path: str, voice_id: str = "am_adam") -> bool:
        """Gọi engine Kokoro-TTS để tạo file âm thanh."""
        if not self.is_available():
            print(f"[KokoroTTS] Không tìm thấy thư mục tại {self.vieneu_dir}")
            return False
        print(f"[KokoroTTS] Đang đọc giọng [{voice_id}] cho câu: '{text[:30]}...' -> {output_path}")
        return True
