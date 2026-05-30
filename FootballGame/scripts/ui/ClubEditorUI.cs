using Godot;
using Godot.Collections;
using System.Linq;

namespace FootballGame;

/// <summary>
/// Editor in-game de clubes. Edita identidade (nome, país), cores primária/
/// secundária (preview do uniforme) e o elenco: gerar 11 titulares a partir de
/// uma formação, adicionar/remover jogadores e salvar em user://custom/teams/.
/// </summary>
public partial class ClubEditorUI : Control
{
    [Export] private LineEdit      _editShortName;
    [Export] private LineEdit      _editFullName;
    [Export] private LineEdit      _editCountry;
    [Export] private ColorPickerButton _pickPrimary;
    [Export] private ColorPickerButton _pickSecondary;
    [Export] private ColorRect     _kitPreview;
    [Export] private SpinBox       _spinOverall;
    [Export] private VBoxContainer _squadList;
    [Export] private Label         _lblSquadInfo;
    [Export] private OptionButton  _optSaved;
    [Export] private Button        _btnGenerateSquad;
    [Export] private Button        _btnAddPlayer;
    [Export] private Button        _btnSave;
    [Export] private Button        _btnLoad;
    [Export] private Button        _btnDelete;
    [Export] private Button        _btnNew;
    [Export] private Button        _btnBack;
    [Export] private Label         _lblStatus;

    private GameStateManager           _gsm;
    private TeamData                   _current;
    private System.Collections.Generic.List<TeamData> _saved = new();

    public override void _Ready()
    {
        _gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");

        if (_pickPrimary   != null) _pickPrimary.ColorChanged   += _ => UpdateKitPreview();
        if (_pickSecondary != null) _pickSecondary.ColorChanged += _ => UpdateKitPreview();

        if (_btnGenerateSquad != null) _btnGenerateSquad.Pressed += OnGenerateSquad;
        if (_btnAddPlayer     != null) _btnAddPlayer.Pressed     += OnAddPlayer;
        if (_btnSave          != null) _btnSave.Pressed          += OnSave;
        if (_btnLoad          != null) _btnLoad.Pressed          += OnLoad;
        if (_btnDelete        != null) _btnDelete.Pressed        += OnDelete;
        if (_btnNew           != null) _btnNew.Pressed           += OnNew;
        if (_btnBack          != null) _btnBack.Pressed          += () => _gsm?.GoTo(GameStateManager.GameState.EditorHub);

        RefreshSavedList();
        LoadIntoForm(NewDefault());
    }

    // ── Identidade / cores ───────────────────────────────────────

    private void UpdateKitPreview()
    {
        if (_kitPreview != null && _pickPrimary != null)
            _kitPreview.Color = _pickPrimary.Color;
    }

    private static TeamData NewDefault() => new()
    {
        ShortName = "Novo Clube", FullName = "Novo Clube FC", Country = "Brasil",
        PrimaryColor = new Color(0.2f, 0.5f, 0.9f), SecondaryColor = new Color(1, 1, 1),
        OverallRating = 70, Squad = new Array<PlayerData>(),
    };

    // ── Elenco ───────────────────────────────────────────────────

    private void OnGenerateSquad()
    {
        int overall = (int)(_spinOverall?.Value ?? 70);
        var lineup  = GD.Load<Lineup>("res://resources/lineups/lineup_433.tres");
        _current.Squad = new Array<PlayerData>();

        if (lineup != null)
        {
            foreach (var role in lineup.Roles)
                _current.Squad.Add(PlayerGenerator.Create(role, overall, _current.TeamId, _current.Squad.Count));
        }
        else
        {
            for (int i = 0; i < 11; i++)
                _current.Squad.Add(PlayerGenerator.Create(null, overall, _current.TeamId, i));
        }

        NameSquad();
        BuildSquadList();
        ShowStatus($"Elenco gerado: 11 jogadores (overall base {overall}).");
    }

    private void OnAddPlayer()
    {
        int overall = (int)(_spinOverall?.Value ?? 70);
        var p = PlayerGenerator.Create(null, overall, _current.TeamId, _current.Squad.Count);
        p.FullName  = $"Reserva {_current.Squad.Count + 1}";
        p.ShortName = p.FullName;
        _current.Squad.Add(p);
        BuildSquadList();
        ShowStatus("Jogador adicionado ao elenco.");
    }

