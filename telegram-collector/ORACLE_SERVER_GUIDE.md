# SỔ TAY HƯỚNG DẪN QUẢN TRỊ SERVER ORACLE (24/24)

Tài liệu này lưu lại toàn bộ các câu lệnh cần thiết để kết nối, vận hành, kiểm tra và bảo trì bot Telegram Collector trên máy chủ Oracle Cloud.

---

## 1. Thông tin máy chủ
- **Public IP:** `161.33.149.146`
- **Username:** `ubuntu`
- **Hệ điều hành:** Ubuntu 24.04 LTS
- **SSH Private Key:** Nằm ngay trong thư mục này: `ssh-key-2026-10-18.key`
- **Thư mục dự án trên server:** `/home/ubuntu/tools`
- **Thư mục Telegram Collector:** `/home/ubuntu/tools/telegram-collector`

---

## 2. Câu lệnh kết nối SSH từ máy tính cá nhân

Mở **PowerShell** hoặc **Command Prompt (CMD)** trên máy tính và chạy:

```powershell
ssh -i "f:\tools\telegram-collector\ssh-key-2026-10-18.key" ubuntu@161.33.149.146
```

*(Nếu ở thư mục khác, chỉ cần trỏ đúng đường dẫn tới file `.key`)*

---

## 3. Các câu lệnh quản trị Bot chạy 24/24 (Systemd Service)

Sau khi đã SSH vào server:

### Xem trạng thái bot:
```bash
sudo systemctl status telegram-collector
```

### Xem Log thời gian thực (Xem bot đang bắt những tin nhắn nào):
```bash
journalctl -u telegram-collector -f
```
*(Nhấn `Ctrl + C` để thoát màn hình xem log)*

### Khởi động lại bot (Restart):
```bash
sudo systemctl restart telegram-collector
```

### Tạm dừng bot (Stop):
```bash
sudo systemctl stop telegram-collector
```

### Bật lại bot (Start):
```bash
sudo systemctl start telegram-collector
```

---

## 4. Chạy thủ công bằng tay (Dùng khi cần đăng nhập / nhập OTP Telegram)

Khi cần chạy tương tác trực tiếp để nhập mã OTP:

```bash
# 1. Tạm dừng service đang chạy ngầm (nếu có)
sudo systemctl stop telegram-collector

# 2. Vào thư mục và chạy bằng môi trường ảo Python
cd ~/tools/telegram-collector
./venv/bin/python main.py

# 3. Sau khi đăng nhập thành công, nhấn Ctrl + C và bật lại service chạy ngầm:
sudo systemctl start telegram-collector
```

---

## 5. Cơ chế Tự động Đồng bộ từ GitHub (Auto Git Pull)

Server đã được cài đặt Timer tự động kiểm tra GitHub nhánh `master` mỗi **1 phút**:
- Khi bạn push code mới lên GitHub nhánh `master`:
  ```powershell
  git push origin master
  ```
- Server sẽ **tự động phát hiện, pull code mới về và tự restart bot** mà bạn không cần phải làm gì thêm.

### Kiểm tra trạng thái bộ auto-update:
```bash
sudo systemctl status git-autoupdate.timer
```

### Chạy cập nhật thủ công ngay lập tức:
```bash
/bin/bash /home/ubuntu/auto_update.sh
```

---

## 6. Lấy file Excel dữ liệu về máy tính

Từ máy tính cá nhân (PowerShell), chạy lệnh sau để tải file Excel về máy:

```powershell
scp -i "f:\tools\telegram-collector\ssh-key-2026-10-18.key" ubuntu@161.33.149.146:~/tools/telegram-collector/account_sales.xlsx f:\tools\telegram-collector\
```
