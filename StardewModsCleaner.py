import os
import json
import shutil
import time
import tkinter as tk
from tkinter import ttk, filedialog, messagebox
from typing import Optional, Dict, List, Tuple

# ----------------------------
# Definitions
# ----------------------------

TRASH_PREFIX = "_Trash_"

# OS junk (safe to remove for Stardew/SMAPI; these are metadata/cache artifacts)
OS_JUNK_FILES = {
    ".DS_Store",      # macOS Finder metadata
    "Thumbs.db",      # Windows Explorer thumbnails cache
    "ehthumbs.db",    # older Windows thumbnail cache
}

OS_JUNK_DIRS = {
    "__MACOSX",       # macOS zip artifact folder
    ".Spotlight-V100",
    ".Trashes",
}

APPLEDOUBLE_PREFIX = "._"     # macOS AppleDouble resource fork files (._filename)
MAC_ICON_FILE = "Icon\r"      # macOS Finder icon metadata file (rare)

ARCHIVE_EXTS = {".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz"}


# ----------------------------
# Helpers
# ----------------------------

def now_stamp() -> str:
    return time.strftime("%Y%m%d_%H%M%S")

def open_in_explorer(path: str):
    try:
        os.startfile(path)  # Windows
    except Exception:
        pass

def is_under_trash(mods_root: str, path: str) -> bool:
    try:
        mods_root_abs = os.path.abspath(mods_root)
        path_abs = os.path.abspath(path)
        rel = os.path.relpath(path_abs, mods_root_abs)
        first = rel.split(os.sep, 1)[0]
        return first.startswith(TRASH_PREFIX)
    except Exception:
        return False

def find_manifest_json(folder: str) -> Optional[str]:
    m = os.path.join(folder, "manifest.json")
    return m if os.path.isfile(m) else None

def read_manifest(manifest_path: str) -> Optional[dict]:
    for enc in ("utf-8-sig", "utf-8"):
        try:
            with open(manifest_path, "r", encoding=enc) as f:
                return json.load(f)
        except Exception:
            continue
    return None

def iter_manifest_folders(mods_root: str) -> List[Tuple[str, str]]:
    """Return list of (folder, manifest_path) for every manifest.json found under Mods root (excluding Trash)."""
    results: List[Tuple[str, str]] = []
    mods_root_abs = os.path.abspath(mods_root)

    for dirpath, dirnames, filenames in os.walk(mods_root_abs):
        if is_under_trash(mods_root_abs, dirpath):
            dirnames[:] = []
            continue

        if "manifest.json" in filenames:
            results.append((dirpath, os.path.join(dirpath, "manifest.json")))

    return results

def ensure_trash_dir(mods_root: str) -> str:
    trash_dir = os.path.join(mods_root, f"{TRASH_PREFIX}{now_stamp()}")
    os.makedirs(trash_dir, exist_ok=True)
    return trash_dir

def safe_relpath(mods_root: str, target_path: str) -> str:
    """Return a safe relpath; if anything goes wrong, fall back to basename."""
    try:
        rel = os.path.relpath(os.path.abspath(target_path), os.path.abspath(mods_root))
        if rel.startswith(".."):
            return os.path.basename(target_path.rstrip("\\/"))
        return rel
    except Exception:
        return os.path.basename(target_path.rstrip("\\/"))

def move_to_trash(trash_dir: str, mods_root: str, target_path: str) -> str:
    """
    Move a file/folder into trash while preserving folder structure under trash_dir.
    Example:
      Mods/A/B/Thumbs.db -> Mods/_Trash_x/A/B/Thumbs.db
    """
    rel = safe_relpath(mods_root, target_path)
    dest = os.path.join(trash_dir, rel)

    dest_parent = os.path.dirname(dest)
    os.makedirs(dest_parent, exist_ok=True)

    # avoid collisions
    if os.path.exists(dest):
        base = os.path.basename(dest)
        dest = os.path.join(dest_parent, f"{base}_{now_stamp()}")

    shutil.move(target_path, dest)
    return dest

