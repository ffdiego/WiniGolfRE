using System.Numerics;
using Raylib_cs;
using Winigolf.Physics;
using static Raylib_cs.Raylib;

namespace Winigolf.Game;

internal sealed partial class GameApp : IDisposable
{
    public LocalMatch Match { get; private set; }
    public GameSession Session => Match.Active.Session;
    private bool setup;
    private readonly List<string> playerNames = ["Jogador 1", "Jogador 2"];
    private int editingName;
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

    public GameApp(Course[] courses, bool hidden, CameraOptions? cameraOptions = null)
    {
        Camera = new SceneCamera { Yaw = -.35f, Options = cameraOptions ?? new CameraOptions(),
            RightOverlayWidth = 310, TopOverlayHeight = 128, BottomOverlayHeight = 90 };
        SetTraceLogLevel(TraceLogLevel.Warning);
        SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint |
            (hidden ? ConfigFlags.HiddenWindow : 0));
        InitWindow(1280, 820, "WiniGolf · primeira versão jogável");
        if (!IsWindowReady()) throw new InvalidOperationException("Não foi possível inicializar o contexto gráfico raylib.");
        // Coordinates retain the original game's units; some fields exceed the default far plane.
        Rlgl.SetClipPlanes(1, 20000);
        SetWindowMinSize(1024, 720); SetExitKey(KeyboardKey.Null); SetTargetFPS(hidden ? 0 : 120);
        Match = new LocalMatch(courses, ["Jogador 1"]);
        setup = !hidden;
        Audio = new GameAudio(silent: hidden);
        var fontFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeuib.ttf");
        if (File.Exists(fontFile))
        {
            var glyphs = Enumerable.Range(32, 95).Concat(Enumerable.Range(160, 96)).ToArray();
            font = LoadFontEx(fontFile, 64, glyphs, glyphs.Length);
            ownsFont = true; SetTextureFilter(font.Texture, TextureFilter.Bilinear);
        }
        else font = GetFontDefault();
        Scene.Build(Session.Course); Resize();
        Camera.Focus = Camera.Options.CenterOnBallAtTurnStart ? SceneCamera.World(Session.VisualPosition) + System.Numerics.Vector3.UnitY * 4 : null;
    }

    public void Select(int index)
    {
        CancelAim(); Match.Select(index); Scene.Build(Session.Course); Camera.Zoom = 1; picker = false;
        turns.Reset(Camera);
        Camera.Focus = Camera.Options.CenterOnBallAtTurnStart ? SceneCamera.World(Session.VisualPosition) + System.Numerics.Vector3.UnitY * 4 : null;
        Audio.Update(Session);
        Camera.Fit(Session.Course, (int)viewport.Width, (int)viewport.Height);
    }

    private void Resize()
    {
        var width = Math.Max(1, GetScreenWidth());
        var height = Math.Max(1, GetScreenHeight());
        if (canvas.Id != 0 && canvas.Texture.Width == width * 2 && canvas.Texture.Height == height * 2) return;
        CancelAim();
        if (canvas.Id != 0) UnloadRenderTexture(canvas);
        // Supersampling also smooths polygon edges inside the offscreen viewport.
        canvas = LoadRenderTexture(width * 2, height * 2);
        if (canvas.Id == 0) throw new InvalidOperationException("Falha ao criar a superfície de renderização.");
        SetTextureFilter(canvas.Texture, TextureFilter.Bilinear);
        viewport = new Rectangle(0, 0, width, height);
    }

    private Vector2 LocalMouse => GetMousePosition() - new Vector2(viewport.X, viewport.Y);

    public bool StartAim(Vector2 point, MouseButton button)
    {
        if (setup || Match.WaitingToPass || !Session.CanShoot || Camera.IsTurning || aiming || button is not (MouseButton.Left or MouseButton.Right)) return false;
        var projected = Camera.ProjectBallCenter(Session.VisualPosition);
        if (Vector2.DistanceSquared(point, projected) > 22 * 22) return false;
        aimButton = button; aimMode = button == MouseButton.Left ? ShotMode.Mode1 : ShotMode.Mode2;
        Camera.Zoom = Camera.Zoom; // Freeze scale while dragging the shot.
        anchor = endpoint = projected; aiming = true; return true;
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
        if (setup) { SetupInput(); return; }
        if (!IsWindowFocused()) CancelAim();
        if (IsKeyPressed(KeyboardKey.Escape)) { CancelAim(); picker = false; }
        if (IsKeyPressed(KeyboardKey.N) && Session.Outcome != EndReason.Moving) { OpenPlayerSetup(); return; }
        if (IsKeyPressed(KeyboardKey.M)) Audio.ToggleMute();
        if (IsKeyPressed(KeyboardKey.Tab)) { CancelAim(); picker = !picker; }
        if (picker) return;
        if (IsKeyPressed(KeyboardKey.R)) Select(Session.Index);
        if (IsKeyPressed(KeyboardKey.Space)) { CancelAim(); Session.Paused = !Session.Paused; }
        if (Camera.Options.AllowTopDown && IsKeyPressed(KeyboardKey.V)) { CancelAim(); Camera.CancelTurn(); Camera.TopDown = !Camera.TopDown; }
        if (IsKeyPressed(KeyboardKey.F2))
        {
            CancelAim(); Match.SetPolicy(Session.Policy == CollisionPolicy.SurfaceTraversal
                ? CollisionPolicy.ReferenceOrder : CollisionPolicy.SurfaceTraversal);
        }
        if (IsKeyPressed(KeyboardKey.Enter) && Match.CourseCompleted) Select(Session.Index + 1);
        if (Camera.Options.AllowManualRotation && !Camera.TopDown && !Camera.IsTurning && (IsKeyDown(KeyboardKey.Q) || IsKeyDown(KeyboardKey.E)))
        {
            CancelAim(); Camera.Yaw += (IsKeyDown(KeyboardKey.Q) ? -1 : 1) * (float)dt;
        }
        var over = CheckCollisionPointRec(GetMousePosition(), viewport) && !OverHud(GetMousePosition());
        if (over && !aiming && !Camera.IsTurning)
        {
            var wheel = GetMouseWheelMove();
            Camera.RequestZoom(wheel);
            if (Camera.Options.AllowManualRotation && IsMouseButtonDown(MouseButton.Middle) && !Camera.TopDown) Camera.Yaw += GetMouseDelta().X * .007f;
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
            Input(dt);
            if (!setup)
            {
                var active = Match.ActiveIndex; Match.Advance(dt);
                if (active != Match.ActiveIndex) { CancelAim(); turns.Reset(Camera); }
                AdvancePresentation(dt); Audio.Update(Session);
            }
            Draw();
        }
    }

    public void AdvancePresentation(double elapsed)
    {
        if (!Match.WaitingToPass) turns.Update(Session, Camera, elapsed);
        if (!aiming) Camera.AdvanceZoom(elapsed);
    }

    private void Text(string text, float x, float y, float size, Color color) => DrawTextEx(font, text, new Vector2(x, y), size, .3f, color);
    private void Box(Rectangle rect, Color color, float round = .12f) => DrawRectangleRounded(rect, round, 12, color);

    private bool Button(string label, Rectangle rect, bool active = false, bool enabled = true, bool modal = false)
    {
        var hover = enabled && (!picker || modal) && (!setup || modal) && CheckCollisionPointRec(GetMousePosition(), rect);
        Box(rect, active ? mint : hover ? new Color(49, 76, 85, 255) : new Color(33, 53, 64, 255));
        var size = 20f;
        while (size > 15 && MeasureTextEx(font, label, size, .3f).X > rect.Width - 20) size -= 1;
        var measured = MeasureTextEx(font, label, size, .3f);
        Text(label, rect.X + (rect.Width - measured.X) / 2, rect.Y + (rect.Height - size) / 2 - 1, size,
            !enabled ? muted : active ? background : white);
        return enabled && hover && IsMouseButtonPressed(MouseButton.Left);
    }

    public void Draw(bool acceptUi = true)
    {
        Resize(); Camera.Fit(Session.Course, (int)viewport.Width, (int)viewport.Height);
        BeginTextureMode(canvas); ClearBackground(background); Scene.Draw(Session, Camera, Match.Players); EndTextureMode();
        BeginDrawing(); ClearBackground(background);
        DrawTexturePro(canvas.Texture, new Rectangle(0, 0, canvas.Texture.Width, -canvas.Texture.Height), viewport, Vector2.Zero, 0, Color.White);
        Box(new Rectangle(16, 12, 410, 116), new Color(14, 25, 34, 220));
        Box(new Rectangle(16, GetScreenHeight() - 82, Math.Min(850, GetScreenWidth() - 32), 72), new Color(14, 25, 34, 220));
        Text("wini", 27, 18, 40, white); Text("golf", 112, 18, 40, mint);
        var course = Session.Course;
        Text($"{course.Id:00}  /  {course.Name}", 28, 91, 28, white);
        if (Button("Todas as pistas  [Tab]", new Rectangle(GetScreenWidth() - 294, 24, 270, 42)) && acceptUi) { CancelAim(); picker = true; }
        if (aiming)
        {
            var ball = Camera.ProjectBallCenter(Session.VisualPosition) + new Vector2(viewport.X, viewport.Y);
            var delta = endpoint - anchor;
            var limit = Camera.DragLimit(Session.Ball.Position, aimMode);
            var pull = delta.Length() > limit ? Vector2.Normalize(delta) * limit : delta;
            var color = Match.Active.Color;
            DrawLineEx(ball, ball + pull, aimMode == ShotMode.Mode1 ? 1.5f : 4, color);
            DrawCircleV(ball + pull, 4, color);
            var direction = Camera.Impulse(delta, Session.Ball.Position, aimMode);
            var angle = (int)Math.Round((Math.Atan2(direction.Y, direction.X) * 180 / Math.PI + 180 + 360) % 360,
                MidpointRounding.AwayFromZero) % 360;
            var force = Math.Clamp((int)Math.Round(delta.Length() / limit * 100, MidpointRounding.AwayFromZero), 0, 100);
            Text($"ÂNGULO {angle:000}°   ·   FORÇA {force:00}%", 28, GetScreenHeight() - 101, 20, color);
        }

        var sideX = GetScreenWidth() - 294;
        Box(new Rectangle(sideX, 86, 270, 260 + Match.Players.Count * 36), new Color(24, 41, 51, 225));
        DrawScoreboard(sideX);
        var message = Session.Outcome switch
        {
            EndReason.Captured => "EMBOCOU!",
            EndReason.StrokeLimitReached => "CAMPO CONCLUÍDO · LIMITE DE TACADAS",
            EndReason.Moving => Session.Paused ? "PAUSADO" : "BOLA EM JOGO",
            EndReason.GeometryResolutionFailed => "A SIMULAÇÃO NÃO RESOLVEU A GEOMETRIA. REINICIE O CAMPO.",
            EndReason.SimulationLimitReached => "LIMITE DA SIMULAÇÃO ATINGIDO. REINICIE O CAMPO.",
            _ when Session.Completed => "CAMPO CONCLUÍDO · LIMITE DE TACADAS",
            _ => Session.CanShoot ? Camera.IsTurning ? "PREPARANDO A TACADA…" : "ESCOLHA A DIREÇÃO." : "REINICIE O CAMPO PARA CONTINUAR."
        };
        if (Match.WaitingToPass) message = "PASSANDO A VEZ…";
        if (Match.CourseCompleted) message = "PISTA CONCLUÍDA!   ENTER: PRÓXIMA PISTA";
        Text(message, 28, GetScreenHeight() - 66, 21, Session.Completed ? mint : white);
        Text("Puxe no sentido oposto à tacada  ·  Esc cancela  ·  Espaço pausa", 28, GetScreenHeight() - 32, 18, muted);
        if (picker) DrawPicker(acceptUi);
        if (setup) DrawSetup(acceptUi);
        EndDrawing();
    }

    private void DrawPicker(bool acceptUi)
    {
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new Color(5, 12, 19, 235));
        var left = (GetScreenWidth() - 900) / 2f; var top = (GetScreenHeight() - 610) / 2f;
        Box(new Rectangle(left, top, 900, 610), panel, .04f);
        Text("Escolha sua próxima pista", left + 25, top + 23, 30, white);
        Text("Resultados desta sessão aparecem ao lado do nome. Tab / Esc para voltar.", left + 25, top + 65, 18, muted);
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
