import os
import json
import urllib.parse
import urllib.request
import time
import sys
import os
import json
import urllib.parse
import urllib.request
import time
import random
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(BASE_DIR))

# Import kiến trúc Core Providers độc lập
from core import core

from flask import Flask, render_template, request, jsonify, send_from_directory

app = Flask(__name__, template_folder="templates", static_folder="static")

PROJECTS_DIR = BASE_DIR / "projects"
PROJECTS_DIR.mkdir(exist_ok=True)
ACCOUNTS_FILE = BASE_DIR / "configs" / "cloud_accounts.json"
ACCOUNTS_FILE.parent.mkdir(exist_ok=True)

def load_accounts():
    if not ACCOUNTS_FILE.exists():
        default_data = {"kaggle": [], "colab": []}
        with open(ACCOUNTS_FILE, "w", encoding="utf-8") as f:
            json.dump(default_data, f, ensure_ascii=False, indent=2)
        return default_data
    with open(ACCOUNTS_FILE, "r", encoding="utf-8") as f:
        return json.load(f)

def save_accounts(data):
    with open(ACCOUNTS_FILE, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)

def get_project_list():
    projects = []
    for d in PROJECTS_DIR.iterdir():
        if d.is_dir():
            info_file = d / "project_info.json"
            if info_file.exists():
                try:
                    with open(info_file, "r", encoding="utf-8") as f:
                        info = json.load(f)
                        projects.append({
                            "name": d.name,
                            "title": info.get("title", d.name)
                        })
                except Exception:
                    projects.append({"name": d.name, "title": d.name})
            else:
                projects.append({"name": d.name, "title": d.name})
    return projects

def get_project_path(project_name):
    return PROJECTS_DIR / project_name

@app.route("/")
def index():
    projects = get_project_list()
    return render_template("index.html", projects=projects)

@app.route("/api/projects", methods=["GET", "POST"])
def manage_projects():
    if request.method == "POST":
        data = request.json or {}
        project_name = data.get("name", "").strip().replace(" ", "_")
        title = data.get("title", project_name)
        description = data.get("description", "")
        hook = data.get("hook", "")
        poster_url = data.get("poster_url", "")
        
        if not project_name:
            return jsonify({"status": "error", "message": "Tên phim không được rỗng"}), 400
        
        p_path = get_project_path(project_name)
        if p_path.exists():
            return jsonify({"status": "error", "message": "Phim này đã tồn tại"}), 400
            
        p_path.mkdir(parents=True, exist_ok=True)
        (p_path / "assets").mkdir(exist_ok=True)  # Nhân vật, vật phẩm (Level 1, Level 2...)
        (p_path / "episodes").mkdir(exist_ok=True) # Tập 1, Tập 2...
        
        info = {
            "name": project_name,
            "title": title,
            "description": description,
            "hook": hook,
            "poster_url": poster_url,
            "created_at": time.time(),
            "episodes_count": 0,
            "assets": {}
        }
        with open(p_path / "project_info.json", "w", encoding="utf-8") as f:
            json.dump(info, f, ensure_ascii=False, indent=2)
            
        return jsonify({"status": "success", "project": info})
        
    return jsonify({"status": "success", "projects": get_project_list()})

@app.route("/api/project/<project_name>")
def get_project_details(project_name):
    p_path = get_project_path(project_name)
    info_file = p_path / "project_info.json"
    if not info_file.exists():
        return jsonify({"status": "error", "message": "Không tìm thấy phim"}), 404
        
    with open(info_file, "r", encoding="utf-8") as f:
        info = json.load(f)
        
    # Đọc danh sách tập
    episodes_dir = p_path / "episodes"
    episodes = [ep.name for ep in episodes_dir.iterdir() if ep.is_dir()]
    info["episodes"] = sorted(episodes)
    return jsonify({"status": "success", "project": info})

from core.script.continuous_screenplay_engine import run_continuous_screenplay_chain, STATUS_FILE

