#!/usr/bin/env python3
"""
Stardew JSON Doctor – GUI Edition (星露谷 JSON 诊所 GUI 版)
One-click JSON checker & trailing-comma fixer for Stardew Valley mods.
一键检查模组 JSON，并自动修复末尾多余逗号的小工具。
"""

import tkinter as tk
from tkinter import filedialog, messagebox, scrolledtext
from dataclasses import dataclass, field
from typing import List, Optional, Dict, Any, Tuple
import os
import json

# ---------- Data structures (数据结构) ----------

@dataclass
class FileIssue:
    path: str
    issue_type: str
    message: str
    line: Optional[int] = None
    column: Optional[int] = None
    details: Dict[str, Any] = field(default_factory=dict)

@dataclass
class FileResult:
    path: str
    ok: bool
    issues: List[FileIssue] = field(default_factory=list)
    fixed: bool = False

# ---------- Core JSON logic (核心 JSON 逻辑) ----------

def iter_json_files(root: str):
    """Yield all .json files under a root folder (递归遍历根目录下的所有 .json 文件)."""
    for dirpath, dirnames, filenames in os.walk(root):
        for name in filenames:
            if name.lower().endswith(".json"):
                yield os.path.join(dirpath, name)

def strip_line_comments(text: str) -> str:
    """
    Remove //-style comments that are outside of strings (移除字符串外部的 // 行注释).
    Keeps newlines so line numbers stay roughly correct (保留换行符以尽量保持行号一致).
    """
    out_chars = []
    i = 0
    in_string = False
    escape = False
    length = len(text)

    while i < length:
        ch = text[i]

        if in_string:
            out_chars.append(ch)
            if escape:
                escape = False
            elif ch == '\\':
                escape = True
            elif ch == '"':
                in_string = False
            i += 1
            continue

        # entering string (进入字符串)
        if ch == '"':
            in_string = True
            out_chars.append(ch)
            i += 1
            continue

        # line comment start? (是否为行注释开始？)
        if ch == '/' and i + 1 < length and text[i + 1] == '/':
            # skip until newline or EOF (跳过直到换行或文件结尾)
            i += 2
            while i < length and text[i] not in '\r\n':
                i += 1
            # keep the newline itself (保留换行符本身)
            if i < length:
                out_chars.append(text[i])
                i += 1
            continue

        out_chars.append(ch)
        i += 1

    return ''.join(out_chars)

def remove_trailing_commas(text: str) -> str:
    """
    Remove trailing commas before '}' or ']' while ignoring commas inside strings.
    在不影响字符串中的逗号的前提下，移除 '}' 或 ']' 之前的多余逗号。
    """
    out_chars = []
    i = 0
    in_string = False
    escape = False
    length = len(text)

    while i < length:
        ch = text[i]

        if in_string:
            out_chars.append(ch)
            if escape:
                escape = False
            elif ch == '\\':
                escape = True
            elif ch == '"':
                in_string = False
            i += 1
            continue

        if ch == '"':
            in_string = True
            out_chars.append(ch)
            i += 1
            continue

        if ch == ',':
            # look ahead: only whitespace then } or ] → treat as trailing comma
            # 向前查看：若之后只有空白再跟 '}' 或 ']' → 视为多余逗号
            j = i + 1
            while j < length and text[j] in ' \t\r\n':
                j += 1
            if j < length and text[j] in '}]':
                # skip this comma (跳过这个逗号)
                i += 1
                continue
            else:
                out_chars.append(ch)
                i += 1
                continue

        out_chars.append(ch)
        i += 1

    return ''.join(out_chars)

def detect_duplicates_object_pairs_hook(issues: List[FileIssue], path: str):
    """
    object_pairs_hook that records duplicate keys while still returning a dict.
    object_pairs_hook：记录重复键，同时返回字典。
    """
    def hook(pairs: List[Tuple[str, Any]]):
        seen_counts: Dict[str, int] = {}
        result: Dict[str, Any] = {}
        dup_keys: List[str] = []
        for key, value in pairs:
            if key in seen_counts:
                seen_counts[key] += 1
                if key not in dup_keys:
                    dup_keys.append(key)
            else:
                seen_counts[key] = 1
            result[key] = value
        if dup_keys:
            issues.append(FileIssue(
                path=path,
                issue_type="duplicate_keys",
                message=f"Duplicate keys found (发现重复键): {', '.join(dup_keys)}",
                details={"keys": dup_keys}
            ))
        return result
    return hook