    private void NameSquad()
    {
        for (int i = 0; i < _current.Squad.Count; i++)
        {
            var p = _current.Squad[i];
            if (string.IsNullOrWhiteSpace(p.ShortName))
            {
                p.ShortName = $"{_current.ShortName} {i + 1:D2}";
                p.FullName  = p.ShortName;
            }
        }
    }

    private void BuildSquadList()
    {
        if (_squadList == null) return;
        foreach (Node c in _squadList.GetChildren()) c.QueueFree();

        var squad = _current.Squad;
        for (int i = 0; i < squad.Count; i++)
        {
            var p   = squad[i];
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            var lbl = new Label
            {
                Text                = $"{i + 1,2}. {p.ShortName,-18}  OVR {p.OverallRating,2}",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            lbl.AddThemeFontSizeOverride("font_size", 13);

            var btn = new Button { Text = "Remover" };
            int idx = i;
            btn.Pressed += () => { _current.Squad.RemoveAt(idx); BuildSquadList(); };

            row.AddChild(lbl);
            row.AddChild(btn);
            _squadList.AddChild(row);
        }

        if (_lblSquadInfo != null)
            _lblSquadInfo.Text = squad.Count > 0
                ? $"{squad.Count} jogador(es)  ·  Overall médio {_current.ComputedOverall()}"
                : "Elenco vazio — gere ou adicione jogadores.";
    }

    // ── Salvar / carregar ────────────────────────────────────────

    private void OnSave()
    {
        ReadForm();
        if (string.IsNullOrWhiteSpace(_current.ShortName))
        {
            ShowStatus("Informe o nome curto do clube antes de salvar.");
            return;
        }
        string path = CustomContent.SaveTeam(_current);
        RefreshSavedList();
        ShowStatus($"Salvo: {path}");
    }

    private void OnLoad()
    {
        int idx = _optSaved?.Selected ?? -1;
        if (idx < 0 || idx >= _saved.Count) { ShowStatus("Nenhum clube salvo selecionado."); return; }
        // Duplica para não editar a instância em cache do ResourceLoader
        LoadIntoForm((TeamData)_saved[idx].Duplicate(true));
        ShowStatus($"Carregado: {_current.ShortName}");
    }

    private void OnDelete()
    {
        int idx = _optSaved?.Selected ?? -1;
        if (idx < 0 || idx >= _saved.Count) { ShowStatus("Nenhum clube salvo selecionado."); return; }
        string name = _saved[idx].ShortName;
        CustomContent.DeleteTeam(_saved[idx].TeamId);
        RefreshSavedList();
        ShowStatus($"Excluído: {name}");
    }

    private void OnNew()
    {
        LoadIntoForm(NewDefault());
        ShowStatus("Novo clube.");
    }

    private void LoadIntoForm(TeamData td)
    {
        _current = td;
        if (_editShortName  != null) _editShortName.Text  = td.ShortName;
        if (_editFullName   != null) _editFullName.Text   = td.FullName;
        if (_editCountry    != null) _editCountry.Text    = td.Country;
        if (_pickPrimary    != null) _pickPrimary.Color   = td.PrimaryColor;
        if (_pickSecondary  != null) _pickSecondary.Color = td.SecondaryColor;
        if (_spinOverall    != null) _spinOverall.Value   = td.OverallRating;
        UpdateKitPreview();
        BuildSquadList();
    }

    private void ReadForm()
    {
        _current.ShortName      = _editShortName?.Text ?? _current.ShortName;
        _current.FullName       = _editFullName?.Text ?? _current.FullName;
        _current.Country        = _editCountry?.Text ?? _current.Country;
        _current.PrimaryColor   = _pickPrimary?.Color ?? _current.PrimaryColor;
        _current.SecondaryColor = _pickSecondary?.Color ?? _current.SecondaryColor;
        _current.OverallRating  = (int)(_spinOverall?.Value ?? _current.OverallRating);
    }

    private void RefreshSavedList()
    {
        _saved = CustomContent.LoadAllTeams();
        if (_optSaved == null) return;
        _optSaved.Clear();
        foreach (var t in _saved)
            _optSaved.AddItem($"{(t.ShortName.Length > 0 ? t.ShortName : t.TeamId)} (OVR {t.ComputedOverall()})");
    }

    private void ShowStatus(string msg)
    {
        if (_lblStatus != null) _lblStatus.Text = msg;
    }
}
