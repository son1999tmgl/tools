# Tài Liệu Kỹ Thuật & Kiến Trúc Tổng Thể: Auto Film Studio

## 1. Mục Tiêu Dự Án
Xây dựng một hệ thống sản xuất phim dài tập AI tự động (thời lượng mỗi tập từ 1 đến 2 tiếng), hoạt động dựa trên các tài nguyên miễn phí tối ưu phần cứng (Oracle Cloud Always Free VPS, Kaggle GPU 30h/tuần, Google Colab).

## 2. Các Thành Phần Cốt Lõi Trong Hệ Thống

| Thành Phần | Công Nghệ / Mô Hình | Vai Trò & Môi Trường Chạy |
| :--- | :--- | :--- |
| **Biên kịch & Phân cảnh** | Antigravity AI / LLM Engine | Viết kịch bản chuẩn 4 Hồi điện ảnh (~60 phút), chia thành hàng trăm scene, bóc tách nhân vật. |
| **Giọng đọc & Lồng tiếng** | **VieNeu-TTS** / **Kokoro-TTS** | Tạo giọng đọc thoại tiếng Việt tự nhiên/quốc tế, gán voice cố định cho từng nhân vật. Chạy trên CPU/Local. |
| **Hình ảnh & Poster** | **Flux** (via Pollinations API miễn phí) | Sinh ảnh nhân vật, kiến trúc (Pháo đài Lv1, Lv2...), poster điện ảnh. Hỗ trợ bấm sinh thử nhiều lần đến khi chốt. |
| **Sinh chuyển động video** | **Wan 2.1 (Bản 1.3B)** | Chế độ Image-to-Video (I2V) lấy ảnh chốt làm frame gốc để giữ nhất quán nhân vật. Chạy trên GPU T4 Kaggle/Colab. |
| **Hậu kỳ làm mượt & Nâng nét** | **RIFE** + **Real-ESRGAN / Lanczos** | **Bước 1:** RIFE nội suy từ 16fps lên 60fps mượt mà.<br>**Bước 2:** Upscale từ 480p lên **1080p Full HD**. |
| **Server & Ghép phim** | **Oracle Always Free VPS / Local** | Chạy 24/7 điều phối, Web UI quản lý, FFMPEG ghép nối video dài 1-2 tiếng bằng stream copy concat. |

## 3. Cấu Trúc Mã Nguồn (Modular Architecture)
- `auto_film_studio/core/`: Tách biệt độc lập từng module (Audio, Image, Video, Cloud GPU, Script).
- `auto_film_studio/projects/`: Lưu từng bộ phim riêng biệt (`project_info.json`, `assets/`, `episodes/`).
- `auto_film_studio/web_ui/`: Giao diện Web trực quan (Flask + Bootstrap Dark Mode) điều khiển toàn bộ studio.
- `auto_film_studio/configs/`: Quản lý danh sách tài khoản GPU xoay vòng (`cloud_accounts.json`).
- `auto_film_studio/docs/`: Lưu trữ toàn bộ tài liệu quy trình sản xuất và ghi nhớ phiên làm việc.
