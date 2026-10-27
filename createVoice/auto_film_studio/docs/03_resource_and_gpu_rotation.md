# Cơ Chế Quản Lý Tài Nguyên & Xoay Vòng Tài Khoản GPU

## 1. Nguồn Tài Nguyên Khai Thác

| Nguồn | Giới Hạn / Quota | Vai Trò Trong Hệ Thống |
| :--- | :--- | :--- |
| **Oracle Always Free VPS** | 4 OCPU ARM, 24GB RAM, 200GB ổ cứng, chạy 24/7 vĩnh viễn | Máy chủ tổng điều phối, chạy Web UI, viết kịch bản, chạy TTS (VieNeu/Kokoro), ghép phim bằng FFMPEG. |
| **Kaggle GPU** | 30 giờ GPU/tuần cho mỗi tài khoản, tối đa 12h/phiên, GPU Tesla T4 (16GB VRAM) | Render các phân cảnh video nặng bằng Wan 2.1 1.3B Image-to-Video và nâng cấp RIFE 60fps. |
| **Google Colab (Free)** | 3-5 giờ GPU T4/ngày | Bổ sung thêm quota dự phòng khi Kaggle hết giờ hoặc cần render song song. |
| **Pollinations API** | Miễn phí, không giới hạn request, model Flux | Sinh ảnh concept art nhân vật, kiến trúc và poster phim 8k. |

---

## 2. Cách Cấu Hình & Xác Thực Tài Khoản Kaggle (Chuẩn Mới)

Kaggle hỗ trợ chuẩn Token bảo mật mới:
- **Chuỗi Token:** Bắt đầu bằng tiền tố `KGAT_` (Ví dụ: `KGAT_769ead605ae640e129c9801444d7b13f`).
- **Cơ chế xác thực:** Dùng **Bearer Token** (`Authorization: Bearer KGAT_...`), không phụ thuộc vào username.
- **Cách lấy Token trên Kaggle:**
  1. Vào `kaggle.com/settings`.
  2. Tại mục **API Tokens (Recommended)**, bấm **Generate New Token**.
  3. Copy chuỗi token dán vào mục Quản Lý Tài Khoản trên Web UI.
  4. Bấm **"Test Kết Nối"** để xác thực trực tiếp trước khi đưa vào vòng xoay.

---

## 3. Chiến Lược Xoay Vòng Nhiều Tài Khoản (Multi-Account Rotation)
- Toàn bộ danh sách tài khoản được lưu trữ tại: `auto_film_studio/configs/cloud_accounts.json`.
- Hệ thống theo dõi số giờ đã sử dụng trong tuần (`hours_used_this_week`).
- Khi render 1 tập phim dài 60 phút:
  - Task được chia thành các batch (ví dụ: mỗi batch 20–30 phân cảnh).
  - Batch 1 gửi vào Tài khoản Kaggle 1.
  - Khi tài khoản 1 chạm ngưỡng hoặc bận $\rightarrow$ Tự động chuyển batch tiếp theo sang Tài khoản Kaggle 2 hoặc Colab.
  - Video render xong tự động chuyển về máy chủ để tiếp tục quy trình lắp ráp.
