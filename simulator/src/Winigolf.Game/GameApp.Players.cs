using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Winigolf.Game;

internal sealed partial class GameApp
{
    private bool replaceName = true;

    public void ConfigurePlayers(IEnumerable<string> names)
    {
        var courses = Session.Courses; var index = Session.Index; var policy = Session.Policy;
        Match = new LocalMatch(courses, names);
        Match.SetPolicy(policy); setup = false; Select(index);
        if (!Camera.Options.AllowTopDown) Camera.TopDown = false;
    }

    public void OpenPlayerSetup()
    {
        CancelAim(); picker = false; setup = true;
        playerNames.Clear(); playerNames.AddRange(Match.Players.Select(p => p.Name));
        editingName = 0; replaceName = true;
    }

    private void SetupInput()
    {
        if (IsKeyPressed(KeyboardKey.Tab)) { editingName = (editingName + 1) % playerNames.Count; replaceName = true; }
        if (IsKeyPressed(KeyboardKey.Backspace))
        {
            playerNames[editingName] = replaceName ? "" : playerNames[editingName][..Math.Max(0, playerNames[editingName].Length - 1)];
            replaceName = false;
        }
        for (var code = GetCharPressed(); code != 0; code = GetCharPressed())
        {
            if (code is < 32 or > 255) continue;
            if (replaceName) { playerNames[editingName] = ""; replaceName = false; }
            if (playerNames[editingName].Length < 18) playerNames[editingName] += (char)code;
        }
        if (IsKeyPressed(KeyboardKey.Enter) && playerNames.All(n => !string.IsNullOrWhiteSpace(n))) ConfigurePlayers(playerNames);
    }

    private void DrawSetup(bool acceptUi)
    {
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new Color(5, 12, 19, 245));
        var left = (GetScreenWidth() - 700) / 2f; var top = (GetScreenHeight() - 620) / 2f;
        Box(new Rectangle(left, top, 700, 620), panel, .05f);
        Text("Quem vai jogar?", left + 32, top + 28, 36, white);
        Text("Cada cor é uma bola. Uma tacada por vez.", left + 32, top + 80, 21, muted);
        for (var i = 0; i < playerNames.Count; i++)
        {
            var row = new Rectangle(left + 32, top + 124 + i * 54, 636, 46);
            Box(row, i == editingName ? new Color(48, 73, 83, 255) : background);
            DrawCircleV(new Vector2(row.X + 24, row.Y + 23), 10, LocalMatch.Palette[i]);
            Text(playerNames[i] + (i == editingName && GetTime() % 1 < .5 ? "|" : ""), row.X + 48, row.Y + 10, 24, white);
            if (acceptUi && CheckCollisionPointRec(GetMousePosition(), row) && IsMouseButtonPressed(MouseButton.Left))
            { editingName = i; replaceName = true; }
        }
        if (Button("+ Jogador", new Rectangle(left + 32, top + 466, 210, 42), enabled: playerNames.Count < 6, modal: true) && acceptUi)
        { playerNames.Add($"Jogador {playerNames.Count + 1}"); editingName = playerNames.Count - 1; replaceName = true; }
        if (Button("- Jogador", new Rectangle(left + 254, top + 466, 210, 42), enabled: playerNames.Count > 1, modal: true) && acceptUi)
        { playerNames.RemoveAt(editingName); editingName = Math.Min(editingName, playerNames.Count - 1); replaceName = true; }
        Text("Clique no nome para editar · até 6 jogadores", left + 32, top + 522, 19, muted);
        if (Button("Começar partida  [Enter]", new Rectangle(left + 32, top + 558, 636, 42), true,
            playerNames.All(n => !string.IsNullOrWhiteSpace(n)), modal: true) && acceptUi) ConfigurePlayers(playerNames);
    }

    private string ShortName(string value, float width, float size)
    {
        if (MeasureTextEx(font, value, size, .3f).X <= width) return value;
        while (value.Length > 1 && MeasureTextEx(font, value + "…", size, .3f).X > width) value = value[..^1];
        return value + "…";
    }

    internal bool OverHud(Vector2 point) =>
        CheckCollisionPointRec(point, new Rectangle(16, 12, 410, 116)) ||
        CheckCollisionPointRec(point, new Rectangle(GetScreenWidth() - 294, 24, 270, 42)) ||
        CheckCollisionPointRec(point, new Rectangle(GetScreenWidth() - 294, 86, 270, 260 + Match.Players.Count * 36)) ||
        point.Y >= GetScreenHeight() - 90;

    private void DrawScoreboard(float x)
    {
        Text("VEZ DE", x + 24, 105, 18, muted);
        Text(ShortName(Match.Active.Name, 222, 26), x + 24, 134, 26, Match.Active.Color);
        Text($"{Session.Strokes:00}", x + 20, 174, 50, white);
        Text($"PAR {Session.Course.Par}", x + 134, 197, 22, mint);
        Text("PLACAR", x + 24, 242, 18, muted);
        Text("PISTA / TOTAL", x + 128, 268, 15, muted);
        for (var i = 0; i < Match.Players.Count; i++)
        {
            var player = Match.Players[i]; var y = 298 + i * 36;
            if (i == Match.ActiveIndex) Box(new Rectangle(x + 14, y - 4, 242, 32), new Color(40, 61, 70, 230));
            DrawCircleV(new Vector2(x + 27, y + 10), 6, player.Color);
            Text(ShortName(player.Name, 112, 18), x + 42, y, 18, white);
            Text($"{player.Session.Strokes} / {player.Total}", x + 174, y, 18, player.Color);
        }
        Text("N: nova partida", x + 24, 318 + Match.Players.Count * 36, 17, muted);
    }
}
