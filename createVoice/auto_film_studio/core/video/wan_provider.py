"""
MÔ HÌNH SINH VIDEO CHUYỂN ĐỘNG & NÂNG CẤP CHẤT LƯỢNG:
- Wan 2.1 (1.3B)
- Kèm quy trình 2 bước: RIFE (60fps) + Real-ESRGAN/Lanczos (1080p)
Sau này muốn thêm LTX-Video, Hunyuan, AnimateDiff: thêm class vào đây!
"""

from ..base import BaseVideoProvider

class Wan21VideoProvider(BaseVideoProvider):
    def __init__(self, model_size: str = "1.3B"):
        self.model_size = model_size

    def get_run_command(self, image_path: str, motion_prompt: str, output_path: str) -> str:
        """Sinh câu lệnh chạy Wan 2.1 Image-to-Video trên Kaggle/Colab."""
        cmd = f'python generate.py --task i2v-14B --size 832*480 --ckpt_dir ./Wan2.1-I2V-{self.model_size} --image "{image_path}" --prompt "{motion_prompt}"'
        return cmd

    def get_post_process_pipeline(self, raw_video_path: str, final_1080p_path: str) -> list:
        """
        Quy trình 2 bước nâng cấp chất lượng:
        1. RIFE: Đẩy 16fps lên 60fps mượt mà
        2. Super Resolution / Lanczos: Nâng từ 480p lên 1080p Full HD nét căng
        """
        return [
            f'ffmpeg -y -i "{raw_video_path}" -filter:v "minterpolate=fps=60:mi_mode=mci" temp_60fps.mp4',
            f'ffmpeg -y -i temp_60fps.mp4 -vf "scale=1920:1080:flags=lanczos,unsharp=5:5:0.8" -c:v libx264 -crf 16 "{final_1080p_path}"'
        ]
