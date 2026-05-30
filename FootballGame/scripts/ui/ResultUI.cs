using Godot;

namespace FootballGame;

/// <summary>
/// Tela de resultado final. Mostra placar, tempo jogado, modo e oferece
/// opções de jogar novamente ou voltar ao menu.
/// </summary>
public partial class ResultUI : Control
{
    [Export] private Label  _lblScore;
    [Export] private Label  _lblMode;
    [Export] private Label  _lblTime;
    [Export] private Label  _lblStats;
    [Export] private Button _btnPlayAgain;
    [Export] private Button _btnCareerHub;
    [Export] private Button _btnMainMenu;

    public override void _Ready()
    {
        var gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");
        var cm  = GetNodeOrNull<CareerManager>("/root/CareerManager");
        bool isCareer = cm?.IsAwaitingResult ?? false;

        RefreshResult(gsm);

        // Em modo carreira: aplica resultado nas classificações e oferece
        // botão de continuar para o hub; esconde "Jogar Novamente".
        if (isCareer && gsm != null)
        {
            var r = gsm.CurrentMatchResult;
            cm.ApplyUserMatchResult(r.ScoreHome, r.ScoreAway);
            if (_btnPlayAgain != null) _btnPlayAgain.Visible = false;
            if (_btnCareerHub != null) { _btnCareerHub.Visible = true; _btnCareerHub.Pressed += OnCareerHubPressed; }
        }
        else
        {
            if (_btnCareerHub != null) _btnCareerHub.Visible = false;
            if (_btnPlayAgain != null) _btnPlayAgain.Pressed += OnPlayAgainPressed;
        }

        if (_btnMainMenu != null) _btnMainMenu.Pressed += OnMainMenuPressed;
    }

    private void RefreshResult(GameStateManager gsm)
    {
        if (gsm == null) return;
        var r = gsm.CurrentMatchResult;

        if (_lblScore != null) _lblScore.Text = $"{r.ScoreHome}  –  {r.ScoreAway}";

        if (_lblMode != null)
            _lblMode.Text = r.Mode switch
            {
                GameManager.MatchMode.Friendly => "Amistoso",
                GameManager.MatchMode.League   => "Liga",
                GameManager.MatchMode.Cup      => "Copa",
                GameManager.MatchMode.Career   => "Carreira",
                _                              => ""
            };

        if (_lblTime != null) _lblTime.Text = $"{r.MinutesPlayed}'";

        // Estatísticas da partida (MatchStats reseta a cada partida)
        if (_lblStats != null && r.Stats != null)
        {
            var s = r.Stats;
            _lblStats.Text =
                $"Posse: {s.PossessionPct(0):F0}% – {s.PossessionPct(1):F0}%\n" +
                $"Chutes: {s.Shots(0)} – {s.Shots(1)}   (No gol: {s.ShotsOnTarget(0)} – {s.ShotsOnTarget(1)})\n" +
                $"Passes: {s.PassCompleted(0)}/{s.PassAttempts(0)} – {s.PassCompleted(1)}/{s.PassAttempts(1)}\n" +
                $"Faltas: {s.Fouls(0)} – {s.Fouls(1)}   " +
                $"Amarelos: {s.YellowCards(0)} – {s.YellowCards(1)}";
        }
        else if (_lblStats != null)
        {
            _lblStats.Text = "";
        }
    }

    private void OnPlayAgainPressed()
    {
        var gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");
        gsm?.GoTo(GameStateManager.GameState.Match);
    }

    private void OnCareerHubPressed()
    {
        var gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");
        gsm?.GoTo(GameStateManager.GameState.CareerHub);
    }

    private void OnMainMenuPressed()
    {
        var gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");
        gsm?.GoTo(GameStateManager.GameState.MainMenu);
    }
}
