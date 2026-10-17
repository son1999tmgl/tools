import os
from datetime import datetime
from openpyxl import Workbook, load_workbook
from openpyxl.styles import Font, PatternFill, Alignment, numbers

DEFAULT_HEADERS = [
    "Thời gian cập nhật",   # Col 1 (A)
    "Tên TK bán",           # Col 2 (B) - VD: Gemini, Netflix, ChatGPT
    "Loại tài khoản",       # Col 3 (C) - VD: Gemini Pro, Gemini Ultra
    "Gói / Thời hạn",       # Col 4 (D) - VD: 1 tháng, 1 năm
    "Giá bán (VNĐ)",        # Col 5 (E) - Số nguyên quy đổi (35,000)
    "Giá gốc / Mô tả",      # Col 6 (F) - VD: 35 cành, thương lượng
    "Bảo hành / Ghi chú",   # Col 7 (G)
    "Người bán",            # Col 8 (H)
    "Username / Liên hệ",   # Col 9 (I)
    "Tên nhóm",             # Col 10 (J)
    "Link tin nhắn",        # Col 11 (K)
    "Nội dung gốc"          # Col 12 (L)
]

def _norm(val) -> str:
    """Chuẩn hóa chuỗi để so sánh không phân biệt hoa thường và khoảng trắng."""
    return str(val or "").strip().lower()

