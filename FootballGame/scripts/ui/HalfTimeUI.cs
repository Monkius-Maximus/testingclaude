using Godot;

namespace FootballGame;

/// <summary>
/// Tela de intervalo. Aparece como overlay sobre a partida pausada (não troca
/// de cena, para que o 2° tempo continue a mesma partida). Emite
/// <see cref="ContinuePressed"/> quando o jogador confirma o retorno.
/// </summary>
public partial class HalfTimeUI : CanvasLayer
{
    [Signal] public delegate void ContinuePressedEventHandler();

    [Export] private Label  _lblScore;
    [Export] private Label  _lblInfo;
    [Export] private Button _btnContinue;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
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
        EmitSignal(SignalName.ContinuePressed);
        QueueFree();
    }
}
