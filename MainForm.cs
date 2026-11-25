using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using Microsoft.Win32;

namespace AutoContinue;

internal sealed class MainForm : Form
{
    private const int CaptureHotkeyId = 1;
    private const uint ModifierControl = 0x0002;
    private const uint ModifierAlt = 0x0001;
    private const int WmHotkey = 0x0312;
    private const string StartupKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "AutoContinue";

    private readonly ComboBox _clickTypeInput = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 140
    };
    private readonly NumericUpDown _intervalInput = new()
    {
        Minimum = 1,
        Maximum = 1440,
        Value = 5,
        DecimalPlaces = 0,
        Width = 100
    };
    private readonly ComboBox _intervalUnitInput = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 100
    };
    private readonly CheckBox _repeatIndefinitelyCheckBox = new() { Text = "Repeat indefinitely", AutoSize = true, Checked = true };
    private readonly NumericUpDown _runCountInput = new() { Minimum = 1, Maximum = 1_000_000, Value = 10, DecimalPlaces = 0, Width = 100, Enabled = false };
    private readonly NumericUpDown _xInput = CreateCoordinateInput();
    private readonly NumericUpDown _yInput = CreateCoordinateInput();
    private readonly Button _startButton = new() { Text = "Start", AutoSize = true };
    private readonly Button _stopButton = new() { Text = "Stop", AutoSize = true };
    private readonly Button _pauseButton = new() { Text = "Pause", AutoSize = true };
    private readonly Button _testButton = new() { Text = "Test Click", AutoSize = true };
    private readonly Button _selectPositionButton = new() { Text = "Select Button Position", AutoSize = true };
    private readonly CheckBox _startupCheckBox = new() { Text = "Start with Windows", AutoSize = true };
    private readonly Label _statusLabel = new() { AutoSize = true, Text = "Stopped" };
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _trayMenu;
    private readonly AutoContinueScheduler _scheduler;
    private readonly SynchronizationContext _uiContext;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private bool _hotkeyRegistered;
    private string? _lastError;

    public MainForm()
    {
        Text = "Auto Continue";
        Width = 560;
        Height = 435;
        MinimumSize = new Size(520, 410);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _scheduler = new AutoContinueScheduler(NativeMethods.ClickAt);
        _scheduler.StatusChanged += ScheduleStatusChanged;
        _scheduler.ErrorOccurred += ScheduleErrorOccurred;

        _trayMenu = new ContextMenuStrip();
        _trayMenu.Items.Add("Open", null, (_, _) => ShowWindow());
        _trayMenu.Items.Add("Pause / Resume", null, (_, _) => TogglePause());
        _trayMenu.Items.Add("Stop", null, (_, _) => StopAutomation());
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("Exit", null, (_, _) => ExitApplication());
        _trayIcon = new NotifyIcon
        {
            Text = "Auto Continue",
            Icon = SystemIcons.Application,
            ContextMenuStrip = _trayMenu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ShowWindow();

        BuildLayout();
        _startupCheckBox.Checked = IsStartupEnabled();
        WireEvents();
        UpdateControls(_scheduler.GetCurrentStatus());
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 2,
            RowCount = 10,
            AutoSize = false
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 10; i++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        _clickTypeInput.Items.AddRange(["Single click", "Double click", "Two clicks (1.5 sec apart)"]);
        _clickTypeInput.SelectedIndex = 0;
        AddRow(layout, "Click type", _clickTypeInput, 0);

        _intervalUnitInput.Items.AddRange(["seconds", "minutes", "hours"]);
        _intervalUnitInput.SelectedIndex = 1;
        var intervalPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        intervalPanel.Controls.Add(_intervalInput);
        intervalPanel.Controls.Add(_intervalUnitInput);
        AddRow(layout, "Interval", intervalPanel, 1);

        var countPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        countPanel.Controls.Add(_repeatIndefinitelyCheckBox);
        countPanel.Controls.Add(new Label { Text = "Run count:", AutoSize = true, Margin = new Padding(12, 6, 3, 3) });
        countPanel.Controls.Add(_runCountInput);
        AddRow(layout, "How many times", countPanel, 2);

        AddRow(layout, "X screen coordinate", _xInput, 3);
        AddRow(layout, "Y screen coordinate", _yInput, 4);

        _selectPositionButton.Margin = new Padding(3, 10, 3, 5);
        layout.Controls.Add(_selectPositionButton, 0, 5);
        layout.SetColumnSpan(_selectPositionButton, 2);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = false,
            Margin = new Padding(0, 8, 0, 8)
        };
        buttons.Controls.AddRange([_startButton, _stopButton, _pauseButton, _testButton]);
        layout.Controls.Add(buttons, 0, 6);
        layout.SetColumnSpan(buttons, 2);

        var statusPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = false
        };
        statusPanel.Controls.Add(new Label { Text = "Status:", AutoSize = true, Margin = new Padding(3, 5, 6, 3) });
        statusPanel.Controls.Add(_statusLabel);
        layout.Controls.Add(statusPanel, 0, 7);
        layout.SetColumnSpan(statusPanel, 2);

        layout.Controls.Add(_startupCheckBox, 0, 8);
        layout.SetColumnSpan(_startupCheckBox, 2);

        var hint = new Label
        {
            Text = "Capture: move over the button, then press Ctrl+Alt+C",
            AutoSize = true,
            Margin = new Padding(3, 5, 3, 3)
        };
        layout.Controls.Add(hint, 0, 9);
        layout.SetColumnSpan(hint, 2);

        Controls.Add(layout);
    }

    private static void AddRow(TableLayoutPanel layout, string label, Control input, int row)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 3, 3) }, 0, row);
        input.Margin = new Padding(3, 3, 3, 3);
        layout.Controls.Add(input, 1, row);
    }

    private void WireEvents()
    {
        _startButton.Click += (_, _) => StartAutomation();
        _stopButton.Click += (_, _) => StopAutomation();
        _pauseButton.Click += (_, _) => TogglePause();
        _testButton.Click += (_, _) => TestClick();
        _selectPositionButton.Click += (_, _) => BeginPositionCapture();
        _repeatIndefinitelyCheckBox.CheckedChanged += (_, _) =>
            _runCountInput.Enabled = !_repeatIndefinitelyCheckBox.Checked;
        _intervalUnitInput.SelectedIndexChanged += (_, _) => UpdateIntervalMaximum();
        _startupCheckBox.CheckedChanged += StartupCheckBoxChanged;
        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized)
            {
                HideToTray();
            }
        };
        FormClosing += (_, e) =>
        {
            if (_hotkeyRegistered)
            {
                NativeMethods.UnregisterHotKey(Handle, CaptureHotkeyId);
            }

            _lifetimeCancellation.Cancel();
            _scheduler.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayMenu.Dispose();
        };
    }

    private void StartAutomation()
    {
        _lastError = null;
        _scheduler.Start(
            GetInterval(),
            new Point((int)_xInput.Value, (int)_yInput.Value),
            GetClickType(),
            _repeatIndefinitelyCheckBox.Checked ? null : (int)_runCountInput.Value);
    }

    private void StopAutomation()
    {
        _scheduler.Stop();
        _lastError = null;
        UpdateControls(_scheduler.GetCurrentStatus());
    }

    private void TogglePause()
    {
        _scheduler.TogglePause();
    }

    private async void TestClick()
    {
        try
        {
            var point = new Point((int)_xInput.Value, (int)_yInput.Value);
            if (GetClickType() == MouseClickType.TwoClicksWithDelay)
            {
                NativeMethods.ClickAt(point);
                await Task.Delay(AutoContinueScheduler.DelayedClickGap, _lifetimeCancellation.Token);
                NativeMethods.ClickAt(point);
            }
            else
            {
                NativeMethods.ClickAt(point, GetClickType());
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or ArgumentOutOfRangeException)
        {
            MessageBox.Show(this, exception.Message, "Test click failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BeginPositionCapture()
    {
        if (NativeMethods.RegisterHotKey(Handle, CaptureHotkeyId, ModifierControl | ModifierAlt, (uint)Keys.C))
        {
            _hotkeyRegistered = true;
            _selectPositionButton.Enabled = false;
            _startButton.Enabled = false;
            _testButton.Enabled = false;
            _statusLabel.Text = "Move to the button and press Ctrl+Alt+C";
            return;
        }

        var error = new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        MessageBox.Show(this, $"Could not register Ctrl+Alt+C: {error.Message}", "Position capture unavailable",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmHotkey && message.WParam.ToInt32() == CaptureHotkeyId)
        {
            if (NativeMethods.GetCursorPos(out var point))
            {
                _xInput.Value = point.X;
                _yInput.Value = point.Y;
                _statusLabel.Text = $"Position captured: {point.X}, {point.Y}";
            }
            else
            {
                var error = new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                MessageBox.Show(this, $"Could not read the pointer position: {error.Message}", "Position capture failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            NativeMethods.UnregisterHotKey(Handle, CaptureHotkeyId);
            _hotkeyRegistered = false;
            _selectPositionButton.Enabled = true;
            _startButton.Enabled = true;
            _testButton.Enabled = true;
        }

        base.WndProc(ref message);
    }

    private void ScheduleStatusChanged(SchedulerStatus _)
    {
        _uiContext.Post(_ =>
        {
            if (!IsDisposed)
            {
                UpdateControls(_scheduler.GetCurrentStatus());
            }
        }, null);
    }

    private void ScheduleErrorOccurred(Exception exception)
    {
        _uiContext.Post(_ =>
        {
            if (IsDisposed)
            {
                return;
            }

            _lastError = exception.Message;
            _trayIcon.ShowBalloonTip(5000, "Auto Continue stopped", exception.Message, ToolTipIcon.Error);
            UpdateControls(_scheduler.GetCurrentStatus());
        }, null);
    }

    private void UpdateControls(SchedulerStatus status)
    {
        var running = status.State == SchedulerState.Running;
        var paused = status.State == SchedulerState.Paused;
        _startButton.Enabled = status.State == SchedulerState.Stopped && !_hotkeyRegistered;
        _stopButton.Enabled = running || paused;
        _pauseButton.Enabled = running || paused;
        _pauseButton.Text = paused ? "Resume" : "Pause";
        var settingsEnabled = status.State == SchedulerState.Stopped;
        _clickTypeInput.Enabled = settingsEnabled;
        _intervalInput.Enabled = status.State == SchedulerState.Stopped;
        _intervalUnitInput.Enabled = settingsEnabled;
        _repeatIndefinitelyCheckBox.Enabled = settingsEnabled;
        _runCountInput.Enabled = settingsEnabled && !_repeatIndefinitelyCheckBox.Checked;
        _xInput.Enabled = status.State == SchedulerState.Stopped;
        _yInput.Enabled = status.State == SchedulerState.Stopped;
        _selectPositionButton.Enabled = status.State == SchedulerState.Stopped && !_hotkeyRegistered;
        _testButton.Enabled = status.State == SchedulerState.Stopped;

        _statusLabel.Text = status.State switch
        {
            SchedulerState.Running => $"Running{FormatRunsRemaining(status.RunsRemaining)} — next click in {FormatCountdown(status.Remaining)}",
            SchedulerState.Paused => $"Paused{FormatRunsRemaining(status.RunsRemaining)} — {FormatCountdown(status.Remaining)} remaining",
            _ => _lastError is null ? "Stopped" : $"Stopped — {_lastError}"
        };
    }

    private static string FormatCountdown(TimeSpan remaining)
    {
        var seconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
        var countdown = TimeSpan.FromSeconds(seconds);
        return $"{(int)countdown.TotalHours:00}:{countdown.Minutes:00}:{countdown.Seconds:00}";
    }

    private TimeSpan GetInterval()
    {
        var value = (double)_intervalInput.Value;
        return _intervalUnitInput.SelectedIndex switch
        {
            0 => TimeSpan.FromSeconds(value),
            1 => TimeSpan.FromMinutes(value),
            2 => TimeSpan.FromHours(value),
            _ => throw new InvalidOperationException("Select an interval unit.")
        };
    }

    private MouseClickType GetClickType() =>
        _clickTypeInput.SelectedIndex switch
        {
            0 => MouseClickType.Single,
            1 => MouseClickType.Double,
            2 => MouseClickType.TwoClicksWithDelay,
            _ => throw new InvalidOperationException("Select a click type.")
        };

    private static string FormatRunsRemaining(int? runsRemaining) =>
        runsRemaining is int count ? $" — {count} runs left" : string.Empty;

    private void UpdateIntervalMaximum()
    {
        _intervalInput.Maximum = _intervalUnitInput.SelectedIndex switch
        {
            0 => 86_400,
            1 => 1_440,
            2 => 24,
            _ => 1_440
        };
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
    }

    private void ShowWindow()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        Close();
    }

    private bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupKeyPath);
        return key?.GetValue(StartupValueName) is string;
    }

    private void SetStartupEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(StartupKeyPath, writable: true)
                ?? throw new InvalidOperationException("Could not open the Windows startup registry key.");
            if (enabled)
            {
                var executable = Environment.ProcessPath
                    ?? throw new InvalidOperationException("Could not determine the application executable path.");
                key.SetValue(StartupValueName, $"\"{executable}\"");
            }
            else
            {
                key.DeleteValue(StartupValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException
            or IOException or InvalidOperationException)
        {
            _startupCheckBox.CheckedChanged -= StartupCheckBoxChanged;
            _startupCheckBox.Checked = !enabled;
            _startupCheckBox.CheckedChanged += StartupCheckBoxChanged;
            MessageBox.Show(this, exception.Message, "Startup setting failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StartupCheckBoxChanged(object? sender, EventArgs e) =>
        SetStartupEnabled(_startupCheckBox.Checked);

    private static NumericUpDown CreateCoordinateInput() => new()
    {
        Minimum = int.MinValue,
        Maximum = int.MaxValue,
        DecimalPlaces = 0,
        Width = 140
    };
}
