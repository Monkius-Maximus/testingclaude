using Godot;
using Godot.Collections;

namespace FootballGame;

/// <summary>
/// Tela de configurações com três seções: Vídeo, Áudio e Controles.
/// Persiste valores em <c>user://settings.cfg</c>.
/// </summary>
public partial class SettingsUI : Control
{
    private static readonly string[] Resolutions = { "1280x720", "1920x1080", "2560x1440", "3840x2160" };
    private static readonly Vector2I[] ResolutionSizes =
    {
        new(1280,  720),
        new(1920, 1080),
        new(2560, 1440),
        new(3840, 2160)
    };

    // ── Video ─────────────────────────────────────────────────────
    [Export] private OptionButton _resolutionOption;
    [Export] private OptionButton _windowModeOption;
    [Export] private CheckButton  _vsyncCheck;
    [Export] private OptionButton _qualityOption;

    // ── Audio ─────────────────────────────────────────────────────
    [Export] private HSlider _masterSlider;
    [Export] private HSlider _musicSlider;
    [Export] private HSlider _sfxSlider;
    [Export] private Label   _lblMaster;
    [Export] private Label   _lblMusic;
    [Export] private Label   _lblSfx;

    // ── Controls ─────────────────────────────────────────────────
    [Export] private VBoxContainer _controlsContainer;
    [Export] private Button        _btnResetControls;

    // ── Navigation ────────────────────────────────────────────────
    [Export] private Button _btnSave;
    [Export] private Button _btnBack;

    private readonly ConfigFile _cfg = new();

    private int   _windowMode    = 0;
    private int   _resolutionIdx = 1;   // 1920x1080 default
    private bool  _vsync         = true;
    private int   _qualityIdx    = 1;   // Medium
    private float _masterVolume  = 1f;
    private float _musicVolume   = 0.8f;
    private float _sfxVolume     = 1f;

    public override void _Ready()
    {
        LoadConfig();
        PopulateVideo();
        PopulateAudio();
        PopulateControls();

        if (_btnSave          != null) _btnSave.Pressed         += OnSavePressed;
        if (_btnBack          != null) _btnBack.Pressed         += OnBackPressed;
        if (_btnResetControls != null) _btnResetControls.Pressed += OnResetControlsPressed;

        if (_masterSlider != null) _masterSlider.ValueChanged += v => { _masterVolume = (float)v; UpdateVolumeLabels(); };
        if (_musicSlider  != null) _musicSlider.ValueChanged  += v => { _musicVolume  = (float)v; UpdateVolumeLabels(); };
        if (_sfxSlider    != null) _sfxSlider.ValueChanged    += v => { _sfxVolume    = (float)v; UpdateVolumeLabels(); };
    }

    private void LoadConfig()
    {
        if (_cfg.Load("user://settings.cfg") != Error.Ok) return;

        _resolutionIdx = (int)_cfg.GetValue("video", "resolution_idx", 1);
        _windowMode    = (int)_cfg.GetValue("video", "window_mode",    0);
        _vsync         = (bool)_cfg.GetValue("video", "vsync",         true);
        _qualityIdx    = (int)_cfg.GetValue("video", "quality",        1);
        _masterVolume  = (float)_cfg.GetValue("audio", "master_volume", 1.0f);
        _musicVolume   = (float)_cfg.GetValue("audio", "music_volume",  0.8f);
        _sfxVolume     = (float)_cfg.GetValue("audio", "sfx_volume",    1.0f);
    }

    private void PopulateVideo()
    {
        if (_resolutionOption != null)
        {
            _resolutionOption.Clear();
            foreach (var r in Resolutions)
                _resolutionOption.AddItem(r);
            _resolutionOption.Selected = Mathf.Clamp(_resolutionIdx, 0, Resolutions.Length - 1);
            _resolutionOption.ItemSelected += idx => _resolutionIdx = (int)idx;
        }

        if (_windowModeOption != null)
        {
            _windowModeOption.Clear();
            _windowModeOption.AddItem("Janela");
            _windowModeOption.AddItem("Tela Cheia");
            _windowModeOption.AddItem("Sem Borda");
            _windowModeOption.Selected = Mathf.Clamp(_windowMode, 0, 2);
            _windowModeOption.ItemSelected += idx => _windowMode = (int)idx;
        }

        if (_vsyncCheck != null)
        {
            _vsyncCheck.ButtonPressed = _vsync;
            _vsyncCheck.Toggled += v => _vsync = v;
        }

        if (_qualityOption != null)
        {
            _qualityOption.Clear();
            _qualityOption.AddItem("Baixo");
            _qualityOption.AddItem("Médio");
            _qualityOption.AddItem("Alto");
            _qualityOption.Selected = Mathf.Clamp(_qualityIdx, 0, 2);
            _qualityOption.ItemSelected += idx => _qualityIdx = (int)idx;
        }
    }

