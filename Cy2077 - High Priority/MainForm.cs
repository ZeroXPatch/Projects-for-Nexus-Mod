using CyberpunkPriorityTray;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsTimer = System.Windows.Forms.Timer;

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

        // ✅ Background image embedded resource + opacity
        private Image? backgroundImage;
        private const float BackgroundOpacity = 0.30f; // 30%
        private const string EmbeddedBackgroundFileName = "cyberpunk_bg.png";

        // UI controls
        private readonly TextBox txtProcess;
        private readonly ComboBox cmbMode;
        private readonly ComboBox cmbManualPriority;
        private readonly ComboBox cmbFocusedPriority;
        private readonly ComboBox cmbUnfocusedPriority;
        private readonly NumericUpDown nudPollMs;
        private readonly CheckBox chkMinimizeToTray;
        private readonly CheckBox chkStartMinimized;
        private readonly WinFormsLabel lblStatus;
        private readonly Button btnSave;
        private readonly Button btnApplyNow;

        public MainForm()
        {
            this.Text = "Cyberpunk Priority Tray";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.ClientSize = new Size(560, 380);

            // Helps reduce flicker when drawing custom background
            this.DoubleBuffered = true;

            this.config = AppConfig.Load();

            // ✅ Load embedded background image (no file on disk needed)
            this.TryLoadEmbeddedBackgroundImage();

            // ----- Tray -----
            this.trayMenu = new ContextMenuStrip();
            this.trayIcon = new NotifyIcon
            {
                Text = "Cyberpunk Priority Tray",
                Icon = SystemIcons.Application,
                Visible = true,
                ContextMenuStrip = this.trayMenu
            };
            this.trayIcon.DoubleClick += (_, __) => this.RestoreFromTray();
            this.BuildTrayMenu();

            // ----- Layout -----
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 0,
                Padding = new Padding(12),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            this.Controls.Add(root);

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

            // --- Create controls ---
            this.txtProcess = new TextBox { Width = 280, Text = this.config.ProcessName };

            this.cmbMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
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
                Width = 140
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
                Height = 54,
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
                this.config.Save();
                this.BuildTrayMenu();
                this.ApplyOnce(force: true);
                MessageBox.Show("Saved.", "Cyberpunk Priority Tray");
            };

            // --- Add rows ---
            AddRow(root, "Process name:", this.txtProcess);
            AddRow(root, "Mode:", this.cmbMode);
            AddRow(root, "Manual priority:", this.cmbManualPriority);
            AddRow(root, "Focused priority:", this.cmbFocusedPriority);
            AddRow(root, "Unfocused priority:", this.cmbUnfocusedPriority);
            AddRow(root, "Poll interval (ms):", this.nudPollMs);

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

            // status row
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, this.lblStatus.Height + 6));
            int statusRow = root.RowCount++;
            root.Controls.Add(this.lblStatus, 0, statusRow);
            root.SetColumnSpan(this.lblStatus, 2);
            this.lblStatus.Margin = new Padding(0, 8, 0, 0);

            // buttons row
            buttonBar.Controls.Add(this.btnApplyNow);
            buttonBar.Controls.Add(this.btnSave);

            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            int btnRow = root.RowCount++;
            root.Controls.Add(buttonBar, 0, btnRow);
            root.SetColumnSpan(buttonBar, 2);

            // ----- Minimize-to-tray behavior -----
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

            // ----- Poll timer -----
            this.pollTimer = new WinFormsTimer();
            this.pollTimer.Interval = Math.Clamp(this.config.PollMs, 100, 5000);
            this.pollTimer.Tick += (_, __) => this.ApplyOnce(force: false);
            this.pollTimer.Start();

            this.RefreshUiEnabledState();

            if (this.config.StartMinimized)
                this.Shown += (_, __) => this.MinimizeToTray();
        }

        // ✅ Draw embedded image as faded background
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

                // Find resource by suffix (so you don't need to guess the full resource name)
                string? resName = asm
                    .GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("." + EmbeddedBackgroundFileName, StringComparison.OrdinalIgnoreCase)
                                      || n.EndsWith(EmbeddedBackgroundFileName, StringComparison.OrdinalIgnoreCase));

                if (string.IsNullOrWhiteSpace(resName))
                    return;

                using var stream = asm.GetManifestResourceStream(resName);
                if (stream is null)
                    return;

                using var temp = Image.FromStream(stream);
                this.backgroundImage = new Bitmap(temp); // copy into memory
            }
            catch
            {
                // ignore; app still works without background
            }
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

        private static ComboBox MakePriorityCombo(PriorityChoice selected)
        {
            var cb = new ComboBox
            {
                Width = 280,
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
        }

        private void PullUiToConfig()
        {
            this.config.ProcessName = this.txtProcess.Text.Trim();
            this.config.Mode = (Mode)(this.cmbMode.SelectedItem ?? Mode.AutoFocus);

            this.config.ManualPriority = (PriorityChoice)(this.cmbManualPriority.SelectedItem ?? PriorityChoice.High);
            this.config.FocusedPriority = (PriorityChoice)(this.cmbFocusedPriority.SelectedItem ?? PriorityChoice.High);
            this.config.UnfocusedPriority = (PriorityChoice)(this.cmbUnfocusedPriority.SelectedItem ?? PriorityChoice.Normal);

            this.config.PollMs = (int)this.nudPollMs.Value;
            this.config.MinimizeToTray = this.chkMinimizeToTray.Checked;
            this.config.StartMinimized = this.chkStartMinimized.Checked;

            this.pollTimer.Interval = Math.Clamp(this.config.PollMs, 100, 5000);
        }

        private void ApplyOnce(bool force)
        {
            this.PullUiToConfig();

            if (!PriorityManager.TryGetProcess(this.config.ProcessName, out var proc) || proc is null)
            {
                this.lastApplied = null;
                this.lastFocused = null;
                this.lastPidStatus = null;
                this.lblStatus.Text = $"Status: Waiting for {this.config.ProcessName}.exe ...";
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

        private void ExitApplication()
        {
            this.isExiting = true;

            try { this.pollTimer.Stop(); } catch { }
            try { this.trayIcon.Visible = false; } catch { }

            this.Close();
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
    }
}
