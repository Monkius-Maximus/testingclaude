using Godot;

namespace FootballGame;

/// <summary>
/// Entrada de um jogador humano. Cada controle físico tem um <see cref="HumanInput"/>.
/// Comanda diretamente o <see cref="ActivePlayer"/> e sinaliza modificadores
/// coletivos (pressão, goleiro fora) que o <see cref="ControlSwitcher"/> propaga.
/// </summary>
public partial class HumanInput : Node
{
    [Export] public int             ControllerIndex = 0;
    [Export] public int             Team            = 0;
    [Export] public ControlSwitcher Switcher;

    /// <summary>TeamController do time deste humano. Evita buscar jogadores na árvore.</summary>
    [Export] private NodePath _teamControllerPath;

    /// <summary>Câmera usada como referência para o movimento (relativa).</summary>
    [Export] public Camera3D ReferenceCamera;

    public Player ActivePlayer        { get; private set; }
    public bool   RequestingTeamPress { get; private set; }
    public bool   RequestingGKRush    { get; private set; }

    private TeamController _teamCtrl;

    // Prefixo de ações no Input Map: "p1_*", "p2_*", etc
    private string Prefix => $"p{ControllerIndex + 1}_";

    public override void _Ready()
    {
        _teamCtrl = GetNodeOrNull<TeamController>(_teamControllerPath);
    }

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

        if (ReferenceCamera != null)
        {
            var fwd = -ReferenceCamera.GlobalTransform.Basis.Z; fwd.Y = 0; fwd = fwd.Normalized();
            var rgt =  ReferenceCamera.GlobalTransform.Basis.X; rgt.Y = 0; rgt = rgt.Normalized();
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
        if (_teamCtrl == null || _teamCtrl.Ball == null) return null;
        var ballPos = _teamCtrl.Ball.GlobalPosition;

        Player best = null;
        float minDist = float.MaxValue;

        // Itera lista cacheada — não toca na SceneTree
        foreach (var p in _teamCtrl.Players)
        {
            float d = p.GlobalPosition.DistanceSquaredTo(ballPos);
            if (d < minDist) { minDist = d; best = p; }
        }
        return best;
    }
}