    private void PopulateAudio()
    {
        if (_masterSlider != null) { _masterSlider.MinValue = 0; _masterSlider.MaxValue = 1; _masterSlider.Step = 0.01; _masterSlider.Value = _masterVolume; }
        if (_musicSlider  != null) { _musicSlider.MinValue  = 0; _musicSlider.MaxValue  = 1; _musicSlider.Step = 0.01; _musicSlider.Value  = _musicVolume; }
        if (_sfxSlider    != null) { _sfxSlider.MinValue    = 0; _sfxSlider.MaxValue    = 1; _sfxSlider.Step   = 0.01; _sfxSlider.Value    = _sfxVolume; }
        UpdateVolumeLabels();
    }

    private void UpdateVolumeLabels()
    {
        if (_lblMaster != null) _lblMaster.Text = $"{Mathf.RoundToInt(_masterVolume * 100)}%";
        if (_lblMusic  != null) _lblMusic.Text  = $"{Mathf.RoundToInt(_musicVolume  * 100)}%";
        if (_lblSfx    != null) _lblSfx.Text    = $"{Mathf.RoundToInt(_sfxVolume    * 100)}%";
    }

    private void PopulateControls()
    {
        if (_controlsContainer == null) return;

        // Limpa filhos antigos
        foreach (Node child in _controlsContainer.GetChildren())
            child.QueueFree();

        string[] actions = { "p1_forward", "p1_back", "p1_left", "p1_right",
                              "p1_kick", "p1_pass", "p1_tackle", "p1_sprint",
                              "p1_switch_player", "p1_team_press", "p1_pause" };

        foreach (var action in actions)
        {
            if (!InputMap.HasAction(action)) continue;

            var row = new HBoxContainer();
            var lbl = new Label { Text = action.Replace("p1_", "").Replace("_", " ").ToUpper() };
            lbl.CustomMinimumSize = new Vector2(160, 0);

            var events = InputMap.ActionGetEvents(action);
            string keyText = events.Count > 0 ? events[0].AsText() : "–";
            var keyLbl = new Label { Text = keyText };

            row.AddChild(lbl);
            row.AddChild(keyLbl);
            _controlsContainer.AddChild(row);
        }
    }

    private void OnSavePressed()
    {
        _cfg.SetValue("video", "resolution_idx", _resolutionIdx);
        _cfg.SetValue("video", "window_mode",    _windowMode);
        _cfg.SetValue("video", "vsync",          _vsync);
        _cfg.SetValue("video", "quality",        _qualityIdx);
        _cfg.SetValue("audio", "master_volume",  _masterVolume);
        _cfg.SetValue("audio", "music_volume",   _musicVolume);
        _cfg.SetValue("audio", "sfx_volume",     _sfxVolume);
        _cfg.Save("user://settings.cfg");

        ApplySettings();
        OnBackPressed();
    }

    private void ApplySettings()
    {
        // Áudio
        int masterBus = AudioServer.GetBusIndex("Master");
        if (masterBus >= 0)
            AudioServer.SetBusVolumeDb(masterBus, Mathf.LinearToDb(_masterVolume));

        // Vsync
        DisplayServer.WindowSetVsyncMode(_vsync
            ? DisplayServer.VSyncMode.Enabled
            : DisplayServer.VSyncMode.Disabled);

        // Modo janela
        DisplayServer.WindowSetMode(_windowMode switch
        {
            1 => DisplayServer.WindowMode.Fullscreen,
            2 => DisplayServer.WindowMode.ExclusiveFullscreen,
            _ => DisplayServer.WindowMode.Windowed
        });

        // Resolução (apenas no modo janela)
        if (_windowMode == 0 && _resolutionIdx >= 0 && _resolutionIdx < ResolutionSizes.Length)
            DisplayServer.WindowSetSize(ResolutionSizes[_resolutionIdx]);
    }

    private void OnResetControlsPressed()
    {
        InputMap.LoadFromProjectSettings();
        PopulateControls();
    }

    private void OnBackPressed()
    {
        var gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");
        gsm?.ReturnFromSettings();
    }
}
