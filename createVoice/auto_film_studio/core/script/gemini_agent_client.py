import subprocess
import json
import re
import time
import os
import sys

EXE_PATH = r"C:\Users\ADMIN\AppData\Local\Programs\antigravity\resources\bin\language_server.exe"

def delete_transient_conversation(conv_id: str):
    """
    XÓA NGAY LẬP TỨC cuộc trò chuyện tạm thời sau khi AI phản hồi xong:
    - Đợi một nhịp ngắn (1.5s) để language server đóng luồng.
    - Xóa bản ghi trong SQLite database conversation_summaries.db.
    - Xóa các file .db, -wal, -shm trong conversations/.
    - Xóa thư mục logs và não bộ trong brain/.
    - Không để lại bất kỳ rác nào trên thanh menu bên trái!
    """
    if not conv_id:
        return
    import sqlite3
    import shutil
    time.sleep(1.5)
    try:
        # 1. Xóa khỏi CSDL danh sách hội thoại
        db_path = r'C:\Users\ADMIN\.gemini\antigravity\conversation_summaries.db'
        if os.path.exists(db_path):
            conn = sqlite3.connect(db_path)
            cur = conn.cursor()
            cur.execute("DELETE FROM conversation_summaries WHERE conversation_id = ?", (conv_id,))
            try:
                cur.execute("DELETE FROM conversations_fts WHERE conversation_id = ?", (conv_id,))
            except Exception:
                pass
            conn.commit()
            conn.close()
            
        # 2. Xóa các file db của phiên đó
        for ext in ['', '-shm', '-wal']:
            f = rf'C:\Users\ADMIN\.gemini\antigravity\conversations\{conv_id}.db{ext}'
            if os.path.exists(f):
                try:
                    os.remove(f)
                except Exception:
                    pass
                    
        # 3. Xóa thư mục brain tạm thời
        b_dir = rf'C:\Users\ADMIN\.gemini\antigravity\brain\{conv_id}'
        if os.path.exists(b_dir):
            try:
                shutil.rmtree(b_dir, ignore_errors=True)
            except Exception:
                pass

        # 4. Xóa file annotation
        ann = rf'C:\Users\ADMIN\.gemini\antigravity\annotations\{conv_id}.pbtxt'
        if os.path.exists(ann):
            try:
                os.remove(ann)
            except Exception:
                pass
    except Exception:
        pass

def call_gemini_ai(prompt: str, timeout: int = 35) -> str:
    """
    Gọi trực tiếp mô hình Google Gemini (Flash / Pro) tích hợp sẵn trong Google Antigravity.
    KHÔNG DÙNG TEMPLATE RẬP KHUÔN, SINH BẰNG TRÍ TUỆ NHÂN TẠO 100%.
    TỰ ĐỘNG XÓA PHIÊN NGAY LẬP TỨC SAU KHI XONG ĐỂ KHÔNG SINH RÁC TRÊN GIAO DIỆN.
    """
    cmd = [
        EXE_PATH,
        "agentapi",
        "new-conversation",
        "--model=flash_lite",
        prompt
    ]
    
    conv_id = None
    try:
        proc = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8")
        if proc.returncode != 0:
            return None
        resp = json.loads(proc.stdout)
        conv_id = resp["response"]["newConversation"]["conversationId"]
    except Exception as e:
        return None
        
    log_file = rf"C:\Users\ADMIN\.gemini\antigravity\brain\{conv_id}\.system_generated\logs\transcript.jsonl"
    full_log_file = rf"C:\Users\ADMIN\.gemini\antigravity\brain\{conv_id}\.system_generated\logs\transcript_full.jsonl"
    
    result_content = None
    start_time = time.time()
    try:
        while time.time() - start_time < timeout:
            target_file = full_log_file if os.path.exists(full_log_file) else log_file
            if os.path.exists(target_file):
                try:
                    with open(target_file, "r", encoding="utf-8") as f:
                        for line in f:
                            if not line.strip():
                                continue
                            step = json.loads(line)
                            if step.get("type") == "PLANNER_RESPONSE" and step.get("content"):
                                result_content = step.get("content")
                                break
                    if result_content:
                        break
                except Exception:
                    pass
            time.sleep(1)
    finally:
        # BẤT KỂ THÀNH CÔNG HAY THẤT BẠI: TỰ ĐỘNG XÓA LUÔN PHIÊN CHAT KHỎI HỆ THỐNG
        if conv_id:
            delete_transient_conversation(conv_id)
            
    return result_content

def extract_json_from_text(text: str) -> dict:
    """Bóc tách đối tượng JSON từ phản hồi markdown của AI."""
    if not text:
        return None
    # Tìm khối code ```json ... ```
    m = re.search(r"```(?:json)?\s*(\{.*?\})\s*```", text, re.DOTALL)
    if m:
        try:
            return json.loads(m.group(1))
        except Exception:
            pass
    # Tìm bất kỳ khối { ... }
    m = re.search(r"(\{.*\})", text, re.DOTALL)
    if m:
        try:
            return json.loads(m.group(1))
        except Exception:
            pass
    return None
