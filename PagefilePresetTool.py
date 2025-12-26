import ctypes
import json
import os
import subprocess
import tkinter as tk
from tkinter import ttk, messagebox, filedialog

APP_TITLE = "Pagefile Preset Tool (Below 20GB) - Backup + Restore"
DEFAULT_BACKUP_NAME = "pagefile_backup.json"

PRESETS = [
    ("Windows Default (System-managed)", None, None,
     "Let Windows manage the pagefile automatically. Good default if you don’t want to think about it."),
    ("8GB (Light Modding)", 8192, 8192,
     "Small safety cushion. Helps reduce random crashes from short memory spikes with small-to-medium mod lists."),
    ("12GB (Moderate Modding)", 12288, 12288,
     "Balanced stability for medium collections. Helps with longer sessions and smoother area/menu loading."),
    ("16GB (Heavy Modding)", 16384, 16384,
     "More breathing room for big mod lists (script-heavy mods, expansions). Helps reduce stutter/crashes under pressure."),
    ("20GB (Large Mod Collections - Recommended)", 20480, 20480,
     "Common recommendation for large mod collections. Focused on stability and smoother loading."),
]


def is_admin() -> bool:
    try:
        return bool(ctypes.windll.shell32.IsUserAnAdmin())
    except Exception:
        return False


def run_powershell(ps_script: str) -> tuple[int, str, str]:
    cmd = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", ps_script]
    p = subprocess.run(cmd, capture_output=True, text=True)
    return p.returncode, p.stdout.strip(), p.stderr.strip()


def broadcast_setting_change():
    # Tell Windows "system settings changed" (best-effort).
    HWND_BROADCAST = 0xFFFF
    WM_SETTINGCHANGE = 0x001A
    SMTO_ABORTIFHUNG = 0x0002
    ctypes.windll.user32.SendMessageTimeoutW(HWND_BROADCAST, WM_SETTINGCHANGE, 0,
                                             "Environment", SMTO_ABORTIFHUNG, 2000, None)


def get_current_state() -> dict:
    ps = r"""
$cs = Get-CimInstance Win32_ComputerSystem
$auto = [bool]$cs.AutomaticManagedPagefile

$settings = Get-CimInstance Win32_PageFileSetting -ErrorAction SilentlyContinue |
    Select-Object Name, InitialSize, MaximumSize

$result = [PSCustomObject]@{
  automaticManaged = $auto
  pagefileSettings = @()
}

if ($settings) {
  foreach ($s in $settings) {
    $result.pagefileSettings += [PSCustomObject]@{
      name = $s.Name
      initial = [int]$s.InitialSize
      maximum = [int]$s.MaximumSize
    }
  }
}

$result | ConvertTo-Json -Depth 4
"""
    code, out, err = run_powershell(ps)
    if code != 0:
        raise RuntimeError(err or out or "Unknown PowerShell error while reading state.")
    return json.loads(out) if out else {"automaticManaged": True, "pagefileSettings": []}


def backup_to_file(path: str) -> dict:
    state = get_current_state()
    with open(path, "w", encoding="utf-8") as f:
        json.dump(state, f, indent=2)
    return state


def set_system_managed() -> None:
    # Enable Windows automatic management
    ps = r"""
$cs = Get-CimInstance Win32_ComputerSystem
$cs.AutomaticManagedPagefile = $true
Set-CimInstance -InputObject $cs | Out-Null

# Clear explicit PagingFiles (optional, but keeps things clean)
$regPath = "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management"
Try {
  Remove-ItemProperty -Path $regPath -Name "PagingFiles" -ErrorAction SilentlyContinue | Out-Null
} Catch {}
"OK"
"""
    code, out, err = run_powershell(ps)
    if code != 0:
        raise RuntimeError(err or out or "Failed to enable system-managed pagefile.")
    broadcast_setting_change()


def set_fixed_size(initial_mb: int, max_mb: int, drive: str = "C:") -> None:
    drive = (drive or "C:").strip().upper()
    if not drive.endswith(":"):
        drive += ":"
    paging_line = rf"{drive}\pagefile.sys {int(initial_mb)} {int(max_mb)}"

    ps = rf"""
# Disable automatic management
$cs = Get-CimInstance Win32_ComputerSystem
$cs.AutomaticManagedPagefile = $false
Set-CimInstance -InputObject $cs | Out-Null

# Write registry value used by Virtual Memory UI
$regPath = "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management"
Set-ItemProperty -Path $regPath -Name "PagingFiles" -Type MultiString -Value @("{paging_line}") | Out-Null

"OK"
"""
    code, out, err = run_powershell(ps)
    if code != 0:
        raise RuntimeError(err or out or "Failed to set fixed pagefile size.")
    broadcast_setting_change()