def _rmtree_onerror(func, path, excinfo):
    # Windows: clear read-only and retry
    try:
        os.chmod(path, 0o700)
        func(path)
    except Exception:
        raise

def delete_permanently(path: str):
    if os.path.isdir(path):
        shutil.rmtree(path, onerror=_rmtree_onerror)
    else:
        try:
            os.chmod(path, 0o600)
        except Exception:
            pass
        os.remove(path)

def basename_or_empty(p: str) -> str:
    try:
        return os.path.basename(p)
    except Exception:
        return ""


# ----------------------------
# Detection
# ----------------------------

def detect_os_junk(mods_root: str) -> List[dict]:
    """
    OS junk detection (safe metadata/cache):
      - Files: .DS_Store, Thumbs.db, ehthumbs.db, AppleDouble '._*', Icon\\r
      - Folders: __MACOSX, .Spotlight-V100, .Trashes
    """
    items: List[dict] = []

    for dirpath, dirnames, filenames in os.walk(mods_root):
        if is_under_trash(mods_root, dirpath):
            dirnames[:] = []
            continue

        for d in list(dirnames):
            if d in OS_JUNK_DIRS:
                items.append({"kind": "OS junk folder", "path": os.path.join(dirpath, d)})

        for f in filenames:
            full = os.path.join(dirpath, f)
            if f in OS_JUNK_FILES:
                items.append({"kind": "OS junk file", "path": full})
            elif f == MAC_ICON_FILE:
                items.append({"kind": "OS junk file", "path": full})
            elif f.startswith(APPLEDOUBLE_PREFIX):
                items.append({"kind": "OS junk file (AppleDouble)", "path": full})

    return items

def detect_empty_top_level_folders(mods_root: str) -> List[dict]:
    """Only empty folders directly inside Mods root (safer)."""
    items: List[dict] = []
    try:
        for name in os.listdir(mods_root):
            p = os.path.join(mods_root, name)
            if not os.path.isdir(p):
                continue
            if name.startswith(TRASH_PREFIX):
                continue
            try:
                if len(os.listdir(p)) == 0:
                    items.append({"kind": "Empty top-level folder", "path": p})
            except Exception:
                continue
    except Exception:
        pass
    return items

def detect_archive_leftovers(mods_root: str) -> List[dict]:
    """Optional: find archives directly in Mods root (common leftover clutter)."""
    items: List[dict] = []
    try:
        for name in os.listdir(mods_root):
            p = os.path.join(mods_root, name)
            if os.path.isfile(p):
                ext = os.path.splitext(name)[1].lower()
                if ext in ARCHIVE_EXTS:
                    items.append({"kind": "Archive leftover", "path": p})
    except Exception:
        pass
    return items

def detect_duplicate_uniqueids(mods_root: str) -> List[dict]:
    """Find mods sharing the same UniqueID (real SMAPI identity)."""
    mods: List[dict] = []
    for folder, manifest_path in iter_manifest_folders(mods_root):
        data = read_manifest(manifest_path)
        if not data:
            continue

        unique_id = str(data.get("UniqueID", "")).strip()
        name = str(data.get("Name", "")).strip()
        version = str(data.get("Version", "")).strip()

        if not unique_id:
            continue

        mods.append({
            "folder": folder,
            "unique_id": unique_id,
            "name": name,
            "version": version
        })

    by_id: Dict[str, List[dict]] = {}
    for m in mods:
        by_id.setdefault(m["unique_id"], []).append(m)

    dup_items: List[dict] = []
    for uid, group in by_id.items():
        if len(group) <= 1:
            continue
        paths = [g["folder"] for g in group]
        for m in group:
            dup_items.append({
                "kind": "Duplicate UniqueID",
                "path": m["folder"],
                "mod_name": m["name"] or "(unknown name)",
                "unique_id": uid,
                "version": m["version"] or "",
                "group_paths": paths,
                "group_count": len(group),
            })

    return dup_items

