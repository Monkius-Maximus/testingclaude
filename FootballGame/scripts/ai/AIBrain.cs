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
        // Auto-detecta o Player pai se não foi atribuído no editor
        if (Player == null)
            Player = GetParentOrNull<Player>();

        if (_teamControllerPath != null && !_teamControllerPath.IsEmpty)
        {
            var teamCtrl = GetNode<TeamController>(_teamControllerPath);
            Initialize(teamCtrl);
        }
    }

    /// <summary>Inicializa o brain com o TeamController do time. Chamado pelo MatchBootstrap.</summary>
    public void Initialize(TeamController teamCtrl)
    {
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
        var   goal       = b.Blackboard.OpponentGoalPosition;
        float distToGoal = b.Player.GlobalPosition.DistanceTo(goal);

        // Distância de chute escala com Shooting: 70→19m, 90→23m, 50→15m
        float shootRange = 13f + b.Player.Shooting * 0.14f;

        // Jogadores com Passing muito superior a Shooting preferem passar mesmo perto
        bool prefersPass = b.Player.Passing > b.Player.Shooting + 12;

        if (distToGoal < shootRange && !prefersPass)
        {
            b.Player.LookTarget        = goal;
            b.Player.IntendsToKick     = true;
            b.Player.IntendedKickPower = Mathf.Clamp(1.2f - distToGoal / (shootRange * 1.5f), 0.5f, 1f);
            return BTStatus.Success;
        }

        // Procura o melhor companheiro para passar
        var passTarget = FindBestPassTarget(b);
        if (passTarget != null)
        {
            b.Player.LookTarget        = passTarget.GlobalPosition + Vector3.Up * 0.3f;
            b.Player.IntendsToKick     = true;
            b.Player.IntendedKickPower = CalculatePassPower(b.Player.GlobalPosition,
                                                             passTarget.GlobalPosition);
            return BTStatus.Success;
        }

        // Sem passe: dribla em direção ao gol (Pace alto → sprint mais frequente)
        b.Player.IntendedMovement = (goal - b.Player.GlobalPosition).Normalized();
        b.Player.IntendsToSprint  = distToGoal < 35f + b.Player.Pace * 0.1f;
        return BTStatus.Success;
    }

    private Player FindBestPassTarget(AIBrain b)
    {
        var   myPos  = b.Player.GlobalPosition;
        var   goal   = b.Blackboard.OpponentGoalPosition;
        float myDist = myPos.DistanceTo(goal);

        // Visão de passe: Passing alto enxerga alvos mais adiantados e sob pressão
        float advanceThreshold = 4f + b.Player.Passing * 0.05f;   // 70→7.5m, 90→8.5m
        float minPressure      = 3f - b.Player.Passing * 0.01f;   // 70→2.3m, 90→2.1m

        Player best      = null;
        float  bestScore = -1f;

        foreach (var node in b.Player.GetTree().GetNodesInGroup("players"))
        {
            if (node is not Player mate)    continue;
            if (mate == b.Player)           continue;
            if (mate.Team != b.Player.Team) continue;

            float mateDist = mate.GlobalPosition.DistanceTo(goal);
            if (mateDist >= myDist - advanceThreshold) continue;

            float pressure = NearestOpponentDist(b, mate.GlobalPosition);
            if (pressure < minPressure) continue;

            if (IsPassLaneBlocked(b, myPos, mate.GlobalPosition)) continue;

            // Bônus se o alvo é um avançado com Shooting alto
            float shootBonus = mate.Shooting > 75 ? 8f : 0f;
            float score = (myDist - mateDist) * 0.6f + pressure * 0.4f + shootBonus;
            if (score > bestScore) { bestScore = score; best = mate; }
        }
        return best;
    }

    private float NearestOpponentDist(AIBrain b, Vector3 pos)
    {
        float min = float.MaxValue;
        foreach (var node in b.Player.GetTree().GetNodesInGroup("players"))
            if (node is Player opp && opp.Team != b.Player.Team)
            {
                float d = opp.GlobalPosition.DistanceTo(pos);
                if (d < min) min = d;
            }
        return min;
    }

    private bool IsPassLaneBlocked(AIBrain b, Vector3 from, Vector3 to)
    {
        var   dir  = (to - from);
        float dist = dir.Length();
        if (dist < 0.01f) return false;
        var dirN = dir.Normalized();

        foreach (var node in b.Player.GetTree().GetNodesInGroup("players"))
        {
            if (node is not Player opp) continue;
            if (opp.Team == b.Player.Team) continue;

            var   toOpp = opp.GlobalPosition - from;
            float proj  = dirN.Dot(toOpp);
            if (proj < 0f || proj > dist) continue;

            var closest = from + dirN * proj;
            if (closest.DistanceTo(opp.GlobalPosition) < 2.5f) return true;
        }
        return false;
    }

    private static float CalculatePassPower(Vector3 from, Vector3 to)
    {
        float dist = from.DistanceTo(to);
        return Mathf.Clamp(dist / 28f, 0.2f, 0.65f);
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
        // Defending alto → sprinta para pressionar mesmo com stamina baixa
        b.Player.IntendsToSprint = b.Player.CurrentStamina > 20f
                                    || b.Player.Defending > 78;
        // Carrinho: Defending alto tenta de um pouco mais longe
        float tackleAttemptRange = 1.3f + b.Player.Defending * 0.008f;
        if (dir.Length() < tackleAttemptRange)
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
