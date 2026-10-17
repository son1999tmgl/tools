import os
import sys
import asyncio
from dotenv import load_dotenv
from telethon import TelegramClient

if sys.stdout.encoding != 'utf-8':
    try:
        sys.stdout.reconfigure(encoding='utf-8')
    except Exception:
        pass

load_dotenv()

API_ID = os.getenv("TELEGRAM_API_ID")
API_HASH = os.getenv("TELEGRAM_API_HASH")
PHONE = os.getenv("TELEGRAM_PHONE")

async def main():
    if not API_ID or not API_HASH:
        print("[LỖI] Thiếu TELEGRAM_API_ID hoặc TELEGRAM_API_HASH trong file .env!")
        return

    client = TelegramClient("telegram_collector", int(API_ID), API_HASH)
    
    phone_to_use = PHONE.strip() if PHONE and PHONE.strip() else None
    if phone_to_use:
        clean_p = phone_to_use.replace(" ", "").replace("-", "")
        if clean_p.startswith("0") and len(clean_p) >= 10:
            phone_to_use = "+84" + clean_p[1:]
        elif not clean_p.startswith("+") and clean_p.startswith("84"):
            phone_to_use = "+" + clean_p
        else:
            phone_to_use = clean_p
        await client.start(phone=phone_to_use)
    else:
        await client.start()

    print("\n" + "=" * 80)
    print("                 DANH SÁCH TẤT CẢ CÁC NHÓM / KÊNH BẠN ĐÃ THAM GIA               ")
    print("=" * 80)
    print(f"{'STT':<5} | {'Tên nhóm / Kênh':<35} | {'ID Nhóm':<16} | {'Username':<15}")
    print("-" * 80)

    count = 0
    groups_data = []
    async for dialog in client.iter_dialogs():
        if dialog.is_group or dialog.is_channel:
            count += 1
            chat_id = str(dialog.id)
            title = dialog.title or "Không tên"
            # Giới hạn độ dài tên để bảng hiển thị thẳng hàng
            display_title = (title[:32] + "...") if len(title) > 35 else title
            username = f"@{dialog.entity.username}" if getattr(dialog.entity, 'username', None) else "(Nhóm kín)"

            print(f"{count:<5} | {display_title:<35} | {chat_id:<16} | {username:<15}")
            groups_data.append((chat_id, title, username))

    print("-" * 80)
    print(f"Tổng cộng: {count} nhóm/kênh.")
    print("\n[MẸO]: Nếu muốn tool chỉ quét một vài nhóm cụ thể:")
    print("Mở file .env và dán ID Nhóm hoặc Username vào dòng TARGET_GROUPS:")
    print("Ví dụ: TARGET_GROUPS=-100123456789,@ten_nhom\n")

if __name__ == "__main__":
    asyncio.run(main())
