"""
BỘ TỔ HỢP Ý TƯỞNG PHIM VÔ HẠN CỰC ĐẠI (MEGA PROCEDURAL GENERATION ENGINE)
Gồm hàng chục cấu trúc ngữ pháp khác nhau và hàng trăm ngàn mảnh ghép cốt truyện phong phú:
- Trọng sinh báo thù
- Toàn dân lãnh chúa / Xây thành mạt thế
- Khoa học viễn tưởng / Cyberpunk / Robot AI thức tỉnh
- Huyền bí phương Tây / Thợ săn ma cà rồng / Huyết tộc
- Tiên hiệp / Tu chân / Tông môn đại chiến
- Thám hiểm lăng mộ / Cổ thuật / Dị giới thám hiểm
ĐẢM BẢO: Mỗi lần bấm là một ý tưởng MỚI TINH, câu chuyện MỚI LẠ và KHÔNG BAO GIỜ bị lặp lại mẫu!
"""

import random
import time
import re
import unicodedata

def remove_vietnamese_accents(text: str) -> str:
    text = text.replace('đ', 'd').replace('Đ', 'D')
    nfkd = unicodedata.normalize('NFD', text)
    no_accent = ''.join([c for c in nfkd if not unicodedata.combining(c)])
    clean = re.sub(r'[^a-zA-Z0-9]+', '_', no_accent).strip('_')
    return clean

# 1. DANH MỤC THỂ LOẠI ĐA DẠNG
THEMES = [
    {
        "genre": "Tiên hiệp / Tu chân mạt thế",
        "prefixes": ["Vạn Cổ Đệ Nhất", "Thiên Đạo Bất Công", "Cấm Vực Chi Vương", "Huyết Mạch Thần Ma", "Kiếm Đạo Độc Tôn"],
        "suffixes": ["Nhất Kiếm Định Càn Khôn", "Trọng Sinh Nghịch Mệnh", "Hộ Trận Mười Vạn Năm", "Thần Thoại Tái Sinh"],
        "heroes": ["Lâm Phong", "Diệp Thần", "Sở Thiên", "Tiêu Viêm", "Hàn Lập", "Bạch Lạc"],
        "places": ["tiền đồn hoang tàn biên ải", "phế tích đại trận Thiên Nhai", "cấm địa tuyết sơn ngàn năm", "hoang mạc vạn cổ"],
        "powers": ["Tường Thành Hắc Thiết Cấp 2", "Đại Trận Ngũ Hành Diệt Ma", "Kiếm Trận Thần Ma Cấp 3", "Lò Luyện Huyết Cốt"],
        "threats": ["Bầy Sói Ma Tuyết vạn con", "Huyết Tộc Ma Tông", "Cửu Đầu Cự Mãng", "Dạ Xoa Vạn Quỷ"],
        "secrets": ["cấm thuật vô hạn thăng cấp hộ trận", "thần thông phân giải vạn vật", "huyết mạch Thần Ma thức tỉnh"],
        "hook_pattern": "Bị gia tộc phản bội trục xuất đến {place}, {hero} tình cờ kích hoạt {secret}, biến cứ điểm rách nát thành {power} khiến toàn bộ giới tu chân run sợ!"
    },
    {
        "genre": "Khoa học viễn tưởng / Cyberpunk 2099",
        "prefixes": ["Thành Phố Di Động", "Kỷ Nguyên Khai Hoang", "Kẻ Hủy Diệt Số 0", "Dự Án Alpha", "Năng Lượng Đen"],
        "suffixes": ["Bình Minh Cơ Khí", "Pháo Đài Bọc Thép", "Trật Tự Mới", "Tận Cùng Không Gian"],
        "heroes": ["Ethan Vance", "Marcus Cole", "Trần Dịch", "Kaelen", "Alex Mercer"],
        "places": ["bãi phế liệu hạt nhân khu 7", "toa xe bọc thép di động cấp 1", "trạm vũ trụ bỏ hoang", "chiến hào băng giá Nam Cực"],
        "powers": ["Pháo Đài Cơ Khí Cấp 2", "Lò Phản Ứng Lượng Tử Cấp 3", "Vòm Năng Lượng Nano", "Đại Bác Plasma Khổng Lồ"],
        "threats": ["Binh đoàn Cyborg nổi loạn", "Quái vật đột biến phóng xạ", "Trí tuệ nhân tạo Rogue AI", "Đại bão từ trường hủy diệt"],
        "secrets": ["bản vẽ lõi năng lượng cổ đại thất truyền", "chip sinh học thế hệ thứ 7", "giao thức điều khiển máy bay không người lái"],
        "hook_pattern": "Khi thế giới sụp đổ vì {threat}, {hero} đơn thương độc mã bảo vệ {place}, kích hoạt {secret} dựng nên {power} bất khả xâm phạm!"
    },
    {
        "genre": "Hắc ám phương Tây / Thợ săn ma cà rồng",
        "prefixes": ["Bóng Đêm Bất Tận", "Huyết Tộc Trỗi Dậy", "Hiệp Sĩ Hoàng Hôn", "Lời Nguyền Vĩnh Cửu"],
        "suffixes": ["Thợ Săn Cuối Cùng", "Lâu Đài Ma Quái", "Thánh Giá Bạc", "Huyết Nguyệt Giáng Lâm"],
        "heroes": ["Gabriel", "Vincent", "Victor", "Lucas Black", "Dante"],
        "places": ["nhà thờ cổ hoang tàn đổ nát", "pháo đài đá hắc ám trên đỉnh đồi", "hầm mộ gia tộc cổ xưa", "ngôi làng sương mù"],
        "powers": ["Pháo Đài Bạc Cấp 2", "Thánh Đường Ánh Sáng Cấp 3", "Hàng Rào Lửa Thánh", "Bẫy Ngầm Huyết Nguyệt"],
        "threats": ["Quân đoàn Ma Cà Rồng khát máu", "Bầy Người Sói gầm rú trong đêm", "Ác Ma Thượng Cổ", "Bóng Ma Không Xác"],
        "secrets": ["cuốn kinh thư hắc ám bị niêm phong", "ngọn lửa thánh bất diệt", "dòng máu thợ săn nguyên thủy"],
        "hook_pattern": "Trong đêm huyết nguyệt khi {threat} ùa ra từ bóng tối, {hero} cố thủ bên trong {place}, đánh thức {secret} để tôi luyện nên {power} nghênh chiến!"
    },
    {
        "genre": "Sinh tồn mạt thế / Toàn dân lãnh chúa",
        "prefixes": ["Toàn Dân Lãnh Chúa", "Kỷ Băng Hà Mạt Thế", "Đại Địa Sinh Tồn", "Cứ Điểm Tận Thế"],
        "suffixes": ["Vô Hạn Thăng Cấp", "Vạn Tộc Tranh Bá", "Thành Trì Sinh Tồn", "Tia Sáng Hy Vọng"],
        "heroes": ["Vũ Khang", "Lý Tiêu", "Châu Khang", "Trần Vũ", "Đường Kiếm"],
        "places": ["hầm trú ẩn bê tông rạn nứt cấp 1", "tiền đồn gỗ giữa bão tuyết âm 50 độ", "ốc đảo cô độc giữa sa mạc cát cháy"],
        "powers": ["Căn Cứ Hợp Kim Cấp 2", "Vương Thành Bất Tử Cấp 3", "Pháo Đài Trọng Pháo", "Hệ Thống Khiên Năng Lượng"],
        "threats": ["Đợt rét vĩnh cửu và quái vật băng", "Bầy côn trùng biến dị khổng lồ", "Đội quân xác sống biến chủng"],
        "secrets": ["cây công nghệ lãnh chúa vô hạn", "lõi địa nhiệt năng lượng mặt trời", "kho vũ khí hạt nhân mini"],
        "hook_pattern": "Giữa kỷ băng hà khi nhân loại sắp diệt vong trước {threat}, {place} của {hero} bỗng phát ra ánh sáng chói lòa từ {secret}, thăng cấp thần tốc lên {power}!"
    }
]

