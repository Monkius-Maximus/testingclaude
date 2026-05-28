using Godot;

namespace FootballGame;

/// <summary>
/// Papel tático que um jogador ocupa em campo. Resource para que
/// instâncias possam ser criadas como `.tres` e atribuídas a jogadores
/// no editor — permite trocar um zagueiro de papel sem mexer no jogador.
/// </summary>
[GlobalClass]
public partial class PlayerRole : Resource
{
    public enum RoleType
    {
        Goalkeeper,
        CenterBack,
        FullBack,
        DefensiveMid,
        CentralMid,
        AttackingMid,
        Winger,
        Striker
    }

    [Export] public RoleType Type        = RoleType.CentralMid;
    [Export] public string   DisplayName = "Volante";

    /// <summary>Posição base relativa ao campo, normalizada (-1..1 em ambos eixos).</summary>
    [Export] public Vector2 BasePosition = Vector2.Zero;

    /// <summary>Raio em metros dentro do qual o jogador prioriza ficar.</summary>
    [Export] public float ActionRadius = 12f;

    // Pesos comportamentais (0..1) — consumidos pelo AIBrain
    [Export] public float DefensiveBias  = 0.5f;
    [Export] public float Aggressiveness = 0.5f;
    [Export] public float StaminaUsage   = 0.7f;

    /// <summary>Apenas o goleiro tem <c>true</c>. Regra também checada pela posição em campo.</summary>
    [Export] public bool CanUseHands = false;
}
