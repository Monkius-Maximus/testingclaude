using Godot;

namespace FootballGame;

/// <summary>
/// HUD da partida. Mostra placar e dispara o banner animado de gol.
/// </summary>
public partial class HUD : CanvasLayer
{
    [Export] private Label  _scoreLeft;
    [Export] private Label  _scoreRight;
    [Export] private Control _goalBanner;
    [Export] private Label  _goalTeamLabel;
    [Export] private AnimationPlayer _hudAnimPlayer;

    private readonly int[] _score = { 0, 0 };

    public override void _Ready()
    {
        // Estado inicial
        if (_scoreLeft  != null) _scoreLeft.Text  = "0";
        if (_scoreRight != null) _scoreRight.Text = "0";
        if (_goalBanner != null) _goalBanner.Visible = false;
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
