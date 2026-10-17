import os
import sys
import asyncio
from datetime import datetime
from dotenv import load_dotenv
from telethon import TelegramClient, events
from excel_handler import ExcelHandler
from ai_extractor import AIExtractor

if sys.stdout.encoding != 'utf-8':
    try:
        sys.stdout.reconfigure(encoding='utf-8')
    except Exception:
        pass

load_dotenv()

API_ID = os.getenv("TELEGRAM_API_ID")
API_HASH = os.getenv("TELEGRAM_API_HASH")
PHONE = os.getenv("TELEGRAM_PHONE")
GEMINI_API_KEY = os.getenv("GEMINI_API_KEY")

EXCEL_PATH = os.getenv("EXCEL_PATH", "account_sales.xlsx")
EXCEL_SHEET_NAME = os.getenv("EXCEL_SHEET_NAME", "AccountSales")

# Nhóm cần quét (để trống để quét tất cả các nhóm tham gia)
raw_targets = os.getenv("TARGET_GROUPS", "")
TARGET_GROUPS = [t.strip() for t in raw_targets.split(",") if t.strip()]

def validate_config():
    if not API_ID or not API_HASH:
        print("[LỖI] Thiếu TELEGRAM_API_ID hoặc TELEGRAM_API_HASH trong file .env!")
        sys.exit(1)
    if not GEMINI_API_KEY:
        print("=" * 65)
        print("[LƯU Ý QUAN TRỌNG] Chưa cấu hình GEMINI_API_KEY trong file .env!")
        print("Để AI có thể tự động hiểu tiếng lóng, nhận diện tin bán acc và bóc giá:")
        print("1. Vào https://aistudio.google.com/apikey để lấy API Key miễn phí.")
        print("2. Thêm vào file .env dòng: GEMINI_API_KEY=AIzaSy...")
        print("=" * 65)
        sys.exit(1)

def build_message_link(chat, message_id: int) -> str:
    """Tạo link dẫn đến tin nhắn."""
    if getattr(chat, 'username', None):
        return f"https://t.me/{chat.username}/{message_id}"
    elif getattr(chat, 'id', None):
        chat_id_str = str(chat.id)
        if chat_id_str.startswith("-100"):
            clean_id = chat_id_str[4:]
            return f"https://t.me/c/{clean_id}/{message_id}"
    return ""

