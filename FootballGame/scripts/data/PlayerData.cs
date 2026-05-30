using Godot;

namespace FootballGame;

/// <summary>
/// Atributos individuais de um jogador. Resource salvo como .tres,
/// editável no Inspector e referenciado pelo TeamData.Squad.
/// Escala FIFA: 0–100. Usado pelo Player para física e pela IA para decisões.
/// </summary>
[GlobalClass]
public partial class PlayerData : Resource
{
    // ── Identidade ───────────────────────────────────────────────
    [Export] public string PlayerId    = "";
    [Export] public string FullName    = "";
    [Export] public string ShortName   = "";
    [Export] public int    Age         = 23;
    [Export] public string Nationality = "";

    // ── Atributos de campo (0–100) ───────────────────────────────
    /// <summary>Velocidade máxima e aceleração.</summary>
    [Export] public int Pace       = 70;
    /// <summary>Potência e precisão de chute.</summary>
    [Export] public int Shooting   = 70;
    /// <summary>Precisão e visão de passe.</summary>
    [Export] public int Passing    = 70;
    /// <summary>Controle de bola em drible.</summary>
    [Export] public int Dribbling  = 70;
    /// <summary>Interceptação, marcação e carrinho.</summary>
    [Export] public int Defending  = 70;
    /// <summary>Resistência física e força em duelos.</summary>
    [Export] public int Physical   = 70;
    /// <summary>Precisão de cabeceio.</summary>
    [Export] public int Heading    = 70;

    // ── Atributos exclusivos do goleiro ──────────────────────────
    [Export] public int Reflexes    = 50;
    [Export] public int Diving      = 50;
    [Export] public int GkPositioning = 50;

    // ── Meta (carreira) ──────────────────────────────────────────
    [Export] public int OverallRating = 70;
    [Export] public int Potential     = 75;
    [Export] public int MarketValue   = 500_000;

    // ── Helpers ──────────────────────────────────────────────────

    /// <summary>Recalcula OverallRating como média ponderada dos atributos de campo.</summary>
    public int ComputeOverall()
        => (Pace + Shooting + Passing + Dribbling + Defending + Physical + Heading) / 7;

    /// <summary>Overall para goleiros: troca Shooting/Defending por reflexos/diving.</summary>
    public int ComputeGkOverall()
        => (Reflexes + Diving + GkPositioning + Physical + Passing + Pace) / 6;
}
