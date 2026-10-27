import os
import json

proj_dir = r"d:\ThuNghiem\createVoice\auto_film_studio\projects\Giao_Dien_Than_Thanh_Ke_Tai_Dinh_Nghia_Thuc_Tai_6516\episodes\tap_01"
txt_file = os.path.join(proj_dir, "script_screenplay.txt")
json_file = os.path.join(proj_dir, "storyboard.json")

with open(txt_file, "r", encoding="utf-8") as f:
    text = f.read()

part_next = """
--------------------------------------------------------------------------------
CẢNH TIẾP NỐI X1 -> X2: ĐẠI ĐIỆN VÔ CỰC - BÊN TRONG THIÊN CẦU LÕI
[EXTREME WIDE ANGLE - ZERO GRAVITY SHOT]
Không gian không có phương hướng. Hàng tỷ mảnh gương vỡ lơ lửng phản chiếu hàng triệu thực tại khác nhau của Trái Đất qua các thế kỷ.
Đinh Hải bám vào một phiến đá bay, lồng ngực anh nhấp nháy ánh hào quang vàng kim của Root Kernel.
Maya lộn người giữa không trung, khẩu súng từ trường trong tay cô rít lên từng hồi cảnh báo nhiệt độ quá tải.

MAYA
(Gào lên qua mũ giáp liên lạc, tiếng thở dốc nặng nề)
"Hải! Nhìn lên trên đi! Đó không phải là lỗi hệ thống... mà là toàn bộ dữ liệu sinh mạng của nhân loại đang bị hút về cỗ máy kia!"

ĐINH HẢI
(Mắt đỏ ngầu, cánh tay phải chập điện xèo xèo)
"Lan... Su... Tôi cảm nhận được ý thức của họ! Họ đang bị giam cầm trong lõi xử lý trung tâm!"

[SFX: Tiếng kim loại biến dạng rợn người, tiếng sóng hạ âm bóp nghẹt lồng ngực.]

TỪ GIỮA TÂM THIÊN CẦU:
Kẻ Sắp Xếp (The Architect) từ từ bước ra từ một vết nứt không gian. Tấm gương trên mặt hắn xoay chuyển, phản chiếu hình ảnh người vợ đã khuất của Hải đang mỉm cười giơ tay vẫy chào.

KẺ SẮP XẾP
(Giọng nói biến điệu thành giọng của Lan - người vợ quá cố của Hải)
"Hải à... anh quay về với em đi... chỉ cần anh tắt lõi nguồn trong ngực anh... chúng ta sẽ bên nhau mãi mãi..."

Đinh Hải sững người, bàn tay đang nắm chặt đoản kiếm run lên bần bật. Nước mắt trào ra hòa vào máu trên khóe mắt.

MAYA
(Bắn một phát đạn từ trường phá tan ảo ảnh, thét lớn)
"HẢI! TỈNH LẠI ĐI! ĐÓ LÀ BẪY TÂM LÝ CỦA HỆ THỐNG! NẾU ANH TẮT NÓ, TOÀN BỘ TRÁI ĐẤT SẼ BỊ ĐỊNH DẠNG LẠI!"

Đinh Hải cắn chặt môi đến bật máu, ánh mắt bừng lên ngọn lửa căm hờn tột độ.

ĐINH HẢI
"MÀY DÁM DÙNG KÝ ỨC CỦA VỢ TAO ĐỂ THAO TÚNG TAO?!
HÔM NAY... TAO SẼ ĐẬP NÁT CÁI GIAO DIỆN NÀY!"

[ACTION SEQUENCE - 360 DEGREE DYNAMIC PANNING]
Đinh Hải lao vút lên như một tia chớp vàng kim, đâm thẳng thanh kiếm năng lượng vào lõi thiên cầu!
[SFX: Tiếng nổ long trời lở đất, không gian rạn nứt thành từng mảng kính khổng lồ!]

--------------------------------------------------------------------------------
CẢNH TIẾP NỐI X2 -> X3: THỰC TẠI SỤP ĐỔ VÀ KHOẢNH KHẮC CAO TRÀO CLIFFHANGER
[DUTCH TILT EXTREME CLOSE-UP]
Lõi thiên cầu nứt toác, bắn ra hàng ngàn luồng photon vàng kim và đỏ máu.
Kẻ Sắp Xếp lùi lại, mặt nạ gương của hắn nứt làm đôi, để lộ một vực thẳm đen ngòm đang rít gào.

KẺ SẮP XẾP
(Giọng máy biến dạng, rung chuyển cõi không gian)
"Ngươi nghĩ ngươi đã thắng? Đinh Hải... ngươi vừa mở phong ấn của Giao Thức Tuyệt Chủng!"

[SFX: Tiếng còi báo động khẩn cấp cấp vũ trụ rú lên, mặt đất dưới chân sụp đổ hoàn toàn.]

TỪ TẦNG TRỜI NEO-SAIGON:
Bầu trời đêm rách toạc. Con mắt đỏ khổng lồ giáng xuống chùm tia năng lượng hủy diệt bao trùm Trái Đất.
Đồng hồ đếm ngược trên võng mạc của Đinh Hải và Maya nhấp nháy: [00:00:10... 00:00:09...].
Đinh Hải giơ cao bàn tay rực sáng năng lượng Sáng Thế, hét lên trước khi màn hình chìm vào bóng tối...

[MÀN HÌNH TỐI SẦM - HẾT TẬP 1]
"""

full_content = text + "\n" + part_next

with open(txt_file, "w", encoding="utf-8") as f:
    f.write(full_content)

with open(json_file, "r", encoding="utf-8") as f:
    data = json.load(f)

data["full_screenplay"] = full_content

with open(json_file, "w", encoding="utf-8") as f:
    json.dump(data, f, ensure_ascii=False, indent=2)

print("Updated continuous script. Total length:", len(full_content), "chars")
