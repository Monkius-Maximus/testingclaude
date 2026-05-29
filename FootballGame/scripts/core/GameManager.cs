using Godot;

namespace FootballGame;

/// <summary>
/// Singleton de alto nível do jogo. Hospeda estado persistente entre cenas:
/// modo (amistoso, carreira), times selecionados, configurações, etc.
/// Recomendado registrar como Autoload em Project Settings → Autoload.
/// </summary>
public partial class GameManager : Node
{
    public enum MatchMode { Friendly, League, Cup, Career }

    public MatchMode CurrentMode { get; set; } = MatchMode.Friendly;

    public TeamData HomeTeam { get; set; }
    public TeamData AwayTeam { get; set; }

    /// <summary>Multiplicador atual de XP (preenchido conforme a competição em curso).</summary>
    public float ExperienceMultiplier { get; set; } = 1f;

    public override void _Ready()
    {
        ApplySavedSettings();
        GD.Print("GameManager pronto.");
    }

    /// <summary>
    /// Carrega <c>user://settings.cfg</c> no boot e aplica áudio, vsync e modo
    /// de janela. Mantém as preferências do jogador entre sessões. A tela de
    /// Settings escreve neste mesmo arquivo.
    /// </summary>
    private void ApplySavedSettings()
    {
        var cfg = new ConfigFile();
        if (cfg.Load("user://settings.cfg") != Error.Ok) return;

        float master = (float)cfg.GetValue("audio", "master_volume", 1.0f);
        int masterBus = AudioServer.GetBusIndex("Master");
        if (masterBus >= 0)
            AudioServer.SetBusVolumeDb(masterBus, Mathf.LinearToDb(master));

        bool vsync = (bool)cfg.GetValue("video", "vsync", true);
        DisplayServer.WindowSetVsyncMode(vsync
            ? DisplayServer.VSyncMode.Enabled
            : DisplayServer.VSyncMode.Disabled);

        int windowMode = (int)cfg.GetValue("video", "window_mode", 0);
        DisplayServer.WindowSetMode(windowMode switch
        {
            1 => DisplayServer.WindowMode.Fullscreen,
            2 => DisplayServer.WindowMode.ExclusiveFullscreen,
            _ => DisplayServer.WindowMode.Windowed
        });
    }
}
