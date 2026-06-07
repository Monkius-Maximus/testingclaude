using Godot;

namespace FootballGame;

/// <summary>
/// Area3D posicionada dentro de cada gol. Emite <see cref="GoalScored"/>
/// quando a bola entra, indicando qual time marcou.
/// </summary>
public partial class GoalDetector : Area3D
{
    /// <summary>Time DONO deste gol (não quem marca). 0 ou 1.</summary>
    [Export] public int Team = 0;

    [Signal] public delegate void GoalScoredEventHandler(int teamThatScored, Vector3 ballPosition);

    public override void _Ready()
    {
        AddToGroup("goal_detectors");
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (!body.IsInGroup("ball")) return;

        // Quem marcou é o time oposto ao dono do gol
        int scoringTeam = Team == 0 ? 1 : 0;
        EmitSignal(SignalName.GoalScored, scoringTeam, body.GlobalPosition);

        // Desativa até reativar manualmente (evita duplo gol)
        SetDeferred("monitoring", false);
    }

    public void Reactivate() => Monitoring = true;
}
