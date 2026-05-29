using Godot;
using System.Collections.Generic;

namespace FootballGame;

/// <summary>
/// Tela de seleção de time e modo antes da partida.
/// Escaneia recursos de time e formação disponíveis e configura o GameManager.
/// </summary>
public partial class TeamSelectUI : Control
{
    [Export] private OptionButton _modeOption;
    [Export] private OptionButton _homeTeamOption;
    [Export] private OptionButton _awayTeamOption;
    [Export] private OptionButton _formationOption;
    [Export] private Button       _btnPlay;
    [Export] private Button       _btnBack;
    [Export] private Label        _lblHomeRating;
    [Export] private Label        _lblAwayRating;

    private readonly List<TeamData> _teams     = new();
    private readonly List<string>   _lineupPaths = new();

    public override void _Ready()
    {
        LoadTeams();
        LoadFormations();
        PopulateUI();

        if (_btnPlay != null) _btnPlay.Pressed += OnPlayPressed;
        if (_btnBack != null) _btnBack.Pressed += OnBackPressed;

        if (_homeTeamOption != null) _homeTeamOption.ItemSelected += _ => UpdateRatingLabels();
        if (_awayTeamOption != null) _awayTeamOption.ItemSelected += _ => UpdateRatingLabels();

        UpdateRatingLabels();
    }

    private void LoadTeams()
    {
        // Tenta scan dinâmico, cai em paths conhecidos se falhar
        bool loaded = false;
        using var dir = DirAccess.Open("res://resources/leagues/");
        if (dir != null)
        {
            dir.ListDirBegin();
            string name = dir.GetNext();
            while (!string.IsNullOrEmpty(name))
            {
                if (name.EndsWith(".tres"))
                {
                    string path = $"res://resources/leagues/{name}";
                    var res = GD.Load<Resource>(path);
                    if (res is TeamData td)
                    {
                        _teams.Add(td);
                        loaded = true;
                    }
                }
                name = dir.GetNext();
            }
        }

        if (!loaded)
        {
            TryAddTeam("res://resources/leagues/team_home.tres");
            TryAddTeam("res://resources/leagues/team_away.tres");
        }
    }

    private void TryAddTeam(string path)
    {
        var res = GD.Load<Resource>(path);
        if (res is TeamData td) _teams.Add(td);
    }

    private void LoadFormations()
    {
        using var dir = DirAccess.Open("res://resources/lineups/");
        if (dir != null)
        {
            dir.ListDirBegin();
            string name = dir.GetNext();
            while (!string.IsNullOrEmpty(name))
            {
                if (name.EndsWith(".tres"))
                    _lineupPaths.Add($"res://resources/lineups/{name}");
                name = dir.GetNext();
            }
        }

        if (_lineupPaths.Count == 0)
        {
            _lineupPaths.Add("res://resources/lineups/lineup_433_home.tres");
            _lineupPaths.Add("res://resources/lineups/lineup_433_away.tres");
        }
    }

    private void PopulateUI()
    {
        if (_modeOption != null)
        {
            _modeOption.Clear();
            _modeOption.AddItem("Amistoso",  (int)GameManager.MatchMode.Friendly);
            _modeOption.AddItem("Liga",       (int)GameManager.MatchMode.League);
            _modeOption.AddItem("Copa",       (int)GameManager.MatchMode.Cup);
            _modeOption.AddItem("Carreira",   (int)GameManager.MatchMode.Career);
        }

        if (_homeTeamOption != null)
        {
            _homeTeamOption.Clear();
            foreach (var t in _teams)
                _homeTeamOption.AddItem(t.ShortName.Length > 0 ? t.ShortName : t.TeamId);
        }

        if (_awayTeamOption != null)
        {
            _awayTeamOption.Clear();
            foreach (var t in _teams)
                _awayTeamOption.AddItem(t.ShortName.Length > 0 ? t.ShortName : t.TeamId);

            // Padrão: times diferentes
            if (_teams.Count >= 2)
                _awayTeamOption.Selected = 1;
        }

        if (_formationOption != null)
        {
            _formationOption.Clear();
            foreach (Lineup.FormationKind fk in System.Enum.GetValues(typeof(Lineup.FormationKind)))
                _formationOption.AddItem(fk.ToString().Replace("F_", "").Replace("_", "-"));
        }
    }

    private void UpdateRatingLabels()
    {
        if (_teams.Count == 0) return;

        int homeIdx = _homeTeamOption?.Selected ?? 0;
        int awayIdx = _awayTeamOption?.Selected ?? 0;

        homeIdx = Mathf.Clamp(homeIdx, 0, _teams.Count - 1);
        awayIdx = Mathf.Clamp(awayIdx, 0, _teams.Count - 1);

        if (_lblHomeRating != null)
            _lblHomeRating.Text = $"OVR {_teams[homeIdx].OverallRating}";
        if (_lblAwayRating != null)
            _lblAwayRating.Text = $"OVR {_teams[awayIdx].OverallRating}";
    }

    private void OnPlayPressed()
    {
        var gm  = GetNodeOrNull<GameManager>("/root/GameManager");
        var gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");
        if (gm == null || gsm == null || _teams.Count == 0) return;

        int homeIdx = Mathf.Clamp(_homeTeamOption?.Selected ?? 0, 0, _teams.Count - 1);
        int awayIdx = Mathf.Clamp(_awayTeamOption?.Selected ?? 0, 0, _teams.Count - 1);
        int modeIdx = _modeOption?.Selected ?? 0;

        gm.HomeTeam    = _teams[homeIdx];
        gm.AwayTeam    = _teams[awayIdx];
        gm.CurrentMode = (GameManager.MatchMode)modeIdx;

        gsm.GoTo(GameStateManager.GameState.Match);
    }

    private void OnBackPressed()
    {
        var gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");
        gsm?.GoTo(GameStateManager.GameState.MainMenu);
    }
}
