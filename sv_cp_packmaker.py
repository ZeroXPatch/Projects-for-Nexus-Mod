from __future__ import annotations

import json
import os
import platform
import shutil
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Optional, Tuple, List

import tkinter as tk
from tkinter import ttk, filedialog, messagebox
from tkinter.scrolledtext import ScrolledText

from PIL import Image


# ---------------------------
# Presets
# ---------------------------

@dataclass(frozen=True)
class Preset:
    name: str
    cell_w: int
    cell_h: int
    default_target: str


PRESETS: list[Preset] = [
    Preset("Objects (16×16)", 16, 16, "TileSheets/objects"),
    Preset("Crops/Characters (16×32)", 16, 32, "TileSheets/crops"),
    Preset("Portraits (64×64)", 64, 64, "Portraits/Abigail"),  # example; user should change
    Preset("Player (Custom)", 16, 32, "Characters/Farmer"),
]


# ---------------------------
# Modes
# ---------------------------

MODE_AS_IS = "As-is (pad to grid only)"
MODE_RESIZE = "Resize to cells (N×M)"
MODE_EXTRACT_TILE = "Extract single tile (source X/Y → dest X/Y)"
MODE_SLICE_BLOCK = "Slice & patch a block (one patch per tile)"

MODES = [MODE_AS_IS, MODE_RESIZE, MODE_EXTRACT_TILE, MODE_SLICE_BLOCK]


def safe_name(name: str) -> str:
    bad = '<>:"/\\|?*'
    out = "".join("_" if c in bad else c for c in name).strip()
    return out or "ContentPack"


def log_stamp() -> str:
    return datetime.now().strftime("%H:%M:%S")


def is_windows() -> bool:
    return platform.system().lower() == "windows"


def is_macos() -> bool:
    return platform.system().lower() == "darwin"


def default_mods_dirs() -> list[Path]:
    home = Path.home()
    candidates: list[Path] = []
    if is_windows():
        candidates.append(Path(r"C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods"))
        candidates.append(home / "AppData" / "Local" / "StardewValley" / "Mods")
    elif is_macos():
        candidates.append(
            home
            / "Library"
            / "Application Support"
            / "Steam"
            / "steamapps"
            / "common"
            / "Stardew Valley"
            / "Contents"
            / "MacOS"
            / "Mods"
        )
    else:
        candidates.append(home / ".steam" / "steam" / "steamapps" / "common" / "Stardew Valley" / "Mods")
        candidates.append(home / ".local" / "share" / "Steam" / "steamapps" / "common" / "Stardew Valley" / "Mods")
    return candidates


def find_mods_dir() -> Optional[Path]:
    for p in default_mods_dirs():
        if p.exists() and p.is_dir():
            return p
    return None


def load_image(path: Path) -> Image.Image:
    img = Image.open(path)
    if img.mode != "RGBA":
        img = img.convert("RGBA")
    return img


def autocrop_transparent(img: Image.Image) -> Image.Image:
    alpha = img.split()[-1]
    bbox = alpha.getbbox()
    if bbox is None:
        return img
    return img.crop(bbox)