class ExcelHandler:
    def __init__(self, file_path: str = "account_sales.xlsx", sheet_name: str = "AccountSales"):
        self.file_path = file_path
        self.sheet_name = sheet_name
        self._ensure_file_exists()

    def _ensure_file_exists(self):
        """Khởi tạo file Excel nếu chưa có với header chuẩn."""
        if not os.path.exists(self.file_path):
            wb = Workbook()
            ws = wb.active
            ws.title = self.sheet_name
            ws.append(DEFAULT_HEADERS)

            # Styling header
            header_font = Font(name="Segoe UI", size=11, bold=True, color="FFFFFF")
            header_fill = PatternFill(start_color="1F497D", end_color="1F497D", fill_type="solid")
            for col_num in range(1, len(DEFAULT_HEADERS) + 1):
                cell = ws.cell(row=1, column=col_num)
                cell.font = header_font
                cell.fill = header_fill
                cell.alignment = Alignment(horizontal="center", vertical="center")

            # Độ rộng từng cột
            col_widths = {
                "A": 20, # Thời gian cập nhật
                "B": 16, # Tên TK bán (Platform)
                "C": 22, # Loại tài khoản
                "D": 18, # Gói / Thời hạn
                "E": 18, # Giá bán (VNĐ)
                "F": 18, # Giá gốc / Mô tả
                "G": 28, # Bảo hành / Ghi chú
                "H": 20, # Người bán
                "I": 22, # Username / Liên hệ
                "J": 25, # Tên nhóm
                "K": 35, # Link tin nhắn
                "J": 25,
                "L": 50  # Nội dung gốc
            }
            for col, width in col_widths.items():
                ws.column_dimensions[col].width = width

            wb.save(self.file_path)
            print(f"[ExcelHandler] Đã tạo file Excel mới: {self.file_path}")

    def upsert_account_offer(self, platform: str, account_type: str, plan: str,
                             price_vnd: int | None, price_raw: str, warranty: str,
                             seller_name: str, contact_info: str, group_name: str,
                             message_link: str, original_text: str,
                             timestamp: datetime = None) -> str:
        """
        Ghi hoặc cập nhật bản ghi vào file Excel:
        - Nếu trùng (loại tài khoản; gói/thời hạn; người bán; tên nhóm) -> cập nhật lại dòng cũ.
          (Cột nào có dữ liệu mới thì cập nhật, cột nào trống thì giữ nguyên dữ liệu cũ).
        - Nếu chưa có -> thêm dòng mới.
        """
        if timestamp is None:
            timestamp = datetime.now()

        formatted_time = timestamp.strftime("%Y-%m-%d %H:%M:%S")

        try:
            wb = load_workbook(self.file_path)
            if self.sheet_name in wb.sheetnames:
                ws = wb[self.sheet_name]
            else:
                ws = wb.create_sheet(self.sheet_name)
                ws.append(DEFAULT_HEADERS)

            # Tìm kiếm dòng cũ thỏa mãn: (Loại tài khoản, Gói/thời hạn, Người bán, Tên nhóm)
            # Col 3 (C): Loại tài khoản
            # Col 4 (D): Gói / Thời hạn
            # Col 8 (H): Người bán
            # Col 10 (J): Tên nhóm
            target_row_idx = None
            for row_idx in range(2, ws.max_row + 1):
                cur_acc_type = _norm(ws.cell(row=row_idx, column=3).value)
                cur_plan     = _norm(ws.cell(row=row_idx, column=4).value)
                cur_seller   = _norm(ws.cell(row=row_idx, column=8).value)
                cur_group    = _norm(ws.cell(row=row_idx, column=10).value)

                if (cur_acc_type == _norm(account_type) and
                    cur_plan     == _norm(plan) and
                    cur_seller   == _norm(seller_name) and
                    cur_group    == _norm(group_name)):
                    target_row_idx = row_idx
                    break

            if target_row_idx is not None:
                # --- CẬP NHẬT DÒNG CŨ (UPDATE) ---
                # 1. Thời gian cập nhật: luôn cập nhật mới nhất
                ws.cell(row=target_row_idx, column=1, value=formatted_time)

                # 2. Tên TK bán (Platform): nếu có mới thì cập nhật
                if platform:
                    ws.cell(row=target_row_idx, column=2, value=platform)

                # 3. Loại tài khoản: cập nhật nếu có
                if account_type:
                    ws.cell(row=target_row_idx, column=3, value=account_type)

                # 4. Gói / thời hạn: cập nhật nếu có
                if plan:
                    ws.cell(row=target_row_idx, column=4, value=plan)

                # 5. Giá bán VNĐ (Col E):
                if price_vnd is not None and price_vnd > 0:
                    cell_price = ws.cell(row=target_row_idx, column=5, value=price_vnd)
                    cell_price.number_format = '#,##0 "₫"'
                elif not ws.cell(row=target_row_idx, column=5).value:
                    ws.cell(row=target_row_idx, column=5, value="Thương lượng")

                # 6. Giá gốc / Mô tả (Col F):
                if price_raw:
                    ws.cell(row=target_row_idx, column=6, value=price_raw)

                # 7. Bảo hành / Ghi chú (Col G):
                if warranty:
                    ws.cell(row=target_row_idx, column=7, value=warranty)

                # 8. Người bán: giữ/cập nhật
                if seller_name:
                    ws.cell(row=target_row_idx, column=8, value=seller_name)

                # 9. Username / Liên hệ (Col I):
                if contact_info:
                    ws.cell(row=target_row_idx, column=9, value=contact_info)

                # 10. Tên nhóm: giữ
                if group_name:
                    ws.cell(row=target_row_idx, column=10, value=group_name)

                # 11. Link tin nhắn: luôn cập nhật link mới nhất
                if message_link:
                    ws.cell(row=target_row_idx, column=11, value=message_link)

                # 12. Nội dung gốc: luôn cập nhật tin mới nhất
                if original_text:
                    ws.cell(row=target_row_idx, column=12, value=original_text)

                wb.save(self.file_path)
                return "UPDATED"

            else:
                # --- THÊM DÒNG MỚI (INSERT) ---
                new_row_idx = ws.max_row + 1

                ws.cell(row=new_row_idx, column=1, value=formatted_time).alignment = Alignment(horizontal="center")
                ws.cell(row=new_row_idx, column=2, value=platform or "Khác")
                ws.cell(row=new_row_idx, column=3, value=account_type or "Tài khoản chung")
                ws.cell(row=new_row_idx, column=4, value=plan or "").alignment = Alignment(horizontal="center")

                # Giá bán số nguyên (VND)
                if price_vnd is not None and price_vnd > 0:
                    c_price = ws.cell(row=new_row_idx, column=5, value=price_vnd)
                    c_price.number_format = '#,##0 "₫"'
                    c_price.alignment = Alignment(horizontal="right")
                else:
                    ws.cell(row=new_row_idx, column=5, value=price_raw or "Thương lượng").alignment = Alignment(horizontal="center")

                ws.cell(row=new_row_idx, column=6, value=price_raw or "")
                ws.cell(row=new_row_idx, column=7, value=warranty or "")
                ws.cell(row=new_row_idx, column=8, value=seller_name or "")
                ws.cell(row=new_row_idx, column=9, value=contact_info or "")
                ws.cell(row=new_row_idx, column=10, value=group_name or "")
                ws.cell(row=new_row_idx, column=11, value=message_link or "")
                ws.cell(row=new_row_idx, column=12, value=original_text or "")

                wb.save(self.file_path)
                return "INSERTED"

        except PermissionError:
            print(f"[CẢNH BÁO] File '{self.file_path}' đang mở trong Excel! Hãy đóng file lại để lưu dữ liệu!")
            return "ERROR_LOCKED"
        except Exception as e:
            print(f"[LỖI Excel] {e}")
            return "ERROR"
