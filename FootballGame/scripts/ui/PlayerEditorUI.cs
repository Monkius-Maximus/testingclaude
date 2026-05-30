using Godot;
using System;
using System.Collections.Generic;

namespace FootballGame;

/// <summary>
/// Editor in-game de jogadores. Permite criar/editar um PlayerData com todos
/// os atributos via sliders, gerar aleatoriamente por função, e salvar/carregar
/// de user://custom/players/. O OVR é recalculado ao vivo.
/// </summary>
public partial class PlayerEditorUI : Control
{
    [Export] private LineEdit      _editFullName;
    [Export] private LineEdit      _editShortName;
    [Export] private LineEdit      _editNationality;
    [Export] private SpinBox       _spinAge;
    [Export] private OptionButton  _optRole;
    [Export] private VBoxContainer _attrContainer;
    [Export] private Label         _lblOverall;
    [Export] private OptionButton  _optSaved;
    [Export] private Button        _btnGenerate;
    [Export] private Button        _btnSave;
    [Export] private Button        _btnLoad;
    [Export] private Button        _btnDelete;
    [Export] private Button        _btnNew;
    [Export] private Button        _btnBack;
    [Export] private Label         _lblStatus;

    private GameStateManager _gsm;

    // Atributos editáveis: nome interno do campo PlayerData
    private static readonly string[] FieldAttrs =
        { "Pace", "Shooting", "Passing", "Dribbling", "Defending", "Physical", "Heading" };
    private static readonly string[] GkAttrs =
        { "Reflexes", "Diving", "GkPositioning" };

    private static readonly Dictionary<string, string> AttrLabels = new()
    {
        ["Pace"]          = "Velocidade",
        ["Shooting"]      = "Finalização",
        ["Passing"]       = "Passe",
        ["Dribbling"]     = "Drible",
        ["Defending"]     = "Defesa",
        ["Physical"]      = "Físico",
        ["Heading"]       = "Cabeceio",
        ["Reflexes"]      = "Reflexos (GL)",
        ["Diving"]        = "Mergulho (GL)",
        ["GkPositioning"] = "Posicion. (GL)",
    };

    private readonly Dictionary<string, HSlider> _sliders = new();
    private readonly Dictionary<string, Label>   _valueLabels = new();
    private List<PlayerData> _saved = new();

    public override void _Ready()
    {
        _gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");

        PopulateRoleOptions();
        BuildSliders();
        RefreshSavedList();

        if (_btnGenerate != null) _btnGenerate.Pressed += OnGeneratePressed;
        if (_btnSave     != null) _btnSave.Pressed     += OnSavePressed;
        if (_btnLoad     != null) _btnLoad.Pressed     += OnLoadPressed;
        if (_btnDelete   != null) _btnDelete.Pressed   += OnDeletePressed;
        if (_btnNew      != null) _btnNew.Pressed      += OnNewPressed;
        if (_btnBack     != null) _btnBack.Pressed     += () => _gsm?.GoTo(GameStateManager.GameState.EditorHub);

        LoadIntoForm(NewDefault());
    }

    // ── Construção de UI ─────────────────────────────────────────

    private void PopulateRoleOptions()
    {
        if (_optRole == null) return;
        _optRole.Clear();
        foreach (PlayerRole.RoleType t in Enum.GetValues(typeof(PlayerRole.RoleType)))
            _optRole.AddItem(RoleLabel(t), (int)t);
    }

    private void BuildSliders()
    {
        if (_attrContainer == null) return;
        foreach (Node c in _attrContainer.GetChildren()) c.QueueFree();
        _sliders.Clear();
        _valueLabels.Clear();

        foreach (var attr in FieldAttrs) AddSliderRow(attr);
        AddSeparator();
        foreach (var attr in GkAttrs) AddSliderRow(attr);
    }

