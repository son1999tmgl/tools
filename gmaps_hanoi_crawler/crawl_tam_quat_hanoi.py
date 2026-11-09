import os
import sys
import time
import re
import json
import urllib.parse
import argparse
import asyncio
from playwright.async_api import async_playwright

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
JSON_FILE = os.path.join(BASE_DIR, "tam_quat_hanoi.json")

# 40+ khu vực và phường trọng điểm tại Hà Nội
AREAS = [
    # Cầu Giấy
    "Cầu Giấy", "Dịch Vọng Cầu Giấy", "Trung Hòa Cầu Giấy", "Nghĩa Tân Cầu Giấy", "Mai Dịch Cầu Giấy",
    # Đống Đa
    "Đống Đa", "Láng Hạ Đống Đa", "Ô Chợ Dừa Đống Đa", "Khâm Thiên Đống Đa", "Kim Liên Đống Đa",
    # Ba Đình
    "Ba Đình", "Kim Mã Ba Đình", "Đội Cấn Ba Đình", "Giảng Võ Ba Đình", "Ngọc Khánh Ba Đình",
    # Hoàn Kiếm
    "Hoàn Kiếm", "Phố Cổ Hoàn Kiếm", "Hàng Bông Hoàn Kiếm",
    # Hai Bà Trưng
    "Hai Bà Trưng", "Bạch Mai Hai Bà Trưng", "Minh Khai Hai Bà Trưng", "Trương Định Hai Bà Trưng",
    # Thanh Xuân
    "Thanh Xuân", "Khương Trung Thanh Xuân", "Nhân Chính Thanh Xuân", "Kim Giang Thanh Xuân",
    # Tây Hồ
    "Tây Hồ", "Thụy Khuê Tây Hồ", "Xuân La Tây Hồ", "Quảng An Tây Hồ",
    # Nam Từ Liêm
    "Nam Từ Liêm", "Mỹ Đình Nam Từ Liêm", "Mễ Trì Nam Từ Liêm", "Trung Văn Nam Từ Liêm",
    # Bắc Từ Liêm
    "Bắc Từ Liêm", "Cổ Nhuế Bắc Từ Liêm", "Xuân Đỉnh Bắc Từ Liêm",
    # Hà Đông
    "Hà Đông", "Văn Quán Hà Đông", "Mộ Lao Hà Đông", "Quang Trung Hà Đông",
    # Hoàng Mai
    "Hoàng Mai", "Linh Đàm Hoàng Mai", "Định Công Hoàng Mai", "Giáp Bát Hoàng Mai",
    # Long Biên
    "Long Biên", "Ngọc Lâm Long Biên", "Bồ Đề Long Biên",
    # Ngoại thành
    "Thanh Trì", "Gia Lâm", "Đông Anh", "Hoài Đức"
]

# Từ khóa chuyên biệt cho tẩm quất, xoa bóp, trị liệu
KEYWORDS = [
    "tẩm quất",
    "tẩm quất người mù",
    "tẩm quất khiếm thị",
    "tẩm quất cổ truyền",
    "xoa bóp bấm huyệt",
    "massage khiếm thị",
    "massage trị liệu",
    "giác hơi xông hơi",
    "trị liệu cổ vai gáy",
    "dưỡng sinh đông y",
    "phục hồi chức năng xoa bóp"
]

all_data_memory = []
total_reviews_count = 0

def init_storage():
    global all_data_memory
    seen_ids = set()
    if os.path.exists(JSON_FILE):
        try:
            with open(JSON_FILE, 'r', encoding='utf-8') as f:
                all_data_memory = json.load(f)
                for item in all_data_memory:
                    key = item.get("place_id") or item.get("url") or item.get("name")
                    seen_ids.add(key)
            print(f"[Khởi tạo] Đã nạp {len(all_data_memory)} địa điểm sẵn có trong file JSON.", flush=True)
        except Exception as e:
            print(f"[Khởi tạo] Lưu ý: {e}", flush=True)
            all_data_memory = []
    else:
        all_data_memory = []
        with open(JSON_FILE, 'w', encoding='utf-8') as f:
            json.dump([], f, ensure_ascii=False, indent=2)
    return seen_ids

def save_single_item_json(item):
    global all_data_memory, total_reviews_count
    all_data_memory.append(item)
    total_reviews_count += len(item.get("reviews", []))
    try:
        with open(JSON_FILE, 'w', encoding='utf-8') as f:
            json.dump(all_data_memory, f, ensure_ascii=False, indent=2)
    except Exception as e:
        print(f"[Lỗi ghi JSON] {e}", flush=True)

