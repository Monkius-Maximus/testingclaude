using Godot;

namespace FootballGame;

/// <summary>
/// HUD da partida. Escuta o <see cref="MatchEventBus"/> e reage aos eventos:
/// placar nos gols, indicador de cartões, e um "toast" para faltas.
/// </summary>
public partial class HUD : CanvasLayer
{
    [Export] private NodePath _eventBusPath;
    [Export] private Label  _scoreLeft;
    [Export] private Label  _scoreRight;
    [Export] private Control _goalBanner;
    [Export] private Label  _goalTeamLabel;
    [Export] private Label  _toastLabel;   // mensagens rápidas (falta, cartão)
    [Export] private AnimationPlayer _hudAnimPlayer;

    private MatchEventBus _bus;
    private readonly int[] _score = { 0, 0 };

    public override void _Ready()
    {
        if (_scoreLeft  != null) _scoreLeft.Text  = "0";
        if (_scoreRight != null) _scoreRight.Text = "0";
        if (_goalBanner != null) _goalBanner.Visible = false;
        if (_toastLabel != null) _toastLabel.Visible = false;

        _bus = GetNodeOrNull<MatchEventBus>(_eventBusPath);
        if (_bus != null) _bus.EventOccurred += OnMatchEvent;
    }

    // ── Roteia cada evento para a apresentação correta ───────────
    private void OnMatchEvent(MatchEvent e)
    {
        switch (e.Type)
        {
            case MatchEventType.Goal:
                AnimateGoalScored(e.Team);
                break;
            case MatchEventType.YellowCard:
                ShowToast($"🟨 Cartão amarelo — {e.MainPlayerId}");
                break;
            case MatchEventType.RedCard:
                ShowToast($"🟥 Cartão vermelho — {e.MainPlayerId}");
                break;
            case MatchEventType.Foul:
                ShowToast("Falta");
                break;
            case MatchEventType.Penalty:
                ShowToast("Pênalti!");
                break;
            case MatchEventType.FreeKick:
                ShowToast("Tiro livre");
                break;
        }
    }

    public void AnimateGoalScored(int team)
    {
        _score[team]++;

        if (team == 0 && _scoreLeft  != null) _scoreLeft.Text  = _score[0].ToString();
        if (team == 1 && _scoreRight != null) _scoreRight.Text = _score[1].ToString();

        if (_goalTeamLabel != null)
            _goalTeamLabel.Text = team == 0 ? "Time Azul" : "Time Vermelho";

        _hudAnimPlayer?.Play("goal_banner_in");
    }

    /// <summary>Mensagem efêmera (some sozinha após alguns segundos).</summary>
    private void ShowToast(string message)
    {
        if (_toastLabel == null) return;
        _toastLabel.Text    = message;
        _toastLabel.Visible = true;

        GetTree().CreateTimer(2.5f).Timeout += () =>
        {
            if (_toastLabel != null) _toastLabel.Visible = false;
        };
    }

    /// <summary>Chamado via Method Track no meio da animação do banner.</summary>
    public void GoalBannerMidpoint()
    {
        var target = _score[0] > _score[1] - 1 ? _scoreLeft : _scoreRight;
        if (target == null) return;

        var tw = CreateTween();
        tw.TweenProperty(target, "scale", new Vector2(1.5f, 1.5f), 0.1f)
          .SetTrans(Tween.TransitionType.Bounce);
        tw.TweenProperty(target, "scale", Vector2.One, 0.2f)
          .SetTrans(Tween.TransitionType.Elastic);

        GetTree().CreateTimer(2.0f).Timeout += () =>
            _hudAnimPlayer?.Play("goal_banner_out");
    }
}