    private void AddSliderRow(string attr)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);

        var name = new Label
        {
            Text                = AttrLabels.GetValueOrDefault(attr, attr),
            CustomMinimumSize   = new Vector2(140, 0),
        };
        name.AddThemeFontSizeOverride("font_size", 14);

        var slider = new HSlider
        {
            MinValue            = 25,
            MaxValue            = 99,
            Step                = 1,
            Value               = 70,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize   = new Vector2(0, 24),
        };

        var val = new Label
        {
            Text              = "70",
            CustomMinimumSize = new Vector2(36, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        val.AddThemeFontSizeOverride("font_size", 14);

        slider.ValueChanged += v =>
        {
            val.Text = ((int)v).ToString();
            UpdateOverall();
        };

        row.AddChild(name);
        row.AddChild(slider);
        row.AddChild(val);
        _attrContainer.AddChild(row);

        _sliders[attr]      = slider;
        _valueLabels[attr]  = val;
    }

    private void AddSeparator()
    {
        var sep = new HSeparator();
        _attrContainer.AddChild(sep);
    }

    // ── Lógica ───────────────────────────────────────────────────

    private PlayerRole.RoleType SelectedRole()
        => (PlayerRole.RoleType)(_optRole?.GetSelectedId() ?? (int)PlayerRole.RoleType.CentralMid);

    private void OnGeneratePressed()
    {
        var role = new PlayerRole { Type = SelectedRole() };
        int ovr  = 70;
        // Usa o gerador para distribuir atributos conforme a função
        var data = PlayerGenerator.Create(role, ovr);
        // Mantém identidade já digitada
        data.FullName    = _editFullName?.Text ?? "";
        data.ShortName   = _editShortName?.Text ?? "";
        data.Nationality = _editNationality?.Text ?? "";
        data.Age         = (int)(_spinAge?.Value ?? 23);
        LoadIntoForm(data);
        ShowStatus("Atributos gerados para a função selecionada.");
    }

    private void OnSavePressed()
    {
        var data = ReadForm();
        if (string.IsNullOrWhiteSpace(data.ShortName))
        {
            ShowStatus("Informe ao menos um nome curto antes de salvar.");
            return;
        }
        string path = CustomContent.SavePlayer(data);
        RefreshSavedList();
        ShowStatus($"Salvo: {path}");
    }

    private void OnLoadPressed()
    {
        int idx = _optSaved?.Selected ?? -1;
        if (idx < 0 || idx >= _saved.Count)
        {
            ShowStatus("Nenhum jogador salvo selecionado.");
            return;
        }
        LoadIntoForm(_saved[idx]);
        ShowStatus($"Carregado: {_saved[idx].ShortName}");
    }

    private void OnDeletePressed()
    {
        int idx = _optSaved?.Selected ?? -1;
        if (idx < 0 || idx >= _saved.Count)
        {
            ShowStatus("Nenhum jogador salvo selecionado.");
            return;
        }
        string name = _saved[idx].ShortName;
        CustomContent.DeletePlayer(_saved[idx].PlayerId);
        RefreshSavedList();
        ShowStatus($"Excluído: {name}");
    }

    private void OnNewPressed()
    {
        LoadIntoForm(NewDefault());
        ShowStatus("Novo jogador.");
    }

    // ── Conversão form <-> PlayerData ────────────────────────────

    private static PlayerData NewDefault() => new()
    {
        FullName = "", ShortName = "", Nationality = "Brasil", Age = 23,
        Pace = 70, Shooting = 70, Passing = 70, Dribbling = 70,
        Defending = 70, Physical = 70, Heading = 70,
        Reflexes = 50, Diving = 50, GkPositioning = 50,
    };

    private void LoadIntoForm(PlayerData d)
    {
        if (_editFullName    != null) _editFullName.Text    = d.FullName;
        if (_editShortName   != null) _editShortName.Text   = d.ShortName;
        if (_editNationality != null) _editNationality.Text = d.Nationality;
        if (_spinAge         != null) _spinAge.Value         = d.Age;

        SetSlider("Pace", d.Pace);          SetSlider("Shooting", d.Shooting);
        SetSlider("Passing", d.Passing);    SetSlider("Dribbling", d.Dribbling);
        SetSlider("Defending", d.Defending);SetSlider("Physical", d.Physical);
        SetSlider("Heading", d.Heading);
        SetSlider("Reflexes", d.Reflexes);  SetSlider("Diving", d.Diving);
        SetSlider("GkPositioning", d.GkPositioning);

        UpdateOverall();
    }

    private PlayerData ReadForm()
    {
        var d = new PlayerData
        {
            FullName    = _editFullName?.Text ?? "",
            ShortName   = _editShortName?.Text ?? "",
            Nationality = _editNationality?.Text ?? "",
            Age         = (int)(_spinAge?.Value ?? 23),
            Pace        = Get("Pace"),      Shooting  = Get("Shooting"),
            Passing     = Get("Passing"),   Dribbling = Get("Dribbling"),
            Defending   = Get("Defending"), Physical  = Get("Physical"),
            Heading     = Get("Heading"),
            Reflexes    = Get("Reflexes"),  Diving    = Get("Diving"),
            GkPositioning = Get("GkPositioning"),
        };
        d.OverallRating = ComputeOverall(d);
        d.Potential     = Mathf.Max(d.OverallRating, d.Potential);
        d.MarketValue   = d.OverallRating * d.OverallRating * 150;
        return d;
    }

    private int ComputeOverall(PlayerData d)
        => SelectedRole() == PlayerRole.RoleType.Goalkeeper
            ? d.ComputeGkOverall()
            : d.ComputeOverall();

    private void UpdateOverall()
    {
        if (_lblOverall == null) return;
        var d = ReadForm();
        _lblOverall.Text = $"OVR {ComputeOverall(d)}";
    }

    private void SetSlider(string attr, int value)
    {
        if (_sliders.TryGetValue(attr, out var s)) s.Value = value;
        if (_valueLabels.TryGetValue(attr, out var l)) l.Text = value.ToString();
    }

    private int Get(string attr)
        => _sliders.TryGetValue(attr, out var s) ? (int)s.Value : 70;

    // ── Lista de salvos ──────────────────────────────────────────

    private void RefreshSavedList()
    {
        _saved = CustomContent.LoadAllPlayers();
        if (_optSaved == null) return;
        _optSaved.Clear();
        foreach (var p in _saved)
            _optSaved.AddItem($"{(p.ShortName.Length > 0 ? p.ShortName : p.PlayerId)} (OVR {p.OverallRating})");
    }

    private void ShowStatus(string msg)
    {
        if (_lblStatus != null) _lblStatus.Text = msg;
    }

    private static string RoleLabel(PlayerRole.RoleType t) => t switch
    {
        PlayerRole.RoleType.Goalkeeper   => "Goleiro",
        PlayerRole.RoleType.CenterBack   => "Zagueiro",
        PlayerRole.RoleType.FullBack     => "Lateral",
        PlayerRole.RoleType.DefensiveMid => "Volante",
        PlayerRole.RoleType.CentralMid   => "Meio-campista",
        PlayerRole.RoleType.AttackingMid => "Meia-atacante",
        PlayerRole.RoleType.Winger       => "Ponta",
        PlayerRole.RoleType.Striker      => "Atacante",
        _                                => t.ToString(),
    };
}
