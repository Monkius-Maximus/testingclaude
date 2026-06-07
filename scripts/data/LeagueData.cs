using Godot;
using Godot.Collections;

namespace FootballGame;

/// <summary>
/// Liga de futebol. Define prestígio (usado pelo multiplicador de XP do modo carreira),
/// país, divisão e clubes participantes. Esqueleto inicial — será expandido junto
/// com o sistema de carreira (ver docs/leagues_design.md).
/// </summary>
[GlobalClass]
public partial class LeagueData : Resource
{
    public enum PrestigeTier
    {
        SuperLeague    = 1, // Premier League, La Liga, Brasileirão A...
        EliteLeague    = 2, // Primeira Liga PT, Eredivisie, MLS...
        RegionalForce  = 3, // Liga Profesional AR, Liga MX...
        DevelopingLeague = 4 // Demais
    }

    [Export] public string         Name              = "";
    [Export] public string         Country           = "";
    [Export] public int            Division          = 1;
    [Export] public PrestigeTier   Tier              = PrestigeTier.DevelopingLeague;

    /// <summary>Multiplicador de experiência base. Premier League ≈ 1.0, divisão regional ≈ 0.3.</summary>
    [Export] public float          PrestigeMultiplier = 0.5f;

    /// <summary>IDs ou referências dos clubes participantes.</summary>
    [Export] public Array<string>  TeamIds            = new();

    /// <summary>Indica se a liga foi gerada automaticamente (clubes genéricos).</summary>
    [Export] public bool           IsGenerated        = false;
}
