using Godot;

namespace FootballGame;

/// <summary>
/// Tela de intervalo. Mostra o placar atual e aguarda confirmação para o 2° tempo.
/// </summary>
public partial class HalfTimeUI : Control
{
    [Export] private Label  _lblScore;
    [Export] private Label  _lblInfo;
    [Export] private Button _btnContinue;

    public override void _Ready()
    {
        RefreshScore();

        if (_btnContinue != null)
            _btnContinue.Pressed += OnContinuePressed;
    }

    private void RefreshScore()
    {
        var gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");
        if (gsm == null) return;

        var r = gsm.CurrentMatchResult;
        if (_lblScore != null)
            _lblScore.Text = $"{r.ScoreHome}  –  {r.ScoreAway}";
        if (_lblInfo != null)
            _lblInfo.Text = "Intervalo — 2° Tempo a seguir";
    }

    private void OnContinuePressed()
    {
        // Volta à cena de partida para o segundo tempo
        var gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");
        gsm?.GoTo(GameStateManager.GameState.Match);
    }
}
