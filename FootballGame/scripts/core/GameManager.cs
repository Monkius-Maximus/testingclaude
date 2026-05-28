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
        // Configurações globais aqui (volume, idioma, etc).
        GD.Print("GameManager pronto.");
    }
}