def pad_to_grid(img: Image.Image, cell_w: int, cell_h: int, pad_rgba=(0, 0, 0, 0)) -> Tuple[Image.Image, Tuple[int, int]]:
    w, h = img.size
    new_w = ((w + cell_w - 1) // cell_w) * cell_w
    new_h = ((h + cell_h - 1) // cell_h) * cell_h
    pad_right = new_w - w
    pad_bottom = new_h - h

    if pad_right == 0 and pad_bottom == 0:
        return img, (0, 0)

    new_img = Image.new("RGBA", (new_w, new_h), pad_rgba)
    new_img.paste(img, (0, 0))
    return new_img, (pad_right, pad_bottom)


def resize_nearest(img: Image.Image, size_px: Tuple[int, int]) -> Image.Image:
    return img.resize(size_px, resample=Image.NEAREST)


def tile_bbox(tile: Image.Image) -> Optional[Tuple[int, int, int, int]]:
    """Returns bbox of non-transparent pixels in tile alpha, or None if fully transparent."""
    alpha = tile.split()[-1]
    return alpha.getbbox()


def write_manifest(pack_dir: Path, name: str, author: str, version: str, description: str, unique_id: str) -> None:
    manifest = {
        "Name": name,
        "Author": author,
        "Version": version,
        "Description": description,
        "UniqueID": unique_id,
        "MinimumApiVersion": "4.0.0",
        "ContentPackFor": {"UniqueID": "Pathoschild.ContentPatcher"},
    }
    (pack_dir / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")


def write_content_json(pack_dir: Path, content: dict) -> None:
    (pack_dir / "content.json").write_text(json.dumps(content, indent=2), encoding="utf-8")


def copy_tree(src: Path, dst: Path) -> None:
    if dst.exists():
        shutil.rmtree(dst)
    shutil.copytree(src, dst)


# ---------------------------
# GUI App
# ---------------------------

class App(tk.Tk):
    def __init__(self) -> None:
        super().__init__()
        self.title("Stardew Content Patcher Pack Maker")
        self.minsize(920, 720)

        self.columnconfigure(0, weight=1)
        self.rowconfigure(1, weight=1)

        self.vars: dict[str, tk.Variable] = {
            "input_png": tk.StringVar(value=""),
            "output_dir": tk.StringVar(value=str(Path.cwd())),
            "mods_dir": tk.StringVar(value=""),
            "install": tk.BooleanVar(value=False),

            "preset": tk.StringVar(value=PRESETS[0].name),
            "cell_w": tk.IntVar(value=PRESETS[0].cell_w),
            "cell_h": tk.IntVar(value=PRESETS[0].cell_h),

            "mode": tk.StringVar(value=MODE_AS_IS),

            # Resize mode options
            "resize_tiles_w": tk.IntVar(value=1),
            "resize_tiles_h": tk.IntVar(value=1),

            # Extract tile options (source tile)
            "src_tile_x": tk.IntVar(value=0),
            "src_tile_y": tk.IntVar(value=0),

            # Slice options
            "skip_transparent_tiles": tk.BooleanVar(value=True),

            "pack_name": tk.StringVar(value="My Content Pack"),
            "author": tk.StringVar(value=os.environ.get("USERNAME") or os.environ.get("USER") or "Unknown"),
            "version": tk.StringVar(value="1.0.0"),
            "description": tk.StringVar(value="Generated by sv_cp_packmaker_gui.py"),
            "unique_id": tk.StringVar(value="YourName.GeneratedPack"),

            "target": tk.StringVar(value=PRESETS[0].default_target),
            "patch_mode": tk.StringVar(value="Overlay"),
            "place_x": tk.IntVar(value=0),
            "place_y": tk.IntVar(value=0),

            "cp_format": tk.StringVar(value="2.3.0"),
            "autocrop": tk.BooleanVar(value=True),
            "pad": tk.BooleanVar(value=True),
        }

        self._build_ui()
        self._bind_events()

        detected = find_mods_dir()
        if detected:
            self.vars["mods_dir"].set(str(detected))

        # Apply initial preset
        self._apply_preset()
        self._update_mode_ui()

    def _build_ui(self) -> None:
        top = ttk.Frame(self, padding=12)
        top.grid(row=0, column=0, sticky="ew")
        top.columnconfigure(1, weight=1)

        ttk.Label(top, text="Input PNG:").grid(row=0, column=0, sticky="w")
        ttk.Entry(top, textvariable=self.vars["input_png"]).grid(row=0, column=1, sticky="ew", padx=(6, 8))
        ttk.Button(top, text="Browse…", command=self._browse_png).grid(row=0, column=2, sticky="ew")

        ttk.Label(top, text="Output folder:").grid(row=1, column=0, sticky="w", pady=(8, 0))
        ttk.Entry(top, textvariable=self.vars["output_dir"]).grid(row=1, column=1, sticky="ew", padx=(6, 8), pady=(8, 0))
        ttk.Button(top, text="Browse…", command=self._browse_output).grid(row=1, column=2, sticky="ew", pady=(8, 0))

        ttk.Label(top, text="Mods folder (optional):").grid(row=2, column=0, sticky="w", pady=(8, 0))
        ttk.Entry(top, textvariable=self.vars["mods_dir"]).grid(row=2, column=1, sticky="ew", padx=(6, 8), pady=(8, 0))
        mods_btns = ttk.Frame(top)
        mods_btns.grid(row=2, column=2, sticky="ew", pady=(8, 0))
        mods_btns.columnconfigure(0, weight=1)
        mods_btns.columnconfigure(1, weight=1)
        ttk.Button(mods_btns, text="Browse…", command=self._browse_mods).grid(row=0, column=0, sticky="ew", padx=(0, 4))
        ttk.Button(mods_btns, text="Auto-detect", command=self._auto_mods).grid(row=0, column=1, sticky="ew")
        ttk.Checkbutton(top, text="Install into Mods folder", variable=self.vars["install"]).grid(row=3, column=1, sticky="w", pady=(8, 0))

        mid = ttk.Frame(self, padding=(12, 0, 12, 12))
        mid.grid(row=1, column=0, sticky="nsew")
        mid.columnconfigure(0, weight=1)
        mid.rowconfigure(2, weight=1)

        settings = ttk.LabelFrame(mid, text="Setup", padding=12)
        settings.grid(row=0, column=0, sticky="ew")
        for c in range(6):
            settings.columnconfigure(c, weight=1)

        ttk.Label(settings, text="Preset:").grid(row=0, column=0, sticky="w")
        ttk.Combobox(settings, textvariable=self.vars["preset"], values=[p.name for p in PRESETS], state="readonly") \
            .grid(row=0, column=1, columnspan=2, sticky="ew", padx=(6, 12))

        ttk.Label(settings, text="Cell W/H:").grid(row=0, column=3, sticky="w")
        ttk.Spinbox(settings, from_=1, to=512, textvariable=self.vars["cell_w"], width=6).grid(row=0, column=4, sticky="w", padx=(6, 6))
        ttk.Spinbox(settings, from_=1, to=512, textvariable=self.vars["cell_h"], width=6).grid(row=0, column=5, sticky="w")

        ttk.Label(settings, text="Mode:").grid(row=1, column=0, sticky="w", pady=(10, 0))
        ttk.Combobox(settings, textvariable=self.vars["mode"], values=MODES, state="readonly") \
            .grid(row=1, column=1, columnspan=5, sticky="ew", padx=(6, 0), pady=(10, 0))

        ttk.Label(settings, text="Pack name:").grid(row=2, column=0, sticky="w", pady=(10, 0))
        ttk.Entry(settings, textvariable=self.vars["pack_name"]).grid(row=2, column=1, columnspan=2, sticky="ew", padx=(6, 12), pady=(10, 0))
        ttk.Label(settings, text="UniqueID:").grid(row=2, column=3, sticky="w", pady=(10, 0))
        ttk.Entry(settings, textvariable=self.vars["unique_id"]).grid(row=2, column=4, columnspan=2, sticky="ew", padx=(6, 0), pady=(10, 0))

        ttk.Label(settings, text="Author:").grid(row=3, column=0, sticky="w", pady=(6, 0))
        ttk.Entry(settings, textvariable=self.vars["author"]).grid(row=3, column=1, sticky="ew", padx=(6, 12), pady=(6, 0))
        ttk.Label(settings, text="Version:").grid(row=3, column=2, sticky="w", pady=(6, 0))
        ttk.Entry(settings, textvariable=self.vars["version"]).grid(row=3, column=3, sticky="ew", padx=(6, 12), pady=(6, 0))
        ttk.Label(settings, text="CP Format:").grid(row=3, column=4, sticky="w", pady=(6, 0))
        ttk.Entry(settings, textvariable=self.vars["cp_format"]).grid(row=3, column=5, sticky="ew", pady=(6, 0))

        ttk.Label(settings, text="Description:").grid(row=4, column=0, sticky="w", pady=(6, 0))
        ttk.Entry(settings, textvariable=self.vars["description"]).grid(row=4, column=1, columnspan=5, sticky="ew", padx=(6, 0), pady=(6, 0))

        patch = ttk.LabelFrame(mid, text="Content Patcher EditImage", padding=12)
        patch.grid(row=1, column=0, sticky="ew", pady=(12, 0))
        patch.columnconfigure(1, weight=1)

        ttk.Label(patch, text="Target:").grid(row=0, column=0, sticky="w")
        ttk.Entry(patch, textvariable=self.vars["target"]).grid(row=0, column=1, columnspan=5, sticky="ew", padx=(6, 0))

        ttk.Label(patch, text="PatchMode:").grid(row=1, column=0, sticky="w", pady=(8, 0))
        ttk.Combobox(patch, textvariable=self.vars["patch_mode"], values=["Overlay", "Replace"], state="readonly") \
            .grid(row=1, column=1, sticky="w", padx=(6, 12), pady=(8, 0))

        ttk.Label(patch, text="Dest (tiles) X/Y:").grid(row=1, column=2, sticky="w", pady=(8, 0))
        ttk.Spinbox(patch, from_=0, to=999999, textvariable=self.vars["place_x"], width=10).grid(row=1, column=3, sticky="w", padx=(6, 6), pady=(8, 0))
        ttk.Spinbox(patch, from_=0, to=999999, textvariable=self.vars["place_y"], width=10).grid(row=1, column=4, sticky="w", pady=(8, 0))

        ttk.Checkbutton(patch, text="Auto-crop transparent border", variable=self.vars["autocrop"]).grid(row=2, column=0, columnspan=2, sticky="w", pady=(8, 0))
        ttk.Checkbutton(patch, text="Pad to grid multiples", variable=self.vars["pad"]).grid(row=2, column=2, columnspan=2, sticky="w", pady=(8, 0))

        # Mode-specific options area
        self.mode_frame = ttk.LabelFrame(mid, text="Mode Options", padding=12)
        self.mode_frame.grid(row=2, column=0, sticky="nsew", pady=(12, 0))
        self.mode_frame.columnconfigure(1, weight=1)

        # Resize options
        self.resize_row = ttk.Frame(self.mode_frame)
        self.resize_row.grid(row=0, column=0, sticky="ew")
        self.resize_row.columnconfigure(3, weight=1)

        ttk.Label(self.resize_row, text="Resize to tiles (W×H):").grid(row=0, column=0, sticky="w")
        ttk.Spinbox(self.resize_row, from_=1, to=9999, textvariable=self.vars["resize_tiles_w"], width=8).grid(row=0, column=1, sticky="w", padx=(6, 6))
        ttk.Spinbox(self.resize_row, from_=1, to=9999, textvariable=self.vars["resize_tiles_h"], width=8).grid(row=0, column=2, sticky="w")
        ttk.Label(self.resize_row, text="(Set 1×1 to force a single 16×16 / 16×32 tile)").grid(row=0, column=3, sticky="w", padx=(12, 0))

        # Extract tile options
        self.extract_row = ttk.Frame(self.mode_frame)
        self.extract_row.grid(row=1, column=0, sticky="ew", pady=(10, 0))
        ttk.Label(self.extract_row, text="Source tile X/Y:").grid(row=0, column=0, sticky="w")
        ttk.Spinbox(self.extract_row, from_=0, to=999999, textvariable=self.vars["src_tile_x"], width=10).grid(row=0, column=1, sticky="w", padx=(6, 6))
        ttk.Spinbox(self.extract_row, from_=0, to=999999, textvariable=self.vars["src_tile_y"], width=10).grid(row=0, column=2, sticky="w")
        ttk.Label(self.extract_row, text="(Extracts exactly 1 cell and patches it to Dest X/Y above)").grid(row=0, column=3, sticky="w", padx=(12, 0))

        # Slice block options
        self.slice_row = ttk.Frame(self.mode_frame)
        self.slice_row.grid(row=2, column=0, sticky="ew", pady=(10, 0))
        ttk.Checkbutton(self.slice_row, text="Skip fully transparent tiles", variable=self.vars["skip_transparent_tiles"]) \
            .grid(row=0, column=0, sticky="w")
        ttk.Label(self.slice_row, text="(Exports assets/tiles/ and generates one patch per tile, placed starting at Dest X/Y)").grid(
            row=0, column=1, sticky="w", padx=(12, 0)
        )

        hint = ttk.Label(
            mid,
            text="Important: Cell size is the tile grid. As-is mode only pads. Resize/Slice will actively change/export tiles.",
            foreground="#555"
        )
        hint.grid(row=3, column=0, sticky="w", pady=(10, 0))

        bottom = ttk.Frame(self, padding=12)
        bottom.grid(row=2, column=0, sticky="nsew")
        bottom.columnconfigure(0, weight=1)
        bottom.rowconfigure(0, weight=1)

        self.log_box = ScrolledText(bottom, height=12, wrap="word")
        self.log_box.grid(row=0, column=0, sticky="nsew")

        btns = ttk.Frame(bottom)
        btns.grid(row=1, column=0, sticky="ew", pady=(10, 0))
        btns.columnconfigure(0, weight=1)
        btns.columnconfigure(1, weight=1)
        btns.columnconfigure(2, weight=1)

        ttk.Button(btns, text="Build Pack", command=self._build_pack).grid(row=0, column=0, sticky="ew", padx=(0, 6))
        ttk.Button(btns, text="Open Output Folder", command=self._open_output).grid(row=0, column=1, sticky="ew", padx=(0, 6))
        ttk.Button(btns, text="Quit", command=self.destroy).grid(row=0, column=2, sticky="ew")

    def _bind_events(self) -> None:
        self.vars["preset"].trace_add("write", lambda *_: self._apply_preset())
        self.vars["mode"].trace_add("write", lambda *_: self._update_mode_ui())

    def _apply_preset(self) -> None:
        name = str(self.vars["preset"].get())
        preset = next((p for p in PRESETS if p.name == name), None)
        if not preset:
            return
        self.vars["cell_w"].set(preset.cell_w)
        self.vars["cell_h"].set(preset.cell_h)
        self.vars["target"].set(preset.default_target)
        self._log(f"Preset applied: {preset.name} (cell {preset.cell_w}×{preset.cell_h}, target '{preset.default_target}')")

    def _update_mode_ui(self) -> None:
        mode = str(self.vars["mode"].get())

        # Show/hide mode rows
        def set_visible(widget: ttk.Frame, visible: bool) -> None:
            widget.grid_remove() if not visible else widget.grid()

        set_visible(self.resize_row, mode == MODE_RESIZE)
        set_visible(self.extract_row, mode == MODE_EXTRACT_TILE)
        set_visible(self.slice_row, mode == MODE_SLICE_BLOCK)

        self._log(f"Mode set: {mode}")

    def _browse_png(self) -> None:
        f = filedialog.askopenfilename(title="Select PNG", filetypes=[("PNG image", "*.png"), ("All files", "*.*")])
        if f:
            self.vars["input_png"].set(f)
            self._log(f"Selected input: {f}")

    def _browse_output(self) -> None:
        d = filedialog.askdirectory(title="Select output folder")
        if d:
            self.vars["output_dir"].set(d)
            self._log(f"Selected output folder: {d}")

    def _browse_mods(self) -> None:
        d = filedialog.askdirectory(title="Select Stardew Valley Mods folder")
        if d:
            self.vars["mods_dir"].set(d)
            self._log(f"Selected Mods folder: {d}")

    def _auto_mods(self) -> None:
        detected = find_mods_dir()
        if detected:
            self.vars["mods_dir"].set(str(detected))
            self._log(f"Auto-detected Mods folder: {detected}")
        else:
            messagebox.showwarning("Not found", "Could not auto-detect Mods folder. Please browse to it manually.")

    def _open_output(self) -> None:
        out_dir = Path(str(self.vars["output_dir"].get())).expanduser().resolve()
        if not out_dir.exists():
            messagebox.showerror("Missing folder", "Output folder does not exist.")
            return
        try:
            if is_windows():
                os.startfile(str(out_dir))  # type: ignore[attr-defined]
            elif is_macos():
                os.system(f'open "{out_dir}"')
            else:
                os.system(f'xdg-open "{out_dir}"')
        except Exception as ex:
            messagebox.showerror("Failed", f"Could not open folder:\n{ex}")

    def _log(self, msg: str) -> None:
        self.log_box.insert("end", f"[{log_stamp()}] {msg}\n")
        self.log_box.see("end")

    def _validate(self) -> Optional[str]:
        input_png = Path(str(self.vars["input_png"].get())).expanduser()
        if not str(input_png) or not input_png.exists():
            return "Please select a valid input PNG."

        out_dir = Path(str(self.vars["output_dir"].get())).expanduser()
        if not str(out_dir) or not out_dir.exists():
            return "Please select a valid output folder."

        unique_id = str(self.vars["unique_id"].get()).strip()
        if not unique_id or "." not in unique_id:
            return "UniqueID should look like 'YourName.ModName' (must contain a dot)."

        target = str(self.vars["target"].get()).strip()
        if not target:
            return "Target cannot be empty (e.g. TileSheets/objects)."

        cell_w = int(self.vars["cell_w"].get())
        cell_h = int(self.vars["cell_h"].get())
        if cell_w <= 0 or cell_h <= 0:
            return "Cell width/height must be > 0."

        mode = str(self.vars["mode"].get())
        if mode == MODE_RESIZE:
            tw = int(self.vars["resize_tiles_w"].get())
            th = int(self.vars["resize_tiles_h"].get())
            if tw <= 0 or th <= 0:
                return "Resize tiles W/H must be > 0."
        elif mode == MODE_EXTRACT_TILE:
            sx = int(self.vars["src_tile_x"].get())
            sy = int(self.vars["src_tile_y"].get())
            if sx < 0 or sy < 0:
                return "Source tile X/Y must be >= 0."

        if bool(self.vars["install"].get()):
            mods_dir = Path(str(self.vars["mods_dir"].get())).expanduser()
            if not str(mods_dir) or not mods_dir.exists():
                return "Install is enabled, but Mods folder is missing/invalid. Set Mods folder or disable install."

        return None

    def _build_pack(self) -> None:
        err = self._validate()
        if err:
            messagebox.showerror("Fix required", err)
            return

        try:
            input_png = Path(str(self.vars["input_png"].get())).expanduser().resolve()
            output_root = Path(str(self.vars["output_dir"].get())).expanduser().resolve()

            pack_name = str(self.vars["pack_name"].get()).strip()
            author = str(self.vars["author"].get()).strip()
            version = str(self.vars["version"].get()).strip()
            description = str(self.vars["description"].get()).strip()
            unique_id = str(self.vars["unique_id"].get()).strip()

            target = str(self.vars["target"].get()).strip()
            patch_mode = str(self.vars["patch_mode"].get()).strip()
            place_x = int(self.vars["place_x"].get())
            place_y = int(self.vars["place_y"].get())

            cell_w = int(self.vars["cell_w"].get())
            cell_h = int(self.vars["cell_h"].get())

            cp_format = str(self.vars["cp_format"].get()).strip()
            do_autocrop = bool(self.vars["autocrop"].get())
            do_pad = bool(self.vars["pad"].get())

            mode = str(self.vars["mode"].get())
            install = bool(self.vars["install"].get())
            mods_dir = Path(str(self.vars["mods_dir"].get())).expanduser().resolve() if install else None

            self._log("Building pack…")
            self._log(f"Mode: {mode}")
            self._log(f"Input: {input_png.name}")
            self._log(f"Target: {target} | PatchMode: {patch_mode} | Dest tiles: ({place_x}, {place_y})")
            self._log(f"Cell: {cell_w}×{cell_h} | AutoCrop: {do_autocrop} | Pad: {do_pad}")

            img = load_image(input_png)
            self._log(f"Loaded image: {img.size[0]}×{img.size[1]} px")

            if do_autocrop:
                before = img.size
                img = autocrop_transparent(img)
                if img.size != before:
                    self._log(f"Auto-cropped to: {img.size[0]}×{img.size[1]} px")

            if do_pad:
                before = img.size
                img, (pr, pb) = pad_to_grid(img, cell_w, cell_h)
                if img.size != before:
                    self._log(f"Padded to grid: {img.size[0]}×{img.size[1]} px (pad right {pr}, bottom {pb})")

            # For slice/extract modes, ensure divisible if not padded
            if not do_pad and mode in (MODE_EXTRACT_TILE, MODE_SLICE_BLOCK):
                if img.size[0] % cell_w != 0 or img.size[1] % cell_h != 0:
                    raise ValueError("Image is not divisible by cell size. Enable 'Pad to grid multiples' or fix image size.")

            safe_pack_folder = safe_name(pack_name)
            pack_dir = output_root / safe_pack_folder
            assets_dir = pack_dir / "assets"
            assets_dir.mkdir(parents=True, exist_ok=True)

            changes: List[dict] = []

            # Helper for ToArea conversion
            def to_area_from_tiles(x_tiles: int, y_tiles: int, w_px: int, h_px: int) -> dict:
                return {
                    "X": x_tiles * cell_w,
                    "Y": y_tiles * cell_h,
                    "Width": w_px,
                    "Height": h_px,
                }

            if mode == MODE_AS_IS:
                out_png_name = safe_name(input_png.stem) + ".png"
                out_png = assets_dir / out_png_name
                img.save(out_png, "PNG")
                self._log(f"Saved asset: {out_png}")

                changes.append({
                    "Action": "EditImage",
                    "Target": target,
                    "FromFile": f"assets/{out_png_name}",
                    "PatchMode": patch_mode,
                    "ToArea": to_area_from_tiles(place_x, place_y, img.size[0], img.size[1]),
                })

            elif mode == MODE_RESIZE:
                tw = int(self.vars["resize_tiles_w"].get())
                th = int(self.vars["resize_tiles_h"].get())

                dest_px = (cell_w * tw, cell_h * th)
                self._log(f"Resizing image to: {dest_px[0]}×{dest_px[1]} px (tiles {tw}×{th})")
                resized = resize_nearest(img, dest_px)

                out_png_name = safe_name(input_png.stem) + f"_resized_{tw}x{th}.png"
                out_png = assets_dir / out_png_name
                resized.save(out_png, "PNG")
                self._log(f"Saved resized asset: {out_png}")

                changes.append({
                    "Action": "EditImage",
                    "Target": target,
                    "FromFile": f"assets/{out_png_name}",
                    "PatchMode": patch_mode,
                    "ToArea": to_area_from_tiles(place_x, place_y, resized.size[0], resized.size[1]),
                })

            elif mode == MODE_EXTRACT_TILE:
                sx = int(self.vars["src_tile_x"].get())
                sy = int(self.vars["src_tile_y"].get())

                cols = img.size[0] // cell_w
                rows = img.size[1] // cell_h
                self._log(f"Sheet tiles: {cols}×{rows}")

                if sx >= cols or sy >= rows:
                    raise ValueError(f"Source tile ({sx},{sy}) is out of bounds for {cols}×{rows}.")

                left = sx * cell_w
                top = sy * cell_h
                tile = img.crop((left, top, left + cell_w, top + cell_h))

                out_png_name = f"tile_{sx}_{sy}.png"
                out_png = assets_dir / out_png_name
                tile.save(out_png, "PNG")
                self._log(f"Extracted tile saved: {out_png_name}")

                changes.append({
                    "Action": "EditImage",
                    "Target": target,
                    "FromFile": f"assets/{out_png_name}",
                    "PatchMode": patch_mode,
                    "ToArea": to_area_from_tiles(place_x, place_y, cell_w, cell_h),
                })

            elif mode == MODE_SLICE_BLOCK:
                cols = img.size[0] // cell_w
                rows = img.size[1] // cell_h
                self._log(f"Slicing into tiles: {cols}×{rows} (cell {cell_w}×{cell_h})")

                tiles_dir = assets_dir / "tiles"
                tiles_dir.mkdir(parents=True, exist_ok=True)

                skip_empty = bool(self.vars["skip_transparent_tiles"].get())
                exported = 0
                patched = 0

                for ty in range(rows):
                    for tx in range(cols):
                        left = tx * cell_w
                        top = ty * cell_h
                        tile = img.crop((left, top, left + cell_w, top + cell_h))

                        if skip_empty and tile_bbox(tile) is None:
                            continue

                        tile_name = f"tile_{tx}_{ty}.png"
                        tile_path = tiles_dir / tile_name
                        tile.save(tile_path, "PNG")
                        exported += 1

                        # Patch each tile to a block starting at dest place_x/place_y
                        changes.append({
                            "Action": "EditImage",
                            "Target": target,
                            "FromFile": f"assets/tiles/{tile_name}",
                            "PatchMode": patch_mode,
                            "ToArea": to_area_from_tiles(place_x + tx, place_y + ty, cell_w, cell_h),
                        })
                        patched += 1

                self._log(f"Exported tiles: {exported}")
                self._log(f"Generated patches: {patched}")

                if patched == 0:
                    raise ValueError("No tiles were patched. If your image is blank/transparent, disable 'Skip fully transparent tiles'.")

            else:
                raise ValueError(f"Unknown mode: {mode}")

            # Write pack files
            write_manifest(pack_dir, pack_name, author, version, description, unique_id)
            self._log("Wrote manifest.json")

            content = {"Format": cp_format, "Changes": changes}
            write_content_json(pack_dir, content)
            self._log(f"Wrote content.json ({len(changes)} change(s))")

            final_path = pack_dir
            if mods_dir:
                install_dir = mods_dir / safe_pack_folder
                self._log(f"Installing to Mods: {install_dir}")
                copy_tree(pack_dir, install_dir)
                final_path = install_dir
                self._log("Install complete.")

            messagebox.showinfo("Done", f"Pack created at:\n{final_path}")
            self._log(f"Done! Pack at: {final_path}")

        except Exception as ex:
            self._log(f"ERROR: {ex}")
            messagebox.showerror("Failed", f"Something went wrong:\n{ex}")


def main() -> int:
    app = App()
    app.mainloop()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
