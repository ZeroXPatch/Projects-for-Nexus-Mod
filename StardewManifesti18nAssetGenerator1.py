import os
import json
import re
import tkinter as tk
from tkinter import ttk, filedialog, messagebox


class EmptyStardewModScaffolder(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("Stardew Mod Scaffolder - Empty manifest + i18n (+ optional C#)")
        self.resizable(True, True)

        self.mod_folder_path = tk.StringVar()
        self.mod_name = tk.StringVar()

        self.create_assets_folder = tk.BooleanVar(value=True)
        self.create_config_file = tk.BooleanVar(value=False)

        # C# skeleton
        self.create_csharp_skeleton = tk.BooleanVar(value=False)

        # NEW: GMCM API interface stub
        self.create_gmcm_api_stub = tk.BooleanVar(value=False)

        self._build_ui()
        self.after(0, self._autosize_to_content)

    # ---------- Window sizing ----------
    def _autosize_to_content(self):
        self.update_idletasks()
        req_w = self.winfo_reqwidth()
        req_h = self.winfo_reqheight()

        screen_w = self.winfo_screenwidth()
        screen_h = self.winfo_screenheight()

        w = min(req_w + 20, screen_w - 80)
        h = min(req_h + 20, screen_h - 80)

        self.geometry(f"{w}x{h}")
        self.minsize(min(req_w + 20, screen_w - 80), min(req_h + 20, screen_h - 80))

        x = max((screen_w - w) // 2, 0)
        y = max((screen_h - h) // 3, 0)
        self.geometry(f"{w}x{h}+{x}+{y}")

    # ---------- Helpers ----------
    @staticmethod
    def _to_pascal_identifier(name: str) -> str:
        parts = re.split(r"[^A-Za-z0-9]+", (name or "").strip())
        parts = [p for p in parts if p]
        if not parts:
            ident = "MyMod"
        else:
            ident = "".join(p[:1].upper() + p[1:] for p in parts)

        if not re.match(r"^[A-Za-z_]", ident):
            ident = "Mod" + ident
        return ident

    def _suggest_mod_name_from_folder(self, folder: str) -> str:
        base = os.path.basename(os.path.normpath(folder)) if folder else ""
        return base or "MyMod"

    def _get_namespace(self, folder: str) -> str:
        raw_name = self.mod_name.get().strip() or self._suggest_mod_name_from_folder(folder)
        return self._to_pascal_identifier(raw_name)

    # ---------- UI ----------
    def _build_ui(self):
        main = ttk.Frame(self, padding=10)
        main.pack(fill="both", expand=True)

        ttk.Label(
            main,
            text="Create empty manifest.json and i18n/default.json",
            font=("Segoe UI", 11, "bold")
        ).pack(anchor="w", pady=(0, 6))

        ttk.Label(
            main,
            text=(
                "This tool will create in the selected folder:\n"
                " • manifest.json      -> {}\n"
                " • i18n/default.json  -> {}\n"
                "Optional:\n"
                " • config.json        -> {}\n"
                " • <ModName>.csproj, ModEntry.cs, ModConfig.cs -> (C# skeleton)\n"
                " • IGenericModConfigMenuApi.cs -> (GMCM API stub)"
            ),
            justify="left"
        ).pack(anchor="w", pady=(0, 10))

        folder_frame = ttk.LabelFrame(main, text="Mod Folder Location")
        folder_frame.pack(fill="x", pady=(0, 8))

        ttk.Label(folder_frame, text="Mod folder (will be created if missing):").grid(
            row=0, column=0, padx=5, pady=5, sticky="w"
        )

        ttk.Entry(folder_frame, textvariable=self.mod_folder_path, width=55).grid(
            row=1, column=0, padx=5, pady=5, sticky="we"
        )

        ttk.Button(folder_frame, text="Browse...", command=self._browse_folder).grid(
            row=1, column=1, padx=5, pady=5
        )

        folder_frame.grid_columnconfigure(0, weight=1)

        name_frame = ttk.LabelFrame(main, text="Mod Name (for .csproj / namespace)")
        name_frame.pack(fill="x", pady=(0, 8))

        ttk.Label(name_frame, text="Mod name:").grid(row=0, column=0, padx=5, pady=5, sticky="w")
        ttk.Entry(name_frame, textvariable=self.mod_name, width=40).grid(
            row=0, column=1, padx=5, pady=5, sticky="w"
        )
        ttk.Label(
            name_frame,
            text="(auto-fills from folder name; you can edit)",
            foreground="#555"
        ).grid(row=0, column=2, padx=5, pady=5, sticky="w")

        optional_frame = ttk.LabelFrame(main, text="Optional")
        optional_frame.pack(fill="x", pady=(0, 8))

        ttk.Checkbutton(
            optional_frame,
            text='Create "assets" folder (for sprites / data / etc.)',
            variable=self.create_assets_folder
        ).grid(row=0, column=0, padx=5, pady=5, sticky="w")

        ttk.Checkbutton(
            optional_frame,
            text='Create empty "config.json" (for SMAPI config)',
            variable=self.create_config_file
        ).grid(row=1, column=0, padx=5, pady=5, sticky="w")

        ttk.Checkbutton(
            optional_frame,
            text='Create C# skeleton: <ModName>.csproj + ModEntry.cs + ModConfig.cs',
            variable=self.create_csharp_skeleton
        ).grid(row=2, column=0, padx=5, pady=5, sticky="w")

        # NEW checkbox
        ttk.Checkbutton(
            optional_frame,
            text='Create empty IGenericModConfigMenuApi.cs (GMCM API stub)',
            variable=self.create_gmcm_api_stub
        ).grid(row=3, column=0, padx=5, pady=5, sticky="w")

        button_frame = ttk.Frame(main)
        button_frame.pack(fill="x", pady=(15, 0))

        ttk.Label(button_frame, text="").pack(side="left", expand=True)

        ttk.Button(button_frame, text="Quit", command=self.destroy).pack(side="right", padx=5)
        ttk.Button(button_frame, text="Create Files", command=self._create_files).pack(side="right", padx=5)

    # ---------- Handlers ----------
    def _browse_folder(self):
        folder = filedialog.askdirectory(title="Select or create your mod folder")
        if folder:
            self.mod_folder_path.set(folder)
            suggested = self._suggest_mod_name_from_folder(folder)
            if not self.mod_name.get().strip():
                self.mod_name.set(suggested)
            self.after(0, self._autosize_to_content)

    def _validate(self):
        if not self.mod_folder_path.get():
            messagebox.showerror("Missing folder", "Please choose your mod folder path.")
            return False
        return True

    def _create_files(self):
        if not self._validate():
            return

        folder = self.mod_folder_path.get()

        try:
            os.makedirs(folder, exist_ok=True)
            created_paths = []

            # manifest.json => {}
            manifest_path = os.path.join(folder, "manifest.json")
            with open(manifest_path, "w", encoding="utf-8") as f:
                json.dump({}, f, indent=2, ensure_ascii=False)
            created_paths.append(manifest_path)

            # i18n/default.json => {}
            i18n_folder = os.path.join(folder, "i18n")
            os.makedirs(i18n_folder, exist_ok=True)

            default_path = os.path.join(i18n_folder, "default.json")
            with open(default_path, "w", encoding="utf-8") as f:
                json.dump({}, f, indent=2, ensure_ascii=False)
            created_paths.append(default_path)

            # Optional config.json => {}
            if self.create_config_file.get():
                config_path = os.path.join(folder, "config.json")
                with open(config_path, "w", encoding="utf-8") as f:
                    json.dump({}, f, indent=2, ensure_ascii=False)
                created_paths.append(config_path)

            # Optional assets folder
            if self.create_assets_folder.get():
                assets_folder = os.path.join(folder, "assets")
                os.makedirs(assets_folder, exist_ok=True)
                created_paths.append(assets_folder + os.sep)

            ns = self._get_namespace(folder)

            # Optional C# skeleton
            if self.create_csharp_skeleton.get():
                csproj_path = os.path.join(folder, f"{ns}.csproj")
                csproj_content = f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <RootNamespace>{ns}</RootNamespace>
    <AssemblyName>{ns}</AssemblyName>
    <OutputType>Library</OutputType>

    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
"""
                with open(csproj_path, "w", encoding="utf-8") as f:
                    f.write(csproj_content)
                created_paths.append(csproj_path)

                modentry_path = os.path.join(folder, "ModEntry.cs")
                modentry_content = f"""using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace {ns}
{{
    public sealed class ModEntry : Mod
    {{
        private ModConfig Config = new();

        public override void Entry(IModHelper helper)
        {{
            this.Config = helper.ReadConfig<ModConfig>();
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        }}

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {{
            // TODO: Register GMCM later if you want.
        }}
    }}
}}
"""
                with open(modentry_path, "w", encoding="utf-8") as f:
                    f.write(modentry_content)
                created_paths.append(modentry_path)

                modconfig_path = os.path.join(folder, "ModConfig.cs")
                modconfig_content = f"""namespace {ns}
{{
    public sealed class ModConfig
    {{
        // Add your config options here later.
        public bool ExampleToggle {{ get; set; }} = true;
    }}
}}
"""
                with open(modconfig_path, "w", encoding="utf-8") as f:
                    f.write(modconfig_content)
                created_paths.append(modconfig_path)

            # NEW: Optional GMCM API interface stub (empty-but-useful)
            if self.create_gmcm_api_stub.get():
                gmcm_api_path = os.path.join(folder, "IGenericModConfigMenuApi.cs")
                gmcm_api_content = f"""namespace {ns}
{{
    // Minimal GMCM API interface stub.
    // If you install GMCM later, you can expand this interface (or copy the official one).
    public interface IGenericModConfigMenuApi
    {{
    }}
}}
"""
                with open(gmcm_api_path, "w", encoding="utf-8") as f:
                    f.write(gmcm_api_content)
                created_paths.append(gmcm_api_path)

            messagebox.showinfo(
                "Success",
                "Created:\n\n" + "\n".join(created_paths) +
                "\n\nAll JSON files created by this tool contain empty JSON: {}"
            )

        except Exception as exc:
            messagebox.showerror("Error", f"Something went wrong while creating files:\n{exc}")


if __name__ == "__main__":
    app = EmptyStardewModScaffolder()
    app.mainloop()
