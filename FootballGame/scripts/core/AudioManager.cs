using Godot;
using System.Collections.Generic;

namespace FootballGame;

/// <summary>
/// Autoload de áudio. Gerencia buses Master/Music/SFX/Crowd e carrega
/// streams da pasta assets/audio/ sob demanda. Trabalha silenciosamente
/// se os arquivos de áudio ainda não existirem — basta colocá-los nas
/// pastas corretas para que o som comece a funcionar sem mais código.
/// </summary>
public partial class AudioManager : Node
{
    // ── Buses ─────────────────────────────────────────────────────
    private const string BusMaster = "Master";
    private const string BusMusic  = "Music";
    private const string BusSfx    = "SFX";
    private const string BusCrowd  = "Crowd";

    // ── Pastas dos assets ─────────────────────────────────────────
    private const string MusicPath = "res://assets/audio/music/";
    private const string SfxPath   = "res://assets/audio/sfx/";

    // ── Players internos ─────────────────────────────────────────
    private AudioStreamPlayer _musicPlayer;
    private AudioStreamPlayer _crowdPlayer;
    private readonly List<AudioStreamPlayer> _sfxPool = new();
    private const int SfxPoolSize = 8;

    // Mapeamento de chave → arquivo SFX esperado
    private static readonly Dictionary<string, string> SfxFiles = new()
    {
        { "kick",         "kick.wav"      },
        { "goal",         "goal.ogg"      },
        { "whistle",      "whistle.wav"   },
        { "whistle_long", "whistle_long.wav" },
        { "card",         "card.wav"      },
        { "crowd_goal",   "crowd_goal.ogg"},
        { "save",         "save.wav"      },
        { "tackle",       "tackle.wav"    },
    };

    private static readonly Dictionary<string, string> MusicFiles = new()
    {
        { "menu",     "menu_theme.ogg"     },
        { "match",    "match_ambient.ogg"  },
        { "halftime", "halftime.ogg"       },
        { "victory",  "victory.ogg"        },
        { "defeat",   "defeat.ogg"         },
    };

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        EnsureBuses();

        _musicPlayer = MakePlayer(BusMusic);
        _musicPlayer.Name = "MusicPlayer";
        AddChild(_musicPlayer);

        _crowdPlayer = MakePlayer(BusCrowd);
        _crowdPlayer.Name = "CrowdPlayer";
        AddChild(_crowdPlayer);

        for (int i = 0; i < SfxPoolSize; i++)
        {
            var p = MakePlayer(BusSfx);
            p.Name = $"Sfx_{i}";
            AddChild(p);
            _sfxPool.Add(p);
        }
    }

    // ── API pública ───────────────────────────────────────────────

    /// <summary>Toca um SFX pela chave (kick, goal, whistle, etc.).</summary>
    public void PlaySfx(string key)
    {
        if (!SfxFiles.TryGetValue(key, out string file)) return;
        var stream = TryLoad(SfxPath + file);
        if (stream == null) return;

        // Usa o primeiro player livre do pool
        foreach (var p in _sfxPool)
        {
            if (!p.Playing) { p.Stream = stream; p.Play(); return; }
        }
        // Pool esgotado: interrompe o mais antigo
        _sfxPool[0].Stream = stream;
        _sfxPool[0].Play();
    }

    /// <summary>Inicia uma faixa de música pelo nome-chave.</summary>
    public void PlayMusic(string key, bool loop = true)
    {
        if (!MusicFiles.TryGetValue(key, out string file)) return;
        var stream = TryLoad(MusicPath + file);
        if (stream == null) return;

        if (stream is AudioStreamOggVorbis ogg)   ogg.Loop   = loop;
        if (stream is AudioStreamWav wav)          wav.LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled;

        _musicPlayer.Stream = stream;
        _musicPlayer.Play();
    }

    public void StopMusic() => _musicPlayer.Stop();

    /// <summary>
    /// Ajusta a intensidade da torcida (0 = silêncio, 1 = estádio cheio).
    /// Também carrega e inicia o loop de torcida se não estiver tocando.
    /// </summary>
    public void SetCrowdIntensity(float intensity)
    {
        int bus = AudioServer.GetBusIndex(BusCrowd);
        if (bus < 0) return;
        AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(Mathf.Clamp(intensity, 0f, 1f)));

        if (!_crowdPlayer.Playing && intensity > 0.01f)
        {
            var stream = TryLoad(SfxPath + "crowd_loop.ogg");
            if (stream != null) { _crowdPlayer.Stream = stream; _crowdPlayer.Play(); }
        }
    }

    /// <summary>Aplica volume (0–1) a um bus pelo nome.</summary>
    public void SetBusVolume(string busName, float linear)
    {
        int idx = AudioServer.GetBusIndex(busName);
        if (idx >= 0) AudioServer.SetBusVolumeDb(idx, Mathf.LinearToDb(Mathf.Clamp(linear, 0f, 1f)));
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static AudioStream TryLoad(string path)
    {
        if (!ResourceLoader.Exists(path)) return null;
        return GD.Load<AudioStream>(path);
    }

    private static AudioStreamPlayer MakePlayer(string bus)
        => new() { Bus = bus, VolumeDb = 0f };

    private static void EnsureBuses()
    {
        string[] needed = { BusMusic, BusSfx, BusCrowd };
        foreach (string name in needed)
        {
            if (AudioServer.GetBusIndex(name) >= 0) continue;
            int idx = AudioServer.BusCount;
            AudioServer.AddBus(idx);
            AudioServer.SetBusName(idx, name);
            AudioServer.SetBusSendName(idx, BusMaster);
        }
    }
}
