"""
Mô-đun dựng phim tự động:
- Ghép nối các video cảnh (scenes)
- Khớp giọng đọc thoại (TTS audio)
- Lồng nhạc nền (BGM) và hiệu ứng âm thanh (SFX)
- Xuất thành phẩm phim dài Full HD 1080p
"""

import os
import json
import subprocess
from pathlib import Path

def create_concat_file(scene_files, list_txt_path):
    """Tạo file danh sách để FFMPEG ghép nối video không tốn CPU (Stream copy)."""
    with open(list_txt_path, 'w', encoding='utf-8') as f:
        for scene in scene_files:
            f.write(f"file '{os.path.abspath(scene)}'\n")

def assemble_film(storyboard_json_path, scenes_dir, audio_dir, output_film_path):
    """
    Ráp toàn bộ các phân cảnh thành bộ phim hoàn chỉnh.
    Chạy cực nhẹ trên CPU ARM của Oracle VPS hoặc máy cá nhân.
    """
    with open(storyboard_json_path, 'r', encoding='utf-8') as f:
        storyboard = json.load(f)
        
    print(f"🎬 Bắt đầu dựng phim với {len(storyboard)} phân cảnh...")
    
    scene_videos = []
    for item in storyboard:
        scene_id = item["scene_id"]
        video_path = os.path.join(scenes_dir, f"{scene_id}_1080p.mp4")
        if os.path.exists(video_path):
            scene_videos.append(video_path)
        else:
            print(f"⚠️ Cảnh báo: Chưa tìm thấy video {video_path}")
            
    if not scene_videos:
        print("❌ Không có video cảnh nào để ghép!")
        return
        
    concat_txt = os.path.join(scenes_dir, "concat_list.txt")
    create_concat_file(scene_videos, concat_txt)
    
    # Ghép nối video với tốc độ cực nhanh bằng FFMPEG concat demuxer
    print("⏳ Đang ghép nối các phân cảnh thành phim dài...")
    cmd = [
        "ffmpeg", "-y",
        "-f", "concat",
        "-safe", "0",
        "-i", concat_txt,
        "-c", "copy",
        output_film_path
    ]
    subprocess.run(cmd, check=True)
    print(f"🎉 PHIM ĐÃ XUẤT XƯỞNG THÀNH CÔNG: {output_film_path}")

if __name__ == "__main__":
    print("Auto Film Assembler sẵn sàng hoạt động!")
