import tkinter as tk
from tkinter import messagebox
import winreg
import ctypes

# ==========================
# Registry constants
# ==========================
REG_PATH = r"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"
VALUE_NAME = "EnableTransparency"

HWND_BROADCAST = 0xFFFF
WM_SETTINGCHANGE = 0x001A  # 26


def get_transparency_enabled() -> bool:
    """Read current Transparency setting from registry."""
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, REG_PATH, 0, winreg.KEY_READ) as key:
            value, _ = winreg.QueryValueEx(key, VALUE_NAME)
            return bool(value)
    except FileNotFoundError:
        # If the key/value doesn't exist, Windows usually behaves as if it's enabled
        return True
    except OSError:
        # Any other registry error: assume enabled so we don't mislead the user
        return True


def broadcast_change():
    """Tell Windows that settings changed so it refreshes the UI."""
    ctypes.windll.user32.SendMessageTimeoutW(
        HWND_BROADCAST,
        WM_SETTINGCHANGE,
        0,
        ctypes.c_wchar_p("ImmersiveColorSet"),
        0x0002,   # SMTO_ABORTIFHUNG
        5000,
        None
    )


def set_transparency(enabled: bool):
    """Enable or disable Transparency effects via registry."""
    try:
        # Open or create the Personalize key
        with winreg.CreateKey(winreg.HKEY_CURRENT_USER, REG_PATH) as key:
            winreg.SetValueEx(key, VALUE_NAME, 0, winreg.REG_DWORD, 1 if enabled else 0)

        broadcast_change()

        return True, None
    except OSError as e:
        return False, str(e)


# ==========================
# UI
# ==========================

class TransparencyApp:
    def __init__(self, root):
        self.root = root
        root.title("Windows Transparency Toggle")
        root.geometry("360x200")
        root.resizable(False, False)

        # Center window a bit: optional, just for niceness
        try:
            root.eval('tk::PlaceWindow . center')
        except tk.TclError:
            pass

        # Title label
        title = tk.Label(
            root,
            text="Windows Transparency Effects",
            font=("Segoe UI", 13, "bold")
        )
        title.pack(pady=(15, 5))

        # Status label
        self.status_label = tk.Label(
            root,
            text="",
            font=("Segoe UI", 10)
        )
        self.status_label.pack(pady=(0, 15))

        # Buttons frame
        btn_frame = tk.Frame(root)
        btn_frame.pack(pady=5)

        self.btn_disable = tk.Button(
            btn_frame,
            text="Disable Transparency",
            width=18,
            command=self.on_disable
        )
        self.btn_disable.grid(row=0, column=0, padx=5)

        self.btn_enable = tk.Button(
            btn_frame,
            text="Enable Transparency",
            width=18,
            command=self.on_enable
        )
        self.btn_enable.grid(row=0, column=1, padx=5)

        # Small note at bottom
        note = tk.Label(
            root,
            text="Note: In rare cases you may need to sign out/in\nor restart Explorer to see changes everywhere.",
            font=("Segoe UI", 8),
            fg="#555555",
            justify="center"
        )
        note.pack(side=tk.BOTTOM, pady=(5, 8))

        self.refresh_status()

    def refresh_status(self):
        enabled = get_transparency_enabled()
        if enabled:
            self.status_label.config(
                text="Current status: Transparency ENABLED",
                fg="green"
            )
        else:
            self.status_label.config(
                text="Current status: Transparency DISABLED",
                fg="red"
            )

    def on_disable(self):
        ok, err = set_transparency(False)
        if ok:
            self.refresh_status()
            messagebox.showinfo("Done", "Transparency effects have been DISABLED.")
        else:
            messagebox.showerror("Error", f"Could not change setting:\n{err}")

    def on_enable(self):
        ok, err = set_transparency(True)
        if ok:
            self.refresh_status()
            messagebox.showinfo("Done", "Transparency effects have been ENABLED.")
        else:
            messagebox.showerror("Error", f"Could not change setting:\n{err}")


if __name__ == "__main__":
    root = tk.Tk()
    app = TransparencyApp(root)
    root.mainloop()
