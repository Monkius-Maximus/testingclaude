using Godot;
using System.Collections.Generic;

namespace FootballGame;

/// <summary>
/// Decide, a cada frame, quem está comandando cada jogador: IA padrão,
/// humano direto, IA assistida (modificadores tipo LB), ou override manual
/// (goleiro fora). É a peça que torna a transição entre humano/IA invisível
/// — basta um deles preencher as <c>Intended*</c> properties.
/// </summary>
public partial class ControlSwitcher : Node
{
    public enum ControlMode { AI, HumanDirect, HumanAssisted, ManualOverride }

    [Export] private NodePath[] _humanInputPaths;
    [Export] private NodePath   _teamAPath;
    [Export] private NodePath   _teamBPath;

    private readonly List<HumanInput> _humans = new();
    private readonly Dictionary<Player, ControlMode> _modes = new();

    /// <summary>Buffer reutilizado por <see cref="GetNearbyTeammates"/> — evita aloc por frame.</summary>
    private readonly List<Player> _nearbyBuffer = new();

    private TeamController _teamA;
    private TeamController _teamB;

    public override void _Ready()
    {
        foreach (var path in _humanInputPaths)
            _humans.Add(GetNode<HumanInput>(path));

        _teamA = GetNodeOrNull<TeamController>(_teamAPath);
        _teamB = GetNodeOrNull<TeamController>(_teamBPath);

        // Defer: TeamControllers ainda estão coletando jogadores nesta hora
        CallDeferred(MethodName.RegisterAllPlayers);
    }

    private void RegisterAllPlayers()
    {
        foreach (var node in GetTree().GetNodesInGroup("players"))
            if (node is Player p) _modes[p] = ControlMode.AI;
    }

    public override void _PhysicsProcess(double delta)
    {
        // 1. Cada humano controla diretamente seu boneco ativo
        foreach (var h in _humans)
            if (h.ActivePlayer != null)
                _modes[h.ActivePlayer] = ControlMode.HumanDirect;

        // 2. Modificador de pressão coletiva
        foreach (var h in _humans)
        {
            var tc = GetTeamController(h.Team);
            if (tc == null) continue;

            if (h.RequestingTeamPress && h.ActivePlayer != null)
            {
                var teammates = GetNearbyTeammates(h.ActivePlayer, 12f);
                foreach (var mate in teammates)
                    _modes[mate] = ControlMode.HumanAssisted;

                tc.Blackboard.PressureTarget = FindNearestOpponent(h.ActivePlayer);
            }
            else
            {
                tc.Blackboard.PressureTarget = null;
            }
        }

        // 3. Goleiro sai (override manual)
        foreach (var h in _humans)
        {
            var tc = GetTeamController(h.Team);
            if (tc == null) continue;
            tc.Blackboard.GoalkeeperRush = h.RequestingGKRush;
        }

        // 4. Bonecos sem humano voltam à IA
        foreach (var pair in new Dictionary<Player, ControlMode>(_modes))
        {
            if (pair.Value == ControlMode.HumanDirect && !IsAnyHumanControlling(pair.Key))
                _modes[pair.Key] = ControlMode.AI;
        }
    }

    public ControlMode GetMode(Player p) =>
        _modes.TryGetValue(p, out var m) ? m : ControlMode.AI;

    /// <summary>Pedido de troca de boneco — registra novo ativo e libera o anterior.</summary>
    public Player RequestSwitch(HumanInput human, Player newTarget)
    {
        if (human.ActivePlayer != null)
            _modes[human.ActivePlayer] = ControlMode.AI;
        _modes[newTarget] = ControlMode.HumanDirect;
        return newTarget;
    }

    // ── Helpers ──────────────────────────────────────────────────
    private bool IsAnyHumanControlling(Player p)
    {
        foreach (var h in _humans)
            if (h.ActivePlayer == p) return true;
        return false;
    }

    /// <summary>
    /// Companheiros de <paramref name="p"/> num raio. Retorna um buffer COMPARTILHADO —
    /// não armazene a referência, apenas itere imediatamente.
    /// </summary>
    private List<Player> GetNearbyTeammates(Player p, float radius)
    {
        _nearbyBuffer.Clear();
        var tc = p.Team == 0 ? _teamA : _teamB;
        if (tc == null) return _nearbyBuffer;

        float r2 = radius * radius;
        foreach (var m in tc.Players)
        {
            if (m == p) continue;
            if (m.GlobalPosition.DistanceSquaredTo(p.GlobalPosition) < r2)
                _nearbyBuffer.Add(m);
        }
        return _nearbyBuffer;
    }

    private Player FindNearestOpponent(Player p)
    {
        var tc = p.Team == 0 ? _teamA : _teamB;
        if (tc == null) return null;

        Player best = null;
        float minDist = float.MaxValue;
        foreach (var o in tc.OpponentPlayers)
        {
            float d = o.GlobalPosition.DistanceSquaredTo(p.GlobalPosition);
            if (d < minDist) { minDist = d; best = o; }
        }
        return best;
    }

    private TeamController GetTeamController(int team) =>
        team == 0 ? _teamA : _teamB;
}
