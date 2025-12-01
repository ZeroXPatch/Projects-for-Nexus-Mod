import tkinter as tk
from tkinter import messagebox
import winreg
import ctypes

# ==========================
# Registry constants
# ==========================
REG_PATH = r"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management"
VALUE_NAME = "PagingFiles"


def is_admin() -> bool:
    """Check if script is running with admin rights."""
    try:
        return ctypes.windll.shell32.IsUserAnAdmin()
    except Exception:
        return False


def read_current_pagefile():
    """
    Read current pagefile config from registry.

    Returns:
        (drive_path, initial_mb, max_mb) or (None, None, None) on failure.
    """
    try:
        with winreg.OpenKey(
            winreg.HKEY_LOCAL_MACHINE,
            REG_PATH,
            0,
            winreg.KEY_READ
        ) as key:
            value, vtype = winreg.QueryValueEx(key, VALUE_NAME)

        if vtype != winreg.REG_MULTI_SZ or not value:
            return None, None, None

        # Typically: ["C:\\pagefile.sys 20480 20480", ...]
        first = value[0]
        parts = first.split()
        if len(parts) < 3:
            return None, None, None

        path = parts[0]
        try:
            initial_mb = int(parts[1])
            max_mb = int(parts[2])
        except ValueError:
            return path, None, None

        return path, initial_mb, max_mb

    except OSError:
        return None, None, None


def set_pagefile_size(new_size_mb: int):
    """
    Set pagefile size (initial and max) to new_size_mb for the first entry.
    Leaves any additional entries unchanged.
    """
    if new_size_mb <= 0:
        raise ValueError("Pagefile size must be positive")

    with winreg.OpenKey(
        winreg.HKEY_LOCAL_MACHINE,
        REG_PATH,
        0,
        winreg.KEY_READ | winreg.KEY_SET_VALUE
    ) as key:
        value, vtype = winreg.QueryValueEx(key, VALUE_NAME)

        if vtype != winreg.REG_MULTI_SZ or not value:
            # If something is weird, create a default on C:
            page_path = r"C:\pagefile.sys"
            new_list = [f"{page_path} {new_size_mb} {new_size_mb}"]
        else:
            # Update only the first string
            first = value[0]
            parts = first.split()
            page_path = parts[0] if parts else r"C:\pagefile.sys"
            value[0] = f"{page_path} {new_size_mb} {new_size_mb}"
            new_list = value

        winreg.SetValueEx(key, VALUE_NAME, 0, winreg.REG_MULTI_SZ, new_list)


# ==========================
# UI
# ==========================

class PagefileApp:
    def __init__(self, root):
        self.root = root
        root.title("Pagefile Size Tool")
        root.geometry("420x220")
        root.resizable(False, False)

        try:
            root.eval('tk::PlaceWindow . center')
        except tk.TclError:
            pass

        title = tk.Label(
            root,
            text="Windows Pagefile Size (Virtual Memory)",
            font=("Segoe UI", 13, "bold")
        )
        title.pack(pady=(12, 4))

        # Admin warning
        if not is_admin():
            admin_label = tk.Label(
                root,
                text="⚠ Run this program as Administrator to change settings.",
                fg="orange",
                font=("Segoe UI", 9)
            )
            admin_label.pack(pady=(0, 8))

        # Current status
        self.status_label = tk.Label(root, text="", font=("Segoe UI", 10))
        self.status_label.pack(pady=(0, 10))

        # Input frame
        frame = tk.Frame(root)
        frame.pack(pady=4)

        lbl = tk.Label(frame, text="New pagefile size (MB):", font=("Segoe UI", 10))
        lbl.grid(row=0, column=0, padx=(0, 5), sticky="e")

        self.size_var = tk.StringVar(value="20480")  # default 20 GB
        self.entry = tk.Entry(frame, textvariable=self.size_var, width=12, justify="right")
        self.entry.grid(row=0, column=1, padx=(0, 5))

        lbl_mb = tk.Label(frame, text="MB (Recommended: 20480 for 20GB)", font=("Segoe UI", 9))
        lbl_mb.grid(row=0, column=2, sticky="w")

        # Apply button
        apply_btn = tk.Button(
            root,
            text="Apply Pagefile Size",
            width=20,
            command=self.on_apply
        )
        apply_btn.pack(pady=10)

        # Note
        note = tk.Label(
            root,
            text="Changes require a restart to fully take effect.\n"
                 "This tool only edits the first pagefile entry.",
            font=("Segoe UI", 8),
            fg="#555555",
            justify="center"
        )
        note.pack(side=tk.BOTTOM, pady=(5, 8))

        self.refresh_status()

    def refresh_status(self):
        path, initial_mb, max_mb = read_current_pagefile()
        if path is None:
            self.status_label.config(
                text="Current setting: could not read from registry.",
                fg="red"
            )
        else:
            if initial_mb is None or max_mb is None:
                self.status_label.config(
                    text=f"Current setting: {path} (unable to parse sizes)",
                    fg="red"
                )
            else:
                self.status_label.config(
                    text=f"Current setting: {path}  Initial {initial_mb} MB, Max {max_mb} MB",
                    fg="green"
                )

    def on_apply(self):
        text = self.size_var.get().strip()
        try:
            new_size = int(text)
        except ValueError:
            messagebox.showerror("Invalid input", "Please enter a whole number (size in MB).")
            return

        if new_size < 1024:
            if not messagebox.askyesno(
                "Low size warning",
                "You entered less than 1024 MB.\n"
                "This may cause stability issues.\n\n"
                "Are you sure you want to continue?"
            ):
                return

        try:
            set_pagefile_size(new_size)
            self.refresh_status()
            messagebox.showinfo(
                "Success",
                f"Pagefile size set to {new_size} MB (initial and max) for the main entry.\n\n"
                "You must restart Windows for the change to fully apply."
            )
        except PermissionError:
            messagebox.showerror(
                "Permission denied",
                "Could not change the setting.\n\n"
                "Please run this program as Administrator."
            )
        except Exception as e:
            messagebox.showerror("Error", f"Could not change pagefile size:\n{e}")


if __name__ == "__main__":
    root = tk.Tk()
    app = PagefileApp(root)
    root.mainloop()
