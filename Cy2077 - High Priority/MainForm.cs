using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using WinFormsTimer = System.Windows.Forms.Timer;
using WinFormsLabel = System.Windows.Forms.Label;

namespace CyberpunkPriorityOnce
{
    public sealed class MainForm : Form
    {
        private readonly NotifyIcon trayIcon;
        private readonly ContextMenuStrip trayMenu;
        private readonly WinFormsTimer pollTimer;

        private AppConfig config;

        private ProcessPriorityClass? lastApplied;
        private bool? lastFocused;
        private int? lastPidStatus;

        private bool isExiting;

        // Track the game process we launched (if any)
        private Process? launchedProcess;
        private bool autoLaunchAttempted;

        // Background embedded resource
        private Image? backgroundImage;
        private const float BackgroundOpacity = 0.30f;
        private const string EmbeddedBackgroundFileName = "cyberpunk_bg.png";

        // UI controls
        private readonly TextBox txtProcess;
        private readonly TextBox txtExePath;
        private readonly Button btnBrowseExe;
        private readonly CheckBox chkRememberExe;

        private readonly ComboBox cmbMode;
        private readonly ComboBox cmbManualPriority;
        private readonly ComboBox cmbFocusedPriority;
        private readonly ComboBox cmbUnfocusedPriority;

        private readonly NumericUpDown nudPollMs;

        private readonly CheckBox chkAutoLaunch;
        private readonly TextBox txtLaunchCommand;
        private readonly Button btnLaunchNow;
        private readonly WinFormsLabel lblAutoLaunchNote;

        private readonly CheckBox chkCloseGameOnExit;

        private readonly CheckBox chkMinimizeToTray;
        private readonly CheckBox chkStartMinimized;

        private readonly WinFormsLabel lblStatus;
        private readonly Button btnSave;
        private readonly Button btnApplyNow;

        public MainForm()
        {
            this.Text = "Cyberpunk Priority Tool";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.DoubleBuffered = true;

            this.ClientSize = new Size(680, 620);
            this.MinimumSize = new Size(680, 620);

            this.config = AppConfig.Load();

            // If not remembering EXE, don't prefill it in UI
            if (!this.config.RememberExePath)
                this.config.ExePath = "";

            this.TryLoadEmbeddedBackgroundImage();

            // ----- Tray -----
            this.trayMenu = new ContextMenuStrip();
            this.trayIcon = new NotifyIcon
            {
                Text = "Cyberpunk Priority Tool",
                Icon = SystemIcons.Application,
                Visible = true,
                ContextMenuStrip = this.trayMenu
            };
            this.trayIcon.DoubleClick += (_, __) => this.RestoreFromTray();
            this.BuildTrayMenu();

            // Scroll container so buttons never disappear
            var scrollHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Transparent
            };
            this.Controls.Add(scrollHost);

            // Layout root inside scroll host
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 0,
                Padding = new Padding(12),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            scrollHost.Controls.Add(root);

            // Controls
            this.txtProcess = new TextBox { Text = this.config.ProcessName };

            this.txtExePath = new TextBox { Text = this.config.ExePath };
            this.btnBrowseExe = new Button { Text = "Browse…", AutoSize = true, Padding = new Padding(10, 6, 10, 6) };
            this.btnBrowseExe.Click += (_, __) => this.BrowseForExe();

