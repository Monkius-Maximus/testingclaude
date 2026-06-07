using Godot;
using System.Collections.Generic;

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

        // Wiring por cena: só resolve o TeamController se o caminho foi configurado.
        // No spawn dinâmico (MatchBootstrap) o caminho fica vazio e o brain é
        // inicializado via Initialize(tc) após o AddChild.
        if (_teamControllerPath != null && !_teamControllerPath.IsEmpty)
        {
            var teamCtrl = GetNode<TeamController>(_teamControllerPath);
            Initialize(teamCtrl);
        }

        // Stagger: distribui os ticks de IA entre os jogadores ao longo do ciclo
        // (evita que os 22 brains decidam no mesmo frame, suavizando spikes de CPU).
        _tickAccumulator = (float)GD.RandRange(0.0, TickInterval);
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
            // 2. Time atacando (sem a bola comigo)? Movimentação ofensiva sem bola.
            new BTSequence(
                new BTCondition(b => b.Blackboard.Phase == TeamPhase.Attacking),
                new BTAction(OffBallAttack)
            ),
            // 3. Ordem do humano: pressionar (botão LB).
            new BTSequence(
                new BTCondition(b =>
                    b.Blackboard.PressureTarget != null && IsCloseToBall(8f)),
                new BTAction(PressBallCarrier)
            ),
            // 4. Time defendendo? Recompõe / marca.
            new BTSequence(
                new BTCondition(b => b.Blackboard.Phase == TeamPhase.Defending),
                new BTAction(DefensivePositioning)
            ),
            // 5. Default (transição / fallback): ficar no slot da formação
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

    // ── Movimentação ofensiva sem bola ───────────────────────────
    private const int   OffBallSamples    = 9;
    private const float OffBallSampleRing = 6f;   // raio do anel de amostragem (m)
    private const float SprintDistSquared = 25f;  // >5m do alvo → sprinta

    private BTStatus OffBallAttack(AIBrain b, float dt)
    {
        var bb   = b.Blackboard;
        var self = b.Player;
        if (bb.Opponents == null || bb.Teammates == null)
            return HoldFormationSlot(b, dt);

        float attackingGoalX = self.Team == 0 ? 52.5f : -52.5f;
        var   forward        = new Vector3(Mathf.Sign(attackingGoalX), 0f, 0f);

        // Atacantes avançam mais; defensores quase não sobem
        float aggressiveness = self.Role != null ? (1f - self.Role.DefensiveBias) : 0.5f;
        float offsideLine    = SpaceEvaluator.OffsideLineX(self.Team, bb.Opponents);

        Vector3 best      = b.SlotTarget;
        float   bestScore = float.NegativeInfinity;

        for (int i = 0; i < OffBallSamples; i++)
        {
            var cand = SampleCandidate(self.GlobalPosition, forward, aggressiveness, i);
            float score = SpaceEvaluator.ScoreAttackingPosition(
                cand, bb.BallPosition, attackingGoalX, offsideLine,
                self.Team, bb.Opponents, bb.Teammates, self);

            if (score > bestScore) { bestScore = score; best = cand; }
        }

        MoveToward(self, best);
        return BTStatus.Success;
    }

    /// <summary>Gera uma posição candidata: i==0 é a corrida reta em profundidade.</summary>
    private Vector3 SampleCandidate(Vector3 origin, Vector3 forward, float aggressiveness, int i)
    {
        if (i == 0)
            return origin + forward * (aggressiveness * 8f); // corrida em profundidade

        float angle  = Mathf.Tau / (OffBallSamples - 1) * (i - 1);
        var   offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * OffBallSampleRing;
        return origin + offset + forward * (aggressiveness * 3f); // viés pra frente
    }

    // ── Posicionamento defensivo sem bola ────────────────────────
    private BTStatus DefensivePositioning(AIBrain b, float dt)
    {
        var bb   = b.Blackboard;
        var self = b.Player;
        if (bb.Opponents == null)
            return HoldFormationSlot(b, dt);

        var mark = FindNearestOpponentInZone(self, bb.Opponents, 10f);
        if (mark != null)
        {
            // Fica "goalside": entre o marcado e o próprio gol
            float ownGoalX = self.Team == 0 ? -52.5f : 52.5f;
            var   goal     = new Vector3(ownGoalX, 0f, 0f);
            var   goalside = mark.GlobalPosition
                           + (goal - mark.GlobalPosition).Normalized() * 1.5f;
            MoveToward(self, goalside);
        }
        else
        {
            // Sem ninguém pra marcar: recompõe no slot (já comprimido pela bola)
            MoveToward(self, b.SlotTarget);
        }
        return BTStatus.Success;
    }

    private Player FindNearestOpponentInZone(Player self, IReadOnlyList<Player> opponents, float radius)
    {
        Player best = null;
        float minDist = radius * radius;
        foreach (var o in opponents)
        {
            float d = o.GlobalPosition.DistanceSquaredTo(self.GlobalPosition);
            if (d < minDist) { minDist = d; best = o; }
        }
        return best;
    }

    /// <summary>Define IntendedMovement em direção ao alvo, com sprint se estiver longe.</summary>
    private void MoveToward(Player p, Vector3 target)
    {
        var dir = target - p.GlobalPosition;
        dir.Y = 0f;
        float distSq = dir.LengthSquared();
        p.IntendedMovement = distSq > 1f ? dir.Normalized() : Vector3.Zero;
        p.IntendsToSprint  = distSq > SprintDistSquared;
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
