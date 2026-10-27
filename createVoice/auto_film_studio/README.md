# Auto Film Studio - Hệ Thống Sản Xuất Phim Tự Động Bằng AI

Hệ thống sản xuất phim dài tập tự động kết hợp các mô hình mã nguồn mở tối ưu phần cứng miễn phí:
- **Biên kịch & Phân cảnh:** Gemini / Antigravity AI
- **Giọng đọc & Lồng tiếng (TTS):** VieNeu-TTS (hoặc Kokoro-TTS)
- **Hình ảnh & Nhân vật nhất quán (Character Consistency):** Thư viện Character Sheets + Image-to-Video
- **Sinh chuyển động (Video Generation):** Wan 2.1 - 1.3B (chạy trên Kaggle / Google Colab T4 GPU)
- **Hậu kỳ nâng cấp chất lượng (2-Step Post-Processing):**
  1. *Bước 1 - Làm mượt chuyển động:* **RIFE** (Interpolation từ 16fps lên 32fps/60fps)
  2. *Bước 2 - Nâng nét Full HD:* **Real-ESRGAN / FFMPEG Lanczos** (Nâng từ 480p lên 1080p sắc nét)
- **Tổng hợp & Ghép phim hoàn chỉnh:** FFMPEG (chạy 24/7 trên Oracle Free Tier VPS hoặc Local)

---

## Cấu trúc thư mục dự án

```
auto_film_studio/
├── characters/             # Chứa ảnh phôi mẫu của từng nhân vật/đồ vật để giữ nhất quán
├── configs/                # Cấu hình project và kịch bản phân cảnh mẫu (storyboard.json)
├── notebooks/              # Mã nguồn chạy trên Kaggle / Google Colab (Wan 2.1 + RIFE + Real-ESRGAN)
├── scripts/                # Script Python dựng phim, gọi TTS, ghép nối FFMPEG
└── output/
    ├── scenes/             # Chứa các đoạn video cảnh lẻ đã render & nâng cấp
    └── final/              # Phim dài thành phẩm (Full HD 1080p)
```

---

## Quy trình sản xuất 5 bước

### Bước 1: Chuẩn bị nhân vật nhất quán (`characters/`)
- Tạo 1 hoặc nhiều ảnh chân dung/toàn thân chuẩn của nhân vật chính (`hero.png`, `villain.png`).
- Luôn dùng ảnh này làm điểm khởi đầu (Initial Frame) cho Wan 2.1 Image-to-Video để nhân vật không bao giờ bị đổi mặt qua các cảnh.

### Bước 2: Sinh kịch bản phân cảnh (`configs/storyboard.json`)
- Agent Antigravity viết kịch bản chi tiết theo định dạng JSON chuẩn.
- Mỗi cảnh gồm: Lời thoại tiếng Việt, ảnh nhân vật tham chiếu, prompt chuyển động tiếng Anh, góc máy.

### Bước 3: Sinh giọng đọc thoại (TTS)
- Sử dụng **VieNeu-TTS** (trong `D:\ThuNghiem\createVoice\VieNeu-TTS`) để đọc từng câu thoại theo từng phân cảnh, xuất ra các file âm thanh tương ứng (`scene_001.wav`).

### Bước 4: Render video chuyển động trên Kaggle / Colab (`notebooks/`)
- Nạp notebook chạy **Wan 2.1 - 1.3B**.
- Sinh ra video cảnh 5s ở độ phân giải 480p @ 16fps.
- **Tự động áp dụng 2 bước nâng cấp:**
  1. Chạy **RIFE** để nhân đôi số khung hình lên 32fps hoặc 60fps mượt mà.
  2. Chạy **Real-ESRGAN** phóng to khung hình lên **1080p Full HD**.

### Bước 5: Lắp ráp phim hoàn chỉnh (FFMPEG trên Oracle VPS / Local)
- Ghép các video cảnh lại với nhau khớp với thời lượng giọng đọc thoại.
- Thêm phụ đề tự động (Subtitle burn-in).
- Mix nhạc nền (BGM Audio Ducking: tự giảm âm lượng khi nhân vật nói).