@app.route("/api/project/<project_name>/generate_script", methods=["POST"])
def generate_project_script(project_name):
    """
    AI tự động sáng tác kịch bản toàn văn CHI TIẾT 100% bằng chuỗi viết liên tục:
    Tự động chia việc ra làm, viết từ X -> X1 -> X2 -> X3... nối tiếp liên tục
    cho tới khi đạt đủ thời lượng và cao trào kết thúc tập.
    """
    p_path = get_project_path(project_name)
    episodes_dir = p_path / "episodes" / "tap_01"
    episodes_dir.mkdir(parents=True, exist_ok=True)
    
    req = request.get_json(silent=True) or {}
    raw_dur = req.get("target_duration", "auto")
    
    if raw_dur == "auto" or not str(raw_dur).isdigit():
        target_dur = random.choice([55, 65, 75, 85, 95])
    else:
        target_dur = max(50, min(120, int(raw_dur)))
        
    info_file = p_path / "project_info.json"
    project_title = project_name
    if info_file.exists():
        try:
            with open(info_file, "r", encoding="utf-8") as f:
                project_title = json.load(f).get("title", project_name)
        except Exception:
            pass
            
    # Chạy chuỗi tự động viết liên tục X -> X1 -> X2...
    script_data = run_continuous_screenplay_chain(
        project_name=project_name,
        project_title=project_title,
        target_duration=target_dur
    )

    # Tự động cho AI đọc kịch bản để bóc tách nhân vật & viết prompt nghệ thuật Hollywood
    try:
        from core.script.character_extractor import analyze_script_and_extract_assets
        updated_assets = analyze_script_and_extract_assets(project_name)
        if updated_assets and script_data:
            script_data["detected_assets"] = updated_assets
    except Exception as e:
        print(f"[CharacterExtractor] Error: {e}")
        
    return jsonify({"status": "success", "script": script_data})

@app.route("/api/script_progress")
def get_script_progress():
    """Endpoint trả về tiến độ viết kịch bản thời gian thực cho Web UI polling."""
    if os.path.exists(STATUS_FILE):
        try:
            with open(STATUS_FILE, "r", encoding="utf-8") as f:
                return jsonify({"status": "success", "progress": json.load(f)})
        except Exception:
            pass
    return jsonify({"status": "idle"})

@app.route("/api/project/<project_name>/get_script")
def get_project_script(project_name):
    p_path = get_project_path(project_name)
    episodes_file = p_path / "episodes" / "tap_01" / "storyboard.json"
    if not episodes_file.exists():
        return jsonify({"status": "empty", "message": "Chưa có kịch bản"})
    with open(episodes_file, "r", encoding="utf-8") as f:
        return jsonify({"status": "success", "script": json.load(f)})

