import os
from datetime import datetime
from openpyxl import Workbook, load_workbook
from openpyxl.styles import Font, PatternFill, Alignment

DEFAULT_HEADERS = [
    "Thời gian",
    "Tên nhóm / Kênh",
    "ID Nhóm",
    "Người gửi",
    "Username",
    "Từ khóa khớp",
    "Nội dung",
    "Link tin nhắn"
]

class ExcelHandler:
    def __init__(self, file_path: str = "data.xlsx", sheet_name: str = "Messages"):
        self.file_path = file_path
        self.sheet_name = sheet_name
        self._ensure_file_exists()

    def _ensure_file_exists(self):
        """Khởi tạo file Excel nếu chưa tồn tại với header định dạng đẹp."""
        if not os.path.exists(self.file_path):
            wb = Workbook()
            ws = wb.active
            ws.title = self.sheet_name
            ws.append(DEFAULT_HEADERS)

            # Styling header
            header_font = Font(name="Calibri", size=11, bold=True, color="FFFFFF")
            header_fill = PatternFill(start_color="1F497D", end_color="1F497D", fill_type="solid")
            for col_num in range(1, len(DEFAULT_HEADERS) + 1):
                cell = ws.cell(row=1, column=col_num)
                cell.font = header_font
                cell.fill = header_fill
                cell.alignment = Alignment(horizontal="center", vertical="center")

            # Độ rộng cột mặc định
            col_widths = {
                "A": 20, # Thời gian
                "B": 25, # Tên nhóm
                "C": 16, # ID Nhóm
                "D": 22, # Người gửi
                "E": 18, # Username
                "F": 18, # Từ khóa khớp
                "G": 50, # Nội dung
                "H": 35  # Link tin nhắn
            }
            for col, width in col_widths.items():
                ws.column_dimensions[col].width = width

            wb.save(self.file_path)
            print(f"[ExcelHandler] Đã tạo file Excel mẫu: {self.file_path}")

    def append_message(self, group_name: str, group_id: str, sender_name: str,
                       username: str, matched_keyword: str, text: str,
                       message_link: str, timestamp: datetime = None) -> bool:
        """Ghi thêm một dòng tin nhắn vào file Excel."""
        if timestamp is None:
            timestamp = datetime.now()

        row_data = [
            timestamp.strftime("%Y-%m-%d %H:%M:%S"),
            group_name or "",
            str(group_id),
            sender_name or "",
            f"@{username}" if username else "",
            matched_keyword or "",
            text or "",
            message_link or ""
        ]

        try:
            wb = load_workbook(self.file_path)
            if self.sheet_name in wb.sheetnames:
                ws = wb[self.sheet_name]
            else:
                ws = wb.create_sheet(self.sheet_name)
                ws.append(DEFAULT_HEADERS)

            ws.append(row_data)

            # Căn lề cho dòng mới
            current_row = ws.max_row
            ws.cell(row=current_row, column=1).alignment = Alignment(horizontal="center")
            ws.cell(row=current_row, column=3).alignment = Alignment(horizontal="center")
            ws.cell(row=current_row, column=6).alignment = Alignment(horizontal="center")

            wb.save(self.file_path)
            print(f"[ExcelHandler] +1 dòng đã thêm: [{group_name}] - {matched_keyword}")
            return True
        except PermissionError:
            print(f"[CẢNH BÁO] Không thể lưu file Excel '{self.file_path}' do file đang được mở trong ứng dụng khác (Excel). Hãy đóng file lại để lưu dữ liệu!")
            return False
        except Exception as e:
            print(f"[LỖI Excel] {e}")
            return False
