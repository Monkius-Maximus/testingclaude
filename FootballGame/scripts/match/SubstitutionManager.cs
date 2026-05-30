using Godot;
using System.Collections.Generic;

namespace FootballGame;

/// <summary>
/// Gerencia substituições em campo. Mantém listas de jogadores em campo e
/// no banco para cada time. Limite: 3 substituições por time (regra FIFA).
/// Registra-se no grupo "substitution_manager" para acesso por outros nós.
/// </summary>
public partial class SubstitutionManager : Node
{
    [Signal] public delegate void SubstitutionMadeEventHandler(int team, string outPlayerId, string inPlayerId);

    private const int MaxSubsPerTeam = 3;
    private const int BenchSize      = 7;

    // Listas de jogadores em campo (preenchidas pelo MatchBootstrap via RegisterPlayer)
    private readonly List<Player> _fieldPlayers0 = new();
    private readonly List<Player> _fieldPlayers1 = new();

    // Banco: PlayerData gerados ou do Squad (posições 11-17)
    private readonly List<PlayerData> _bench0 = new();
    private readonly List<PlayerData> _bench1 = new();

    // Substituições usadas
    private int _subsUsed0 = 0;
    private int _subsUsed1 = 0;

    private PackedScene _playerScene;
    private PackedScene _gkScene;
    private MatchStats  _stats;

    public override void _Ready()
    {
        AddToGroup("substitution_manager");
        _stats       = GetTree().GetFirstNodeInGroup("match_stats") as MatchStats;
        _playerScene = GD.Load<PackedScene>("res://scenes/entities/Player.tscn");
        _gkScene     = GD.Load<PackedScene>("res://scenes/entities/Goalkeeper.tscn");
    }

    // ── Setup (chamado pelo MatchBootstrap após spawn) ────────────

    public void RegisterPlayer(Player p)
    {
        var list = p.Team == 0 ? _fieldPlayers0 : _fieldPlayers1;
        if (!list.Contains(p)) list.Add(p);
    }

    public void GenerateBench(TeamData teamData, int team)
    {
        var bench = team == 0 ? _bench0 : _bench1;
        bench.Clear();
        int overall = teamData?.OverallRating ?? 68;
        int startIdx = 11;

        for (int i = 0; i < BenchSize; i++)
        {
            if (teamData?.Squad != null && startIdx + i < teamData.Squad.Count)
            {
                bench.Add(teamData.Squad[startIdx + i]);
            }
            else
            {
                var data = PlayerGenerator.Create(null, overall - 5, teamData?.TeamId ?? $"bench{team}", startIdx + i);
                data.ShortName = $"Sub {i + 1}";
                bench.Add(data);
            }
        }
    }

    // ── API pública ───────────────────────────────────────────────

    public int SubsRemaining(int team) => MaxSubsPerTeam - (team == 0 ? _subsUsed0 : _subsUsed1);

    public List<Player>     GetFieldPlayers(int team) => team == 0 ? _fieldPlayers0 : _fieldPlayers1;
    public List<PlayerData> GetBench(int team)        => team == 0 ? _bench0        : _bench1;

    public bool MakeSub(Player outPlayer, int benchIndex)
    {
        int team = outPlayer.Team;
        if (SubsRemaining(team) <= 0) return false;

        var bench = GetBench(team);
        if (benchIndex < 0 || benchIndex >= bench.Count) return false;

        var newData = bench[benchIndex];
        var field   = GetFieldPlayers(team);

        // Instancia o novo jogador na mesma posição
        var scene  = _gkScene != null && outPlayer is Goalkeeper ? _gkScene : _playerScene;
        if (scene == null) return false;

        var newPlayer = scene.Instantiate<Player>();
        newPlayer.Team     = team;
        newPlayer.Role     = outPlayer.Role;
        newPlayer.Position = outPlayer.Position;
        newPlayer.ApplyStats(newData);

        GetParent().AddChild(newPlayer);

        // Wires IA
        var tc = GetTree().GetFirstNodeInGroup(team == 0 ? "team_a" : "team_b") as TeamController;
        var brain = newPlayer.GetNodeOrNull<AIBrain>("AIBrain");
        brain?.Initialize(tc);

        // Registra a substituição
        string outId = outPlayer.PlayerId;
        field.Remove(outPlayer);
        outPlayer.QueueFree();
        field.Add(newPlayer);

        bench.RemoveAt(benchIndex);

        if (team == 0) _subsUsed0++; else _subsUsed1++;

        EmitSignal(SignalName.SubstitutionMade, team, outId, newData.PlayerId);
        GD.Print($"Substituição time {team}: {outId} → {newData.PlayerId}");
        return true;
    }
}
