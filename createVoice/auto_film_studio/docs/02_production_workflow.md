# Quy Trình Sản Xuất Phim 4 Bước Chuẩn Chuyên Nghiệp

Quy trình sản xuất 1 tập phim (thời lượng mục tiêu ~60 phút) được thiết kế khép kín trên giao diện Web UI:

```
[BƯỚC 1: KỊCH BẢN & BÓC TÁCH ASSET]
               │
               ▼
[BƯỚC 2: KHÓA MẶT NHÂN VẬT & GÁN GIỌNG NÓI]
               │
               ▼
[BƯỚC 3: PHÂN CẢNH & RENDER (TEST HOẶC BATCH)]
               │
               ▼
[BƯỚC 4: HẬU KỲ NÂNG CẤP 1080P & GHÉP NỐI PHIM HOÀN CHỈNH]
               │
               ▼
[TÙY CHỌN BẤT CỨ LÚC NÀO: VẼ POSTER ĐIỆN ẢNH BẰNG FLUX]
```

---

## Chi Tiết Từng Bước

### Bước 1: Kịch Bản Đi Trước & Bóc Tách Asset (Script-First)
- AI viết kịch bản chi tiết dựa trên bối cảnh phim và nối tiếp nội dung tập trước.
- **Quy tắc thời lượng vàng:** Mỗi tập phim không cố định cứng nhắc mà **dao động linh hoạt từ 50 đến 120 phút** (tùy theo nhịp độ câu chuyện và độ phức tạp của trận chiến).
- **Quy tắc kết thúc cao trào (Cliffhanger Obligation):** 
  - **BẮT BUỘC:** Điểm kết thúc của MỌI tập phim luôn phải là một **móc câu cao trào (Cliffhanger)** tại thời khắc căng thẳng/bất ngờ nhất (ví dụ: nhân vật chính chạm trán boss bí ẩn, pháo đài xuất hiện biến dị cấp tiếp theo, sự phản bội bất ngờ, hoặc đợt thú triều thứ 2 hung hãn hơn vừa trồi lên...).
  - Mục tiêu: Khiến khán giả nghẹt thở, tò mò cực độ và háo hức chờ đón tập tiếp theo.
- Kịch bản được chia theo cấu trúc các Hồi Điện Ảnh kịch tính (Mở đầu -> Phát triển -> Cao trào -> Thăng hoa & Kết thúc mở kịch tính).
- Người dùng có thể bấm **"Gửi Yêu Cầu Điều Chỉnh Kịch Bản"** để yêu cầu AI thêm bớt tình tiết, nhân vật theo ý muốn.
- AI tự động bóc tách danh sách nhân vật/vật phẩm (`detected_assets`) cần tạo.

### Bước 2: Khóa Hình Ảnh (Flux) & Gán Giọng Đọc (Voice Casting)
1. **Khóa hình ảnh:** Bấm "Sinh Ảnh Thử" bằng Flux nhiều lần (đổi seed) đến khi ưng ý thì bấm "Lưu Chốt".
   - Nhân vật người (`lam_phong.png`)
   - Vật phẩm/Kiến trúc nâng cấp (`phao_dai_lv1.png`, `phao_dai_lv2.png`...)
2. **Gán giọng nói (Voice Casting):**
   - Chọn giọng đọc cố định cho từng nhân vật từ menu:
     - *VieNeu-TTS:* Nam Trẻ Hào Sảng, Nam Trầm Kịch Tính, Nữ Dịu Dàng, Nữ Lạnh Lùng, Lão Nhân Cổ Xưa...
     - *Kokoro-TTS:* Adam, Michael, Bella, Sarah...
   - Giọng được khóa cứng vào hồ sơ nhân vật để các tập sau tự động dùng đúng giọng đó.

### Bước 3: Render Phân Cảnh (Wan 2.1 1.3B Image-to-Video)
- Phân cảnh được nạp vào bảng Scenes.
- **Nút Test:** Cho phép render thử 1 cảnh bất kỳ trước khi chạy cả tập.
- **Nút Chạy Hàng Loạt:** Tự động gửi task lên Kaggle/Colab theo danh sách tài khoản đã xoay vòng.
- Wan 2.1 lấy ảnh nhân vật đã chốt ở Bước 2 làm frame khởi đầu $\rightarrow$ giữ nhất quán 100% diện mạo và trang phục.

### Bước 4: Hậu Kỳ 2 Bước & Ghép Phim Dài (FFMPEG)
1. **Làm mượt:** Chạy RIFE nâng từ 16fps lên 60fps mượt mà.
2. **Nâng nét:** Chạy Super Resolution / Lanczos nâng từ 480p lên 1080p Full HD.
3. **Lắp ráp:** Ghép nối liên tiếp các cảnh bằng FFMPEG concat demuxer, lồng tiếng VieNeu-TTS, mix BGM tự giảm âm lượng khi có lời thoại (Audio Ducking), xuất ra file `.mp4` 1080p hoàn chỉnh.
