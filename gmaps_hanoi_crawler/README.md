# Tool Cào Tẩm Quất & Xoa Bóp Trị Liệu Hà Nội (Thuần File JSON)

Dự án chuyên dụng cào toàn bộ cơ sở **Tẩm quất, Xoa bóp bấm huyệt, Massage khiếm thị/người mù, Trị liệu cổ vai gáy, Giác hơi xông hơi, Dưỡng sinh đông y** trên địa bàn TP Hà Nội (lấy đầy đủ review thực tế của khách hàng, lưu thuần file JSON).

---

## 1. Cấu trúc thư mục

```
d:\tools\gmaps_hanoi_crawler\
├── crawl_tam_quat_hanoi.py    # Script cào tự động đa luồng (vừa quét vừa bóc tách review)
├── tam_quat_hanoi.json        # File dữ liệu JSON duy nhất (cho AI nạp / tìm kiếm)
└── README.md                  # Hướng dẫn sử dụng
```

---

## 2. Cách chạy cào toàn thành phố

Mở PowerShell và chạy:

```powershell
cd d:\tools\gmaps_hanoi_crawler
python crawl_tam_quat_hanoi.py
```

### Các tùy chọn tham số:
* **Tăng/giảm số luồng song song** (khuyên dùng 3-5 luồng để Google Maps không chặn và máy chạy mượt):
  ```powershell
  python crawl_tam_quat_hanoi.py --workers 4
  ```
* **Chỉ quét riêng 1 quận/huyện cụ thể:**
  ```powershell
  python crawl_tam_quat_hanoi.py --area "Cầu Giấy"
  python crawl_tam_quat_hanoi.py --area "Đống Đa"
  ```
* **Chế độ quét siêu tốc không lấy review** (chỉ lấy thông tin tên, địa chỉ, sđt, giờ mở cửa):
  ```powershell
  python crawl_tam_quat_hanoi.py --no-reviews
  ```