def restore_from_backup(state: dict) -> None:
    automatic = bool(state.get("automaticManaged", True))
    settings = state.get("pagefileSettings", []) or []

    if automatic:
        set_system_managed()
        return

    # Convert backed up CIM settings into PagingFiles MULTI_SZ lines
    lines = []
    for s in settings:
        name = (s.get("name") or r"C:\pagefile.sys").strip()
        initial = int(s.get("initial", 20480))
        maximum = int(s.get("maximum", initial))
        lines.append(f"{name} {initial} {maximum}")

    if not lines:
        lines = [r"C:\pagefile.sys 20480 20480"]

    # Apply
    joined = "\n".join([f'"{ln}"' for ln in lines])
    ps = rf"""
$cs = Get-CimInstance Win32_ComputerSystem
$cs.AutomaticManagedPagefile = $false
Set-CimInstance -InputObject $cs | Out-Null

$regPath = "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management"
Set-ItemProperty -Path $regPath -Name "PagingFiles" -Type MultiString -Value @(
{joined}
) | Out-Null

"OK"
"""
    code, out, err = run_powershell(ps)
    if code != 0:
        raise RuntimeError(err or out or "Failed to restore from backup.")
    broadcast_setting_change()


class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title(APP_TITLE)

        self.selected_preset = tk.StringVar(value=PRESETS[0][0])
        self.drive_var = tk.StringVar(value="C:")
        self.status_var = tk.StringVar(value="Ready.")
        self.current_state_text = tk.StringVar(value="(click Refresh)")

        self._build_ui()
        self._refresh()
        self.after(10, self._autosize_window)

    def _autosize_window(self):
        self.update_idletasks()
        w = max(self.winfo_reqwidth() + 20, 780)
        h = max(self.winfo_reqheight() + 20, 500)
        self.geometry(f"{w}x{h}")
        self.minsize(760, 480)

    def _build_ui(self):
        pad = 12

        top = ttk.Frame(self, padding=pad)
        top.pack(fill="x")

        admin = is_admin()
        ttk.Label(
            top,
            text=("Admin: YES ✅" if admin else "Admin: NO ❌ (Run as Administrator to APPLY/RESTORE)"),
            font=("Segoe UI", 11, "bold"),
        ).pack(anchor="w")

        state_box = ttk.LabelFrame(self, text="Current Pagefile State", padding=pad)
        state_box.pack(fill="x", padx=pad, pady=(0, pad))

        ttk.Label(state_box, textvariable=self.current_state_text, justify="left").pack(anchor="w")
        ttk.Button(state_box, text="Refresh", command=self._refresh).pack(anchor="e", pady=(6, 0))

        preset_box = ttk.LabelFrame(self, text="Choose a preset (Below 20GB)", padding=pad)
        preset_box.pack(fill="both", expand=True, padx=pad, pady=(0, pad))

        left = ttk.Frame(preset_box)
        left.pack(side="left", fill="both", expand=True)

        right = ttk.Frame(preset_box)
        right.pack(side="right", fill="y", padx=(12, 0))

        for label, init_mb, max_mb, desc in PRESETS:
            ttk.Radiobutton(
                left, text=label, value=label,
                variable=self.selected_preset,
                command=self._update_desc,
            ).pack(anchor="w", pady=2)

        drive_row = ttk.Frame(left)
        drive_row.pack(anchor="w", pady=(10, 0))
        ttk.Label(drive_row, text="Target drive (fixed presets): ").pack(side="left")
        ttk.Entry(drive_row, textvariable=self.drive_var, width=6).pack(side="left")
        ttk.Label(drive_row, text="(example: C:)").pack(side="left", padx=(6, 0))

        ttk.Label(left, text="Preset notes:", font=("Segoe UI", 10, "bold")).pack(anchor="w", pady=(10, 0))
        self.desc_text = tk.Text(left, height=10, wrap="word")
        self.desc_text.configure(state="disabled")
        self.desc_text.pack(fill="both", expand=True, pady=(4, 0))

        ttk.Button(right, text="Backup current settings (JSON)", command=self._backup).pack(fill="x", pady=4)
        ttk.Button(right, text="Restore from backup (JSON)", command=self._restore).pack(fill="x", pady=4)

        ttk.Separator(right).pack(fill="x", pady=10)

        ttk.Button(right, text="Apply selected preset", command=self._apply_preset).pack(fill="x", pady=4)

        ttk.Separator(right).pack(fill="x", pady=10)
        ttk.Button(right, text="Exit", command=self.destroy).pack(fill="x", pady=4)

        footer = ttk.Frame(self, padding=(pad, 0, pad, pad))
        footer.pack(fill="x")
        ttk.Label(footer, textvariable=self.status_var).pack(anchor="w")

        self._update_desc()

    def _set_status(self, msg: str):
        self.status_var.set(msg)
        self.update_idletasks()

    def _refresh(self):
        try:
            st = get_current_state()
            lines = [f"Automatic managed pagefile: {st.get('automaticManaged', True)}"]
            settings = st.get("pagefileSettings", []) or []
            if settings:
                lines.append("Win32_PageFileSetting entries:")
                for s in settings:
                    lines.append(f"  - {s.get('name')}  (Initial={s.get('initial')} MB, Max={s.get('maximum')} MB)")
            else:
                lines.append("Win32_PageFileSetting entries: (none)")
            lines.append("\nTip: The Windows Virtual Memory UI may fully update after a restart.")
            self.current_state_text.set("\n".join(lines))
            self._set_status("Refreshed.")
        except Exception as e:
            messagebox.showerror("Refresh failed", str(e))
            self._set_status("Refresh failed.")

    def _update_desc(self):
        label = self.selected_preset.get()
        drive = (self.drive_var.get() or "C:").strip()
        desc = ""
        for l, init_mb, max_mb, d in PRESETS:
            if l == label:
                desc = d
                if init_mb is None:
                    desc += "\n\n• Uses Windows automatic sizing."
                else:
                    desc += f"\n\n• Fixed size: {init_mb} MB (Initial) / {max_mb} MB (Max) on {drive}."
                desc += "\n• Restart Windows after applying."
                break

        self.desc_text.configure(state="normal")
        self.desc_text.delete("1.0", "end")
        self.desc_text.insert("1.0", desc)
        self.desc_text.configure(state="disabled")

    def _require_admin(self) -> bool:
        if is_admin():
            return True
        messagebox.showwarning("Admin required", "Run this app as Administrator to apply or restore pagefile settings.")
        return False

    def _backup(self):
        try:
            path = filedialog.asksaveasfilename(
                title="Save backup JSON",
                defaultextension=".json",
                initialfile=DEFAULT_BACKUP_NAME,
                filetypes=[("JSON files", "*.json"), ("All files", "*.*")]
            )
            if not path:
                return
            self._set_status("Backing up current settings...")
            backup_to_file(path)
            self._set_status(f"Backed up to {os.path.basename(path)}.")
            messagebox.showinfo("Backup saved", f"Saved backup:\n{path}")
        except Exception as e:
            self._set_status("Backup failed.")
            messagebox.showerror("Backup failed", str(e))

    def _restore(self):
        if not self._require_admin():
            return
        try:
            path = filedialog.askopenfilename(
                title="Select backup JSON",
                filetypes=[("JSON files", "*.json"), ("All files", "*.*")]
            )
            if not path:
                return
            self._set_status("Restoring from backup...")
            with open(path, "r", encoding="utf-8") as f:
                state = json.load(f)
            restore_from_backup(state)
            self._set_status("Restored from backup.")
            self._refresh()
            messagebox.showinfo("Restored", "Settings restored.\n\nRestart Windows to fully apply changes.")
        except Exception as e:
            self._set_status("Restore failed.")
            messagebox.showerror("Restore failed", str(e))

    def _apply_preset(self):
        if not self._require_admin():
            return

        label = self.selected_preset.get()
        init_mb = max_mb = None
        for l, i, m, d in PRESETS:
            if l == label:
                init_mb, max_mb = i, m
                break

        drive = (self.drive_var.get() or "C:").strip()

        if init_mb is None:
            confirm_msg = f"Apply preset:\n\n{label}\n\nThis enables Windows automatic pagefile management."
        else:
            confirm_msg = f"Apply preset:\n\n{label}\n\nFixed pagefile on {drive}: {init_mb} MB / {max_mb} MB."

        confirm_msg += "\n\nTip: Backup first (JSON)."

        if not messagebox.askyesno("Confirm", confirm_msg):
            return

        try:
            self._set_status("Applying preset...")
            if init_mb is None:
                set_system_managed()
            else:
                set_fixed_size(init_mb, max_mb, drive=drive)
            self._set_status("Preset applied.")
            self._refresh()
            messagebox.showinfo("Done", "Preset applied.\n\nRestart Windows to fully apply changes.")
        except Exception as e:
            self._set_status("Apply failed.")
            messagebox.showerror("Apply failed", str(e))


def main():
    app = App()
    if not is_admin():
        messagebox.showwarning(
            "Run as Administrator",
            "You can VIEW and BACKUP without admin.\n\nTo APPLY presets or RESTORE settings, run as Administrator."
        )
    app.mainloop()


if __name__ == "__main__":
    main()
