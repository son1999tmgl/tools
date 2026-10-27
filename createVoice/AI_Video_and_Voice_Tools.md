# 🎬 BỘ CÔNG CỤ TẠO PHIM VÀ GIỌNG NÓI AI (MÃ NGUỒN MỞ & MIỄN PHÍ)

Tài liệu tổng hợp các công cụ Text-to-Speech (TTS), Video Generation và Lip-sync mã nguồn mở trên GitHub dùng để xây dựng phim/video AI.

---

## 🎙️ 1. CÔNG CỤ TẠO GIỌNG NÓI (TEXT-TO-SPEECH) ĐÃ CÓ SẴN TRÊN MÁY

### 🦜 VieNeu-TTS (Chuyên Tiếng Việt)
- **Thư mục local:** `D:\ThuNghiem\createVoice\VieNeu-TTS`
- **GitHub:** [https://github.com/pnnbao97/VieNeu-TTS](https://github.com/pnnbao97/VieNeu-TTS)
- **Điểm nổi bật:**
  - Chuyên biệt cho tiếng Việt tự nhiên, âm thanh chất lượng cao 48kHz.
  - Hỗ trợ **Voice Cloning** (nhân bản giọng từ mẫu audio 3–8 giây).
  - Có chế độ **Hội thoại (Conversation)** hỗ trợ phân vai đọc kịch bản phim nhiều nhân vật (lên đến 8 nhân vật).
  - Hỗ trợ chèn biểu cảm phi ngôn ngữ vào kịch bản: `[cười]`, `[thở dài]`, `[hắng giọng]`.
  - Hỗ trợ đọc theo timeline file phụ đề `.srt`.
  - Chạy mượt mà trên CPU bằng ONNX Runtime.
- **Cách khởi động Web UI:**
  ```powershell
  cd D:\ThuNghiem\createVoice\VieNeu-TTS
  uv run vieneu-web
  # Mở trình duyệt: http://127.0.0.1:7860
  ```

---

### 🌸 Kokoro-82M (Chuyên Tiếng Anh & Đa ngôn ngữ)
- **Thư mục local:** `D:\ThuNghiem\createVoice\kokoro`
- **GitHub:** [https://github.com/hexgrad/kokoro](https://github.com/hexgrad/kokoro)
- **Điểm nổi bật:**
  - Siêu nhẹ (82M tham số, model chỉ ~300MB), tốc độ sinh cực nhanh trên CPU.
  - Giọng đọc tiếng Anh (Mỹ / Anh) rất tự nhiên và chuẩn bản xứ.
  - Hỗ trợ thêm nhiều ngôn ngữ khác: Nhật, Trung, Tây Ban Nha, Pháp,...
- **Lưu ý trên Windows:** Cần cài đặt `espeak-ng` (`winget install eSpeak-NG.eSpeak-NG`).
- **Cách khởi động Web UI (chạy trên port 7861 để tránh trùng với VieNeu):**
  ```powershell
  $env:GRADIO_SERVER_PORT = "7861"
  cd D:\ThuNghiem\createVoice\kokoro\demo
  uv run python app.py
  # Mở trình duyệt: http://127.0.0.1:7861
  ```

---

## 🎥 2. CÔNG CỤ TẠO VIDEO AI (IMAGE-TO-VIDEO / TEXT-TO-VIDEO)

> ⚠️ **Lưu ý phần cứng:** Các mô hình tạo video AI yêu cầu GPU hiện đại từ 8GB–16GB VRAM trở lên. Máy có card cũ/nhỏ nên chạy thông qua **Google Colab (GPU Cloud miễn phí)** hoặc **Hugging Face Spaces** thay vì chạy local.

### 🌟 Wan2.1 / Wan2.2 (Alibaba) – *Chất lượng hàng đầu hiện nay*
- **GitHub:** [https://github.com/Wan-Video/Wan2.1](https://github.com/Wan-Video/Wan2.1)
- **Khả năng:**
  - Text-to-Video (T2V) & Image-to-Video (I2V) chất lượng điện ảnh.
  - Chuyển động vật lý cực kỳ tự nhiên (nước, gió, chuyển động cơ thể, ánh sáng).
  - Có các phiên bản: `1.3B` (nhẹ) và `14B` (chất lượng cao).

### ⚡ LTX-Video (Lightricks) – *Tốc độ nhanh nhất cho máy cá nhân*
- **GitHub:** [https://github.com/Lightricks/LTX-Video](https://github.com/Lightricks/LTX-Video)
- **Khả năng:**
  - Tối ưu hóa render nhanh chóng, tiêu tốn ít VRAM nhất trong các dòng mô hình Video Diffusion hiện nay.
  - Phù hợp tạo các cảnh chuyển động 3–5 giây để dựng phim.

### 🎭 LivePortrait (KwaiVGI) – *Khẩu hình & Cử động khuôn mặt (Lip-sync)*
- **GitHub:** [https://github.com/KwaiVGI/LivePortrait](https://github.com/KwaiVGI/LivePortrait)
- **Khả năng:**
  - Ghép 1 ảnh chân dung nhân vật + file audio giọng nói (từ VieNeu-TTS).
  - Làm nhân vật nhép miệng, chớp mắt, biểu cảm khuôn mặt khớp 100% với lời thoại.
  - Cực kỳ cần thiết cho các phân cảnh nhân vật trong phim đang nói chuyện.

### 🎬 Toonflow – *Studio làm phim AI mã nguồn mở*
- **GitHub:** [https://github.com/HBAI-Ltd/Toonflow-app](https://github.com/HBAI-Ltd/Toonflow-app)
- **Khả năng:**
  - Quản lý phân cảnh (Storyboard), duy trì khuôn mặt nhân vật đồng nhất qua các cảnh (Character Consistency), tích hợp pipeline kịch bản sang video.

---

## 🛠️ 3. QUY TRÌNH DỰNG PHIM AI ĐƠN GIẢN

```
[1. Kịch bản]
    ⬇️
[2. Tạo Giọng nói] ──► Dùng VieNeu-TTS (cho tiếng Việt) hoặc Kokoro (tiếng Anh)
    ⬇️
[3. Tạo Ảnh nhân vật/bối cảnh] ──► Dùng Midjourney / Stable Diffusion / Flux / Bing Image Creator
    ⬇️
[4. Tạo Video / Chuyển động]
    ├── Cảnh hội thoại/nói chuyện: Dùng LivePortrait (Ghép ảnh + Audio lồng tiếng)
    └── Cảnh hành động/bối cảnh: Dùng Wan2.1 / LTX-Video (Image-to-Video)
    ⬇️
[5. Dựng & Cắt ghép hoàn thiện] ──► Dùng CapCut / Premiere ghép video, âm thanh và phụ đề
```
