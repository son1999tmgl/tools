"""
CẤU TRÚC KIẾN TRÚC MODULAR CỦA AUTO FILM STUDIO:
Mỗi chức năng (Voice, Image, Video, GPU, Script) là 1 module độc lập.
Khi muốn thêm nguồn mới, bạn chỉ cần tạo 1 file provider mới kế thừa từ Base class.
"""

# ==============================================================================
# 1. CORE AUDIO (GIỌNG ĐỌC & LỒNG TIẾNG)
# ==============================================================================
class BaseAudioProvider:
    """Interface cơ sở cho các nguồn tạo giọng nói (VieNeu, Kokoro, Edge-TTS, ElevenLabs...)."""
    def synthesize(self, text: str, output_path: str, voice_id: str = None) -> bool:
        raise NotImplementedError("Cần hiện thực hàm synthesize trong class con")

# ==============================================================================
# 2. CORE IMAGE & POSTER (TẠO HÌNH ẢNH NHÂN VẬT, PHÁO ĐÀI, POSTER)
# ==============================================================================
class BaseImageProvider:
    """Interface cơ sở cho các nguồn tạo hình ảnh (Flux via Pollinations, Stable Diffusion, Midjourney...)."""
    def generate_image_url(self, prompt: str, seed: int = None, width: int = 1024, height: int = 1024) -> str:
        raise NotImplementedError("Cần hiện thực hàm generate_image_url")
        
    def download_image(self, image_url: str, save_path: str) -> bool:
        raise NotImplementedError("Cần hiện thực hàm download_image")

# ==============================================================================
# 3. CORE VIDEO (MÔ HÌNH SINH VIDEO CHUYỂN ĐỘNG)
# ==============================================================================
class BaseVideoProvider:
    """Interface cơ sở cho các mô hình tạo video (Wan 2.1, LTX-Video, Hunyuan, Kling...)."""
    def get_run_command(self, image_path: str, motion_prompt: str, output_path: str) -> str:
        raise NotImplementedError("Cần hiện thực hàm get_run_command")

# ==============================================================================
# 4. CORE CLOUD GPU (NỀN TẢNG CHẠY GPU MIỄN PHÍ)
# ==============================================================================
class BaseCloudGPUProvider:
    """Interface cơ sở cho nền tảng GPU (Kaggle, Google Colab, Lightning AI...)."""
    def test_connection(self, credentials: dict) -> dict:
        raise NotImplementedError("Cần hiện thực hàm test_connection")
        
    def dispatch_task(self, task_payload: dict, credentials: dict) -> str:
        raise NotImplementedError("Cần hiện thực hàm dispatch_task")
