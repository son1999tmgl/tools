import os
from datetime import datetime
from openpyxl import Workbook, load_workbook
from openpyxl.styles import Font, PatternFill, Alignment

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
        - Dữ liệu luôn được chèn vào ĐẦU FILE (Dòng 2 - ngay dưới tiêu đề), đẩy các dòng cũ dần xuống dưới.
        - Kiểm tra ĐIỀU KIỆN VÀ (AND): (loại tài khoản == cur_acc VÀ gói/thời hạn == cur_plan VÀ người bán == cur_seller VÀ tên nhóm == cur_group):
          + Nếu trùng: Cập nhật dữ liệu (cột có tin mới thì đè, cột thiếu thì giữ cũ) và đưa dòng đó lên đầu bảng.
          + Nếu chưa có: Chèn dòng mới vào đầu bảng (dòng 2), đẩy dữ liệu cũ xuống.
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

            # Tìm dòng cũ thỏa mãn đồng thời cả 4 điều kiện
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
                # --- ĐÃ TỒN TẠI: LẤY DỮ LIỆU CŨ NẾU CỘT MỚI BỊ TRỐNG ---
                old_platform = ws.cell(row=target_row_idx, column=2).value
                old_acc_type = ws.cell(row=target_row_idx, column=3).value
                old_plan     = ws.cell(row=target_row_idx, column=4).value
                old_price_vnd = ws.cell(row=target_row_idx, column=5).value
                old_price_raw = ws.cell(row=target_row_idx, column=6).value
                old_warranty  = ws.cell(row=target_row_idx, column=7).value
                old_seller    = ws.cell(row=target_row_idx, column=8).value
                old_contact   = ws.cell(row=target_row_idx, column=9).value
                old_group     = ws.cell(row=target_row_idx, column=10).value

                final_platform = platform if platform else old_platform
                final_acc_type = account_type if account_type else old_acc_type
                final_plan     = plan if plan else old_plan
                
                # Giá bán VNĐ
                if price_vnd is not None and price_vnd > 0:
                    final_price_vnd = price_vnd
                else:
                    final_price_vnd = old_price_vnd

                final_price_raw = price_raw if price_raw else old_price_raw
                final_warranty  = warranty if warranty else old_warranty
                final_seller    = seller_name if seller_name else old_seller
                final_contact   = contact_info if contact_info else old_contact
                final_group     = group_name if group_name else old_group
                final_link      = message_link if message_link else ws.cell(row=target_row_idx, column=11).value
                final_text      = original_text if original_text else ws.cell(row=target_row_idx, column=12).value

                # Xóa dòng cũ ở vị trí cũ để đưa lên đầu bảng
                ws.delete_rows(target_row_idx, 1)
                status_result = "UPDATED"
            else:
                # --- CHƯA CÓ: TẠO MỚI ---
                final_platform = platform or "Khác"
                final_acc_type = account_type or "Tài khoản chung"
                final_plan     = plan or ""
                final_price_vnd = price_vnd
                final_price_raw = price_raw or ""
                final_warranty  = warranty or ""
                final_seller    = seller_name or ""
                final_contact   = contact_info or ""
                final_group     = group_name or ""
                final_link      = message_link or ""
                final_text      = original_text or ""
                status_result = "INSERTED"

            # CHÈN VÀO DÒNG 2 (ĐẦU BẢNG, NGAY DƯỚI TIÊU ĐỀ), ĐẨY DỮ LIỆU CŨ XUỐNG
            ws.insert_rows(2, 1)

            ws.cell(row=2, column=1, value=formatted_time).alignment = Alignment(horizontal="center")
            ws.cell(row=2, column=2, value=final_platform)
            ws.cell(row=2, column=3, value=final_acc_type)
            ws.cell(row=2, column=4, value=final_plan).alignment = Alignment(horizontal="center")

            # Cột 5: Giá bán VNĐ (định dạng số tiền)
            if isinstance(final_price_vnd, (int, float)) and final_price_vnd > 0:
                cell_p = ws.cell(row=2, column=5, value=final_price_vnd)
                cell_p.number_format = '#,##0 "₫"'
                cell_p.alignment = Alignment(horizontal="right")
            else:
                cell_p = ws.cell(row=2, column=5, value=final_price_raw or "Thương lượng")
                cell_p.alignment = Alignment(horizontal="center")

            ws.cell(row=2, column=6, value=final_price_raw or "")
            ws.cell(row=2, column=7, value=final_warranty or "")
            ws.cell(row=2, column=8, value=final_seller or "")
            ws.cell(row=2, column=9, value=final_contact or "")
            ws.cell(row=2, column=10, value=final_group or "")
            ws.cell(row=2, column=11, value=final_link or "")
            ws.cell(row=2, column=12, value=final_text or "")

            wb.save(self.file_path)
            return status_result

        except PermissionError:
            print(f"[CẢNH BÁO] File '{self.file_path}' đang mở trong Excel! Hãy đóng file lại để lưu dữ liệu!")
            return "ERROR_LOCKED"
        except Exception as e:
            print(f"[LỖI Excel] {e}")
            return "ERROR"
