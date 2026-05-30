using Godot;
using System.Collections.Generic;

namespace FootballGame;

/// <summary>
/// Tela de início/continuação do modo carreira.
/// Permite escolher o time, iniciar nova carreira ou carregar a existente.
/// </summary>
public partial class CareerSetupUI : Control
{
    [Export] private OptionButton _teamOption;
    [Export] private Button       _btnStart;
    [Export] private Button       _btnContinue;
    [Export] private Button       _btnDelete;
    [Export] private Button       _btnBack;
    [Export] private Label        _lblStatus;

    private readonly List<TeamData> _teams = new();

    public override void _Ready()
    {
        LoadTeams();
        PopulateTeamOption();
        RefreshCareerStatus();

        if (_btnStart    != null) _btnStart.Pressed    += OnStartPressed;
        if (_btnContinue != null) _btnContinue.Pressed += OnContinuePressed;
        if (_btnDelete   != null) _btnDelete.Pressed   += OnDeletePressed;
        if (_btnBack     != null) _btnBack.Pressed     += OnBackPressed;
    }

    private void LoadTeams()
    {
        using var dir = DirAccess.Open("res://resources/leagues/");
        if (dir == null) return;
        dir.ListDirBegin();
        string name = dir.GetNext();
        while (!string.IsNullOrEmpty(name))
        {
            if (name.EndsWith(".tres"))
            {
                var res = GD.Load<Resource>($"res://resources/leagues/{name}");
                if (res is TeamData td) _teams.Add(td);
            }
            name = dir.GetNext();
        }
    }

    private void PopulateTeamOption()
    {
        if (_teamOption == null) return;
        _teamOption.Clear();
        foreach (var t in _teams)
            _teamOption.AddItem(t.ShortName.Length > 0 ? t.ShortName : t.TeamId);
    }

    private void RefreshCareerStatus()
    {
        var cm = GetNodeOrNull<CareerManager>("/root/CareerManager");
        bool hasSave = cm?.HasCareer ?? false;

        if (_btnContinue != null) _btnContinue.Visible = hasSave;
        if (_btnDelete   != null) _btnDelete.Visible   = hasSave;

        if (_lblStatus != null)
        {
            _lblStatus.Text = hasSave
                ? $"Carreira salva: {TeamLabel(cm.Current.PlayerTeamId, cm)} | Temporada {cm.Current.Season}"
                : "Nenhuma carreira salva.";
        }
    }

    private void OnStartPressed()
    {
        if (_teams.Count == 0) return;
        int idx = Mathf.Clamp(_teamOption?.Selected ?? 0, 0, _teams.Count - 1);

        var cm  = GetNodeOrNull<CareerManager>("/root/CareerManager");
        var gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");
        if (cm == null || gsm == null) return;

        cm.StartNewCareer(_teams[idx].TeamId);
        gsm.GoTo(GameStateManager.GameState.CareerHub);
    }

    private void OnContinuePressed()
    {
        var gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");
        gsm?.GoTo(GameStateManager.GameState.CareerHub);
    }

    private void OnDeletePressed()
    {
        var cm = GetNodeOrNull<CareerManager>("/root/CareerManager");
        cm?.DeleteSave();
        RefreshCareerStatus();
    }

    private void OnBackPressed()
    {
        var gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");
        gsm?.GoTo(GameStateManager.GameState.MainMenu);
    }

    private static string TeamLabel(string teamId, CareerManager cm)
    {
        var td = cm.GetTeamData(teamId);
        return td?.ShortName is { Length: > 0 } s ? s : teamId;
    }
}