def detect_nested_mods(mods_root: str) -> List[dict]:
    """Detect top-level folders that contain nested 'Mods' with actual mod folders inside."""
    items: List[dict] = []
    try:
        for name in os.listdir(mods_root):
            top = os.path.join(mods_root, name)
            if not os.path.isdir(top):
                continue
            if name.startswith(TRASH_PREFIX):
                continue

            nested = os.path.join(top, "Mods")
            if not os.path.isdir(nested):
                continue

            found_mods = []
            try:
                for child in os.listdir(nested):
                    cpath = os.path.join(nested, child)
                    if os.path.isdir(cpath) and find_manifest_json(cpath):
                        found_mods.append(cpath)
            except Exception:
                pass

            if found_mods:
                items.append({
                    "kind": "Nested Mods/Mods",
                    "path": top,
                    "nested_mods_path": nested,
                    "nested_mod_folders": found_mods,
                    "nested_count": len(found_mods),
                })
    except Exception:
        pass

    return items


# ----------------------------
# "Why is this flagged?"
# ----------------------------

def why_flagged(item: dict) -> str:
    kind = item.get("kind", "")
    path = item.get("path", "")
    name = basename_or_empty(path)

    if kind == "OS junk file":
        if name == ".DS_Store":
            return "This is macOS Finder metadata. It can be bundled in zips from Nexus. Stardew/SMAPI doesn't use it."
        if name.lower() in ("thumbs.db", "ehthumbs.db"):
            return "This is Windows thumbnail cache metadata. Stardew/SMAPI doesn't use it."
        if name == MAC_ICON_FILE:
            return "This is macOS Finder icon metadata. Not used by Stardew/SMAPI."
        return "This is OS metadata/cache. Not used by Stardew/SMAPI."

    if kind == "OS junk file (AppleDouble)":
        return "These '._*' files are macOS resource fork metadata created when copying/zipping. Not used by Stardew/SMAPI."

    if kind == "OS junk folder":
        if name == "__MACOSX":
            return "This folder is created when zipping on macOS. It's safe to remove and doesn't affect mods."
        return "This folder is OS metadata. It doesn't affect Stardew/SMAPI."

    if kind == "Empty top-level folder":
        return "This folder is empty, usually leftover from a failed/partial extraction or a removed mod."

    if kind == "Archive leftover":
        return "This looks like a downloaded archive left in Mods. After extracting, it's not needed for the game."

    if kind == "Duplicate UniqueID":
        uid = item.get("unique_id", "")
        cnt = item.get("group_count", 0)
        return f"Multiple folders share the same SMAPI UniqueID ({uid}). SMAPI treats them as the same mod and this can cause conflicts. You usually want to keep only one copy. (Found {cnt} copies.)"

    if kind == "Nested Mods/Mods":
        return "This usually means a zip was extracted into an extra wrapper folder. Mods inside may not load correctly until moved up into the main Mods folder."

    return "Flagged by a cleanup rule."


# ----------------------------
# GUI
# ----------------------------

