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

    private const string PauseMenuPath = "res://scenes/ui/PauseMenu.tscn";
    private const string HalfTimePath  = "res://scenes/ui/HalfTime.tscn";

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
        ApplyStadium();

        var clock = GetNodeOrNull<MatchClock>("MatchClock");
        if (clock != null)
        {
            clock.HalfTime  += OnHalfTime;
            clock.FullTime  += OnFullTime;
            clock.Start();
        }

        var gm = GetNodeOrNull<GameManager>("/root/GameManager");
        if (gm != null)
        {
            gm.HomeTeam = HomeTeamData;
            gm.AwayTeam = AwayTeamData;
        }

        InstantiatePauseMenu();
    }

    // Carrega recursos padrão se não foram atribuídos no editor.
    // GameManager tem prioridade (vem da tela de seleção de times).
    private void LoadDefaultResources()
    {
        var gm = GetNodeOrNull<GameManager>("/root/GameManager");

        if (PlayerScene == null)
            PlayerScene = GD.Load<PackedScene>("res://scenes/entities/Player.tscn");
        if (GoalkeeperScene == null)
            GoalkeeperScene = GD.Load<PackedScene>("res://scenes/entities/Goalkeeper.tscn");

        if (HomeTeamData == null)
            HomeTeamData = gm?.HomeTeam ?? GD.Load<TeamData>("res://resources/leagues/team_home.tres");
        if (AwayTeamData == null)
            AwayTeamData = gm?.AwayTeam ?? GD.Load<TeamData>("res://resources/leagues/team_away.tres");

        if (HomeLineup == null)
            HomeLineup = gm?.HomeLineup ?? GD.Load<Lineup>("res://resources/lineups/lineup_433_home.tres");
        if (AwayLineup == null)
            AwayLineup = gm?.AwayLineup ?? GD.Load<Lineup>("res://resources/lineups/lineup_433_away.tres");
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

        int count      = Mathf.Min(lineup.Roles.Count, KickoffPositionsTeam0.Length);
        var teamData   = team == 0 ? HomeTeamData : AwayTeamData;
        int teamOverall = teamData?.OverallRating ?? 70;

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

            // Squad preenchido → usa dados reais; senão → geração procedural baseada no overall do time
            var playerData = (teamData?.Squad != null && i < teamData.Squad.Count)
                ? teamData.Squad[i]
                : PlayerGenerator.Create(role, teamOverall, teamData?.TeamId ?? $"t{team}", i);
            player.ApplyStats(playerData);

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

            // Registra no SubstitutionManager
            GetNodeOrNull<SubstitutionManager>("SubstitutionManager")?.RegisterPlayer(player);
        }

        // Gera banco de reservas para este time
        GetNodeOrNull<SubstitutionManager>("SubstitutionManager")
            ?.GenerateBench(teamData, team);
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

    private void InstantiatePauseMenu()
    {
        var scene = GD.Load<PackedScene>(PauseMenuPath);
        if (scene == null) return;
        AddChild(scene.Instantiate());
    }

    private void OnHalfTime()
    {
        // Registra o placar para exibição e pausa a partida ao vivo.
        UpdateMatchResult(GetNodeOrNull<MatchClock>("MatchClock")?.CurrentMinute ?? 45);

        GetTree().Paused = true;

        var scene = GD.Load<PackedScene>(HalfTimePath);
        if (scene == null) return;

        var overlay = scene.Instantiate<HalfTimeUI>();
        AddChild(overlay);
        overlay.ContinuePressed += OnHalfTimeContinue;
    }

    private void OnHalfTimeContinue()
    {
        GetNodeOrNull<MatchClock>("MatchClock")?.StartSecondHalf();
        GetTree().Paused = false;
    }

    private void OnFullTime()
    {
        // Fim de jogo: garante que a partida não fique pausada e vai ao resultado.
        UpdateMatchResult(GetNodeOrNull<MatchClock>("MatchClock")?.CurrentMinute ?? 90);

        GetTree().Paused = false;
        GetNodeOrNull<GameStateManager>("/root/GameStateManager")
            ?.GoTo(GameStateManager.GameState.Result);
    }

    /// <summary>Preenche o resultado da partida no GameStateManager para exibição.</summary>
    private void UpdateMatchResult(int minutesPlayed)
    {
        var gsm   = GetNodeOrNull<GameStateManager>("/root/GameStateManager");
        var rules = GetNodeOrNull<RulesManager>("RulesManager");
        var gm    = GetNodeOrNull<GameManager>("/root/GameManager");
        if (gsm == null || rules == null) return;

        var score = rules.Score;
        gsm.CurrentMatchResult = new GameStateManager.MatchResult
        {
            ScoreHome     = score.Home,
            ScoreAway     = score.Away,
            MinutesPlayed = minutesPlayed,
            Mode          = gm?.CurrentMode ?? GameManager.MatchMode.Friendly,
            Stats         = GetNodeOrNull<MatchStats>("MatchStats")
        };
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

    private void ApplyStadium()
    {
        var gm      = GetNodeOrNull<GameManager>("/root/GameManager");
        var stadium = gm?.ActiveStadium;

        // Tenta encontrar o nó Field (instanciado ou presente na árvore)
        var field = GetNodeOrNull<Node3D>("Field");
        if (field == null) return;

        StadiumLoader.Apply(field, stadium);
    }
}