async def parse_card_info(card_el, link_el):
    raw_text = await card_el.inner_text()
    lines = [l.strip() for l in raw_text.strip().split('\n') if l.strip()]
    href = await link_el.get_attribute("href") or ""
    aria_name = await link_el.get_attribute("aria-label") or ""

    name = aria_name or (lines[0] if lines else "")
    rating = ""
    reviews_count = ""
    category = ""
    address = ""
    phone = ""
    hours = ""
    lat = ""
    lng = ""
    place_id = ""

    coord_m = re.search(r'!3d([0-9.]+)!4d([0-9.]+)', href)
    if coord_m:
        lat, lng = coord_m.group(1), coord_m.group(2)

    id_m = re.search(r'!1s(0x[0-9a-fA-F]+:0x[0-9a-fA-F]+)', href)
    if id_m:
        place_id = id_m.group(1)

    labels = await card_el.locator('[aria-label]').all()
    for el in labels:
        lbl = await el.get_attribute("aria-label") or ""
        s_m = re.search(r'([1-5][,\.][0-9])\s*stars?', lbl, re.IGNORECASE)
        if not s_m:
            s_m = re.search(r'([1-5][,\.][0-9])\s*sao', lbl, re.IGNORECASE)
        if s_m and not rating:
            rating = s_m.group(1).replace(',', '.')

        r_m = re.search(r'([0-9,.]+)\s*(?:bài )?đánh giá', lbl, re.IGNORECASE)
        if not r_m:
            r_m = re.search(r'([0-9,.]+)\s*reviews?', lbl, re.IGNORECASE)
        if r_m and not reviews_count:
            reviews_count = r_m.group(1).replace(',', '').replace('.', '')

    for line in lines:
        if not rating:
            r_m = re.search(r'\b([1-5][,\.][0-9])\b(?:\s*\(([0-9,.]+)\))?', line)
            if r_m:
                rating = r_m.group(1).replace(',', '.')
                if r_m.group(2) and not reviews_count:
                    reviews_count = r_m.group(2).replace(',', '').replace('.', '')

        p_m = re.search(r'(\+?84[\s\d.-]{8,15}|0[235789][\s\d.-]{8,12})', line)
        if p_m and not phone:
            phone = p_m.group(1).strip()

        if '·' in line:
            parts = [p.strip() for p in line.split('·')]
            first = parts[0].lower()
            if any(k in first for k in ['open', 'closed', 'mở', 'đóng', '24 hours', '24 giờ']):
                if not hours:
                    hours = parts[0]
            elif not category:
                category = parts[0]
                if len(parts) > 1 and not address:
                    addr_candidate = parts[1]
                    if not addr_candidate.startswith('+84') and not addr_candidate.startswith('0'):
                        address = addr_candidate

    return {
        "place_id": place_id,
        "name": name,
        "category": category or "Tẩm quất / Xoa bóp trị liệu",
        "rating": rating,
        "reviews_count": reviews_count,
        "phone": phone,
        "address": address,
        "hours": hours,
        "latitude": lat,
        "longitude": lng,
        "url": href
    }

