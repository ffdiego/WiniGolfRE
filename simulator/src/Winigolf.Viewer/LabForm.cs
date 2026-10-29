using System.Diagnostics;
using System.Text.Json;
using Winigolf.Physics;

namespace Winigolf.Viewer;

internal sealed class LabForm : Form
{
    public LabSession Session { get; }
    public CourseView View { get; }
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private double lastTime;
    private readonly Label telemetry;
    private readonly Label status;
    private readonly TrackBar timeline = new() { Dock = DockStyle.Fill, Minimum = 0, Maximum = 1, TickStyle = TickStyle.None };
    private readonly Button pause;
    private readonly ComboBox coursePicker;
    private readonly ComboBox policyPicker;
    private readonly CheckBox overlayCheck;
    private double rate = 1;
    private bool updating;

    public LabForm(Course[] courses, bool animate = true)
    {
        Text = "WiniGolf Lab · física e geometria";
        ClientSize = new Size(1280, 900); MinimumSize = new Size(1050, 720);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(23, 39, 51); ForeColor = Color.FromArgb(225, 236, 241);
        Font = new Font("Segoe UI", 10); KeyPreview = true;
        Session = new LabSession(courses[0]);
        View = new CourseView(Session) { Dock = DockStyle.Fill };

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 322));
        Controls.Add(layout); layout.Controls.Add(View, 0, 0);
        var sidebar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
            Padding = new Padding(20, 18, 14, 12), AutoScroll = true
        };
        layout.Controls.Add(sidebar, 1, 0);

        Label TextBlock(string text, int height, float size = 10, bool bold = false)
        {
            var label = new Label { Text = text, Width = 278, Height = height,
                Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), Margin = new Padding(0, 0, 0, 7) };
            sidebar.Controls.Add(label); return label;
        }
        ComboBox Picker(object[] choices)
        {
            var picker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 278,
                ForeColor = Color.FromArgb(24, 40, 48), BackColor = Color.FromArgb(233, 240, 241), Margin = new Padding(0, 0, 0, 9) };
            picker.Items.AddRange(choices); picker.SelectedIndex = 0; sidebar.Controls.Add(picker); return picker;
        }
        Button Button(string text, Action action, int width = 278)
        {
            var button = new Button { Text = text, Width = width, Height = 30, FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(35, 57, 70), ForeColor = ForeColor, Margin = new Padding(0, 0, 0, 5), TabStop = true };
            button.FlatAppearance.BorderColor = Color.FromArgb(61, 86, 99);
            button.Click += (_, _) => { View.CancelAim(); action(); RefreshState(); };
            sidebar.Controls.Add(button); return button;
        }
        CheckBox Check(string text, bool value, Action<bool> change)
        {
            var check = new CheckBox { Text = text, Checked = value, Width = 278, Height = 23, Margin = new Padding(0, 0, 0, 1) };
            check.CheckedChanged += (_, _) => { View.CancelAim(); change(check.Checked); View.Invalidate(); };
            sidebar.Controls.Add(check);
            return check;
        }

        TextBlock("WINIGOLF / LAB", 29, 15, true);
        TextBlock("36 campos · núcleo C# compartilhado", 25, 9);
        coursePicker = Picker(courses.Select(c => (object)$"{c.Id:00}  {c.Name}   ·   par {c.Par}").ToArray());
        coursePicker.SelectedIndexChanged += (_, _) => { View.CancelAim(); Session.Reset(courses[coursePicker.SelectedIndex]); RefreshState(); };
        TextBlock("PERFIL FÍSICO", 20, 9, true);
        policyPicker = Picker(["Referência · ordem original", "Experimental · travessia corrigida"]);
        policyPicker.SelectedIndexChanged += (_, _) =>
        {
            View.CancelAim(); Session.SetPolicy((CollisionPolicy)policyPicker.SelectedIndex); RefreshState();
        };
        TextBlock("Trocar o perfil repete a última tacada\ncom a mesma origem e o mesmo vetor.", 37, 9);
        TextBlock("PUXADA NA BOLA\nEsquerdo: precisão · Direito: forte", 39, 9);

        pause = Button("Pausar / continuar   [Espaço]", Session.TogglePause);
        Button("Avançar 1 passo · 20 ms   [→]", Session.StepOne);
        Button("Repetir a tacada", Session.Replay);
        sidebar.Controls.Add(timeline); timeline.Width = 278; timeline.Height = 32;
        timeline.Dock = DockStyle.None; timeline.Margin = new Padding(0, 0, 0, 3);
        timeline.ValueChanged += (_, _) => { if (!updating) { View.CancelAim(); Session.Seek(timeline.Value); RefreshState(); } };
        var speed = Picker(["Reprodução 1×", "Reprodução 0,25×", "Reprodução 2×"]);
        speed.SelectedIndexChanged += (_, _) => rate = speed.SelectedIndex switch { 1 => .25, 2 => 2, _ => 1 };
        Check("Vista inclinada (−60°)", false, value => View.Tilted = value);
        Check("Superfícies e portais", true, value => View.Labels = value);
        Check("Trajetória completa", false, value => View.FullTrail = value);
        overlayCheck = Check("Sobrepor outro perfil", false, value => View.Overlay = value);
        telemetry = TextBlock("", 110, 9);
        telemetry.Font.Dispose(); telemetry.Font = new Font("Consolas", 10);
        status = TextBlock("", 39, 9, true);
        Button("Caso de depuração do campo", Session.Demo);
        Button("Reiniciar campo   [R]", () => Session.Reset());
        Button("Exportar tacada em JSON…", Export);
        TextBlock("Protótipo de investigação. A fidelidade\nao original ainda exige medição nele.", 40, 8);

        View.Changed += RefreshState;
        timer.Tick += (_, _) =>
        {
            var now = clock.Elapsed.TotalSeconds;
            Session.Advance((now - lastTime) * rate); lastTime = now; RefreshState();
        };
        if (animate) timer.Start();
        RefreshState();
    }

    public void SelectCourse(int index) => coursePicker.SelectedIndex = index;
    public void SelectPolicy(CollisionPolicy policy) => policyPicker.SelectedIndex = (int)policy;
    public void SetOverlay(bool enabled) => overlayCheck.Checked = enabled;

    public void RefreshState()
    {
        var ball = Session.Ball;
        var speed = Math.Sqrt(ball.Velocity.X * ball.Velocity.X + ball.Velocity.Y * ball.Velocity.Y);
        telemetry.Text = $"POS {ball.Position.X,8:0.00} {ball.Position.Y,8:0.00}\nZ   {ball.Position.Z,8:0.00}   S{ball.SurfaceId}\nVEL {ball.Velocity.X,8:0.000} {ball.Velocity.Y,8:0.000}\n|V| {speed,8:0.000}  COL {ball.BounceCount}\nPASSO {Session.Cursor,4}  TEMPO {Session.Cursor * .02,5:0.00}s\nTACADAS {Session.Strokes}";
        status.Text = Session.Outcome switch
        {
            null => "Pronto para a primeira tacada",
            EndReason.Moving => Session.Paused ? "Reprodução pausada" : "Reproduzindo tacada",
            EndReason.Captured => "Embocou! Campo concluído · reinicie ou escolha outro",
            EndReason.Stopped => "Bola parada · próxima tacada liberada",
            EndReason.StrokeLimitReached => "Limite de tacadas · reinicie o campo",
            EndReason.BounceLimitReset => "Limite de colisões · bola reposicionada",
            EndReason.GeometryResolutionFailed => "Diagnóstico: falha na resolução geométrica",
            _ => "Diagnóstico: limite de passos atingido"
        };
        if (Session.AtEnd && !Session.IsInside)
            status.Text = "Fora da superfície · compare os perfis ou reinicie";
        pause.Text = Session.Paused ? "Continuar   [Espaço]" : "Pausar   [Espaço]";
        updating = true;
        timeline.Maximum = Math.Max(1, Session.Shot?.Frames.Count - 1 ?? 1);
        timeline.Value = Session.Cursor;
        timeline.Enabled = Session.Shot is not null;
        updating = false;
        View.Invalidate();
    }

    private void Export()
    {
        if (Session.Shot is not { } shot)
        {
            MessageBox.Show(this, "Faça uma tacada primeiro.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return;
        }
        using var dialog = new SaveFileDialog { Filter = "Tacada JSON|*.json", FileName = $"campo-{shot.CourseId:00}-{shot.PhysicsVersion}.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(shot, options));
        }
        catch (IOException ex) { MessageBox.Show(this, ex.Message, "Falha ao exportar", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Preserve arrow navigation when interacting with the selectors and timeline.
        if (ActiveControl is ComboBox or TrackBar) return base.ProcessCmdKey(ref msg, keyData);
        switch (keyData)
        {
            case Keys.Space: View.CancelAim(); Session.TogglePause(); break;
            case Keys.Right: View.CancelAim(); Session.StepOne(); break;
            case Keys.R: View.CancelAim(); Session.Reset(); break;
            case Keys.Escape: View.CancelAim(); break;
            default: return base.ProcessCmdKey(ref msg, keyData);
        }
        RefreshState(); return true;
    }

    protected override void OnDeactivate(EventArgs e) { View.CancelAim(); base.OnDeactivate(e); }

    protected override void Dispose(bool disposing)
    {
        if (disposing) timer.Dispose();
        base.Dispose(disposing);
    }
}