def validate_file(
    path: str,
    auto_fix: bool = False,
    backup: bool = True,
    ignore_comments: bool = False,
    allow_trailing_commas: bool = False
) -> FileResult:
    """
    Validate one JSON file (校验单个 JSON 文件).

    Pipeline (处理流程):
    1. (optional) strip // comments → work_text（可选：移除 // 注释）
    2. (optional) if allow_trailing_commas=True, strip trailing commas for parsing only
       （可选：若允许末尾逗号，则仅在解析时移除多余逗号，不记录为错误）
    3. parse → if OK, record duplicate keys（若解析成功，记录重复键）
    4. if NOT OK, record invalid_json_original（记录原始解析错误）
    5. if auto_fix=True AND allow_trailing_commas=False, try remove_trailing_commas and parse again:
       - if OK → record trailing_commas_fixed + any duplicate_keys，并可写回文件
       - if still NOT OK → record invalid_json_after_fix
    """
    issues: List[FileIssue] = []
    fixed = False

    # Read file (读取文件)
    try:
        with open(path, "r", encoding="utf-8-sig") as f:
            original_text = f.read()
    except Exception as ex:
        issues.append(FileIssue(
            path=path,
            issue_type="io_error",
            message=f"Failed to read file (读取文件失败): {ex}",
        ))
        return FileResult(path=path, ok=False, issues=issues)

    # Step 1: prepare working text (准备要解析的文本)
    work_text = original_text
    if ignore_comments:
        work_text = strip_line_comments(work_text)

    # Step 2: prepare parse_text (根据设置准备解析用文本)
    parse_text = work_text
    if allow_trailing_commas:
        # For SMAPI mode: ignore trailing commas for validation only
        # SMAPI 模式：仅在解析时忽略末尾逗号，不视为错误、不写回文件
        parse_text = remove_trailing_commas(parse_text)

    # Step 3: first parse attempt (第一次解析尝试)
    ok = False
    try:
        json.loads(parse_text, object_pairs_hook=detect_duplicates_object_pairs_hook(issues, path))
        ok = True
    except json.JSONDecodeError as e:
        issues.append(FileIssue(
            path=path,
            issue_type="invalid_json_original",
            message=f"Invalid JSON (JSON 无效，原始解析失败): {e}",
            line=e.lineno,
            column=e.colno
        ))
        ok = False

    # Step 4: only if auto_fix is ON and trailing commas are NOT allowed, try trailing comma fix
    # 第四步：仅在 auto_fix=True 且 未开启“允许末尾逗号”时，才尝试修复末尾逗号
    if not ok and auto_fix and not allow_trailing_commas:
        fixed_text = remove_trailing_commas(work_text)
        if fixed_text != work_text:
            try:
                # fresh issues list for this parse ONLY for duplicates
                temp_issues: List[FileIssue] = []
                json.loads(fixed_text, object_pairs_hook=detect_duplicates_object_pairs_hook(temp_issues, path))
                # parse succeeded after removing trailing commas
                work_text = fixed_text
                ok = True
                fixed = True
                issues.append(FileIssue(
                    path=path,
                    issue_type="trailing_commas_fixed",
                    message="Removed trailing commas (已移除末尾多余逗号) before '}' or ']'."
                ))
                issues.extend(temp_issues)
                if ignore_comments:
                    issues.append(FileIssue(
                        path=path,
                        issue_type="comments_removed_on_fix",
                        message="File fixed with comments ignored (在忽略注释的前提下修复文件); "
                                "// comments may be removed in the saved file (保存后的文件可能不再包含 // 注释)."
                    ))
            except json.JSONDecodeError as e:
                issues.append(FileIssue(
                    path=path,
                    issue_type="invalid_json_after_fix",
                    message=f"Still invalid after trailing-comma fix (移除多余逗号后仍无效): {e}",
                    line=e.lineno,
                    column=e.colno
                ))
                ok = False
        # if fixed_text == work_text, we already have invalid_json_original; nothing else to do

    # Step 5: write back if we actually fixed something and auto_fix is ON
    # 第五步：仅在 auto_fix=True 且确实修复了文件时才写回
    if fixed and auto_fix:
        try:
            if backup:
                backup_path = path + ".bak"
                if not os.path.exists(backup_path):
                    with open(backup_path, "w", encoding="utf-8") as bf:
                        bf.write(original_text)
            with open(path, "w", encoding="utf-8") as f:
                f.write(work_text)
        except Exception as ex:
            issues.append(FileIssue(
                path=path,
                issue_type="io_error_write",
                message=f"Failed to write fixed file (写入修复后的文件失败): {ex}"
            ))
            return FileResult(path=path, ok=False, issues=issues, fixed=False)

    return FileResult(path=path, ok=ok, issues=issues, fixed=fixed)