async def fetch_reviews_from_url(page, place_url, max_reviews=0):
    """
    Mở link trực tiếp để lấy đánh giá thực tế của khách.
    max_reviews = 0 nghĩa là KHÔNG GIỚI HẠN (Lấy toàn bộ tất cả reviews có chữ).
    """
    reviews = []
    try:
        await page.goto(place_url, timeout=20000)
        await page.wait_for_timeout(2500)

        # 1. Tìm và bấm tab Bài đánh giá
        all_tabs = await page.locator('button[role="tab"]').all()
        clicked = False
        for t in all_tabs:
            txt = (await t.inner_text()).lower()
            if any(k in txt for k in ['đánh giá', 'review']):
                await t.click()
                clicked = True
                break

        # Fallback 1: Tab thứ 2 (index 1)
        if not clicked and len(all_tabs) > 1:
            await all_tabs[1].click()
            clicked = True

        # Fallback 2: Nút đếm số review dưới tên quán
        if not clicked:
            btn = page.locator('button[aria-label*="đánh giá"], button[aria-label*="reviews"]').first
            if await btn.count() > 0:
                await btn.click()
                clicked = True

        await page.wait_for_timeout(1500)

        # 2. Cuộn tải toàn bộ reviews (kể cả 50, 100+ reviews)
        scroll_attempts = 0
        prev_spans = 0
        max_scrolls = 15 if max_reviews == 0 else 5
        while scroll_attempts < max_scrolls:
            await page.evaluate('''() => {
                const el = document.querySelector('div.m6QErb.DxyBCb.kA9KIf.dS8AEf') || document.querySelector('div[role="main"]');
                if (el) el.scrollTop = el.scrollHeight;
            }''')

            more_reviews = page.locator('button:has-text("Xem các bài đánh giá khác"), button[aria-label*="Xem các bài đánh giá khác"]')
            if await more_reviews.count() > 0:
                try:
                    await more_reviews.first.click(timeout=1000)
                except Exception:
                    pass

            await page.wait_for_timeout(1000)
            cnt = await page.locator('span.wiI7pd').count()
            if max_reviews > 0 and cnt >= max_reviews:
                break
            if cnt == prev_spans and scroll_attempts >= 3:
                break
            prev_spans = cnt
            scroll_attempts += 1

        # 3. Bấm các nút "Thêm" / "More" để bung toàn bộ nội dung review bị rút gọn
        more_btns = page.locator('button.w8nwRe, button:has-text("Thêm"), button:has-text("More")')
        m_count = await more_btns.count()
        for m in range(min(m_count, 60)):
            try:
                await more_btns.nth(m).click(timeout=400)
            except Exception:
                pass

        # 4. Thu thập tất cả nội dung review (KHÔNG GIỚI HẠN nếu max_reviews=0)
        review_spans = await page.locator('span.wiI7pd').all()
        for r_el in review_spans:
            txt = (await r_el.inner_text()).strip()
            if txt and len(txt) > 8 and txt not in reviews:
                reviews.append(txt)
                if max_reviews > 0 and len(reviews) >= max_reviews:
                    break
    except Exception:
        pass
    return reviews

async def worker_loop(worker_id, browser, task_queue, seen_set, get_reviews, max_reviews=0):
    context = await browser.new_context(
        locale="vi-VN",
        viewport={"width": 1366, "height": 768},
        user_agent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36"
    )
    search_page = await context.new_page()
    await search_page.route('**/*.{png,jpg,jpeg,webp,gif}', lambda route: route.abort())

    detail_page = None
    if get_reviews:
        detail_page = await context.new_page()
        await detail_page.route('**/*.{png,jpg,jpeg,webp,gif}', lambda route: route.abort())

    while not task_queue.empty():
        try:
            task_id, total_tasks, area, kw = task_queue.get_nowait()
        except asyncio.QueueEmpty:
            break

        query_str = f"{kw} {area} Hà Nội"
        search_url = f"https://www.google.com/maps/search/{urllib.parse.quote(query_str)}"

        try:
            await search_page.goto(search_url, timeout=25000)
            await search_page.wait_for_timeout(2000)

            feed = search_page.locator('div[role="feed"]')
            if await feed.count() == 0:
                continue

            # Cuộn 4 lần để tải danh sách
            prev_len = 0
            for _ in range(4):
                await feed.evaluate('el => el.scrollBy(0, 3000)')
                await search_page.wait_for_timeout(1000)
                links = await search_page.locator('div[role="feed"] a[href*="/maps/place/"]').all()
                if len(links) == prev_len:
                    break
                prev_len = len(links)

            links = await search_page.locator('div[role="feed"] a[href*="/maps/place/"]').all()

            for link in links:
                try:
                    card = link.locator("xpath=ancestor::div[contains(@class, 'Nv2PK')]")
                    if await card.count() == 0:
                        card = link.locator("xpath=..")
                    
                    item = await parse_card_info(card, link)
                    item["area"] = area
                    item["query"] = kw

                    dedup_key = item["place_id"] or item["url"] or item["name"]
                    # Loại bỏ nếu tên rỗng hoặc là từ "Kết quả"
                    if not item["name"] or item["name"] == "Kết quả" or dedup_key in seen_set:
                        continue

                    seen_set.add(dedup_key)

                    # LẤY REVIEW BẰNG DETAIL PAGE ĐỘC LẬP
                    reviews = []
                    if get_reviews and detail_page and item["url"]:
                        reviews = await fetch_reviews_from_url(detail_page, item["url"], max_reviews=max_reviews)
                    item["reviews"] = reviews

                    save_single_item_json(item)

                    rating_disp = f"{item['rating']}★" if item['rating'] else "Mới"
                    rev_count = len(reviews)
                    rev_disp = f" | {rev_count} reviews" if rev_count > 0 else " | 0 review"
                    addr_disp = item['address'][:25] if item['address'] else "Khu " + area

                    sample_snippet = ""
                    if rev_count > 0:
                        sample_snippet = f' -> "{reviews[0][:40]}..."'

                    print(f" [L.{worker_id}] + Quán #{len(all_data_memory)}: {item['name'][:25]} ({rating_disp}){rev_disp}{sample_snippet} - ĐC: {addr_disp}", flush=True)

                except Exception:
                    continue

        except Exception:
            continue

    await context.close()

