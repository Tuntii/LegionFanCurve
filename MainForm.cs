using System.ComponentModel;

namespace LegionFanCurve;

public sealed class MainForm : Form
{
    private readonly DataGridView _grid = new();
    private readonly Label _status = new();
    private readonly Label _live = new();
    private readonly NumericUpDown _maxRpm = new();
    private readonly NumericUpDown _hyst = new();
    private readonly Button _btnApply = new();
    private readonly Button _btnSave = new();
    private readonly Button _btnDefault = new();
    private readonly Button _btnRefresh = new();
    private readonly NotifyIcon _tray = new();
    private readonly System.Windows.Forms.Timer _timer = new();
    private FanCurveConfig _cfg = FanCurveConfig.Default4400();
    private bool _ecOk;

    public MainForm()
    {
        Text = "Legion Fan Curve — 15ARH05H (max 4400)";
        Width = 720;
        Height = 560;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        MinimumSize = new Size(640, 480);

        var top = new Panel { Dock = DockStyle.Top, Height = 88, Padding = new Padding(12) };
        var lblMax = new Label { Text = "Max RPM:", AutoSize = true, Left = 12, Top = 16 };
        _maxRpm.Minimum = 2000;
        _maxRpm.Maximum = 5500;
        _maxRpm.Increment = 100;
        _maxRpm.Value = 4400;
        _maxRpm.Left = 90;
        _maxRpm.Top = 12;
        _maxRpm.Width = 90;

        var lblH = new Label { Text = "Hysteresis °C:", AutoSize = true, Left = 200, Top = 16 };
        _hyst.Minimum = 1;
        _hyst.Maximum = 8;
        _hyst.Value = 3;
        _hyst.Left = 300;
        _hyst.Top = 12;
        _hyst.Width = 60;

        _btnDefault.Text = "4400 varsayılan";
        _btnDefault.Left = 380;
        _btnDefault.Top = 10;
        _btnDefault.Width = 130;
        _btnDefault.Click += (_, _) => LoadDefault();

        _btnApply.Text = "EC'ye uygula";
        _btnApply.Left = 520;
        _btnApply.Top = 10;
        _btnApply.Width = 120;
        _btnApply.BackColor = Color.FromArgb(0, 120, 215);
        _btnApply.ForeColor = Color.White;
        _btnApply.FlatStyle = FlatStyle.Flat;
        _btnApply.Click += (_, _) => ApplyCurve();

        _btnSave.Text = "Kaydet";
        _btnSave.Left = 520;
        _btnSave.Top = 46;
        _btnSave.Width = 70;
        _btnSave.Click += (_, _) => SaveCurve();

        _btnRefresh.Text = "Yenile";
        _btnRefresh.Left = 600;
        _btnRefresh.Top = 46;
        _btnRefresh.Width = 70;
        _btnRefresh.Click += (_, _) => RefreshLive();

        _status.AutoSize = false;
        _status.Left = 12;
        _status.Top = 48;
        _status.Width = 500;
        _status.Height = 32;
        _status.ForeColor = Color.DimGray;

        top.Controls.AddRange(new Control[]
        {
            lblMax, _maxRpm, lblH, _hyst, _btnDefault, _btnApply, _btnSave, _btnRefresh, _status
        });

        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = true;
        _grid.AllowUserToDeleteRows = true;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.RowHeadersVisible = false;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "cpu", HeaderText = "CPU °C", FillWeight = 30 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "gpu", HeaderText = "GPU °C", FillWeight = 30 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "rpm", HeaderText = "Fan RPM", FillWeight = 40 });

        _live.Dock = DockStyle.Bottom;
        _live.Height = 64;
        _live.Padding = new Padding(12);
        _live.TextAlign = ContentAlignment.MiddleLeft;
        _live.BackColor = Color.FromArgb(32, 32, 32);
        _live.ForeColor = Color.WhiteSmoke;
        _live.Font = new Font("Consolas", 10f);

        Controls.Add(_grid);
        Controls.Add(_live);
        Controls.Add(top);

        _tray.Text = "Legion Fan Curve";
        _tray.Icon = SystemIcons.Application;
        _tray.Visible = true;
        _tray.DoubleClick += (_, _) => { Show(); WindowState = FormWindowState.Normal; };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Göster", null, (_, _) => { Show(); WindowState = FormWindowState.Normal; });
        menu.Items.Add("EC'ye uygula", null, (_, _) => ApplyCurve());
        menu.Items.Add("Çıkış", null, (_, _) => { _tray.Visible = false; Application.Exit(); });
        _tray.ContextMenuStrip = menu;

        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                _tray.ShowBalloonTip(1500, "Legion Fan Curve", "Arka planda çalışıyor (tray).", ToolTipIcon.Info);
            }
        };

        _timer.Interval = 2000;
        _timer.Tick += (_, _) => RefreshLive();
        Load += MainForm_Load;
        FormClosing += MainForm_FormClosing;
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        _ecOk = EcAccess.Init();
        _status.Text = _ecOk
            ? "EC / InpOut: OK — eğri yazılabilir"
            : "EC / InpOut: KAPALI — " + (EcAccess.LastError ?? "bilinmiyor");
        _status.ForeColor = _ecOk ? Color.ForestGreen : Color.Firebrick;

        _cfg = FanCurveConfig.Load();
        _maxRpm.Value = Math.Clamp(_cfg.MaxRpm, _maxRpm.Minimum, _maxRpm.Maximum);
        _hyst.Value = Math.Clamp(_cfg.Hysteresis, _hyst.Minimum, _hyst.Maximum);
        FillGrid(_cfg);

        try { GamezoneWmi.SetPerformanceMode(); } catch { /* ignore */ }
        RefreshLive();
        _timer.Start();

        if (_ecOk)
        {
            // Auto-apply saved curve on launch so fans don't stay stuck at 2k
            try
            {
                ApplyCurve(silent: true);
                _status.Text = "EC OK — kayıtlı eğri uygulandı (max " + _cfg.MaxRpm + ")";
            }
            catch (Exception ex)
            {
                _status.Text = "EC OK ama uygulama hatası: " + ex.Message;
                _status.ForeColor = Color.DarkOrange;
            }
        }
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        _timer.Stop();
        _tray.Visible = false;
    }

    private void FillGrid(FanCurveConfig cfg)
    {
        _grid.Rows.Clear();
        foreach (var p in cfg.Points)
            _grid.Rows.Add(p.Cpu, p.Gpu, p.Rpm);
    }

    private FanCurveConfig ReadFromUi()
    {
        var pts = new List<CurvePoint>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.IsNewRow) continue;
            if (!int.TryParse(Convert.ToString(row.Cells[0].Value), out int cpu)) continue;
            if (!int.TryParse(Convert.ToString(row.Cells[1].Value), out int gpu)) gpu = cpu;
            if (!int.TryParse(Convert.ToString(row.Cells[2].Value), out int rpm)) continue;
            int max = (int)_maxRpm.Value;
            pts.Add(new CurvePoint(cpu, gpu, Math.Min(rpm, max)));
        }
        if (pts.Count < 2)
            throw new InvalidOperationException("En az 2 eğri noktası gir.");

        return new FanCurveConfig
        {
            LegionGen = 5,
            MaxRpm = (int)_maxRpm.Value,
            Accel = 2,
            Decel = 2,
            Hysteresis = (int)_hyst.Value,
            Points = pts.OrderBy(p => p.Cpu).ToList()
        };
    }

    private void LoadDefault()
    {
        _cfg = FanCurveConfig.Default4400();
        _maxRpm.Value = 4400;
        _hyst.Value = 3;
        FillGrid(_cfg);
        _status.Text = "Varsayılan 4400 RPM eğrisi yüklendi (henüz EC'ye yazılmadı).";
        _status.ForeColor = Color.DimGray;
    }

    private void SaveCurve()
    {
        try
        {
            _cfg = ReadFromUi();
            _cfg.Save();
            _status.Text = "Kaydedildi: " + FanCurveConfig.ConfigPath;
            _status.ForeColor = Color.ForestGreen;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Kayıt hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ApplyCurve(bool silent = false)
    {
        try
        {
            if (!_ecOk)
            {
                _ecOk = EcAccess.Init();
                if (!_ecOk)
                {
                    if (!silent)
                        MessageBox.Show(this, EcAccess.LastError ?? "EC kapalı", "InpOut", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            _cfg = ReadFromUi();
            // Enforce max
            _cfg.Points = _cfg.Points.Select(p => p with { Rpm = Math.Min(p.Rpm, _cfg.MaxRpm) }).ToList();
            GamezoneWmi.SetPerformanceMode();
            EcAccess.ApplyCurve(_cfg);
            _cfg.Save();

            int peek = EcAccess.PeekFirstRpmHundreds();
            string msg = $"Eğri EC'ye yazıldı. Max={_cfg.MaxRpm} RPM. EC ilk RPM baytı={peek} (x100≈{peek * 100}).";
            _status.Text = msg;
            _status.ForeColor = Color.ForestGreen;
            if (!silent)
                _tray.ShowBalloonTip(2000, "Legion Fan Curve", msg, ToolTipIcon.Info);
            RefreshLive();
        }
        catch (Exception ex)
        {
            _status.Text = "Uygulama hatası: " + ex.Message;
            _status.ForeColor = Color.Firebrick;
            if (!silent)
                MessageBox.Show(this, ex.Message, "EC yazma hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshLive()
    {
        try
        {
            var (f1, f2, mode, cool) = GamezoneWmi.ReadFans();
            // Prefer EC sensors (real package-ish values on Legion Gen5); fall back to WMI/nvidia/ACPI
            int? cpuEc = EcAccess.ReadCpuTempC();
            int? gpuEc = EcAccess.ReadGpuTempC();
            var (cpuWmi, gpuWmi) = GamezoneWmi.ReadWmiTemps();
            int? gpuNv = GamezoneWmi.ReadNvidiaGpuTemp();
            int? cpuAcpi = GamezoneWmi.ReadAcpiThermalZoneC();

            int? cpu = cpuEc ?? cpuWmi ?? cpuAcpi;
            int? gpu = gpuNv ?? gpuEc ?? gpuWmi;

            string cpuSrc = cpuEc.HasValue ? "EC" : cpuWmi.HasValue ? "WMI" : cpuAcpi.HasValue ? "ACPI" : "-";
            string gpuSrc = gpuNv.HasValue ? "NV" : gpuEc.HasValue ? "EC" : gpuWmi.HasValue ? "WMI" : "-";

            string modeName = mode switch { 1 => "Quiet", 2 => "Balance", 3 => "Perf", _ => mode.ToString() };
            string cpuText = cpu.HasValue ? $"{cpu}°C({cpuSrc})" : "n/a";
            string gpuText = gpu.HasValue ? $"{gpu}°C({gpuSrc})" : "n/a";

            _live.Text =
                $"CPU={cpuText}  GPU={gpuText}  Fan1={f1}  Fan2={f2}  " +
                $"Mode={modeName}  Cool={cool}  EC={(_ecOk ? "OK" : "OFF")}  Max={_maxRpm.Value}";
            _tray.Text = $"CPU={cpu?.ToString() ?? "?"} GPU={gpu?.ToString() ?? "?"} {f1}/{f2}";
        }
        catch (Exception ex)
        {
            _live.Text = "Canlı okuma hatası: " + ex.Message;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _tray.Dispose();
        }
        base.Dispose(disposing);
    }
}
