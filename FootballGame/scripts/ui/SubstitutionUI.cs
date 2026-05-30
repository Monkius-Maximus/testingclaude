using Godot;
using System.Collections.Generic;

namespace FootballGame;

/// <summary>
/// Overlay de substituição. Aparece a partir do menu de pausa.
/// Mostra jogadores em campo (esquerda) e banco (direita) para o time do jogador.
/// Seleção: clique em campo + clique no banco → confirmar.
/// </summary>
public partial class SubstitutionUI : CanvasLayer
{
    [Export] private VBoxContainer _fieldList;
    [Export] private VBoxContainer _benchList;
    [Export] private Button        _btnConfirm;
    [Export] private Button        _btnClose;
    [Export] private Label         _lblInfo;
    [Export] private Label         _lblSubsLeft;

    private SubstitutionManager _mgr;
    private Player              _selectedField;
    private int                 _selectedBenchIndex = -1;
    private int                 _userTeam           = 0;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 12;
        Hide();

        _mgr = GetTree().GetFirstNodeInGroup("substitution_manager") as SubstitutionManager;

        if (_btnConfirm != null) _btnConfirm.Pressed += OnConfirmPressed;
        if (_btnClose   != null) _btnClose.Pressed   += () => Hide();
    }

    public void OpenFor(int team)
    {
        _userTeam           = team;
        _selectedField      = null;
        _selectedBenchIndex = -1;
        RefreshLists();
        Show();
    }

    private void RefreshLists()
    {
        ClearContainer(_fieldList);
        ClearContainer(_benchList);

        if (_mgr == null) return;

        int rem = _mgr.SubsRemaining(_userTeam);
        if (_lblSubsLeft != null) _lblSubsLeft.Text = $"Substituições restantes: {rem}";

        foreach (var p in _mgr.GetFieldPlayers(_userTeam))
        {
            var btn = MakeButton($"{p.PlayerId}  (Pace {p.Pace} · Sht {p.Shooting})", _fieldList);
            var captured = p;
            btn.Pressed += () => { _selectedField = captured; UpdateInfo(); };
        }

        var bench = _mgr.GetBench(_userTeam);
        for (int i = 0; i < bench.Count; i++)
        {
            var data = bench[i];
            var btn  = MakeButton($"{data.ShortName ?? data.PlayerId}  OVR {data.OverallRating}", _benchList);
            int idx  = i;
            btn.Pressed += () => { _selectedBenchIndex = idx; UpdateInfo(); };
        }

        if (_btnConfirm != null) _btnConfirm.Disabled = rem <= 0;
    }

    private void UpdateInfo()
    {
        if (_lblInfo == null) return;
        string fieldTxt = _selectedField != null ? _selectedField.PlayerId : "—";
        string benchTxt = _selectedBenchIndex >= 0
            ? _mgr.GetBench(_userTeam)[_selectedBenchIndex].PlayerId
            : "—";
        _lblInfo.Text = $"Sai: {fieldTxt}  →  Entra: {benchTxt}";
    }

    private void OnConfirmPressed()
    {
        if (_selectedField == null || _selectedBenchIndex < 0) return;
        bool ok = _mgr?.MakeSub(_selectedField, _selectedBenchIndex) ?? false;
        if (ok) RefreshLists();
        else if (_lblInfo != null) _lblInfo.Text = "Sem substituições disponíveis!";
    }

    private static Button MakeButton(string label, VBoxContainer parent)
    {
        var btn = new Button { Text = label, ToggleMode = true };
        btn.AddThemeFontSizeOverride("font_size", 15);
        parent.AddChild(btn);
        return btn;
    }

    private static void ClearContainer(Container c)
    {
        if (c == null) return;
        foreach (Node child in c.GetChildren()) child.QueueFree();
    }
}