            this.chkRememberExe = new CheckBox
            {
                Text = "Remember EXE path",
                Checked = this.config.RememberExePath,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            this.cmbMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            this.cmbMode.Items.AddRange(new object[] { Mode.Disabled, Mode.Manual, Mode.AutoFocus });
            this.cmbMode.SelectedItem = this.config.Mode;
            this.cmbMode.SelectedIndexChanged += (_, __) => this.RefreshUiEnabledState();

            this.cmbManualPriority = MakePriorityCombo(this.config.ManualPriority);
            this.cmbFocusedPriority = MakePriorityCombo(this.config.FocusedPriority);
            this.cmbUnfocusedPriority = MakePriorityCombo(this.config.UnfocusedPriority);

            this.nudPollMs = new NumericUpDown
            {
                Minimum = 100,
                Maximum = 5000,
                Increment = 50,
                Value = this.config.PollMs,
                Width = 160
            };

            this.chkAutoLaunch = new CheckBox
            {
                Text = "Auto-launch game using command",
                Checked = this.config.AutoLaunchEnabled,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            this.chkAutoLaunch.CheckedChanged += (_, __) => this.RefreshUiEnabledState();

            this.txtLaunchCommand = new TextBox
            {
                Text = this.config.LaunchCommand ?? ""
            };
            this.txtLaunchCommand.TextChanged += (_, __) => this.RefreshUiEnabledState();

            // ✅ New: manual backup launch button
            this.btnLaunchNow = new Button
            {
                Text = "Launch",
                AutoSize = true,
                Padding = new Padding(10, 6, 10, 6)
            };
            this.btnLaunchNow.Click += (_, __) => this.LaunchGameNow();

            // Requested note
            this.lblAutoLaunchNote = new WinFormsLabel
            {
                AutoSize = true,
                BackColor = Color.Transparent,
                ForeColor = SystemColors.ControlText,
                Text = "Note: If you change the EXE/launch command, exit this tool and reopen it for Auto-launch to use the new value.",
                Margin = new Padding(0, 2, 0, 6)
            };
            this.lblAutoLaunchNote.MaximumSize = new Size(420, 0);

            this.chkCloseGameOnExit = new CheckBox
            {
                Text = "Close game when this tool exits",
                Checked = this.config.CloseGameOnExit,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            this.chkMinimizeToTray = new CheckBox
            {
                Text = "Minimize to tray",
                Checked = this.config.MinimizeToTray,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            this.chkStartMinimized = new CheckBox
            {
                Text = "Start minimized",
                Checked = this.config.StartMinimized,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            this.lblStatus = new WinFormsLabel
            {
                Text = "Status: Idle",
                AutoSize = false,
                Height = 84,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };

            this.btnApplyNow = new Button
            {
                Text = "Apply now",
                AutoSize = true,
                Padding = new Padding(10, 6, 10, 6),
                Margin = new Padding(0, 0, 10, 0)
            };
            this.btnApplyNow.Click += (_, __) =>
            {
                this.PullUiToConfig();
                this.ApplyOnce(force: true);
            };

            this.btnSave = new Button
            {
                Text = "Save settings",
                AutoSize = true,
                Padding = new Padding(10, 6, 10, 6)
            };
            this.btnSave.Click += (_, __) =>
            {
                this.PullUiToConfig();
                this.SaveConfig();
                this.BuildTrayMenu();
                this.ApplyOnce(force: true);
                MessageBox.Show("Saved.", "Cyberpunk Priority Tool");
            };

            // EXE row layout: textbox fills, button autosizes (no clipping)
            var exeRowLayout = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            exeRowLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            exeRowLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            this.txtExePath.Dock = DockStyle.Fill;
            this.btnBrowseExe.Margin = new Padding(8, 0, 0, 0);
            exeRowLayout.Controls.Add(this.txtExePath, 0, 0);
            exeRowLayout.Controls.Add(this.btnBrowseExe, 1, 0);

            // ✅ Launch command row layout: textbox + Launch button
            var cmdRowLayout = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            cmdRowLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            cmdRowLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            this.txtLaunchCommand.Dock = DockStyle.Fill;
            this.btnLaunchNow.Margin = new Padding(8, 0, 0, 0);
            cmdRowLayout.Controls.Add(this.txtLaunchCommand, 0, 0);
            cmdRowLayout.Controls.Add(this.btnLaunchNow, 1, 0);

            // Add UI rows
            AddRow(root, "Process name:", this.txtProcess);
            AddRow(root, "Game EXE path (optional):", exeRowLayout);
            AddRow(root, " ", this.chkRememberExe);

            AddRow(root, "Mode:", this.cmbMode);
            AddRow(root, "Manual priority:", this.cmbManualPriority);
            AddRow(root, "Focused priority:", this.cmbFocusedPriority);
            AddRow(root, "Unfocused priority:", this.cmbUnfocusedPriority);
            AddRow(root, "Poll interval (ms):", this.nudPollMs);

            AddRow(root, " ", this.chkAutoLaunch);
            AddRow(root, "Launch command:", cmdRowLayout);
            AddRow(root, " ", this.lblAutoLaunchNote);

            AddRow(root, " ", this.chkCloseGameOnExit);

            var checkboxPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            checkboxPanel.Controls.Add(this.chkMinimizeToTray);
            checkboxPanel.Controls.Add(this.chkStartMinimized);
            AddRow(root, " ", checkboxPanel);

            // status row spanning both columns
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, this.lblStatus.Height + 10));
            int statusRow = root.RowCount++;
            root.Controls.Add(this.lblStatus, 0, statusRow);
            root.SetColumnSpan(this.lblStatus, 2);
            this.lblStatus.Margin = new Padding(0, 12, 0, 0);

            // buttons row spanning both columns
            var buttonBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 0),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            buttonBar.Controls.Add(this.btnApplyNow);
            buttonBar.Controls.Add(this.btnSave);

            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            int btnRow = root.RowCount++;
            root.Controls.Add(buttonBar, 0, btnRow);
            root.SetColumnSpan(buttonBar, 2);

            // Minimize-to-tray behavior
            this.Resize += (_, __) =>
            {
                if (!this.isExiting && this.config.MinimizeToTray && this.WindowState == FormWindowState.Minimized)
                    this.MinimizeToTray();
            };

            this.FormClosing += (_, e) =>
            {
                if (this.isExiting)
                {
                    this.trayIcon.Visible = false;
                    return;
                }

                if (this.config.MinimizeToTray)
                {
                    e.Cancel = true;
                    this.MinimizeToTray();
                }
                else
                {
                    this.trayIcon.Visible = false;
                }
            };

            // Poll timer
            this.pollTimer = new WinFormsTimer();
            this.pollTimer.Interval = Math.Clamp(this.config.PollMs, 100, 5000);
            this.pollTimer.Tick += (_, __) => this.ApplyOnce(force: false);
            this.pollTimer.Start();

            this.RefreshUiEnabledState();

            // Auto-launch after form is ready
            this.Load += (_, __) => this.MaybeAutoLaunch();

            if (this.config.StartMinimized)
                this.Shown += (_, __) => this.MinimizeToTray();
        }

        // =====================
        // NEW: Manual launch button handler
        // =====================
        private void LaunchGameNow()
        {
            this.PullUiToConfig();

            string cmd = (this.config.LaunchCommand ?? "").Trim();
            if (string.IsNullOrWhiteSpace(cmd))
            {
                this.lblStatus.Text = "Status: Launch command is empty.";
                return;
            }

            // If already running, don't launch again
            if (this.TryGetTargetProcess(out var running, out _))
            {
                this.lblStatus.Text = $"Status: Game already running (PID {running.Id}); launch skipped.";
                return;
            }

            try
            {
                if (!TrySplitCommand(cmd, out string file, out string args))
                {
                    this.lblStatus.Text = "Status: Launch failed (could not parse command).";
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    UseShellExecute = false
                };

                var p = Process.Start(psi);
                if (p is null)
                {
                    this.lblStatus.Text = "Status: Launch failed (Process.Start returned null).";
                    return;
                }

                this.launchedProcess = p;
                this.lblStatus.Text = "Status: Launched game.";
            }
            catch (Exception ex)
            {
                this.lblStatus.Text = $"Status: Launch failed: {ex.GetType().Name} - {ex.Message}";
            }
        }

        // =====================
        // Background drawing
        // =====================
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);