async def async_main(args):
    areas_to_run = [args.area] if args.area else AREAS
    seen_set = init_storage()

    all_tasks = []
    for area in areas_to_run:
        for kw in KEYWORDS:
            all_tasks.append((area, kw))

    total_tasks = len(all_tasks)
    num_workers = max(1, min(args.workers, 6))

    queue = asyncio.Queue()
    for idx, (area, kw) in enumerate(all_tasks, 1):
        queue.put_nowait((idx, total_tasks, area, kw))

    print("==========================================================================", flush=True)
    print(" 🚀 BẮT ĐẦU CÀO TẨM QUẤT HÀ NỘI (CHUẨN 100% REVIEW KHÁCH HÀNG)", flush=True)
    print(f" - Số luồng song song: {num_workers} LUỒNG", flush=True)
    print(f" - Tổng số khu vực quét: {len(areas_to_run)} khu vực", flush=True)
    print(f" - Tổng số lượt quét: {total_tasks} lượt", flush=True)
    rev_mode = "TẮT" if args.no_reviews else ("BẬT (Lấy HẾT TOÀN BỘ REVIEWS CÓ CHỮ, KHÔNG GIỚI HẠN)" if args.max_reviews == 0 else f"BẬT (Lấy tới {args.max_reviews} reviews/quán)")
    print(f" - Thu thập reviews: {rev_mode}", flush=True)
    print(f" - File JSON duy nhất: {JSON_FILE}", flush=True)
    print("==========================================================================\n", flush=True)

    start_time = time.time()

    chrome_args = [
        '--renderer-process-limit=4',
        '--disable-blink-features=AutomationControlled',
        '--enable-webgl',
        '--use-gl=angle',
        '--disable-background-timer-throttling',
        '--disable-backgrounding-occluded-windows',
        '--disable-renderer-backgrounding',
        '--no-sandbox'
    ]

    async with async_playwright() as p:
        browser = await p.chromium.launch(
            channel="chrome",
            headless=True,
            args=chrome_args
        )

        workers = [
            worker_loop(w_id + 1, browser, queue, seen_set, not args.no_reviews, max_reviews=args.max_reviews)
            for w_id in range(num_workers)
        ]

        await asyncio.gather(*workers)
        await browser.close()

    elapsed = round((time.time() - start_time) / 60, 1)
    print("\n==========================================================================", flush=True)
    print(" 🎉 HOÀN TẤT THU THẬP TOÀN BỘ CƠ SỞ TẨM QUẤT HÀ NỘI!", flush=True)
    print(f" - Thời gian chạy: {elapsed} phút", flush=True)
    print(f" - Tổng số quán lưu trong file JSON: {len(all_data_memory)} quán", flush=True)
    print(f" - Tổng số review đã bóc tách: {total_reviews_count} review", flush=True)
    print(f" - File JSON kết quả: {JSON_FILE}", flush=True)
    print("==========================================================================", flush=True)

def main():
    parser = argparse.ArgumentParser(description="Tool cào tẩm quất Hà Nội (Chuẩn 100% Review)")
    parser.add_argument("--workers", type=int, default=4, help="Số luồng song song (khuyên dùng 3-5 luồng để lấy review mượt)")
    parser.add_argument("--area", type=str, default="", help="Chỉ định 1 quận/huyện cụ thể")
    parser.add_argument("--max-reviews", type=int, default=0, help="Số lượng review tối đa mỗi quán (0 = lấy TOÀN BỘ review)")
    parser.add_argument("--no-reviews", action="store_true", help="Không lấy reviews để chạy tốc độ tối đa")
    args = parser.parse_args()

    asyncio.run(async_main(args))

if __name__ == "__main__":
    main()