async def main():
    validate_config()

    print("\n" + "=" * 60)
    print("      TELEGRAM AI ACCOUNT COLLECTOR (SMART AGENT)      ")
    print("=" * 60)
    print(f"[*] File Excel đích: {EXCEL_PATH} (Sheet: {EXCEL_SHEET_NAME})")
    print(f"[*] Giám sát nhóm: {TARGET_GROUPS if TARGET_GROUPS else 'Tất cả các nhóm bạn tham gia'}")
    print("[*] Đang khởi tạo mô hình AI Gemini Flash...")

    ai = AIExtractor(api_key=GEMINI_API_KEY)
    excel = ExcelHandler(file_path=EXCEL_PATH, sheet_name=EXCEL_SHEET_NAME)

    client = TelegramClient("telegram_collector", int(API_ID), API_HASH)

    @client.on(events.NewMessage)
    async def message_handler(event):
        try:
            # Bỏ qua tin nhắn do chính tài khoản của bạn gửi đi
            if event.out:
                return

            chat = await event.get_chat()
            
            # Xử lý tên: Nhóm/Channel dùng title, Cá nhân/Bot dùng first_name + last_name
            chat_title = getattr(chat, 'title', None)
            if not chat_title:
                first = getattr(chat, 'first_name', '') or ''
                last = getattr(chat, 'last_name', '') or ''
                chat_title = f"{first} {last}".strip()
                if not chat_title:
                    chat_title = getattr(chat, 'username', '') or "Tin nhắn riêng"

            chat_id = str(chat.id)
            chat_username = getattr(chat, 'username', '') or ''

            # Lọc theo danh sách TARGET_GROUPS nếu có chỉ định
            if TARGET_GROUPS:
                matched_target = False
                for target in TARGET_GROUPS:
                    clean_t = target.strip().lower()
                    if not clean_t:
                        continue
                    # 1. So khớp ID (nhóm -100xxx, bot/user số dương hoặc âm)
                    if clean_t == chat_id:
                        matched_target = True
                        break
                    # 2. So khớp Username (@bot, @user, @channel)
                    if chat_username and (clean_t == f"@{chat_username.lower()}" or clean_t == chat_username.lower()):
                        matched_target = True
                        break
                    # 3. So khớp Tên hiển thị
                    if chat_title and clean_t in chat_title.lower():
                        matched_target = True
                        break
                if not matched_target:
                    return
            else:
                # Nếu TARGET_GROUPS để trống: chỉ theo dõi group/channel để tránh quét tin riêng tư gia đình/bạn bè
                if not (event.is_group or event.is_channel):
                    return

            text = event.raw_text or ""
            if len(text.strip()) < 5:
                return

            # Gọi AI phân tích ngữ nghĩa tin nhắn
            analysis = ai.analyze_message(text)
            if not analysis or not analysis.is_selling_account:
                # Bỏ qua tin nhắn không phải rao bán tài khoản
                return

            # Lấy thông tin người bán
            sender = await event.get_sender()
            sender_name = ""
            sender_username = ""
            if sender:
                first = getattr(sender, 'first_name', '') or ''
                last = getattr(sender, 'last_name', '') or ''
                sender_name = f"{first} {last}".strip()
                if getattr(sender, 'username', None):
                    sender_username = f"@{sender.username}"

            contact = analysis.contact_info if analysis.contact_info else sender_username
            msg_link = build_message_link(chat, event.message.id)
            msg_date = event.message.date.astimezone() if event.message.date else datetime.now()

            print(f"\n[PHÁT HIỆN TIN BÁN TÀI KHOẢN] Nhóm: '{chat_title}' | Người đăng: {sender_name} ({sender_username})")

            # Nếu có danh sách từng tài khoản cụ thể
            if analysis.offers:
                for offer in analysis.offers:
                    price_display = f"{offer.price_vnd:,}đ".replace(",", ".") if offer.price_vnd else (offer.price_raw or "Thương lượng")
                    status = excel.upsert_account_offer(
                        platform=offer.platform,
                        account_type=offer.account_type,
                        plan=offer.duration_or_plan,
                        price_vnd=offer.price_vnd,
                        price_raw=offer.price_raw,
                        warranty=offer.warranty_or_notes,
                        seller_name=sender_name,
                        contact_info=contact,
                        group_name=chat_title,
                        message_link=msg_link,
                        original_text=text,
                        timestamp=msg_date
                    )
                    action_label = "CẬP NHẬT DÒNG CŨ" if status == "UPDATED" else "THÊM DÒNG MỚI"
                    print(f"  -> [{action_label}] [{offer.platform}] {offer.account_type} | Gói: {offer.duration_or_plan} | Giá: {price_display}")
            else:
                status = excel.upsert_account_offer(
                    platform="Khác",
                    account_type="Tài khoản khác",
                    plan="",
                    price_vnd=None,
                    price_raw="Thương lượng",
                    warranty="",
                    seller_name=sender_name,
                    contact_info=contact,
                    group_name=chat_title,
                    message_link=msg_link,
                    original_text=text,
                    timestamp=msg_date
                )
                action_label = "CẬP NHẬT DÒNG CŨ" if status == "UPDATED" else "THÊM DÒNG MỚI"
                print(f"  -> [{action_label}] Tin rao bán chung: {chat_title}")

        except Exception as e:
            print(f"[Lỗi xử lý tin nhắn] {e}")

    print("\nĐang kết nối tài khoản Telegram...")
    phone_to_use = PHONE.strip() if PHONE and PHONE.strip() else None
    if phone_to_use:
        # Chuẩn hóa nếu bắt đầu bằng 0 -> chuyển sang mã quốc gia +84
        clean_p = phone_to_use.replace(" ", "").replace("-", "")
        if clean_p.startswith("0") and len(clean_p) >= 10:
            phone_to_use = "+84" + clean_p[1:]
        elif not clean_p.startswith("+") and clean_p.startswith("84"):
            phone_to_use = "+" + clean_p
        else:
            phone_to_use = clean_p

        print(f"[*] Sử dụng số điện thoại: {phone_to_use}")
        await client.start(phone=phone_to_use)
    else:
        await client.start()

    print(">> ĐÃ KẾT NỐI VÀ BẬT AI THÀNH CÔNG!\n")
    print("-" * 75)
    print("DANH SÁCH CÁC NHÓM / KÊNH ĐANG THAM GIA:")
    print("-" * 75)
    group_count = 0
    async for dialog in client.iter_dialogs():
        if dialog.is_group or dialog.is_channel:
            group_count += 1
            chat_id = str(dialog.id)
            title = dialog.title or "Không tên"
            username = f"@{dialog.entity.username}" if getattr(dialog.entity, 'username', None) else ""
            
            # Kiểm tra xem nhóm này có nằm trong danh sách quét không
            is_monitored = True
            if TARGET_GROUPS:
                is_monitored = any(
                    t == chat_id or (username and t.lower() == username.lower()) or t.lower() in title.lower()
                    for t in TARGET_GROUPS
                )
            
            status_tag = " -> [ĐANG THEO DÕI]" if is_monitored else ""
            print(f" {group_count:02d}. [{chat_id}] {title} {username}{status_tag}")
            
    print("-" * 75)
    print(f"[*] Tổng số: {group_count} nhóm/kênh.")
    print("[*] Đang trực chờ tin nhắn mới từ các nhóm... (Nhấn Ctrl+C để dừng)\n")

    await client.run_until_disconnected()

if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("\nĐã dừng chương trình an toàn.")
