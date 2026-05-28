using Godot;

namespace FootballGame;

/// <summary>
/// Cérebro de IA de um jogador. Constrói uma <see cref="BTNode"/> baseada no
/// <see cref="PlayerRole.Type"/> e a tickia a 10 Hz, escrevendo nas
/// <c>Intended*</c> properties do <see cref="Player"/>.
/// </summary>
public partial class AIBrain : Node
{
    [Export] public Player    Player;
    [Export] private NodePath _teamControllerPath;

    public TeamBlackboard Blackboard { get; private set; }

    /// <summary>Slot da formação que o TeamController atribuiu para este jogador.</summary>
    public Vector3 SlotTarget;

    private BTNode _tree;
    private float _tickAccumulator = 0f;
    private const float TickInterval = 0.1f; // 10 Hz

    public override void _Ready()
    {
        var teamCtrl = GetNode<TeamController>(_teamControllerPath);
        Blackboard = teamCtrl.Blackboard;
        _tree = BuildTreeForRole(Player.Role);
    }

    public override void _PhysicsProcess(double delta)
    {
        _tickAccumulator += (float)delta;
        if (_tickAccumulator < TickInterval) return;
        _tickAccumulator = 0f;

        _tree?.Tick(this, TickInterval);
    }

    // ─────────────────────────────────────────────────────────────
    // Construção da árvore conforme o papel
    // ─────────────────────────────────────────────────────────────
    private BTNode BuildTreeForRole(PlayerRole role)
    {
        if (role == null) return new BTAction(HoldFormationSlot);

        if (role.Type == PlayerRole.RoleType.Goalkeeper)
            return BuildGoalkeeperTree();

        return new BTSelector(
            // 1. Estou com a bola? Atacar ou passar.
            new BTSequence(
                new BTCondition(b => b.Player.HasBall),
                new BTAction(AttackOrPass)
            ),
            // 2. Meu time tem a bola? Apoiar o ataque.
            new BTSequence(
                new BTCondition(b => b.Blackboard.TeamHasPossession),
                new BTAction(SupportAttack)
            ),
            // 3. Ordem do humano: pressionar (botão LB).
            new BTSequence(
                new BTCondition(b =>
                    b.Blackboard.PressureTarget != null && IsCloseToBall(8f)),
                new BTAction(PressBallCarrier)
            ),
            // 4. Default: ficar no slot da formação
            new BTAction(HoldFormationSlot)
        );
    }

    private BTNode BuildGoalkeeperTree() => new BTSelector(
        new BTSequence(
            new BTCondition(b => b.Blackboard.GoalkeeperRush),
            new BTAction(RushOutOfBox)
        ),
        new BTSequence(
            new BTCondition(b => BallApproachingGoal()),
            new BTAction(SetupDive)
        ),
        new BTAction(HoldGoalLine)
    );

    // ─────────────────────────────────────────────────────────────
    // AÇÕES (escrevem nas Intended* do Player)
    // ─────────────────────────────────────────────────────────────

    private BTStatus AttackOrPass(AIBrain b, float dt)
    {
        var goal = b.Blackboard.OpponentGoalPosition;
        float dist = b.Player.GlobalPosition.DistanceTo(goal);

        if (dist < 25f)
        {
            b.Player.LookTarget        = goal;
            b.Player.IntendsToKick     = true;
            b.Player.IntendedKickPower = 1.0f;
        }
        else
        {
            b.Player.IntendedMovement = (goal - b.Player.GlobalPosition).Normalized();
            b.Player.IntendsToSprint  = true;
        }
        return BTStatus.Success;
    }

    private BTStatus SupportAttack(AIBrain b, float dt)
    {
        var offset = new Vector3(0f, 0f, b.Player.Team == 0 ? -8f : 8f);
        var supportPos = b.Blackboard.BallPosition + offset;
        var dir = supportPos - b.Player.GlobalPosition;
        b.Player.IntendedMovement = dir.LengthSquared() > 1f ? dir.Normalized() : Vector3.Zero;
        return BTStatus.Success;
    }

    private BTStatus PressBallCarrier(AIBrain b, float dt)
    {
        var target = b.Blackboard.PressureTarget;
        if (target == null) return BTStatus.Failure;

        var dir = target.GlobalPosition - b.Player.GlobalPosition;
        b.Player.IntendedMovement = dir.Normalized();
        b.Player.IntendsToSprint  = true;
        if (dir.Length() < 1.5f)
            b.Player.IntendsToTackle = true;
        return BTStatus.Success;
    }

    private BTStatus HoldFormationSlot(AIBrain b, float dt)
    {
        var dir = SlotTarget - b.Player.GlobalPosition;
        b.Player.IntendedMovement = dir.LengthSquared() > 1f ? dir.Normalized() : Vector3.Zero;
        b.Player.IntendsToSprint = false;
        return BTStatus.Success;
    }

    private BTStatus RushOutOfBox(AIBrain b, float dt)
    {
        var dir = b.Blackboard.BallPosition - b.Player.GlobalPosition;
        b.Player.IntendedMovement = dir.Normalized();
        b.Player.IntendsToSprint = true;
        return BTStatus.Success;
    }

    private BTStatus SetupDive(AIBrain b, float dt)
    {
        if (b.Player is Goalkeeper gk)
        {
            gk.DiveDirection = (b.Blackboard.BallPosition - gk.GlobalPosition).Normalized();
            gk.IntendsToDive = b.Blackboard.BallVelocity.Length() > 12f;
        }
        return BTStatus.Success;
    }

    private BTStatus HoldGoalLine(AIBrain b, float dt)
    {
        var dir = SlotTarget - b.Player.GlobalPosition;
        b.Player.IntendedMovement = dir.LengthSquared() > 0.5f ? dir.Normalized() : Vector3.Zero;
        return BTStatus.Success;
    }

    // ── Helpers ──────────────────────────────────────────────────
    private bool IsCloseToBall(float r) =>
        Player.GlobalPosition.DistanceTo(Blackboard.BallPosition) < r;

    private bool BallApproachingGoal()
    {
        var toGoal = Blackboard.OwnGoalPosition - Blackboard.BallPosition;
        if (toGoal.LengthSquared() < 0.01f) return false;
        return Blackboard.BallVelocity.Dot(toGoal.Normalized()) > 5f;
    }
}
