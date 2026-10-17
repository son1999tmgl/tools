import os
import sys
import re
from datetime import datetime
from dotenv import load_dotenv
from telethon import TelegramClient, events
from excel_handler import ExcelHandler

# Tải cấu hình từ .env
load_dotenv()

API_ID = os.getenv("TELEGRAM_API_ID")
API_HASH = os.getenv("TELEGRAM_API_HASH")
PHONE = os.getenv("TELEGRAM_PHONE")

EXCEL_PATH = os.getenv("EXCEL_PATH", "data.xlsx")
EXCEL_SHEET_NAME = os.getenv("EXCEL_SHEET_NAME", "Messages")

# Danh sách từ khóa (tách bằng dấu phẩy)
raw_keywords = os.getenv("KEYWORDS", "")
KEYWORDS = [k.strip().lower() for k in raw_keywords.split(",") if k.strip()]

# Danh sách nhóm cần theo dõi (ID hoặc username, tách bằng dấu phẩy)
raw_targets = os.getenv("TARGET_GROUPS", "")
TARGET_GROUPS = [t.strip() for t in raw_targets.split(",") if t.strip()]

def validate_config():
    if not API_ID or not API_HASH:
        print("=" * 60)
        print("LỖI CẤU HÌNH: Chưa cấu hình TELEGRAM_API_ID hoặc TELEGRAM_API_HASH!")
        print("1. Hãy sao chép .env.example thành .env")
        print("2. Lấy API_ID và API_HASH từ https://my.telegram.org")
        print("3. Điền vào file .env rồi chạy lại.")
        print("=" * 60)
        sys.exit(1)

def check_keyword_match(text: str):
    """Kiểm tra tin nhắn có chứa từ khóa nào trong danh sách lọc không."""
    if not KEYWORDS:
        return True, "Tất cả (Không lọc)"
    
    text_lower = text.lower()
    for kw in KEYWORDS:
        # Tìm kiếm theo từ khóa hoặc cụm từ
        if kw in text_lower:
            return True, kw
    return False, ""

def build_message_link(chat, message_id: int) -> str:
    """Tạo đường dẫn trực tiếp tới tin nhắn Telegram."""
    if getattr(chat, 'username', None):
        return f"https://t.me/{chat.username}/{message_id}"
    elif getattr(chat, 'id', None):
        # Với nhóm kín (supergroup), ID dạng -100xxxxxxxxxx -> link là /c/xxxxxxxxxx
        chat_id_str = str(chat.id)
        if chat_id_str.startswith("-100"):
            clean_id = chat_id_str[4:]
            return f"https://t.me/c/{clean_id}/{message_id}"
    return ""

async def main():
    validate_config()

    print("\n--- KHỞI ĐỘNG TELEGRAM EXCEL COLLECTOR ---")
    print(f"[Cấu hình] File Excel: {EXCEL_PATH} (Sheet: {EXCEL_SHEET_NAME})")
    print(f"[Cấu hình] Từ khóa lọc ({len(KEYWORDS)}): {KEYWORDS if KEYWORDS else 'Lấy tất cả'}")
    print(f"[Cấu hình] Nhóm theo dõi ({len(TARGET_GROUPS)}): {TARGET_GROUPS if TARGET_GROUPS else 'Tất cả các nhóm bạn tham gia'}")

    excel = ExcelHandler(file_path=EXCEL_PATH, sheet_name=EXCEL_SHEET_NAME)

    # Khởi tạo client Telethon với session name là 'telegram_collector'
    client = TelegramClient("telegram_collector", int(API_ID), API_HASH)

    @client.on(events.NewMessage)
    async def handler(event):
        try:
            # Chỉ xử lý tin nhắn trong Group hoặc Channel
            if not (event.is_group or event.is_channel):
                return

            chat = await event.get_chat()
            chat_title = getattr(chat, 'title', 'Không tên')
            chat_id = str(chat.id)
            chat_username = getattr(chat, 'username', '')

            # Lọc theo danh sách nhóm nếu có chỉ định
            if TARGET_GROUPS:
                matched_target = False
                for target in TARGET_GROUPS:
                    if target == chat_id or (chat_username and f"@{chat_username.lower()}" == target.lower()):
                        matched_target = True
                        break
                    elif target.lower() in chat_title.lower():
                        matched_target = True
                        break
                if not matched_target:
                    return

            message_text = event.raw_text or ""
            if not message_text.strip():
                return

            # Kiểm tra từ khóa
            is_matched, matched_kw = check_keyword_match(message_text)
            if not is_matched:
                return

            # Thông tin người gửi
            sender = await event.get_sender()
            sender_name = ""
            sender_username = ""
            if sender:
                first = getattr(sender, 'first_name', '') or ''
                last = getattr(sender, 'last_name', '') or ''
                sender_name = f"{first} {last}".strip()
                sender_username = getattr(sender, 'username', '') or ''

            msg_link = build_message_link(chat, event.message.id)
            msg_date = event.message.date.astimezone() if event.message.date else datetime.now()

            print(f"\n[PHÁT HIỆN TIN PHÙ HỢP] Nhóm: '{chat_title}' | Khớp từ khóa: [{matched_kw}] | Người gửi: {sender_name}")
            
            # Ghi vào file Excel
            excel.append_message(
                group_name=chat_title,
                group_id=chat_id,
                sender_name=sender_name,
                username=sender_username,
                matched_keyword=matched_kw,
                text=message_text,
                message_link=msg_link,
                timestamp=msg_date
            )

        except Exception as e:
            print(f"[LỖI Xử lý tin nhắn] {e}")

    print("\nĐang kết nối tới Telegram...")
    await client.start(phone=PHONE)
    print(">> Đã kết nối thành công! Đang lắng nghe tin nhắn mới...")
    print(">> Nhấn Ctrl+C để dừng chương trình.\n")
    
    await client.run_until_disconnected()

if __name__ == "__main__":
    import asyncio
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("\nĐã dừng chương trình an toàn.")
