using Godot;
using System.Collections.Generic;
using System.Linq;

namespace FootballGame;

/// <summary>
/// Maestro de um time. Mantém o <see cref="TeamBlackboard"/> atualizado,
/// calcula os slots da formação para cada jogador, e aplica escalações
/// e substituições em tempo real.
/// </summary>
public partial class TeamController : Node
{
    [Export] public int      Team          = 0;
    [Export] public Lineup   CurrentLineup;
    [Export] private NodePath _ballPath;

    public TeamBlackboard Blackboard { get; private set; }
    public List<Player>   Players    { get; private set; } = new();

    private Ball _ball;
    private bool _playersLoaded = false;

    public override void _Ready()
    {
        _ball = GetNode<Ball>(_ballPath);

        Blackboard = new TeamBlackboard { Ball = _ball };
        AddChild(Blackboard);

        // Defer player scan so MatchBootstrap can spawn players first
        CallDeferred(nameof(FindAndApplyLineup));
    }

    private void FindAndApplyLineup()
    {
        Players.Clear();
        foreach (var node in GetTree().GetNodesInGroup("players"))
            if (node is Player p && p.Team == Team)
                Players.Add(p);

        ApplyLineup();
        _playersLoaded = true;
    }

    /// <summary>Re-escaneia jogadores do time. Chamar após spawn dinâmico.</summary>
    public void RefreshPlayers()
    {
        Players.Clear();
        foreach (var node in GetTree().GetNodesInGroup("players"))
            if (node is Player p && p.Team == Team)
                Players.Add(p);

        ApplyLineup();
        _playersLoaded = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateBlackboard();
        UpdateFormationSlots();
    }

    private void UpdateBlackboard()
    {
        Blackboard.BallPosition = _ball.GlobalPosition;
        Blackboard.BallVelocity = _ball.LinearVelocity;
        Blackboard.BallCarrier  = FindBallCarrier();
        Blackboard.TeamHasPossession =
            Blackboard.BallCarrier != null &&
            Blackboard.BallCarrier.Team == Team;
    }

    private Player FindBallCarrier()
    {
        foreach (var p in GetTree().GetNodesInGroup("players").OfType<Player>())
            if (p.GlobalPosition.DistanceTo(_ball.GlobalPosition) < 1.2f)
                return p;
        return null;
    }

    private void UpdateFormationSlots()
    {
        // Quanto mais o time avança, mais a formação inteira sobe
        float advance = Mathf.Clamp(Blackboard.BallPosition.Z / 52.5f, -1f, 1f);

        for (int i = 0; i < Players.Count; i++)
        {
            var p = Players[i];
            if (p.Role == null) continue;

            var basePos = new Vector3(
                p.Role.BasePosition.X * 30f,
                0.5f,
                p.Role.BasePosition.Y * 40f + advance * 8f
            );

            // Time 1 ataca para o outro lado: espelha em Z
            if (Team == 1) basePos.Z = -basePos.Z;

            Blackboard.FormationSlots[p.PlayerId] = basePos;

            // Repassa para o brain do jogador
            var brain = p.GetNodeOrNull<AIBrain>("AIBrain");
            if (brain != null) brain.SlotTarget = basePos;
        }
    }

    /// <summary>Aplica a escalação atual aos jogadores do time.</summary>
    public void ApplyLineup()
    {
        if (CurrentLineup == null) return;

        int count = Mathf.Min(CurrentLineup.PlayerIds.Count, Players.Count);
        for (int i = 0; i < count; i++)
        {
            var pid    = CurrentLineup.PlayerIds[i];
            var role   = CurrentLineup.Roles[i];
            var player = Players.Find(p => p.PlayerId == pid);
            if (player != null) player.Role = role;
        }
    }

    /// <summary>Substituição em tempo real.</summary>
    public void Substitute(string outId, string inId, PlayerRole newRole = null)
    {
        var leaving  = Players.Find(p => p.PlayerId == outId);
        var entering = Players.Find(p => p.PlayerId == inId);
        if (leaving == null || entering == null) return;

        entering.Role = newRole ?? leaving.Role;
        entering.GlobalPosition = leaving.GlobalPosition;
        leaving.QueueFree();
        Players.Remove(leaving);
    }
}
