import os
import tkinter as tk
from tkinter import filedialog, messagebox, ttk
import threading
import office2pdf

class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("Word to PDF Converter (Standalone)")
        self.geometry("550x380")
        self.configure(padx=20, pady=20)
        self.resizable(False, False)
        
        self.files_to_convert = []
        
        # Styles
        style = ttk.Style(self)
        if "vista" in style.theme_names():
            style.theme_use("vista")
        elif "clam" in style.theme_names():
            style.theme_use("clam")
            
        style.configure("TButton", font=("Segoe UI", 10))
        style.configure("Title.TLabel", font=("Segoe UI", 14, "bold"))
        style.configure("Status.TLabel", font=("Segoe UI", 10))
        
        # UI Elements
        ttk.Label(self, text="Chuyển đổi Word (.docx) sang PDF", style="Title.TLabel").pack(pady=(0, 20))
        
        frame_buttons = ttk.Frame(self)
        frame_buttons.pack(fill=tk.X, pady=10)
        
        self.btn_select_files = ttk.Button(frame_buttons, text="Chọn nhiều file Word (.docx)", command=self.select_files)
        self.btn_select_files.pack(side=tk.LEFT, expand=True, padx=5, fill=tk.X)
        
        self.btn_select_folder = ttk.Button(frame_buttons, text="Chọn thư mục", command=self.select_folder)
        self.btn_select_folder.pack(side=tk.RIGHT, expand=True, padx=5, fill=tk.X)
        
        self.lbl_status = ttk.Label(self, text="Chưa có file nào được chọn.", style="Status.TLabel", foreground="gray")
        self.lbl_status.pack(pady=15)
        
        self.btn_convert = ttk.Button(self, text="BẮT ĐẦU CONVERT", command=self.start_conversion, state=tk.DISABLED)
        self.btn_convert.pack(fill=tk.X, pady=10)
        
        self.progress = ttk.Progressbar(self, orient=tk.HORIZONTAL, length=500, mode='determinate')
        self.progress.pack(fill=tk.X, pady=(20, 5))
        
        self.lbl_progress = ttk.Label(self, text="", style="Status.TLabel", foreground="#0066cc")
        self.lbl_progress.pack()
        
    def select_files(self):
        files = filedialog.askopenfilenames(
            title="Chọn file Word",
            filetypes=[("Word Documents", "*.docx")]
        )
        if files:
            self.files_to_convert = list(files)
            self.lbl_status.config(text=f"Đã chọn {len(self.files_to_convert)} file.", foreground="black")
            self.btn_convert.config(state=tk.NORMAL)
            self.reset_progress()
            
    def select_folder(self):
        folder = filedialog.askdirectory(title="Chọn thư mục chứa file Word")
        if folder:
            self.files_to_convert = []
            for root, dirs, files in os.walk(folder):
                for file in files:
                    if file.lower().endswith('.docx') and not file.startswith('~'):
                        self.files_to_convert.append(os.path.join(root, file))
            if self.files_to_convert:
                self.lbl_status.config(text=f"Đã tìm thấy {len(self.files_to_convert)} file Word trong thư mục.", foreground="black")
                self.btn_convert.config(state=tk.NORMAL)
            else:
                self.lbl_status.config(text="Không tìm thấy file Word (.docx) nào trong thư mục được chọn.", foreground="red")
                self.btn_convert.config(state=tk.DISABLED)
            self.reset_progress()
            
    def reset_progress(self):
        self.progress['value'] = 0
        self.lbl_progress.config(text="")
                
    def start_conversion(self):
        self.btn_convert.config(state=tk.DISABLED)
        self.btn_select_files.config(state=tk.DISABLED)
        self.btn_select_folder.config(state=tk.DISABLED)
        self.progress['value'] = 0
        self.progress['maximum'] = len(self.files_to_convert)
        self.lbl_progress.config(text="Đang xử lý...")
        
        thread = threading.Thread(target=self.convert_files)
        thread.daemon = True
        thread.start()
        
    def convert_files(self):
        success_count = 0
        error_count = 0
        
        for i, file_path in enumerate(self.files_to_convert):
            try:
                # Update UI safely
                self.after(0, self.update_progress, i+1, file_path)
                
                # office2pdf conversion
                output_path = os.path.splitext(file_path)[0] + ".pdf"
                result = office2pdf.convert_path(file_path)
                with open(output_path, "wb") as f:
                    f.write(result.pdf)
                
                success_count += 1
            except Exception as e:
                print(f"Error converting {file_path}: {e}")
                error_count += 1
                
        self.after(0, self.conversion_done, success_count, error_count)
        
    def update_progress(self, value, current_file):
        self.progress['value'] = value
        filename = os.path.basename(current_file)
        if len(filename) > 50:
            filename = filename[:47] + "..."
        self.lbl_progress.config(text=f"Đang xử lý ({value}/{len(self.files_to_convert)}): {filename}")
        
    def conversion_done(self, success_count, error_count):
        self.progress['value'] = len(self.files_to_convert)
        self.lbl_progress.config(text="Hoàn tất quá trình chuyển đổi!", foreground="green")
        self.btn_convert.config(state=tk.NORMAL)
        self.btn_select_files.config(state=tk.NORMAL)
        self.btn_select_folder.config(state=tk.NORMAL)
        
        msg = f"Đã chuyển đổi thành công {success_count} file PDF."
        if error_count > 0:
            msg += f"\nCó {error_count} file bị lỗi."
            messagebox.showwarning("Hoàn tất có lỗi", msg)
        else:
            messagebox.showinfo("Thành công", msg)

if __name__ == "__main__":
    app = App()
    app.mainloop()
