"""
TỔNG HỢP CÁC PROVIDER CỦA CORE ENGINE
Tất cả các nguồn được khai báo tập trung tại đây để Web UI hoặc bất kỳ script nào
cũng có thể gọi dùng một cách đồng nhất và dễ dàng.
"""

from .image.internal_ai_provider import InternalAIImageProvider
from .audio.voice_providers import VieNeuTTSProvider, KokoroTTSProvider
from .cloud_gpu.gpu_providers import KaggleGPUProvider, ColabGPUProvider
from .video.wan_provider import Wan21VideoProvider

# Factory / Registry
class StudioCore:
    def __init__(self):
        # 1. Nguồn Hình ảnh & Poster (Nội bộ AI thông minh)
        self.image_engine = InternalAIImageProvider()
        
        # 2. Nguồn Giọng nói
        self.voice_vieneu = VieNeuTTSProvider()
        self.voice_kokoro = KokoroTTSProvider()
        
        # 3. Nguồn Cloud GPU
        self.gpu_kaggle = KaggleGPUProvider()
        self.gpu_colab = ColabGPUProvider()
        
        # 4. Mô hình Video
        self.video_wan = Wan21VideoProvider(model_size="1.3B")

# Khởi tạo instance dùng chung
core = StudioCore()
