# Hướng Dẫn Mở Rộng Hệ Thống (Developer Guide)

Kiến trúc của Auto Film Studio được xây dựng theo mô hình **Provider Pattern** đặt tại thư mục `auto_film_studio/core/`. Mọi tính năng đều có thể mở rộng dễ dàng mà không làm ảnh hưởng đến giao diện Web hay các module khác.

---

## 1. Thêm Nguồn Giọng Đọc Mới (Ví dụ: Edge-TTS)

Mở file `auto_film_studio/core/audio/voice_providers.py`, tạo một class kế thừa từ `BaseAudioProvider`:

```python
from ..base import BaseAudioProvider
import edge_tts
import asyncio

class EdgeTTSProvider(BaseAudioProvider):
    def get_available_voices(self) -> list:
        return [
            {"id": "vi-VN-HoaiMyNeural", "name": "Hoài My (Nữ truyền cảm)", "gender": "Nữ", "lang": "vi"},
            {"id": "vi-VN-NamMinhNeural", "name": "Nam Minh (Nam phóng sự)", "gender": "Nam", "lang": "vi"}
        ]

    def synthesize(self, text: str, output_path: str, voice_id: str = "vi-VN-HoaiMyNeural") -> bool:
        # Gọi thư viện edge_tts sinh file mp3
        communicate = edge_tts.Communicate(text, voice_id)
        asyncio.run(communicate.save(output_path))
        return True
```

Sau đó đăng ký vào `auto_film_studio/core/__init__.py`.

---

## 2. Thêm Nền Tảng GPU Mới (Ví dụ: RunPod, Lightning AI)

Mở file `auto_film_studio/core/cloud_gpu/gpu_providers.py`:

```python
from ..base import BaseCloudGPUProvider

class LightningGPUProvider(BaseCloudGPUProvider):
    def test_connection(self, credentials: dict) -> dict:
        api_key = credentials.get("key")
        # Kiểm tra API Key với Lightning Studio
        return {"connected": True, "message": "Lightning Studio sẵn sàng!"}

    def dispatch_task(self, task_payload: dict, credentials: dict) -> str:
        # Gửi payload render lên Lightning Studio API
        return "dispatched"
```

---

## 3. Thêm Mô Hình Video Mới (Ví dụ: LTX-Video, HunyuanVideo)

Mở file `auto_film_studio/core/video/`, tạo file `ltx_provider.py`:

```python
from ..base import BaseVideoProvider

class LTXVideoProvider(BaseVideoProvider):
    def get_run_command(self, image_path: str, motion_prompt: str, output_path: str) -> str:
        return f'python ltx_generate.py --image "{image_path}" --prompt "{motion_prompt}" --output "{output_path}"'
```

Mọi module đều độc lập, có thể cắm-rút (plug-and-play) tùy ý!
