using Godot;
using System.Collections.Generic;

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
    [Export] private NodePath _eventBusPath;

    public TeamBlackboard Blackboard      { get; private set; }

    /// <summary>Jogadores DESTE time. Populado uma vez no <see cref="_Ready"/>.</summary>
    public List<Player> Players           { get; private set; } = new();

    /// <summary>Jogadores do TIME ADVERSÁRIO. Populado uma vez (defer).</summary>
    public List<Player> OpponentPlayers   { get; private set; } = new();

    /// <summary>Todos os jogadores em campo. Usado por <see cref="FindBallCarrier"/>.</summary>
    private readonly List<Player> _allPlayers = new();

    public Ball Ball { get; private set; }

    private MatchEventBus _bus;

    public override void _Ready()
    {
        Ball = GetNode<Ball>(_ballPath);

        Blackboard = new TeamBlackboard { Ball = Ball };
        AddChild(Blackboard);

        // Escuta expulsões para remover o jogador do campo
        _bus = GetNodeOrNull<MatchEventBus>(_eventBusPath);
        if (_bus != null) _bus.EventOccurred += OnMatchEvent;

        // Defer para garantir que TODOS os jogadores (inclusive do outro time)
        // já estejam na árvore e no grupo "players".
        CallDeferred(MethodName.CollectPlayers);
    }

    private void OnMatchEvent(MatchEvent e)
    {
        if (e.Type != MatchEventType.RedCard) return;

        // Captura o nó antes de remover das listas; só o time DONO o libera.
        if (e.Team == Team)
        {
            var player = Players.Find(p => p.PlayerId == e.MainPlayerId);
            Players.RemoveAll(p => p.PlayerId == e.MainPlayerId);
            _allPlayers.RemoveAll(p => p.PlayerId == e.MainPlayerId);
            player?.QueueFree(); // QueueFree adia a liberação até o fim do frame
        }
        else
        {
            OpponentPlayers.RemoveAll(p => p.PlayerId == e.MainPlayerId);
            _allPlayers.RemoveAll(p => p.PlayerId == e.MainPlayerId);
        }
    }

    private void CollectPlayers()
    {
        Players.Clear();
        OpponentPlayers.Clear();
        _allPlayers.Clear();

        foreach (var node in GetTree().GetNodesInGroup("players"))
        {
            if (node is Player p)
            {
                _allPlayers.Add(p);
                if (p.Team == Team) Players.Add(p);
                else                OpponentPlayers.Add(p);
            }
        }

        ApplyLineup();

        // Expõe os elencos ao blackboard (referências — sempre atuais)
        Blackboard.Teammates = Players;
        Blackboard.Opponents = OpponentPlayers;
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateBlackboard();
        UpdateFormationSlots();
    }

    private void UpdateBlackboard()
    {
        Blackboard.BallPosition = Ball.GlobalPosition;
        Blackboard.BallVelocity = Ball.LinearVelocity;
        Blackboard.BallCarrier  = FindBallCarrier();
        Blackboard.TeamHasPossession =
            Blackboard.BallCarrier != null &&
            Blackboard.BallCarrier.Team == Team;

        // Fase de jogo — orienta a movimentação sem bola
        if (Blackboard.BallCarrier == null)
            Blackboard.Phase = TeamPhase.Transition;
        else if (Blackboard.BallCarrier.Team == Team)
            Blackboard.Phase = TeamPhase.Attacking;
        else
            Blackboard.Phase = TeamPhase.Defending;
    }

    private Player FindBallCarrier()
    {
        const float CarryRadiusSquared = 1.44f; // 1.2² = 1.44
        // Itera lista cacheada — evita a busca na SceneTree a cada frame
        foreach (var p in _allPlayers)
            if (p.GlobalPosition.DistanceSquaredTo(Ball.GlobalPosition) < CarryRadiusSquared)
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