            if (this.backgroundImage is null)
                return;

            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            Rectangle dest = GetCoverRectangle(this.backgroundImage.Size, this.ClientSize);

            using var imageAttributes = new ImageAttributes();
            var matrix = new ColorMatrix { Matrix33 = BackgroundOpacity };
            imageAttributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

            e.Graphics.DrawImage(
                this.backgroundImage,
                dest,
                0, 0, this.backgroundImage.Width, this.backgroundImage.Height,
                GraphicsUnit.Pixel,
                imageAttributes
            );
        }

        private void TryLoadEmbeddedBackgroundImage()
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                string? resName = asm.GetManifestResourceNames()
                    .FirstOrDefault(n =>
                        n.EndsWith("." + EmbeddedBackgroundFileName, StringComparison.OrdinalIgnoreCase) ||
                        n.EndsWith(EmbeddedBackgroundFileName, StringComparison.OrdinalIgnoreCase));

                if (string.IsNullOrWhiteSpace(resName))
                    return;

                using var stream = asm.GetManifestResourceStream(resName);
                if (stream is null)
                    return;

                using var temp = Image.FromStream(stream);
                this.backgroundImage = new Bitmap(temp);
            }
            catch { }
        }

        private static Rectangle GetCoverRectangle(Size img, Size area)
        {
            float imgAspect = (float)img.Width / img.Height;
            float areaAspect = (float)area.Width / area.Height;

            int drawW, drawH;
            if (imgAspect > areaAspect)
            {
                drawH = area.Height;
                drawW = (int)(drawH * imgAspect);
            }
            else
            {
                drawW = area.Width;
                drawH = (int)(drawW / imgAspect);
            }

            int x = (area.Width - drawW) / 2;
            int y = (area.Height - drawH) / 2;
            return new Rectangle(x, y, drawW, drawH);
        }

        // =====================
        // UI helpers
        // =====================
        private static ComboBox MakePriorityCombo(PriorityChoice selected)
        {
            var cb = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cb.Items.AddRange(new object[] { PriorityChoice.Normal, PriorityChoice.AboveNormal, PriorityChoice.High });
            cb.SelectedItem = selected;
            return cb;
        }

        private static void AddRow(TableLayoutPanel table, string labelText, Control field)
        {
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            int row = table.RowCount++;

            var label = new WinFormsLabel
            {
                Text = labelText,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 6, 10, 6),
                BackColor = Color.Transparent
            };

            field.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            field.Margin = new Padding(0, 3, 0, 3);

            if (field is Panel or FlowLayoutPanel or TableLayoutPanel)
                field.BackColor = Color.Transparent;

            table.Controls.Add(label, 0, row);
            table.Controls.Add(field, 1, row);
        }

        private void RefreshUiEnabledState()
        {
            Mode mode = (Mode)(this.cmbMode.SelectedItem ?? Mode.AutoFocus);

            this.cmbManualPriority.Enabled = mode == Mode.Manual;
            this.cmbFocusedPriority.Enabled = mode == Mode.AutoFocus;
            this.cmbUnfocusedPriority.Enabled = mode == Mode.AutoFocus;

            bool autoLaunch = this.chkAutoLaunch.Checked;
            this.txtLaunchCommand.Enabled = autoLaunch;

            // Launch button should only be usable if auto-launch section is enabled + command present
            this.btnLaunchNow.Enabled = autoLaunch && !string.IsNullOrWhiteSpace(this.txtLaunchCommand.Text);
        }

        private void BrowseForExe()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select Cyberpunk 2077 EXE",
                Filter = "Executable (*.exe)|*.exe|All files (*.*)|*.*",
                CheckFileExists = true,
                CheckPathExists = true
            };

            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            this.txtExePath.Text = dlg.FileName;

            try
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
                if (!string.IsNullOrWhiteSpace(name))
                    this.txtProcess.Text = name;

                if (string.IsNullOrWhiteSpace(this.txtLaunchCommand.Text))
                    this.txtLaunchCommand.Text = $"\"{dlg.FileName}\"";
            }
            catch { }
        }

        private void PullUiToConfig()
        {
            this.config.ProcessName = (this.txtProcess.Text ?? "").Trim();

            this.config.RememberExePath = this.chkRememberExe.Checked;
            this.config.ExePath = (this.txtExePath.Text ?? "").Trim();

            this.config.Mode = (Mode)(this.cmbMode.SelectedItem ?? Mode.AutoFocus);

            this.config.ManualPriority = (PriorityChoice)(this.cmbManualPriority.SelectedItem ?? PriorityChoice.High);
            this.config.FocusedPriority = (PriorityChoice)(this.cmbFocusedPriority.SelectedItem ?? PriorityChoice.High);
            this.config.UnfocusedPriority = (PriorityChoice)(this.cmbUnfocusedPriority.SelectedItem ?? PriorityChoice.Normal);

            this.config.PollMs = (int)this.nudPollMs.Value;

            this.config.AutoLaunchEnabled = this.chkAutoLaunch.Checked;
            this.config.LaunchCommand = this.txtLaunchCommand.Text ?? "";

            this.config.CloseGameOnExit = this.chkCloseGameOnExit.Checked;

            this.config.MinimizeToTray = this.chkMinimizeToTray.Checked;
            this.config.StartMinimized = this.chkStartMinimized.Checked;

            this.pollTimer.Interval = Math.Clamp(this.config.PollMs, 100, 5000);
        }

        private void SaveConfig()
        {
            if (!this.config.RememberExePath)
                this.config.ExePath = "";

            this.config.Save();
        }

        // =====================
        // Auto-launch + exit-kill
        // =====================
        private void MaybeAutoLaunch()
        {
            if (this.autoLaunchAttempted)
                return;

            this.autoLaunchAttempted = true;

            this.PullUiToConfig();

            if (!this.config.AutoLaunchEnabled)
                return;

            string cmd = (this.config.LaunchCommand ?? "").Trim();
            if (string.IsNullOrWhiteSpace(cmd))
            {
                this.lblStatus.Text = "Status: Auto-launch enabled, but Launch Command is empty.";
                return;
            }

            if (this.TryGetTargetProcess(out _, out _))
            {
                this.lblStatus.Text = "Status: Game already running; auto-launch skipped.";
                return;
            }

            try
            {
                if (!TrySplitCommand(cmd, out string file, out string args))
                {
                    this.lblStatus.Text = "Status: Auto-launch failed (could not parse command).";
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    UseShellExecute = false
                };

                var p = Process.Start(psi);
                if (p is null)
                {
                    this.lblStatus.Text = "Status: Auto-launch failed (Process.Start returned null).";
                    return;
                }

                this.launchedProcess = p;
                this.lblStatus.Text = "Status: Auto-launched game command.";
            }
            catch (Exception ex)
            {
                this.lblStatus.Text = $"Status: Auto-launch failed: {ex.GetType().Name} - {ex.Message}";
            }
        }

        private void ExitApplication()
        {
            this.isExiting = true;

            try { this.pollTimer.Stop(); } catch { }

            this.PullUiToConfig();

            if (this.config.CloseGameOnExit)
            {
                if (this.launchedProcess is not null)
                    PriorityManager.KillProcessTreeSafe(this.launchedProcess);
                else if (this.TryGetTargetProcess(out var proc, out _))
                    PriorityManager.KillProcessTreeSafe(proc);
            }

            try { this.trayIcon.Visible = false; } catch { }

            this.Close();
        }

        // =====================
        // Core polling logic
        // =====================
        private void ApplyOnce(bool force)
        {
            this.PullUiToConfig();

            if (!this.TryGetTargetProcess(out var proc, out string targetLabel))
            {
                this.lastApplied = null;
                this.lastFocused = null;
                this.lastPidStatus = null;
                this.lblStatus.Text = $"Status: Waiting for {targetLabel} ...";
                return;
            }

            int pid = proc.Id;
            this.lastPidStatus = pid;

            if (this.config.Mode == Mode.Disabled)
            {
                this.lblStatus.Text = $"Status: Disabled (game running, PID {pid})";
                return;
            }

            bool isFocused = NativeMethods.IsProcessInForeground(pid);

            PriorityChoice targetChoice =
                this.config.Mode == Mode.Manual
                    ? this.config.ManualPriority
                    : (isFocused ? this.config.FocusedPriority : this.config.UnfocusedPriority);

            ProcessPriorityClass target = PriorityManager.ToPriorityClass(targetChoice);

            if (!force)
            {
                if (this.lastApplied == target && (this.config.Mode != Mode.AutoFocus || this.lastFocused == isFocused))
                {
                    this.lblStatus.Text = this.config.Mode == Mode.AutoFocus
                        ? $"Status: {(isFocused ? "Focused" : "Unfocused")} | Priority: {target} | PID {pid}"
                        : $"Status: Manual | Priority: {target} | PID {pid}";
                    return;
                }
            }

            if (!PriorityManager.TrySetPriority(proc, targetChoice, out string? error))
            {
                this.lblStatus.Text = $"Status: Failed to set priority: {error}";
                return;
            }

            this.lastApplied = target;
            this.lastFocused = isFocused;

            this.lblStatus.Text = this.config.Mode == Mode.AutoFocus
                ? $"Status: {(isFocused ? "Focused" : "Unfocused")} | Applied: {target} | PID {pid}"
                : $"Status: Manual | Applied: {target} | PID {pid}";
        }

        private bool TryGetTargetProcess(out Process proc, out string label)
        {
            proc = null!;
            label = "";

            string exePath = (this.config.ExePath ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                label = exePath;
                if (PriorityManager.TryGetProcessByExePath(exePath, out var p) && p is not null)
                {
                    proc = p;
                    return true;
                }
                return false;
            }

            string name = (this.config.ProcessName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name))
                name = "Cyberpunk2077";

            label = name + ".exe";

            if (PriorityManager.TryGetProcessByName(name, out var p2) && p2 is not null)
            {
                proc = p2;
                return true;
            }

            return false;
        }

        private void MinimizeToTray()
        {
            this.Hide();
            this.ShowInTaskbar = false;
            this.trayIcon.Visible = true;
        }

        private void RestoreFromTray()
        {
            this.Show();
            this.ShowInTaskbar = true;
            this.WindowState = FormWindowState.Normal;
            this.Activate();
        }

        private void BuildTrayMenu()
        {
            this.trayMenu.Items.Clear();

            var itemOpen = new ToolStripMenuItem("Open")
            {
                Font = new Font(SystemFonts.MenuFont, FontStyle.Bold)
            };
            itemOpen.Click += (_, __) => this.RestoreFromTray();
            this.trayMenu.Items.Add(itemOpen);

            this.trayMenu.Items.Add(new ToolStripSeparator());

            var itemExit = new ToolStripMenuItem("Exit");
            itemExit.Click += (_, __) => this.ExitApplication();
            this.trayMenu.Items.Add(itemExit);
        }

        private static bool TrySplitCommand(string command, out string file, out string args)
        {
            file = "";
            args = "";

            command = (command ?? "").Trim();
            if (command.Length == 0)
                return false;

            if (command[0] == '"')
            {
                int endQuote = command.IndexOf('"', 1);
                if (endQuote <= 1)
                    return false;

                file = command.Substring(1, endQuote - 1).Trim();
                args = command.Substring(endQuote + 1).Trim();
                return file.Length > 0;
            }

            int firstSpace = command.IndexOf(' ');
            if (firstSpace < 0)
            {
                file = command;
                args = "";
                return true;
            }

            file = command.Substring(0, firstSpace).Trim();
            args = command.Substring(firstSpace + 1).Trim();
            return file.Length > 0;
        }
    }
}
