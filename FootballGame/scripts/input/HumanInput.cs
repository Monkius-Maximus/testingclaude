using Godot;

namespace FootballGame;

/// <summary>
/// Entrada de um jogador humano. Cada controle físico tem um <see cref="HumanInput"/>.
/// Comanda diretamente o <see cref="ActivePlayer"/> e sinaliza modificadores
/// coletivos (pressão, goleiro fora) que o <see cref="ControlSwitcher"/> propaga.
/// </summary>
public partial class HumanInput : Node
{
    [Export] public int    ControllerIndex = 0;
    [Export] public int    Team            = 0;
    [Export] public ControlSwitcher Switcher;

    public Player ActivePlayer        { get; private set; }
    public bool   RequestingTeamPress { get; private set; }
    public bool   RequestingGKRush    { get; private set; }

    // Prefixo de ações no Input Map: "p1_*", "p2_*", etc
    private string Prefix => $"p{ControllerIndex + 1}_";

    public override void _PhysicsProcess(double delta)
    {
        if (ActivePlayer == null)
            ActivePlayer = FindNearestTeammateToBall();

        if (ActivePlayer == null) return;

        // ── Movimento (relativo à câmera) ────────────────────────
        var inputDir = Input.GetVector(
            Prefix + "left",  Prefix + "right",
            Prefix + "forward", Prefix + "back"
        );

        var cam = GetViewport().GetCamera3D();
        if (cam != null)
        {
            var fwd = -cam.GlobalTransform.Basis.Z; fwd.Y = 0; fwd = fwd.Normalized();
            var rgt =  cam.GlobalTransform.Basis.X; rgt.Y = 0; rgt = rgt.Normalized();
            ActivePlayer.IntendedMovement = fwd * -inputDir.Y + rgt * inputDir.X;
        }

        ActivePlayer.IntendsToSprint = Input.IsActionPressed(Prefix + "sprint");

        if (Input.IsActionJustPressed(Prefix + "kick"))
            ActivePlayer.IntendsToKick = true;
        if (Input.IsActionJustPressed(Prefix + "tackle"))
            ActivePlayer.IntendsToTackle = true;

        // ── Modificadores coletivos ──────────────────────────────
        RequestingTeamPress = Input.IsActionPressed(Prefix + "team_press");
        RequestingGKRush    = Input.IsActionPressed(Prefix + "gk_rush");

        // ── Troca de boneco ──────────────────────────────────────
        if (Input.IsActionJustPressed(Prefix + "switch_player"))
        {
            var newP = FindNearestTeammateToBall();
            if (newP != null && Switcher != null)
                ActivePlayer = Switcher.RequestSwitch(this, newP);
        }
    }

    private Player FindNearestTeammateToBall()
    {
        var ball = GetTree().GetFirstNodeInGroup("ball") as Node3D;
        if (ball == null) return null;

        Player best = null;
        float minDist = float.MaxValue;

        foreach (var node in GetTree().GetNodesInGroup("players"))
        {
            if (node is Player p && p.Team == Team)
            {
                float d = p.GlobalPosition.DistanceTo(ball.GlobalPosition);
                if (d < minDist) { minDist = d; best = p; }
            }
        }
        return best;
    }
}
