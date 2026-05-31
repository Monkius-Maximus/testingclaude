using Godot;
using System.Collections.Generic;

namespace FootballGame;

/// <summary>
/// Editor in-game de estádios. Exibe um preview 2D top-down interativo
/// com 12 slots clicáveis (tribunes, cantos, holofotes) e permite configurar
/// identidade, cores e salvar/carregar em user://custom/stadiums/*.tres.
/// </summary>
public partial class StadiumEditorUI : Control
{
    // ── Exports (conectados na cena) ─────────────────────────────
    [Export] private LineEdit          _editName;
    [Export] private LineEdit          _editCity;
    [Export] private SpinBox           _spinYear;
    [Export] private ColorPickerButton _pickPitch;
    [Export] private ColorPickerButton _pickStand;
    [Export] private StadiumPreviewUI  _preview;
    [Export] private Label             _lblCapacity;
    [Export] private Label             _lblSlotInfo;
    [Export] private OptionButton      _optSaved;
    [Export] private Button            _btnPresetEmpty;
    [Export] private Button            _btnPresetSmall;
    [Export] private Button            _btnPresetMedium;
    [Export] private Button            _btnPresetLarge;
    [Export] private Button            _btnSelectAll;
    [Export] private Button            _btnDeselectAll;
    [Export] private Button            _btnNew;
    [Export] private Button            _btnSave;
    [Export] private Button            _btnLoad;
    [Export] private Button            _btnDelete;
    [Export] private Button            _btnBack;
    [Export] private Label             _lblStatus;

    // ── Presets (SlotMask) ───────────────────────────────────────
    private const int PresetEmpty  = 0;
    private const int PresetSmall  = 0b0000_1111;         // 4 tribunes
    private const int PresetMedium = 0b1111_1111;         // + 4 cantos
    private const int PresetLarge  = 0b1111_1111_1111;    // tudo

    private StadiumData          _current;
    private List<StadiumData>    _saved = new();
    private GameStateManager     _gsm;

    public override void _Ready()
    {
        _gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");

        // Preview
        if (_preview != null)
        {
            _preview.SlotToggled += OnSlotToggled;
            _preview.SlotHovered += OnSlotHovered;
        }

        // Cores
        if (_pickPitch != null) _pickPitch.ColorChanged += _ => { ReadFormColors(); Redraw(); };
        if (_pickStand != null) _pickStand.ColorChanged += _ => { ReadFormColors(); Redraw(); };

        // Presets
        if (_btnPresetEmpty  != null) _btnPresetEmpty.Pressed  += () => ApplyPreset(PresetEmpty);
        if (_btnPresetSmall  != null) _btnPresetSmall.Pressed  += () => ApplyPreset(PresetSmall);
        if (_btnPresetMedium != null) _btnPresetMedium.Pressed += () => ApplyPreset(PresetMedium);
        if (_btnPresetLarge  != null) _btnPresetLarge.Pressed  += () => ApplyPreset(PresetLarge);

        // Seleção em massa
        if (_btnSelectAll   != null) _btnSelectAll.Pressed   += () => ApplyPreset(PresetLarge);
        if (_btnDeselectAll != null) _btnDeselectAll.Pressed += () => ApplyPreset(PresetEmpty);

        // CRUD
        if (_btnNew    != null) _btnNew.Pressed    += OnNew;
        if (_btnSave   != null) _btnSave.Pressed   += OnSave;
        if (_btnLoad   != null) _btnLoad.Pressed   += OnLoad;
        if (_btnDelete != null) _btnDelete.Pressed += OnDelete;
        if (_btnBack   != null) _btnBack.Pressed   += () => _gsm?.GoTo(GameStateManager.GameState.EditorHub);

        RefreshSavedList();
        LoadIntoForm(NewDefault());
    }

    // ── Slots ────────────────────────────────────────────────────

    private void OnSlotToggled(int idx)
    {
        _current.ToggleSlot((StadiumSlot)idx);
        RefreshCapacity();
        Redraw();
    }

    private void OnSlotHovered(int idx)
    {
        if (_lblSlotInfo == null) return;
        _lblSlotInfo.Text = idx >= 0
            ? $"{StadiumData.SlotNames[idx]}  —  {(_current.HasSlot((StadiumSlot)idx) ? "ativo" : "inativo")}  (clique para alternar)"
            : "Clique em uma seção para ativá-la ou desativá-la.";
    }

