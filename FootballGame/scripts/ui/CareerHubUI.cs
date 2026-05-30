using Godot;
using System.Linq;

namespace FootballGame;

/// <summary>
/// Hub principal da carreira: mostra classificação, próximo jogo
/// e permite disputar a partida ou voltar ao menu.
/// </summary>
public partial class CareerHubUI : Control
{
    [Export] private Label          _lblTeam;
    [Export] private Label          _lblSeason;
    [Export] private Label          _lblBalance;
    [Export] private Label          _lblNextMatch;
    [Export] private Button         _btnPlayNext;
    [Export] private Button         _btnMainMenu;
    [Export] private VBoxContainer  _standingsContainer;

    private CareerManager    _cm;
    private GameManager      _gm;
    private GameStateManager _gsm;

    public override void _Ready()
    {
        _cm  = GetNodeOrNull<CareerManager>("/root/CareerManager");
        _gm  = GetNodeOrNull<GameManager>("/root/GameManager");
        _gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");

        if (_btnPlayNext != null) _btnPlayNext.Pressed += OnPlayNextPressed;
        if (_btnMainMenu != null) _btnMainMenu.Pressed += OnMainMenuPressed;

        if (_cm == null || !_cm.HasCareer)
        {
            GD.PrintErr("CareerHubUI: nenhuma carreira ativa — voltando ao menu.");
            CallDeferred(nameof(FallbackToMenu));
            return;
        }

        Refresh();
    }

    private void FallbackToMenu()
        => _gsm?.GoTo(GameStateManager.GameState.MainMenu);

    private void Refresh()
    {
        var career = _cm.Current;

        if (_lblTeam    != null) _lblTeam.Text    = $"Time: {GetName(career.PlayerTeamId)}";
        if (_lblSeason  != null) _lblSeason.Text  = $"Temporada {career.Season}";
        if (_lblBalance != null) _lblBalance.Text = $"Caixa: R$ {career.Balance:N0}";

        var next = _cm.GetNextUserFixture();
        if (next != null)
        {
            string home = GetName(next.HomeTeamId);
            string away = GetName(next.AwayTeamId);
            if (_lblNextMatch != null) _lblNextMatch.Text = $"Jornada {next.Matchday}:  {home}  ×  {away}";
            if (_btnPlayNext  != null) _btnPlayNext.Disabled = false;
        }
        else
        {
            if (_lblNextMatch != null) _lblNextMatch.Text = "Temporada encerrada!";
            if (_btnPlayNext  != null) _btnPlayNext.Disabled = true;
        }

        BuildStandingsTable(career);
    }

    private void BuildStandingsTable(CareerManager.CareerData career)
    {
        if (_standingsContainer == null) return;

        foreach (Node child in _standingsContainer.GetChildren())
            child.QueueFree();

        _standingsContainer.AddChild(Row("#   Time            Pts  PJ  V  E  D  GP  GC  SG", header: true));

        var sorted = career.Standings
            .OrderByDescending(r => r.Points)
            .ThenByDescending(r => r.GoalDiff)
            .ThenByDescending(r => r.GoalsFor)
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            var r    = sorted[i];
            bool own = r.TeamId == career.PlayerTeamId;
            string mark = own ? "►" : " ";
            string name = PadR(GetName(r.TeamId), 14);
            string line = $"{mark}{i+1,2}. {name} {r.Points,3}  {r.Played,2}  {r.Won,2} {r.Drawn,2} {r.Lost,2}  {r.GoalsFor,2}  {r.GoalsAgainst,2} {r.GoalDiff,+3}";
            var lbl = Row(line);
            if (own) lbl.AddThemeColorOverride("font_color", new Color(0.4f, 0.9f, 0.4f));
            _standingsContainer.AddChild(lbl);
        }
    }

    private static Label Row(string text, bool header = false)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", header ? 14 : 13);
        return lbl;
    }

    private static string PadR(string s, int len)
    {
        if (s.Length >= len) return s[..len];
        return s + new string(' ', len - s.Length);
    }

    private void OnPlayNextPressed()
    {
        if (_cm == null || _gm == null || _gsm == null) return;

        _cm.SimulateUpToNextUserMatch();

        var next = _cm.GetNextUserFixture();
        if (next == null) { Refresh(); return; }

        var homeTeam = _cm.GetTeamData(next.HomeTeamId);
        var awayTeam = _cm.GetTeamData(next.AwayTeamId);

        // Ghostteams não têm TeamData real: cria um placeholder temporário
        homeTeam ??= MakeGhostTeam(next.HomeTeamId);
        awayTeam ??= MakeGhostTeam(next.AwayTeamId);

        _gm.HomeTeam    = homeTeam;
        _gm.AwayTeam    = awayTeam;
        _gm.CurrentMode = GameManager.MatchMode.Career;
        _gm.HomeLineup  = GD.Load<Lineup>("res://resources/lineups/lineup_433.tres");
        _gm.AwayLineup  = _gm.HomeLineup;

        _cm.IsAwaitingResult = true;
        _gsm.GoTo(GameStateManager.GameState.Match);
    }

    private void OnMainMenuPressed()
        => _gsm?.GoTo(GameStateManager.GameState.MainMenu);

    private string GetName(string teamId)
    {
        if (teamId.StartsWith("ghost_"))
            return $"Time {(char)('C' + int.Parse(teamId.Replace("ghost_", "")))}";
        var td = _cm?.GetTeamData(teamId);
        return td?.ShortName is { Length: > 0 } s ? s : teamId;
    }

    private static TeamData MakeGhostTeam(string teamId)
        => new() { TeamId = teamId, ShortName = teamId, OverallRating = 65 };
}
