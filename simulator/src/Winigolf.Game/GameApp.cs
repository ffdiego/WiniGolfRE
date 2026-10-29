using System.Numerics;
using Raylib_cs;
using Winigolf.Physics;
using static Raylib_cs.Raylib;

namespace Winigolf.Game;

internal sealed class GameApp : IDisposable
{
    public GameSession Session { get; }
    public SceneCamera Camera { get; } = new() { Yaw = -.35f };
    public CourseScene Scene { get; } = new();
    public GameAudio Audio { get; }
    private readonly TurnPresentation turns = new(Random.Shared);
    public double PrecisionGain { get; private set; } = 1;
    private RenderTexture2D canvas;
    private Font font;
    private bool ownsFont;
    private bool picker;
    private bool aiming;
    private Vector2 anchor, endpoint;
    private MouseButton aimButton;
    private ShotMode aimMode;
    private Rectangle viewport;
    private bool disposed;
    private readonly Color background = new(14, 25, 34, 255);
    private readonly Color panel = new(24, 41, 51, 255);
    private readonly Color muted = new(146, 173, 181, 255);
    private readonly Color mint = new(142, 229, 190, 255);
    private readonly Color white = new(238, 244, 237, 255);

    public GameApp(Course[] courses, bool hidden)
    {
        SetTraceLogLevel(TraceLogLevel.Warning);
        SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint |
            (hidden ? ConfigFlags.HiddenWindow : 0));
        InitWindow(1280, 820, "WiniGolf · primeira versão jogável");
        if (!IsWindowReady()) throw new InvalidOperationException("Não foi possível inicializar o contexto gráfico raylib.");
        // Coordinates retain the original game's units; some fields exceed the default far plane.
        Rlgl.SetClipPlanes(1, 20000);
        SetWindowMinSize(1024, 720); SetExitKey(KeyboardKey.Null); SetTargetFPS(hidden ? 0 : 120);
        Session = new GameSession(courses);
        Audio = new GameAudio(silent: hidden);
        var fontFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
        if (File.Exists(fontFile))
        {
            var glyphs = Enumerable.Range(32, 95).Concat(Enumerable.Range(160, 96)).ToArray();
            font = LoadFontEx(fontFile, 32, glyphs, glyphs.Length);
            ownsFont = true; SetTextureFilter(font.Texture, TextureFilter.Bilinear);
        }
        else font = GetFontDefault();
        Scene.Build(Session.Course); Resize();
    }

    public void Select(int index)
    {
        CancelAim(); Session.Select(index); Scene.Build(Session.Course); Camera.Zoom = 1; picker = false;
        turns.Reset(Camera);
        Audio.Update(Session);
        Camera.Fit(Session.Course, (int)viewport.Width, (int)viewport.Height);
    }

    private void Resize()
    {
        var width = Math.Max(400, GetScreenWidth() - 344);
        var height = Math.Max(300, GetScreenHeight() - 200);
        if (canvas.Id != 0 && canvas.Texture.Width == width * 2 && canvas.Texture.Height == height * 2) return;
        CancelAim();
        if (canvas.Id != 0) UnloadRenderTexture(canvas);
        // Supersampling also smooths polygon edges inside the offscreen viewport.
        canvas = LoadRenderTexture(width * 2, height * 2);
        if (canvas.Id == 0) throw new InvalidOperationException("Falha ao criar a superfície de renderização.");
        SetTextureFilter(canvas.Texture, TextureFilter.Bilinear);
        viewport = new Rectangle(24, 138, width, height);
    }

    private Vector2 LocalMouse => GetMousePosition() - new Vector2(viewport.X, viewport.Y);

    public bool StartAim(Vector2 point, MouseButton button)
    {
        if (!Session.CanShoot || Camera.IsTurning || aiming || button is not (MouseButton.Left or MouseButton.Right)) return false;
        var projected = Camera.Project(Session.VisualPosition);
        if (Vector2.DistanceSquared(point, projected) > 22 * 22) return false;
        aimButton = button; aimMode = button == MouseButton.Left ? ShotMode.Mode1 : ShotMode.Mode2;
        anchor = endpoint = point; aiming = true; return true;
    }

    public bool FinishAim(Vector2 point, MouseButton button)
    {
        if (!aiming || button != aimButton) return false;
        endpoint = point; aiming = false;
        var impulse = Camera.Impulse(endpoint - anchor, Session.Ball.Position, aimMode);
        if (aimMode == ShotMode.Mode1)
            impulse = impulse with { X = impulse.X * PrecisionGain, Y = impulse.Y * PrecisionGain };
        if (impulse.X == 0 && impulse.Y == 0) return false;
        Session.Shoot(impulse); Audio.Update(Session); return true;
    }

    public void CancelAim() => aiming = false;

    public void SetPrecisionGain(int gain)
    {
        if (gain is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(gain));
        CancelAim(); PrecisionGain = gain;
    }

    private void Input(double dt)
    {
        if (IsWindowResized()) Resize();
        if (!IsWindowFocused()) CancelAim();
        if (IsKeyPressed(KeyboardKey.Escape)) { CancelAim(); picker = false; }
        if (IsKeyPressed(KeyboardKey.M)) Audio.ToggleMute();
        if (IsKeyPressed(KeyboardKey.Tab)) { CancelAim(); picker = !picker; }
        if (picker) return;
        if (IsKeyPressed(KeyboardKey.R)) Select(Session.Index);
        if (IsKeyPressed(KeyboardKey.Space)) { CancelAim(); Session.Paused = !Session.Paused; }
        if (IsKeyPressed(KeyboardKey.V)) { CancelAim(); Camera.CancelTurn(); Camera.TopDown = !Camera.TopDown; }
        if (IsKeyPressed(KeyboardKey.F2))
        {
            CancelAim(); Session.SetPolicy(Session.Policy == CollisionPolicy.SurfaceTraversal
                ? CollisionPolicy.ReferenceOrder : CollisionPolicy.SurfaceTraversal);
        }
        if (IsKeyPressed(KeyboardKey.Enter) && Session.Completed) Select(Session.Index + 1);
        if (!Camera.TopDown && !Camera.IsTurning && (IsKeyDown(KeyboardKey.Q) || IsKeyDown(KeyboardKey.E)))
        {
            CancelAim(); Camera.Yaw += (IsKeyDown(KeyboardKey.Q) ? -1 : 1) * (float)dt;
        }
        var over = CheckCollisionPointRec(GetMousePosition(), viewport);
        if (over && !aiming && !Camera.IsTurning)
        {
            var wheel = GetMouseWheelMove();
            Camera.Zoom = Math.Clamp(Camera.Zoom + wheel * .1f, .65f, 1.8f);
            if (IsMouseButtonDown(MouseButton.Middle) && !Camera.TopDown) Camera.Yaw += GetMouseDelta().X * .007f;
        }
        Camera.Fit(Session.Course, (int)viewport.Width, (int)viewport.Height);
        if (over && IsMouseButtonPressed(MouseButton.Left)) StartAim(LocalMouse, MouseButton.Left);
        if (over && IsMouseButtonPressed(MouseButton.Right)) StartAim(LocalMouse, MouseButton.Right);
        if (aiming)
        {
            endpoint = LocalMouse;
            if (IsMouseButtonReleased(aimButton)) FinishAim(endpoint, aimButton);
        }
    }

    public void Run()
    {
        AdvancePresentation(0);
        while (!WindowShouldClose())
        {
            var dt = (double)GetFrameTime();
            Input(dt); Session.Advance(dt); AdvancePresentation(dt); Audio.Update(Session); Draw();
        }
    }

    public void AdvancePresentation(double elapsed) => turns.Update(Session, Camera, elapsed);

    private void Text(string text, float x, float y, float size, Color color) => DrawTextEx(font, text, new Vector2(x, y), size, .3f, color);
    private void Box(Rectangle rect, Color color, float round = .12f) => DrawRectangleRounded(rect, round, 12, color);

    private bool Button(string label, Rectangle rect, bool active = false, bool enabled = true, bool modal = false)
    {
        var hover = enabled && (!picker || modal) && CheckCollisionPointRec(GetMousePosition(), rect);
        Box(rect, active ? mint : hover ? new Color(49, 76, 85, 255) : new Color(33, 53, 64, 255));
        var size = 16f;
        var measured = MeasureTextEx(font, label, size, .3f);
        Text(label, rect.X + (rect.Width - measured.X) / 2, rect.Y + (rect.Height - size) / 2 - 1, size,
            !enabled ? muted : active ? background : white);
        return enabled && hover && IsMouseButtonPressed(MouseButton.Left);
    }

    public void Draw(bool acceptUi = true)
    {
        Resize(); Camera.Fit(Session.Course, (int)viewport.Width, (int)viewport.Height);
        BeginTextureMode(canvas); ClearBackground(background); Scene.Draw(Session, Camera); EndTextureMode();
        BeginDrawing(); ClearBackground(background);
        DrawTexturePro(canvas.Texture, new Rectangle(0, 0, canvas.Texture.Width, -canvas.Texture.Height), viewport, Vector2.Zero, 0, Color.White);
        Text("wini", 27, 19, 34, white); Text("golf", 86, 19, 34, mint);
        Text("UM CLÁSSICO, UM NOVO COMEÇO", 28, 60, 11, muted);
        Text("PRIMEIRA VERSÃO JOGÁVEL", GetScreenWidth() - 254, 28, 13, mint);
        if (Button(!Audio.Ready ? "Áudio indisponível" : Audio.Muted ? "Som desligado  [M]" : "Som ligado  [M]",
            new Rectangle(GetScreenWidth() - 490, 22, 210, 32), enabled: Audio.Ready) && acceptUi) Audio.ToggleMute();
        var course = Session.Course;
        Text($"{course.Id:00}  /  {course.Name}", 28, 96, 23, white);
        Text($"PAR {course.Par}", 275, 101, 14, muted);
        for (var i = 0; i < 3; i++)
        {
            var index = i switch { 0 => 0, 1 => 2, _ => 24 };
            if (Button(Session.Courses[index].Name, new Rectangle(355 + i * 111, 93, 102, 32), Session.Index == index) && acceptUi) Select(index);
        }
        if (Button("36 pistas  [Tab]", new Rectangle(GetScreenWidth() - 294, 93, 270, 32)) && acceptUi) { CancelAim(); picker = true; }

        if (aiming)
        {
            var ball = Camera.Project(Session.VisualPosition) + new Vector2(viewport.X, viewport.Y);
            var delta = endpoint - anchor;
            var limit = Camera.DragLimit(Session.Ball.Position, aimMode);
            var pull = delta.Length() > limit ? Vector2.Normalize(delta) * limit : delta;
            var color = new Color(250, 195, 113, 255);
            DrawLineEx(ball, ball + pull, aimMode == ShotMode.Mode1 ? 1.5f : 4, color);
            DrawCircleV(ball + pull, 4, color);
            Text($"{(aimMode == ShotMode.Mode1 ? "PRECISÃO" : "FORTE")}   ·   PUXADA {Math.Min(100, delta.Length() / limit * 100):0}%", 28, GetScreenHeight() - 101, 14, color);
        }

        var sideX = GetScreenWidth() - 294;
        Box(new Rectangle(sideX, 138, 270, GetScreenHeight() - 200), panel);
        Text("SUA TACADA", sideX + 20, 158, 12, muted);
        Text($"{Session.Strokes:00}", sideX + 18, 181, 58, white);
        Text($"PAR {course.Par}", sideX + 110, 212, 17, mint);
        Text("CLIQUE NA BOLA E PUXE", sideX + 20, 265, 12, muted);
        Text("Esquerdo   /   precisão", sideX + 20, 290, 17, white);
        Text("Direito        /   forte", sideX + 20, 320, 17, white);
        DrawLine((int)sideX + 20, 360, (int)sideX + 250, 360, new Color(52, 74, 82, 255));
        Text("CÂMERA", sideX + 20, 382, 12, muted);
        if (Button("Inclinada", new Rectangle(sideX + 20, 408, 112, 36), !Camera.TopDown) && acceptUi) { CancelAim(); Camera.CancelTurn(); Camera.TopDown = false; }
        if (Button("Planta", new Rectangle(sideX + 141, 408, 109, 36), Camera.TopDown) && acceptUi) { CancelAim(); Camera.CancelTurn(); Camera.TopDown = true; }
        Text("Q / E: girar  ·  roda: zoom", sideX + 20, 463, 14, muted);
        Text("V: trocar vista  ·  projeção ortográfica", sideX + 20, 487, 13, muted);
        Text("FORÇA DO ESQUERDO", sideX + 20, 529, 12, muted);
        for (var gain = 1; gain <= 3; gain++)
            if (Button($"{gain}×", new Rectangle(sideX + 20 + (gain - 1) * 80, 554, 70, 32), PrecisionGain == gain) && acceptUi)
                SetPrecisionGain(gain);
        if (Button("Recomeçar campo  [R]", new Rectangle(sideX + 20, 606, 230, 34)) && acceptUi) Select(Session.Index);
        if (GetScreenHeight() >= 780)
            Text(Session.Policy == CollisionPolicy.SurfaceTraversal ? "Colisões modernas" : "Colisões de referência", sideX + 20, GetScreenHeight() - 105, 12, muted);

        var message = Session.Outcome switch
        {
            EndReason.Captured => "EMBOCOU!",
            EndReason.StrokeLimitReached => "CAMPO CONCLUÍDO · LIMITE DE TACADAS",
            EndReason.Moving => Session.Paused ? "PAUSADO" : "BOLA EM JOGO",
            EndReason.GeometryResolutionFailed => "A SIMULAÇÃO NÃO RESOLVEU A GEOMETRIA. REINICIE O CAMPO.",
            EndReason.SimulationLimitReached => "LIMITE DA SIMULAÇÃO ATINGIDO. REINICIE O CAMPO.",
            _ when Session.Completed => "CAMPO CONCLUÍDO · LIMITE DE TACADAS",
            _ => Session.CanShoot ? Camera.IsTurning ? "PREPARANDO A PRÓXIMA TACADA…" : "A BOLA ESTÁ PRONTA. ESCOLHA A DIREÇÃO." : "REINICIE O CAMPO PARA CONTINUAR."
        };
        Text(message, 28, GetScreenHeight() - 61, 15, Session.Completed ? mint : muted);
        if (Session.Completed && Button("Próximo campo  [Enter]", new Rectangle(viewport.Width - 220, GetScreenHeight() - 73, 240, 38), true) && acceptUi)
            Select(Session.Index + 1);
        Text("Puxe no sentido oposto à tacada   ·   Esc cancela a mira   ·   Espaço pausa", 28, GetScreenHeight() - 29, 12, muted);
        if (picker) DrawPicker(acceptUi);
        EndDrawing();
    }

    private void DrawPicker(bool acceptUi)
    {
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new Color(5, 12, 19, 235));
        var left = (GetScreenWidth() - 900) / 2f; var top = (GetScreenHeight() - 610) / 2f;
        Box(new Rectangle(left, top, 900, 610), panel, .04f);
        Text("Escolha sua próxima pista", left + 25, top + 23, 25, white);
        Text("Resultados desta sessão aparecem ao lado do nome. Tab / Esc para voltar.", left + 25, top + 62, 14, muted);
        for (var i = 0; i < Session.Courses.Length; i++)
        {
            var course = Session.Courses[i];
            var label = $"{course.Id:00}  {course.Name}";
            if (Session.Scores.TryGetValue(course.Id, out var score)) label += $"   / {score}";
            var rect = new Rectangle(left + 25 + i % 4 * 215, top + 105 + i / 4 * 52, 205, 40);
            if (Button(label, rect, Session.Index == i, modal: true) && acceptUi) Select(i);
        }
    }

    public void Capture(string file)
    {
        var image = LoadImageFromScreen();
        try { if (!ExportImage(image, Path.GetFullPath(file))) throw new IOException("Falha ao salvar a captura."); }
        finally { UnloadImage(image); }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (canvas.Id != 0) UnloadRenderTexture(canvas);
        if (ownsFont) UnloadFont(font);
        Audio.Dispose();
        CloseWindow();
    }
}