    private void ApplyPreset(int mask)
    {
        _current.SlotMask = mask;
        RefreshCapacity();
        Redraw();
    }

    // ── Formulário ───────────────────────────────────────────────

    private static StadiumData NewDefault() => new()
    {
        Name       = "Novo Estádio",
        City       = "",
        BuiltYear  = 2000,
        PitchColor = new Color(0.13f, 0.55f, 0.13f),
        StandColor = new Color(0.28f, 0.30f, 0.36f),
        SlotMask   = PresetSmall,
    };

    private void LoadIntoForm(StadiumData d)
    {
        _current = d;
        if (_editName  != null) _editName.Text  = d.Name;
        if (_editCity  != null) _editCity.Text  = d.City;
        if (_spinYear  != null) _spinYear.Value = d.BuiltYear;
        if (_pickPitch != null) _pickPitch.Color = d.PitchColor;
        if (_pickStand != null) _pickStand.Color = d.StandColor;
        if (_preview   != null) _preview.Stadium = d;
        RefreshCapacity();
        Redraw();
        if (_lblSlotInfo != null) _lblSlotInfo.Text = "Clique em uma seção para ativá-la ou desativá-la.";
    }

    private void ReadForm()
    {
        _current.Name      = _editName?.Text ?? _current.Name;
        _current.City      = _editCity?.Text ?? _current.City;
        _current.BuiltYear = (int)(_spinYear?.Value ?? _current.BuiltYear);
    }

    private void ReadFormColors()
    {
        if (_pickPitch != null) _current.PitchColor = _pickPitch.Color;
        if (_pickStand != null) _current.StandColor = _pickStand.Color;
    }

    private void RefreshCapacity()
    {
        if (_lblCapacity == null) return;
        int cap = _current.ComputeCapacity();
        int active = 0;
        for (int i = 0; i < StadiumData.SlotCount; i++)
            if (_current.HasSlot((StadiumSlot)i)) active++;
        _lblCapacity.Text = cap > 0
            ? $"Capacidade estimada: {cap:N0} torcedores  ·  {active} seções ativas"
            : "Estádio vazio — ative tribunes no preview à direita.";
    }

    private void Redraw() => _preview?.QueueRedraw();

    // ── CRUD ─────────────────────────────────────────────────────

    private void OnNew()
    {
        LoadIntoForm(NewDefault());
        ShowStatus("Novo estádio.");
    }

    private void OnSave()
    {
        ReadForm();
        ReadFormColors();
        if (string.IsNullOrWhiteSpace(_current.Name))
        {
            ShowStatus("Informe o nome do estádio antes de salvar.");
            return;
        }
        string path = CustomContent.SaveStadium(_current);
        RefreshSavedList();
        ShowStatus($"Salvo: {path}");
    }

    private void OnLoad()
    {
        int idx = _optSaved?.Selected ?? -1;
        if (idx < 0 || idx >= _saved.Count) { ShowStatus("Nenhum estádio salvo selecionado."); return; }
        LoadIntoForm((StadiumData)_saved[idx].Duplicate(true));
        ShowStatus($"Carregado: {_current.Name}");
    }

    private void OnDelete()
    {
        int idx = _optSaved?.Selected ?? -1;
        if (idx < 0 || idx >= _saved.Count) { ShowStatus("Nenhum estádio salvo selecionado."); return; }
        string name = _saved[idx].Name;
        CustomContent.DeleteStadium(_saved[idx].StadiumId);
        RefreshSavedList();
        ShowStatus($"Excluído: {name}");
    }

    private void RefreshSavedList()
    {
        _saved = CustomContent.LoadAllStadiums();
        if (_optSaved == null) return;
        _optSaved.Clear();
        foreach (var s in _saved)
            _optSaved.AddItem($"{(s.Name.Length > 0 ? s.Name : s.StadiumId)} ({s.ComputeCapacity():N0})");
    }

    private void ShowStatus(string msg)
    {
        if (_lblStatus != null) _lblStatus.Text = msg;
    }
}