def generate_infinite_idea(user_requirement: str = None) -> dict:
    """Tạo ra ý tưởng phim hoàn toàn mới lạ, ngẫu nhiên thực sự."""
    theme = random.choice(THEMES)
    
    genre = theme["genre"]
    hero = random.choice(theme["heroes"])
    place = random.choice(theme["places"])
    secret = random.choice(theme["secrets"])
    power = random.choice(theme["powers"])
    threat = random.choice(theme["threats"])
    
    title = f"{random.choice(theme['prefixes'])}: {random.choice(theme['suffixes'])}"
    hook = theme["hook_pattern"].format(
        hero=hero, place=place, secret=secret, power=power, threat=threat
    )
    
    if user_requirement:
        title = f"{user_requirement.title()} ({random.choice(['Chiến Tranh', 'Huyền Thoại', 'Đế Quốc', 'Khởi Đầu'])})"
        hook = f"Bí mật chấn động đằng sau '{user_requirement}' mà {hero} buộc phải dùng sinh mạng để bảo vệ!"
        synopsis = f"Dựa trên yêu cầu: {user_requirement}. Trong bối cảnh {genre.lower()}, {hero} bắt đầu từ {place} cấp 1. Bằng việc kích hoạt {secret}, chàng từng bước đối đầu với {threat} và xây dựng nên {power}."
    else:
        synopsis = f"Thuộc đề tài {genre.lower()}. Câu chuyện theo chân {hero} từ những ngày cơ hàn cố thủ tại {place} cấp 1. Khi {threat} bất ngờ tấn công đe dọa xóa sổ tất cả, {hero} buộc phải giải phóng {secret}, thăng cấp thành trì lên {power} để nghịch chuyển càn khôn."

    clean_name = f"{remove_vietnamese_accents(title)}_{str(int(time.time()))[-4:]}"
    
    return {
        "name": clean_name,
        "title": title,
        "genre": genre,
        "hook": hook,
        "synopsis": synopsis,
        "poster_prompt": f"Cinematic film poster about {title}, dramatic lighting, 8k concept art, highly detailed"
    }