class ModCleanerApp(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("Stardew Mod Folder Cleaner")
        self.geometry("1180x700")
        self.minsize(980, 580)

        self.mods_path = tk.StringVar()

        self.safe_mode = tk.BooleanVar(value=True)

        self.include_empty = tk.BooleanVar(value=True)
        self.include_os_junk = tk.BooleanVar(value=True)

        self.include_duplicates = tk.BooleanVar(value=True)
        self.include_nested_fix = tk.BooleanVar(value=False)

        self.include_archives = tk.BooleanVar(value=False)

        self.items: List[dict] = []
        self.row_iids: List[str] = []
        self.last_trash_dir: Optional[str] = None

        self._build_ui()

        self.safe_mode.trace_add("write", lambda *_: self.refresh_actions())

    def _build_ui(self):
        root = ttk.Frame(self, padding=10)
        root.pack(fill="both", expand=True)

        # Top row
        top = ttk.Frame(root)
        top.pack(fill="x", pady=(0, 8))

        ttk.Label(top, text="Mods folder:").pack(side="left")
        ttk.Entry(top, textvariable=self.mods_path).pack(side="left", fill="x", expand=True, padx=8)
        ttk.Button(top, text="Browse…", command=self.pick_folder).pack(side="left")
        ttk.Button(top, text="Scan", command=self.scan).pack(side="left", padx=(8, 0))

        # Options
        opts = ttk.LabelFrame(root, text="Options", padding=10)
        opts.pack(fill="x", pady=(0, 10))

        row1 = ttk.Frame(opts)
        row1.pack(fill="x")

        ttk.Checkbutton(row1, text="Move to Trash (recommended)", variable=self.safe_mode).pack(side="left")
        ttk.Checkbutton(row1, text="Empty top-level folders", variable=self.include_empty).pack(side="left", padx=12)

        ttk.Checkbutton(
            row1,
            text="OS junk (.DS_Store, __MACOSX, Thumbs.db, ehthumbs.db, '._*', Icon\\r)",
            variable=self.include_os_junk
        ).pack(side="left", padx=12)

        row2 = ttk.Frame(opts)
        row2.pack(fill="x", pady=(6, 0))

        ttk.Checkbutton(row2, text="Duplicate mods (by manifest UniqueID)", variable=self.include_duplicates).pack(side="left")
        ttk.Checkbutton(row2, text="Detect nested Mods/Mods (offer flatten)", variable=self.include_nested_fix).pack(side="left", padx=12)
        ttk.Checkbutton(row2, text="Archive leftovers in Mods root (.zip/.7z/.rar)", variable=self.include_archives).pack(side="left", padx=12)

        # Split panel
        main = ttk.PanedWindow(root, orient="horizontal")
        main.pack(fill="both", expand=True)

        left = ttk.Frame(main, padding=(0, 0, 8, 0))
        right = ttk.Frame(main)
        main.add(left, weight=3)
        main.add(right, weight=2)

        # Left header
        left_head = ttk.Frame(left)
        left_head.pack(fill="x", pady=(0, 6))
        ttk.Label(left_head, text="Scan Results").pack(side="left")
        ttk.Button(left_head, text="Select None", command=self.select_none).pack(side="right")
        ttk.Button(left_head, text="Select All", command=self.select_all).pack(side="right", padx=(0, 6))

        # Treeview (kept compact)
        cols = ("sel", "kind", "info", "path", "action")
        self.tree = ttk.Treeview(left, columns=cols, show="headings", selectmode="browse")

        self.tree.heading("sel", text="Sel")
        self.tree.heading("kind", text="Type")
        self.tree.heading("info", text="Info")
        self.tree.heading("path", text="Path")
        self.tree.heading("action", text="Planned Action")

        self.tree.column("sel", width=48, anchor="center")
        self.tree.column("kind", width=190, anchor="w")
        self.tree.column("info", width=280, anchor="w")
        self.tree.column("path", width=540, anchor="w")
        self.tree.column("action", width=140, anchor="w")

        vsb = ttk.Scrollbar(left, orient="vertical", command=self.tree.yview)
        self.tree.configure(yscrollcommand=vsb.set)

        self.tree.pack(side="left", fill="both", expand=True)
        vsb.pack(side="left", fill="y")

        self.tree.bind("<<TreeviewSelect>>", self.on_select_row)
        self.tree.bind("<Double-1>", self.toggle_selected_row)
        self.tree.bind("<space>", self.toggle_selected_row)

        # Preview
        preview_box = ttk.LabelFrame(right, text="Preview", padding=10)
        preview_box.pack(fill="both", expand=True)

        self.preview = tk.Text(preview_box, wrap="word")
        self.preview.pack(fill="both", expand=True)

        # Bottom actions
        bottom = ttk.Frame(root)
        bottom.pack(fill="x", pady=(10, 0))

        ttk.Button(bottom, text="Apply Selected Cleanup", command=self.apply).pack(side="left")
        ttk.Button(bottom, text="Open Mods Folder", command=self.open_mods_folder).pack(side="left", padx=8)
        ttk.Button(bottom, text="Open Selected Item", command=self.open_selected_item).pack(side="left")
        ttk.Button(bottom, text="Open Latest Trash", command=self.open_latest_trash).pack(side="left", padx=8)

        self.status = ttk.Label(bottom, text="Ready.")
        self.status.pack(side="right")

    # ---------- UI helpers ----------

    def set_status(self, msg: str):
        self.status.config(text=msg)
        self.update_idletasks()

    def pick_folder(self):
        folder = filedialog.askdirectory(title="Select your Stardew Valley Mods folder")
        if folder:
            self.mods_path.set(folder)

    def clear_results(self):
        for row in self.tree.get_children():
            self.tree.delete(row)
        self.items.clear()
        self.row_iids.clear()
        self.preview.delete("1.0", "end")

    def plan_action(self, kind: str) -> str:
        if kind == "Nested Mods/Mods":
            return "Flatten"
        if self.safe_mode.get():
            return "Move to Trash"
        return "Delete"

    def refresh_actions(self):
        for idx, iid in enumerate(self.row_iids):
            if idx >= len(self.items):
                continue
            it = self.items[idx]
            it["action"] = self.plan_action(it["kind"])
            values = list(self.tree.item(iid, "values"))
            values[-1] = it["action"]
            self.tree.item(iid, values=tuple(values))

    def make_info_cell(self, it: dict) -> str:
        kind = it.get("kind", "")
        if kind == "Duplicate UniqueID":
            name = it.get("mod_name", "(unknown)")
            uid = it.get("unique_id", "")
            ver = it.get("version", "")
            tail = f" v{ver}" if ver else ""
            return f"{name}{tail} [{uid}]"
        if kind == "Nested Mods/Mods":
            cnt = it.get("nested_count", 0)
            return f"{cnt} mod folder(s) inside nested Mods/"
        # for OS junk or archives, show filename
        return basename_or_empty(it.get("path", ""))

    def scan_summary(self) -> str:
        counts: Dict[str, int] = {}
        selected_count = 0
        for it in self.items:
            counts[it.get("kind", "")] = counts.get(it.get("kind", ""), 0) + 1
            if it.get("selected"):
                selected_count += 1

        lines = []
        lines.append("Summary:")
        for k in sorted(counts.keys()):
            lines.append(f"  - {k}: {counts[k]}")
        lines.append("")
        lines.append(f"Selected: {selected_count} / {len(self.items)}")
        lines.append("")
        lines.append("Controls:")
        lines.append("  - Double-click or Space: toggle selection for the highlighted row.")
        lines.append("  - Safe mode preserves folder structure under one _Trash_ folder per Apply.")
        lines.append("")
        lines.append("Tip:")
        lines.append("  - OS junk often comes from the creator's OS (macOS/Windows) inside a Nexus zip. It's normal.")
        return "\n".join(lines)

    def get_selected_index(self) -> Optional[int]:
        sel = self.tree.selection()
        if not sel:
            return None
        iid = sel[0]
        try:
            return self.row_iids.index(iid)
        except ValueError:
            return None

    def on_select_row(self, _event=None):
        idx = self.get_selected_index()
        if idx is None or idx < 0 or idx >= len(self.items):
            return
        it = self.items[idx]
        self.preview.delete("1.0", "end")
        self.preview.insert("1.0", self.describe_item(it))

    def toggle_selected_row(self, _event=None):
        idx = self.get_selected_index()
        if idx is None or idx < 0 or idx >= len(self.items):
            return
        it = self.items[idx]
        it["selected"] = not it["selected"]

        iid = self.row_iids[idx]
        values = list(self.tree.item(iid, "values"))
        values[0] = "☑" if it["selected"] else "☐"
        self.tree.item(iid, values=tuple(values))

        self.preview.delete("1.0", "end")
        self.preview.insert("1.0", self.describe_item(it))

        self.set_status(f"Toggled selection. ({sum(1 for x in self.items if x.get('selected'))} selected)")

    def describe_item(self, it: dict) -> str:
        lines = [
            f"Selected: {it.get('selected', False)}",
            f"Type:     {it.get('kind', '')}",
            f"Path:     {it.get('path', '')}",
            f"Action:   {it.get('action', '')}",
            "",
            "Why is this flagged?",
            f"  {why_flagged(it)}",
        ]

        if it.get("kind") == "Duplicate UniqueID":
            lines.append("")
            lines.append("Other installed copies (same UniqueID):")
            for p in it.get("group_paths", []):
                lines.append(f"  - {p}")

        if it.get("kind") == "Nested Mods/Mods":
            lines.append("")
            lines.append("Detected mod folders inside nested Mods/:")
            for p in it.get("nested_mod_folders", []):
                lines.append(f"  - {p}")

        return "\n".join(lines)

    def select_all(self):
        for idx, it in enumerate(self.items):
            it["selected"] = True
            iid = self.row_iids[idx]
            values = list(self.tree.item(iid, "values"))
            values[0] = "☑"
            self.tree.item(iid, values=tuple(values))
        self.set_status("Selected all.")

    def select_none(self):
        for idx, it in enumerate(self.items):
            it["selected"] = False
            iid = self.row_iids[idx]
            values = list(self.tree.item(iid, "values"))
            values[0] = "☐"
            self.tree.item(iid, values=tuple(values))
        self.set_status("Selected none.")

    # ---------- scan/apply ----------

    def scan(self):
        mods_root = self.mods_path.get().strip()
        if not mods_root or not os.path.isdir(mods_root):
            messagebox.showerror("Missing folder", "Please select a valid Mods folder.")
            return

        self.clear_results()
        self.set_status("Scanning…")

        found: List[dict] = []

        if self.include_empty.get():
            found.extend(detect_empty_top_level_folders(mods_root))

        if self.include_os_junk.get():
            found.extend(detect_os_junk(mods_root))

        if self.include_duplicates.get():
            found.extend(detect_duplicate_uniqueids(mods_root))

        if self.include_nested_fix.get():
            found.extend(detect_nested_mods(mods_root))

        if self.include_archives.get():
            found.extend(detect_archive_leftovers(mods_root))

        # de-dup by kind+path
        seen = set()
        unique: List[dict] = []
        for it in found:
            key = (it.get("kind"), os.path.normpath(it.get("path", "")))
            if key in seen:
                continue
            seen.add(key)
            unique.append(it)

        # selection defaults:
        # - OS junk + empty folders pre-selected
        # - duplicates / flatten / archives NOT pre-selected
        for it in unique:
            kind = it.get("kind", "")
            it["selected"] = kind in ("Empty top-level folder", "OS junk file", "OS junk folder", "OS junk file (AppleDouble)")
            if kind in ("Duplicate UniqueID", "Nested Mods/Mods", "Archive leftover"):
                it["selected"] = False
            it["action"] = self.plan_action(kind)

        self.items = unique

        for it in self.items:
            sel_text = "☑" if it["selected"] else "☐"
            kind = it.get("kind", "")
            info = self.make_info_cell(it)
            path = it.get("path", "")
            action = it.get("action", "")

            iid = self.tree.insert("", "end", values=(sel_text, kind, info, path, action))
            self.row_iids.append(iid)

        self.preview.insert("1.0", self.scan_summary())
        self.set_status(f"Found {len(self.items)} item(s). Double-click or press Space to toggle selection.")

    def open_mods_folder(self):
        p = self.mods_path.get().strip()
        if p and os.path.isdir(p):
            open_in_explorer(p)
        else:
            messagebox.showinfo("No folder", "Select a Mods folder first.")

    def open_selected_item(self):
        idx = self.get_selected_index()
        if idx is None:
            messagebox.showinfo("No selection", "Select a row first.")
            return
        path = self.items[idx].get("path", "")
        if path and os.path.exists(path):
            open_in_explorer(path)
        else:
            messagebox.showinfo("Not found", "That path no longer exists.")

    def open_latest_trash(self):
        mods_root = self.mods_path.get().strip()
        if not mods_root or not os.path.isdir(mods_root):
            messagebox.showinfo("No folder", "Select a Mods folder first.")
            return

        if self.last_trash_dir and os.path.isdir(self.last_trash_dir):
            open_in_explorer(self.last_trash_dir)
            return

        trash_folders = []
        try:
            for name in os.listdir(mods_root):
                if name.startswith(TRASH_PREFIX):
                    p = os.path.join(mods_root, name)
                    if os.path.isdir(p):
                        trash_folders.append(p)
        except Exception:
            pass

        if not trash_folders:
            messagebox.showinfo("No trash found", "No _Trash_ folder found in Mods yet.")
            return

        latest = sorted(trash_folders)[-1]
        open_in_explorer(latest)

    def apply(self):
        mods_root = self.mods_path.get().strip()
        if not mods_root or not os.path.isdir(mods_root):
            messagebox.showerror("Missing folder", "Please select a valid Mods folder.")
            return

        selected = [it for it in self.items if it.get("selected")]
        if not selected:
            messagebox.showinfo("Nothing selected", "No items selected.")
            return

        mode = "Move to Trash" if self.safe_mode.get() else "Delete permanently"
        if not messagebox.askyesno("Confirm cleanup", f"Apply cleanup to {len(selected)} item(s)?\nMode: {mode}"):
            return

        self.set_status("Applying cleanup…")

        self.last_trash_dir = ensure_trash_dir(mods_root) if self.safe_mode.get() else None

        moved = []
        failed = []

        for it in selected:
            kind = it.get("kind", "")
            path = it.get("path", "")
            try:
                if kind == "Nested Mods/Mods":
                    self.flatten_nested_mods(mods_root, it)
                    moved.append((kind, path, "Flattened"))
                    continue

                if self.safe_mode.get():
                    dest = move_to_trash(self.last_trash_dir, mods_root, path)  # type: ignore[arg-type]
                    moved.append((kind, path, f"Trashed -> {dest}"))
                else:
                    delete_permanently(path)
                    moved.append((kind, path, "Deleted"))
            except Exception as e:
                failed.append((kind, path, str(e)))

        self.scan()

        report = [f"Done. Success: {len(moved)}   Failed: {len(failed)}", ""]
        if self.last_trash_dir:
            report.append(f"Trash folder: {self.last_trash_dir}")
            report.append("")

        if failed:
            report.append("Failures:")
            for kind, path, err in failed[:20]:
                report.append(f"  - {kind}: {path}")
                report.append(f"    {err}")
            if len(failed) > 20:
                report.append(f"  (and {len(failed) - 20} more…)")

        self.preview.delete("1.0", "end")
        self.preview.insert("1.0", "\n".join(report))
        self.set_status("Cleanup complete.")

    def flatten_nested_mods(self, mods_root: str, it: dict):
        top_folder = it.get("path", "")
        nested = it.get("nested_mods_path", "")
        if not top_folder or not nested or not os.path.isdir(nested):
            return

        moved_any = False

        try:
            for child in os.listdir(nested):
                src = os.path.join(nested, child)
                if not os.path.isdir(src):
                    continue
                if not find_manifest_json(src):
                    continue  # skip non-mod folders

                dest = os.path.join(mods_root, os.path.basename(src))
                if os.path.exists(dest):
                    if self.safe_mode.get() and self.last_trash_dir:
                        move_to_trash(self.last_trash_dir, mods_root, src)
                        moved_any = True
                    else:
                        dest = os.path.join(mods_root, f"{os.path.basename(src)}_{now_stamp()}")
                        shutil.move(src, dest)
                        moved_any = True
                else:
                    shutil.move(src, dest)
                    moved_any = True
        except Exception:
            pass

        try:
            if os.path.isdir(nested) and len(os.listdir(nested)) == 0:
                os.rmdir(nested)
        except Exception:
            pass

        try:
            if moved_any and os.path.isdir(top_folder) and len(os.listdir(top_folder)) == 0:
                os.rmdir(top_folder)
        except Exception:
            pass


if __name__ == "__main__":
    # Better scaling on Windows HiDPI
    try:
        import ctypes
        ctypes.windll.shcore.SetProcessDpiAwareness(1)
    except Exception:
        pass

    app = ModCleanerApp()
    app.mainloop()