# ---------- GUI App (图形界面应用) ----------

class JsonDoctorApp:
    def __init__(self, root):
        self.root = root
        root.title("Stardew JSON Doctor (星露谷 JSON 诊所)")

        # State (状态)
        self.mods_path_var = tk.StringVar()
        default_mods = os.path.abspath("Mods")
        if os.path.isdir(default_mods):
            self.mods_path_var.set(default_mods)

        self.auto_fix_var = tk.BooleanVar(value=False)
        self.backup_var = tk.BooleanVar(value=True)
        self.ignore_comments_var = tk.BooleanVar(value=True)
        self.allow_trailing_var = tk.BooleanVar(value=False)  # new: SMAPI mode toggle

        # --- Mods path row (模组路径行) ---
        path_frame = tk.Frame(root)
        path_frame.pack(fill="x", padx=10, pady=(10, 5))

        tk.Label(path_frame, text="Mods folder (模组文件夹):").pack(side="left")
        self.path_entry = tk.Entry(path_frame, textvariable=self.mods_path_var, width=50)
        self.path_entry.pack(side="left", padx=5, expand=True, fill="x")
        tk.Button(path_frame, text="Browse... (浏览…)", command=self.browse_folder).pack(side="left")

        # --- Options row (选项行) ---
        options_frame = tk.Frame(root)
        options_frame.pack(fill="x", padx=10, pady=5)

        self.auto_fix_check = tk.Checkbutton(
            options_frame,
            text="Auto-fix trailing commas (自动修复末尾多余逗号)",
            variable=self.auto_fix_var
        )
        self.auto_fix_check.pack(anchor="w")

        self.backup_check = tk.Checkbutton(
            options_frame,
            text="Create .bak backups (推荐，创建 .bak 备份)",
            variable=self.backup_var
        )
        self.backup_check.pack(anchor="w")

        self.ignore_comments_check = tk.Checkbutton(
            options_frame,
            text="Ignore // comments when validating (校验时忽略 // 注释)",
            variable=self.ignore_comments_var
        )
        self.ignore_comments_check.pack(anchor="w")

        self.allow_trailing_check = tk.Checkbutton(
            options_frame,
            text="Allow trailing commas (SMAPI mode) (末尾逗号视为合法 / SMAPI 模式)",
            variable=self.allow_trailing_var
        )
        self.allow_trailing_check.pack(anchor="w")

        # --- Buttons row (按钮行) ---
        buttons_frame = tk.Frame(root)
        buttons_frame.pack(fill="x", padx=10, pady=5)

        self.run_button = tk.Button(buttons_frame, text="Scan JSON Files (扫描 JSON 文件)", command=self.run_scan)
        self.run_button.pack(side="left")

        tk.Button(buttons_frame, text="Clear Log (清空日志)", command=self.clear_log).pack(side="right")

        # --- Log area (日志区域) ---
        log_frame = tk.Frame(root)
        log_frame.pack(fill="both", expand=True, padx=10, pady=(0, 10))

        self.log_text = scrolledtext.ScrolledText(log_frame, wrap="word", height=20)
        self.log_text.pack(fill="both", expand=True)

        self.append_log("Stardew JSON Doctor (星露谷 JSON 诊所) ready (已就绪).\n")
        self.append_log("Select your Mods folder (选择模组文件夹) and click 'Scan JSON Files (扫描 JSON 文件)'.\n")
        self.append_log(
            "Tip (提示): trailing commas are allowed by SMAPI; you can enable "
            "'Allow trailing commas (SMAPI mode)' to ignore them "
            "(SMAPI 允许末尾逗号，可勾选“末尾逗号视为合法 / SMAPI 模式”以不视其为错误).\n\n"
        )

    # ---- Helpers (辅助函数) ----

    def browse_folder(self):
        folder = filedialog.askdirectory(title="Select Mods folder (选择模组文件夹)")
        if folder:
            self.mods_path_var.set(folder)

    def clear_log(self):
        self.log_text.delete("1.0", tk.END)

    def append_log(self, text: str):
        self.log_text.insert(tk.END, text)
        self.log_text.see(tk.END)
        self.root.update_idletasks()

    def set_controls_state(self, state: str):
        self.path_entry.config(state=state)
        self.auto_fix_check.config(state=state)
        self.backup_check.config(state=state)
        self.ignore_comments_check.config(state=state)
        self.allow_trailing_check.config(state=state)
        self.run_button.config(state=state)

    # ---- Main scan action (主扫描逻辑) ----

    def run_scan(self):
        mods_path = self.mods_path_var.get().strip()
        if not mods_path:
            messagebox.showerror("Error (错误)", "Please select a Mods folder first (请先选择模组文件夹).")
            return
        if not os.path.isdir(mods_path):
            messagebox.showerror("Error (错误)", f"'{mods_path}' is not a valid folder (不是有效的文件夹).")
            return

        auto_fix = self.auto_fix_var.get()
        backup = self.backup_var.get()
        ignore_comments = self.ignore_comments_var.get()
        allow_trailing = self.allow_trailing_var.get()

        if auto_fix and allow_trailing:
            # small warning: in SMAPI mode, auto-fix won't touch trailing commas
            message = (
                "Auto-fix is ON (开启自动修复), but 'Allow trailing commas (SMAPI mode)' "
                "is also ON (同时开启“末尾逗号视为合法 / SMAPI 模式”).\n\n"
                "In this mode, trailing commas are treated as valid and will NOT be auto-fixed.\n"
                "在该模式下，末尾逗号会被视为合法，不会被自动修复。\n\n"
                "Continue? (是否继续？)"
            )
            proceed = messagebox.askyesno("Confirm (确认)", message)
            if not proceed:
                return
        elif auto_fix:
            proceed = messagebox.askyesno(
                "Confirm Auto-Fix (确认自动修复)",
                "Auto-fix will modify JSON files (自动修复会修改 JSON 文件)\n"
                "(after making .bak backups if enabled，如开启则会先创建 .bak 备份).\n\n"
                "Continue? (是否继续？)"
            )
            if not proceed:
                return

        # UI: lock controls while scanning (扫描过程中禁用控件)
        self.set_controls_state("disabled")
        self.clear_log()
        self.append_log(f"Scanning (正在扫描): {mods_path}\n")
        self.append_log(
            f"Auto-fix (自动修复): {'ON (开启)' if auto_fix else 'OFF (关闭)'} | "
            f"Backups (备份): {'ON (开启)' if backup else 'OFF (关闭)'} | "
            f"Ignore // comments (忽略 // 注释): {'ON (开启)' if ignore_comments else 'OFF (关闭)'} | "
            f"Allow trailing commas (允许末尾逗号): {'ON (开启)' if allow_trailing else 'OFF (关闭)'}\n\n"
        )

        results: List[FileResult] = []
        total_files = 0

        for path in iter_json_files(mods_path):
            total_files += 1
            rel = os.path.relpath(path, mods_path)
            self.append_log(f"Checking (正在检查) {rel} ... ")
            res = validate_file(
                path,
                auto_fix=auto_fix,
                backup=backup,
                ignore_comments=ignore_comments,
                allow_trailing_commas=allow_trailing
            )
            results.append(res)

            if res.ok and not res.fixed:
                self.append_log("OK (正常)\n")
            elif res.fixed:
                self.append_log("FIXED (已修复)\n")
            else:
                self.append_log("ERROR (有错误)\n")

            for issue in res.issues:
                loc = ""
                if issue.line is not None and issue.column is not None:
                    loc = f" (line 行 {issue.line}, col 列 {issue.column})"
                self.append_log(f"    [{issue.issue_type}]{loc} {issue.message}\n")

        # Summary (总结)
        ok_count = sum(1 for r in results if r.ok and not r.fixed)
        fixed_count = sum(1 for r in results if r.fixed)
        bad_count = sum(1 for r in results if not r.ok)

        self.append_log("\n===== Summary (总结) =====\n")
        self.append_log(f"Total JSON files scanned (总共扫描的 JSON 文件数): {total_files}\n")
        self.append_log(f"Valid (no changes) (正常，无需修改): {ok_count}\n")
        self.append_log(f"Fixed automatically (已自动修复): {fixed_count}\n")
        self.append_log(f"Still invalid / errors (仍有错误/无法修复): {bad_count}\n")

        # Re-enable UI (重新启用控件)
        self.set_controls_state("normal")

        messagebox.showinfo(
            "Scan complete (扫描完成)",
            f"Total JSON files (JSON 文件总数): {total_files}\n"
            f"Valid (正常): {ok_count}\n"
            f"Fixed (已修复): {fixed_count}\n"
            f"Errors (有错误): {bad_count}"
        )

# ---------- Entrypoint (程序入口) ----------

def main():
    root = tk.Tk()
    app = JsonDoctorApp(root)
    root.mainloop()

if __name__ == "__main__":
    main()