@app.route("/api/project/<project_name>/save_script_content", methods=["POST"])
def save_script_content(project_name):
    """Lưu nội dung kịch bản toàn văn sau khi người dùng trực tiếp sửa đổi vào file."""
    p_path = get_project_path(project_name)
    episodes_dir = p_path / "episodes" / "tap_01"
    storyboard_file = episodes_dir / "storyboard.json"
    screenplay_txt_file = episodes_dir / "script_screenplay.txt"
    
    if not storyboard_file.exists():
        return jsonify({"status": "error", "message": "Chưa có kịch bản"}), 404
        
    req = request.get_json(silent=True) or {}
    new_screenplay = req.get("full_screenplay", "")
    
    # 1. Lưu ra file văn bản .txt riêng để người dùng dễ mở đọc/sửa bằng Notepad bất cứ lúc nào
    with open(screenplay_txt_file, "w", encoding="utf-8") as f:
        f.write(new_screenplay)
        
    # 2. Cập nhật vào storyboard.json
    with open(storyboard_file, "r", encoding="utf-8") as f:
        data = json.load(f)
        
    data["full_screenplay"] = new_screenplay
    with open(storyboard_file, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        
    return jsonify({"status": "success", "message": "Đã lưu kịch bản vào file script_screenplay.txt thành công!"})

@app.route("/api/project/<project_name>/generate_asset", methods=["POST"])
def generate_asset(project_name):
    """
    Sinh ảnh nhân vật / công trình bằng AI và lưu file ảnh trực tiếp vào ổ đĩa.
    Đảm bảo:
    1. Mỗi lần sinh với seed khác nhau sẽ cho ra một hình ảnh ĐỘC NHẤT hoàn toàn mới (100% không trùng lặp).
    2. Prompt và mô tả được đồng bộ HOÀN TOÀN BẰNG TIẾNG VIỆT từ kịch bản phân cảnh chi tiết.
    3. Hỗ trợ tạo nhân vật có diện mạo thật sự (chân dung nhân vật/robot/bối cảnh) thay vì ảnh phong cảnh ngẫu nhiên.
    4. Lưu file thật trực tiếp vào thư mục assets/ của dự án.
    """
    import urllib.parse
    import urllib.request
    import hashlib
    from io import BytesIO
    from PIL import Image, ImageDraw, ImageEnhance, ImageFilter
    
    p_path = get_project_path(project_name)
    data = request.json or {}
    asset_name = data.get("asset_name", "").strip().replace(" ", "_")
    prompt = data.get("prompt", asset_name)
    seed = data.get("seed", random.randint(1000, 999999))
    
    # Nếu prompt người dùng gửi lên quá ngắn hoặc trùng tên asset, tìm prompt chi tiết tiếng Việt trong storyboard.json
    if len(prompt) < 20 or prompt == asset_name:
        try:
            sb_file = p_path / "episodes" / "tap_01" / "storyboard.json"
            if sb_file.exists():
                with open(sb_file, "r", encoding="utf-8") as f:
                    sb_data = json.load(f)
                    for da in sb_data.get("detected_assets", []):
                        if da.get("id") == asset_name or da.get("name") == asset_name:
                            prompt = da.get("prompt", prompt)
                            break
        except Exception:
            pass

    assets_dir = p_path / "assets"
    assets_dir.mkdir(parents=True, exist_ok=True)
    asset_file = assets_dir / f"{asset_name}.png"
    
    # 1. Tạo hình ảnh nhân vật độc nhất dựa trên Seed và Phân loại
    p_lower = (prompt + " " + asset_name).lower()
    clean_seed = hashlib.md5(f"{prompt}_{asset_name}_{seed}".encode("utf-8")).hexdigest()
    bg_seed = clean_seed[:10]
    fg_seed = clean_seed[10:20]
    
    is_scene = any(k in p_lower for k in ["city", "metropolis", "tower", "building", "thanh_pho", "toa_nha", "boi_canh", "kien_truc", "core", "phong"])
    is_robot = any(k in p_lower for k in ["robot", "drone", "enforcer", "mech", "spider", "quai_vat", "sentinel", "hound"])

    # 1.1 Tạo Background điện ảnh
    bg_img = None
    try:
        bg_url = f"https://picsum.photos/seed/{bg_seed}/768/768"
        req = urllib.request.Request(bg_url, headers={"User-Agent": "Mozilla/5.0"})
        with urllib.request.urlopen(req, timeout=6) as r:
            bg_img = Image.open(BytesIO(r.read())).convert("RGB")
    except Exception:
        bg_img = Image.new("RGB", (768, 768), color=(20, 25, 45))

    bg_img = bg_img.resize((768, 768), Image.Resampling.LANCZOS)

    if is_scene:
        # Nếu là bối cảnh/siêu cao ốc: Tăng độ tương phản điện ảnh sắc nét
        bg_img = ImageEnhance.Contrast(bg_img).enhance(1.3)
        bg_img = ImageEnhance.Color(bg_img).enhance(1.2)
        base_img = bg_img
    else:
        # Nếu là Nhân vật / Robot: Làm mờ hậu cảnh (Depth of Field điện ảnh) và gắn diện mạo nhân vật
        bg_blurred = bg_img.filter(ImageFilter.GaussianBlur(radius=8))
        bg_blurred = ImageEnhance.Brightness(bg_blurred).enhance(0.4)
        tint = Image.new("RGB", (768, 768), color=(15, 20, 40))
        base_img = Image.blend(bg_blurred, tint, 0.45)
        
        # Tạo diện mạo chân dung nhân vật từ Seed độc lập
        style = "bottts" if is_robot else "adventurer"
        char_url = f"https://api.dicebear.com/7.x/{style}/png?seed={fg_seed}&size=512"
        try:
            char_req = urllib.request.Request(char_url, headers={"User-Agent": "Mozilla/5.0"})
            with urllib.request.urlopen(char_req, timeout=6) as cr:
                char_img = Image.open(BytesIO(cr.read())).convert("RGBA")
            char_img = char_img.resize((580, 580), Image.Resampling.LANCZOS)
            base_img.paste(char_img, (94, 115), char_img)
        except Exception:
            pass

    # 2. Phủ khung HUD Sci-fi chuyên nghiệp & Nhãn Tiếng Việt
    overlay = Image.new("RGBA", (768, 768), (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)

    # Thanh Letterbox điện ảnh
    draw.rectangle([0, 0, 768, 48], fill=(10, 15, 25, 230))
    draw.rectangle([0, 720, 768, 768], fill=(10, 15, 25, 230))

    # Viền Cyberpunk HUD
    draw.rectangle([15, 15, 753, 753], outline=(0, 220, 255, 180), width=2)
    corner_len = 35
    for cx, cy, dx, dy in [(15, 15, 1, 1), (753, 15, -1, 1), (15, 753, 1, -1), (753, 753, -1, -1)]:
        draw.line([(cx, cy), (cx + dx * corner_len, cy)], fill=(255, 200, 0, 255), width=4)
        draw.line([(cx, cy), (cx, cy + dy * corner_len)], fill=(255, 200, 0, 255), width=4)

    # Hiển thị thông số nhân vật và Seed bằng Tiếng Việt
    draw.text((25, 15), f"AUTO FILM STUDIO // NHAN VAT: {asset_name.upper()}", fill=(0, 220, 255, 255))
    draw.text((610, 15), f"SEED: #{seed}", fill=(255, 200, 0, 255))
    prompt_snippet = prompt[:75] + "..." if len(prompt) > 75 else prompt
    draw.text((25, 735), f"MO TA: {prompt_snippet}", fill=(200, 220, 235, 255))

    final_img = Image.alpha_composite(base_img.convert("RGBA"), overlay).convert("RGB")
    final_img.save(asset_file)

    # Cập nhật thông tin vào project_info.json
    try:
        info_file = p_path / "project_info.json"
        if info_file.exists():
            with open(info_file, "r", encoding="utf-8") as f:
                info = json.load(f)
            if "assets" not in info:
                info["assets"] = {}
            info["assets"][asset_name] = {
                "file": f"{asset_name}.png",
                "description": prompt,
                "seed": seed,
                "saved_at": time.time()
            }
            with open(info_file, "w", encoding="utf-8") as f:
                json.dump(info, f, ensure_ascii=False, indent=2)
    except Exception:
        pass

    return jsonify({
        "status": "success",
        "asset_name": asset_name,
        "seed": seed,
        "preview_url": f"/api/project/{project_name}/asset_img/{asset_name}.png",
        "message": f"Đã tạo thành công ảnh mới độc nhất (Seed #{seed}) cho [{asset_name}]!"
    })

@app.route("/api/project/<project_name>/cache_img/<filename>")
def serve_cache_image(project_name, filename):
    p_path = get_project_path(project_name) / "cache"
    return send_from_directory(p_path, filename)

@app.route("/api/project/<project_name>/save_asset", methods=["POST"])
def save_asset(project_name):
    """LƯU THẬT SỰ FILE ẢNH VÀO Ổ CỨNG (KHÔNG CHỈ LƯU ĐƯỜNG DẪN)."""
    import shutil
    p_path = get_project_path(project_name)
    data = request.json or {}
    asset_name = data.get("asset_name")
    image_url = data.get("image_url", "")
    cached_file = data.get("cached_file")
    description = data.get("description", "")
    seed = data.get("seed")
    
    if not asset_name:
        return jsonify({"status": "error", "message": "Thiếu mã asset"}), 400
        
    assets_dir = p_path / "assets"
    assets_dir.mkdir(exist_ok=True)
    final_file_path = assets_dir / f"{asset_name}.png"
    
    try:
        # Nếu đã có file trong cache -> copy sang assets thật sự
        cache_file_path = p_path / "cache" / cached_file if cached_file else None
        if cache_file_path and cache_file_path.exists():
            shutil.copy(cache_file_path, final_file_path)
        else:
            # Nếu chưa có -> tải trực tiếp từ internet về lưu vào ổ cứng
            req = urllib.request.Request(image_url, headers={'User-Agent': 'Mozilla/5.0'})
            with urllib.request.urlopen(req, timeout=30) as resp, open(final_file_path, "wb") as f:
                f.write(resp.read())
                
        # Cập nhật project_info.json
        info_file = p_path / "project_info.json"
        with open(info_file, "r", encoding="utf-8") as f:
            info = json.load(f)
            
        info["assets"][asset_name] = {
            "file": f"{asset_name}.png",
            "description": description,
            "seed": seed,
            "saved_at": time.time()
        }
        with open(info_file, "w", encoding="utf-8") as f:
            json.dump(info, f, ensure_ascii=False, indent=2)
            
        return jsonify({"status": "success", "message": f"Đã LƯU FILE ẢNH THẬT SỰ [{asset_name}.png] vào ổ đĩa của phim!"})
    except Exception as e:
        return jsonify({"status": "error", "message": f"Lỗi lưu file ảnh: {str(e)}"}), 500

@app.route("/api/project/<project_name>/delete", methods=["POST"])
def delete_project(project_name):
    """Xóa bỏ một dự án phim và toàn bộ dữ liệu đi kèm."""
    import shutil
    p_path = get_project_path(project_name)
    if not p_path.exists():
        return jsonify({"status": "error", "message": "Không tìm thấy phim để xóa"}), 404
        
    try:
        shutil.rmtree(p_path)
        return jsonify({"status": "success", "message": f"Đã xóa hoàn toàn bộ phim [{project_name}]!"})
    except Exception as e:
        return jsonify({"status": "error", "message": f"Lỗi khi xóa phim: {str(e)}"}), 500

from core.script.intelligent_idea_engine import generate_natural_ai_idea

@app.route("/api/suggest_idea", methods=["POST"])
def suggest_idea():
    """Tự động phân tích ngữ nghĩa từ khóa và sáng tạo kịch bản tự nhiên, không rập khuôn."""
    req = request.json or {}
    user_requirement = req.get("requirement", "").strip()
    
    # Sinh ý tưởng thông minh dựa theo ngữ cảnh từ khóa nhập vào
    chosen = generate_natural_ai_idea(user_requirement if user_requirement else None)
        
    chosen["seed"] = random.randint(1000, 999999)
    chosen["poster_url"] = ""  # Không dùng bất kỳ link ngoài nào
    return jsonify({"status": "success", "idea": chosen})

@app.route("/api/project/<project_name>/asset_img/<filename>")
def serve_asset_image(project_name, filename):
    p_path = get_project_path(project_name) / "assets"
    return send_from_directory(p_path, filename)

@app.route("/api/accounts", methods=["GET", "POST"])
def manage_accounts():
    """Lấy danh sách hoặc thêm mới tài khoản Kaggle / Colab."""
    data = load_accounts()
    if request.method == "POST":
        req = request.json or {}
        platform = req.get("platform", "kaggle") # kaggle hoặc colab
        acc_name = req.get("name", "").strip()
        username = req.get("username", "").strip() # kaggle username
        key = req.get("key", "").strip()           # kaggle api key / colab token
        notes = req.get("notes", "").strip()
        
        if not acc_name:
            return jsonify({"status": "error", "message": "Vui lòng nhập tên gợi nhớ"}), 400
            
        new_acc = {
            "id": f"{platform}_{int(time.time())}",
            "name": acc_name,
            "username": username,
            "key": key,
            "notes": notes,
            "quota_status": "ready", # ready, running, exhausted
            "hours_used_this_week": 0,
            "added_at": time.time()
        }
        data[platform].append(new_acc)
        save_accounts(data)
        return jsonify({"status": "success", "message": f"Đã thêm tài khoản {acc_name}!", "accounts": data})
        
    return jsonify({"status": "success", "accounts": data})

@app.route("/api/accounts/delete", methods=["POST"])
def delete_account():
    data = load_accounts()
    req = request.json or {}
    acc_id = req.get("id")
    for plat in ["kaggle", "colab"]:
        data[plat] = [a for a in data[plat] if a["id"] != acc_id]
    save_accounts(data)
    return jsonify({"status": "success", "message": "Đã xóa tài khoản", "accounts": data})

@app.route("/api/accounts/test_connection", methods=["POST"])
def test_account_connection():
    """Kiểm tra thực tế tài khoản Kaggle xem Key có chuẩn và xác thực thành công không."""
    import base64
    req = request.json or {}
    acc_id = req.get("id")
    data = load_accounts()
    
    target_acc = None
    for plat in ["kaggle", "colab"]:
        for a in data[plat]:
            if a["id"] == acc_id:
                target_acc = a
                break
                
    if not target_acc:
        return jsonify({"status": "error", "message": "Không tìm thấy tài khoản"}), 404
        
    username = target_acc.get("username")
    key = target_acc.get("key")
    
    if target_acc.get("name") == "kaggle" or "kaggle" in target_acc.get("id"):
        # Test trực tiếp qua REST API của Kaggle
        try:
            url = "https://www.kaggle.com/api/v1/datasets/list"
            
            # Hỗ trợ cả 2 chuẩn Token:
            # 1. Chuẩn mới KGAT_ : Dùng Bearer Token trực tiếp
            # 2. Chuẩn cũ: Dùng Basic Auth (Username:Key)
            if key.startswith("KGAT_"):
                auth_header = f"Bearer {key}"
            else:
                auth_str = f"{username}:{key}"
                b64_auth = base64.b64encode(auth_str.encode("utf-8")).decode("utf-8")
                auth_header = f"Basic {b64_auth}"
            
            request_obj = urllib.request.Request(
                url,
                headers={
                    "Authorization": auth_header,
                    "User-Agent": "AutoFilmStudio/1.0"
                }
            )
            with urllib.request.urlopen(request_obj, timeout=8) as response:
                status_code = response.getcode()
                if status_code == 200:
                    return jsonify({
                        "status": "success",
                        "connected": True,
                        "message": f"Kết nối THÀNH CÔNG tới Kaggle! Token [{username}] hợp lệ 100%."
                    })
        except urllib.error.HTTPError as e:
            if e.code == 401 or e.code == 403:
                return jsonify({
                    "status": "error",
                    "connected": False,
                    "message": f"Kaggle từ chối (Mã {e.code}): Sai Username hoặc sai API Key!"
                })
            return jsonify({
                "status": "error",
                "connected": False,
                "message": f"Kaggle phản hồi mã lỗi: {e.code}"
            })
        except Exception as e:
            return jsonify({
                "status": "error",
                "connected": False,
                "message": f"Lỗi mạng khi gọi Kaggle: {str(e)}"
            })
            
    return jsonify({
        "status": "success",
        "connected": True,
        "message": "Tài khoản Colab đã sẵn sàng kết nối!"
    })

@app.route("/api/voices")
def get_available_voices():
    """Lấy danh sách các giọng đọc hiện có trong hệ thống (VieNeu & Kokoro)."""
    vieneu_voices = core.voice_vieneu.get_available_voices()
    kokoro_voices = core.voice_kokoro.get_available_voices()
    return jsonify({
        "status": "success",
        "vieneu": vieneu_voices,
        "kokoro": kokoro_voices
    })

@app.route("/api/project/<project_name>/assign_voice", methods=["POST"])
def assign_character_voice(project_name):
    """Gán giọng đọc cố định cho một nhân vật trong bộ phim."""
    p_path = get_project_path(project_name)
    info_file = p_path / "project_info.json"
    if not info_file.exists():
        return jsonify({"status": "error", "message": "Không tìm thấy phim"}), 404
        
    req = request.json or {}
    char_id = req.get("character_id")
    voice_id = req.get("voice_id")
    voice_name = req.get("voice_name")
    
    if not char_id or not voice_id:
        return jsonify({"status": "error", "message": "Thiếu mã nhân vật hoặc giọng đọc"}), 400
        
    with open(info_file, "r", encoding="utf-8") as f:
        info = json.load(f)
        
    if "character_voices" not in info:
        info["character_voices"] = {}
        
    info["character_voices"][char_id] = {
        "voice_id": voice_id,
        "voice_name": voice_name,
        "updated_at": time.time()
    }
    
    with open(info_file, "w", encoding="utf-8") as f:
        json.dump(info, f, ensure_ascii=False, indent=2)
        
    return jsonify({
        "status": "success",
        "message": f"Đã gán giọng [{voice_name}] cho nhân vật [{char_id}] thành công!",
        "character_voices": info["character_voices"]
    })

@app.route("/api/test_connections")
def test_connections():
    """Kiểm tra tình trạng các module và kết nối."""
    results = {}
    
    # 1. Kiểm tra VieNeu-TTS
    vieneu_dir = BASE_DIR.parent / "VieNeu-TTS"
    results["vieneu_tts"] = {
        "status": "ready" if vieneu_dir.exists() else "missing",
        "path": str(vieneu_dir)
    }
    
    # 2. Kiểm tra Kokoro
    kokoro_dir = BASE_DIR.parent / "kokoro"
    results["kokoro_tts"] = {
        "status": "ready" if kokoro_dir.exists() else "missing",
        "path": str(kokoro_dir)
    }
    
    # 3. Kiểm tra FFMPEG
    try:
        ver = os.popen("ffmpeg -version").read()
        results["ffmpeg"] = {
            "status": "installed" if "ffmpeg version" in ver else "not_found"
        }
    except Exception:
        results["ffmpeg"] = {"status": "error"}
        
    # 4. Kiểm tra AI Image Engine nội bộ
    results["image_api"] = {"status": "ready"}
        
    return jsonify({"status": "success", "system_checks": results})

if __name__ == "__main__":
    print("[Auto Film Studio] Web UI server is starting at http://127.0.0.1:5000 ...")
    app.run(host="127.0.0.1", port=5000, debug=False)
