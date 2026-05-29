using Godot;
using System.Collections.Generic;

namespace FootballGame;

/// <summary>
/// Instancia os 22 jogadores a partir dos recursos de escalação,
/// conecta todos os sistemas e inicia a partida. Nó raiz da cena Match.
/// </summary>
public partial class MatchBootstrap : Node
{
    [Export] public PackedScene PlayerScene;
    [Export] public PackedScene GoalkeeperScene;
    [Export] public Lineup      HomeLineup;
    [Export] public Lineup      AwayLineup;
    [Export] public TeamData    HomeTeamData;
    [Export] public TeamData    AwayTeamData;

    [Export] private NodePath _teamAPath;
    [Export] private NodePath _teamBPath;
    [Export] private NodePath _ballPath;
    [Export] private NodePath _penaltyAreaAPath;
    [Export] private NodePath _penaltyAreaBPath;

    // Posições de kickoff para time 0 (ataca em +X). Time 1 é espelhado.
    private static readonly Vector3[] KickoffPositionsTeam0 = new Vector3[]
    {
        new(-45f,  0.5f,  0f),    // GK
        new(-28f,  0.5f, -12f),   // LB
        new(-22f,  0.5f,  -7f),   // CB_L
        new(-22f,  0.5f,   7f),   // CB_R
        new(-28f,  0.5f,  12f),   // RB
        new(-10f,  0.5f,  -8f),   // CM_L
        new(-10f,  0.5f,   0f),   // CM_C
        new(-10f,  0.5f,   8f),   // CM_R
        new( -4f,  0.5f, -15f),   // LW
        new( -4f,  0.5f,  15f),   // RW
        new( -1f,  0.5f,   0f),   // ST
    };

    private readonly List<Player> _allPlayers = new();

    public override void _Ready()
    {
        LoadDefaultResources();
        WireHumanInput();

        if (HomeLineup == null || AwayLineup == null)
        {
            GD.PrintErr("MatchBootstrap: Lineups não configurados!");
            return;
        }

        SpawnTeam(0, HomeLineup, false);
        SpawnTeam(1, AwayLineup, true);
        SetupGoalPositions();
        SetupCinematicDirector();

        var clock = GetNodeOrNull<MatchClock>("MatchClock");
        clock?.Start();

        var gm = GetNodeOrNull<GameManager>("/root/GameManager");
        if (gm != null)
        {
            gm.HomeTeam = HomeTeamData;
            gm.AwayTeam = AwayTeamData;
        }
    }

    // Carrega recursos padrão se não foram atribuídos no editor
    private void LoadDefaultResources()
    {
        if (PlayerScene == null)
            PlayerScene = GD.Load<PackedScene>("res://scenes/entities/Player.tscn");
        if (GoalkeeperScene == null)
            GoalkeeperScene = GD.Load<PackedScene>("res://scenes/entities/Goalkeeper.tscn");
        if (HomeLineup == null)
            HomeLineup = GD.Load<Lineup>("res://resources/lineups/lineup_433_home.tres");
        if (AwayLineup == null)
            AwayLineup = GD.Load<Lineup>("res://resources/lineups/lineup_433_away.tres");
        if (HomeTeamData == null)
            HomeTeamData = GD.Load<TeamData>("res://resources/leagues/team_home.tres");
        if (AwayTeamData == null)
            AwayTeamData = GD.Load<TeamData>("res://resources/leagues/team_away.tres");
    }

    private void WireHumanInput()
    {
        var switcher = GetNodeOrNull<ControlSwitcher>("ControlSwitcher");
        var humanInput = GetNodeOrNull<HumanInput>("HumanInput_P1");
        if (humanInput != null && switcher != null)
            humanInput.Switcher = switcher;
    }

    private void SpawnTeam(int team, Lineup lineup, bool mirrorX)
    {
        var tc = team == 0
            ? GetNodeOrNull<TeamController>(_teamAPath)
            : GetNodeOrNull<TeamController>(_teamBPath);

        var gkArea = team == 0
            ? GetNodeOrNull<Area3D>(_penaltyAreaAPath)
            : GetNodeOrNull<Area3D>(_penaltyAreaBPath);

        int count = Mathf.Min(lineup.Roles.Count, KickoffPositionsTeam0.Length);

        for (int i = 0; i < count; i++)
        {
            var role = lineup.Roles[i];
            bool isGK = role != null && role.Type == PlayerRole.RoleType.Goalkeeper;

            var scene = isGK ? GoalkeeperScene : PlayerScene;
            if (scene == null)
            {
                GD.PrintErr($"MatchBootstrap: cena null para jogador {i} time {team}");
                continue;
            }

            var player = scene.Instantiate<Player>();
            player.Team     = team;
            player.PlayerId = i < lineup.PlayerIds.Count ? lineup.PlayerIds[i] : $"p{team}_{i}";
            player.Role     = role;

            var pos = KickoffPositionsTeam0[i];
            player.Position = mirrorX ? new Vector3(-pos.X, pos.Y, -pos.Z) : pos;

            SetTeamColor(player, team);
            AddChild(player);
            _allPlayers.Add(player);

            if (player is Goalkeeper gk && gkArea != null)
                gk.PenaltyArea = gkArea;

            if (tc != null)
            {
                var brain = player.GetNodeOrNull<AIBrain>("AIBrain");
                brain?.Initialize(tc);
            }
        }
    }

    private void SetTeamColor(Player player, int team)
    {
        var mesh = player.GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        if (mesh == null) return;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = team == 0
            ? new Color(0.1f, 0.3f, 0.9f)
            : new Color(0.9f, 0.2f, 0.1f);
        mesh.MaterialOverride = mat;
    }

    private void SetupGoalPositions()
    {
        var tcA = GetNodeOrNull<TeamController>(_teamAPath);
        var tcB = GetNodeOrNull<TeamController>(_teamBPath);

        if (tcA?.Blackboard != null)
        {
            tcA.Blackboard.OwnGoalPosition      = new Vector3(-52.5f, 0f, 0f);
            tcA.Blackboard.OpponentGoalPosition = new Vector3( 52.5f, 0f, 0f);
        }
        if (tcB?.Blackboard != null)
        {
            tcB.Blackboard.OwnGoalPosition      = new Vector3( 52.5f, 0f, 0f);
            tcB.Blackboard.OpponentGoalPosition = new Vector3(-52.5f, 0f, 0f);
        }
    }

    private void SetupCinematicDirector()
    {
        var director = GetNodeOrNull<CinematicDirector>("CinematicDirector");
        if (director == null) return;

        var paths = new Godot.Collections.Array<NodePath>();
        foreach (var p in _allPlayers)
            paths.Add(p.GetPath());
        director.Set("_playerPaths", paths);
    }
}
