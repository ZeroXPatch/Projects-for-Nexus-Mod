import tkinter as tk
from tkinter import filedialog, messagebox
import ctypes
import subprocess
import os

# ==========================
# Helpers
# ==========================

def is_admin() -> bool:
    """Return True if the script has admin rights."""
    try:
        return ctypes.windll.shell32.IsUserAnAdmin()
    except Exception:
        return False


def guess_stardew_paths():
    """Try to guess common Stardew install paths (Steam / GOG)."""
    guesses = [
        r"C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley",
        r"C:\Program Files\Steam\steamapps\common\Stardew Valley",
        r"C:\GOG Games\Stardew Valley",
        r"D:\Steam\steamapps\common\Stardew Valley",
        r"E:\Steam\steamapps\common\Stardew Valley",
    ]
    for p in guesses:
        if os.path.isdir(p):
            return p
    return ""


def run_powershell_exclusion(paths):
    """
    Call PowerShell Add-MpPreference -ExclusionPath for each path in `paths`.
    Raises subprocess.CalledProcessError on failure.
    """
    # Build a PowerShell command with proper quoting
    ps_paths = ", ".join([f'"{p}"' for p in paths])
    cmd = [
        "powershell",
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-Command",
        f"Add-MpPreference -ExclusionPath {ps_paths}"
    ]

    result = subprocess.run(
        cmd,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace"
    )

    if result.returncode != 0:
        raise subprocess.CalledProcessError(
            result.returncode, cmd, result.stdout, result.stderr
        )


# ==========================
# UI
# ==========================

class ExclusionApp:
    def __init__(self, root):
        self.root = root
        root.title("Stardew Antivirus Exclusion Helper")

        # Try to center window once it knows its size (handled later)
        try:
            root.eval('tk::PlaceWindow . center')
        except tk.TclError:
            pass

        title = tk.Label(
            root,
            text="Add Stardew Valley folders to Windows Security exclusions",
            font=("Segoe UI", 11, "bold"),
            wraplength=520,
            justify="center"
        )
        title.pack(pady=(10, 4))

        if not is_admin():
            warn = tk.Label(
                root,
                text="⚠ Please run this program as Administrator.\n"
                     "Without admin rights, Windows may refuse the changes.",
                fg="orange",
                font=("Segoe UI", 9),
                justify="center"
            )
            warn.pack(pady=(0, 6))

        desc = tk.Label(
            root,
            text="This tool uses PowerShell Add-MpPreference to add exclusions\n"
                 "for your Stardew Valley folder and (optionally) your Mods folder.",
            font=("Segoe UI", 9),
            justify="center"
        )
        desc.pack(pady=(0, 10))

        # Stardew folder chooser
        frame1 = tk.Frame(root)
        frame1.pack(fill="x", padx=12, pady=3)

        lbl1 = tk.Label(frame1, text="Stardew folder:", width=18, anchor="e")
        lbl1.grid(row=0, column=0, padx=(0, 5))

        self.stardew_var = tk.StringVar(value=guess_stardew_paths())
        self.stardew_entry = tk.Entry(frame1, textvariable=self.stardew_var, width=50)
        self.stardew_entry.grid(row=0, column=1, padx=(0, 5))

        btn1 = tk.Button(frame1, text="Browse…", command=self.browse_stardew)
        btn1.grid(row=0, column=2)

        # Mods folder chooser (optional)
        frame2 = tk.Frame(root)
        frame2.pack(fill="x", padx=12, pady=3)

        lbl2 = tk.Label(frame2, text="Mods folder (optional):", width=18, anchor="e")
        lbl2.grid(row=0, column=0, padx=(0, 5))

        self.mods_var = tk.StringVar()
        self.mods_entry = tk.Entry(frame2, textvariable=self.mods_var, width=50)
        self.mods_entry.grid(row=0, column=1, padx=(0, 5))

        btn2 = tk.Button(frame2, text="Browse…", command=self.browse_mods)
        btn2.grid(row=0, column=2)

        # Apply button
        apply_btn = tk.Button(
            root,
            text="Add Exclusions to Windows Security",
            command=self.on_apply,
            width=32
        )
        apply_btn.pack(pady=14)

        note = tk.Label(
            root,
            text=(
                "Note:\n"
                "• Works only with Windows Security / Defender.\n"
                "• If Tamper Protection is on, you may need to temporarily disable it.\n"
                "• You can confirm the folders in Windows Security → "
                "Virus & threat protection → Manage settings → Exclusions."
            ),
            font=("Segoe UI", 8),
            fg="#555555",
            justify="left",
            wraplength=520
        )
        note.pack(padx=12, pady=(0, 8))

        # ---------- auto-size window to fit everything ----------
        self.root.update_idletasks()
        req_w = self.root.winfo_reqwidth()
        req_h = self.root.winfo_reqheight()
        self.root.minsize(req_w, req_h)
        self.root.resizable(True, True)
        # re-center after sizing
        try:
            self.root.eval('tk::PlaceWindow . center')
        except tk.TclError:
            pass
        # --------------------------------------------------------

    def browse_stardew(self):
        path = filedialog.askdirectory(title="Select Stardew Valley folder")
        if path:
            self.stardew_var.set(path)

    def browse_mods(self):
        path = filedialog.askdirectory(title="Select Mods folder")
        if path:
            self.mods_var.set(path)

    def on_apply(self):
        stardew = self.stardew_var.get().strip()
        mods = self.mods_var.get().strip()

        if not stardew:
            messagebox.showerror("Missing folder", "Please select your Stardew folder first.")
            return

        paths = [stardew]
        if mods:
            paths.append(mods)

        # Validate existence
        bad = [p for p in paths if not os.path.isdir(p)]
        if bad:
            messagebox.showerror(
                "Folder not found",
                "These paths do not exist:\n\n" + "\n".join(bad)
            )
            return

        if not is_admin():
            if not messagebox.askyesno(
                "Not running as Administrator",
                "The program is not running with administrator rights.\n\n"
                "Windows will likely refuse changes without admin.\n"
                "Do you still want to try?"
            ):
                return

        try:
            run_powershell_exclusion(paths)
            messagebox.showinfo(
                "Success",
                "The following folders were sent to Windows Security as exclusions:\n\n"
                + "\n".join(paths)
                + "\n\nYou can verify them in Windows Security → Virus & threat protection → "
                  "Manage settings → Exclusions."
            )
        except subprocess.CalledProcessError as e:
            msg = e.stderr or e.stdout or "Unknown error."
            messagebox.showerror(
                "Failed to add exclusions",
                "Windows reported an error while adding exclusions.\n\n"
                "Common causes:\n"
                "• You are not running as Administrator\n"
                "• Tamper Protection is enabled\n"
                "• Another antivirus is managing protection\n\n"
                f"Details:\n{msg}"
            )


if __name__ == "__main__":
    root = tk.Tk()
    app = ExclusionApp(root)
    root.mainloop()
