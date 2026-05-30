using Godot;
using System;

namespace FootballGame;

/// <summary>
/// Gera <see cref="PlayerData"/> proceduralmente a partir de uma função e
/// um nível geral. Usado pelo <see cref="CareerManager"/> para times fantasma
/// e pelo <see cref="MatchBootstrap"/> como fallback quando TeamData.Squad
/// não está preenchido.
/// </summary>
public static class PlayerGenerator
{
    private static readonly Random _rng = new();

    /// <summary>Cria PlayerData para um papel e overall alvo (±5 de variação aleatória).</summary>
    public static PlayerData Create(PlayerRole role, int overall, string teamId = "", int slotIndex = 0)
    {
        overall = Mathf.Clamp(overall, 40, 99);
        var d = new PlayerData
        {
            PlayerId      = string.IsNullOrEmpty(teamId) ? $"p_{slotIndex:D2}" : $"{teamId}_{slotIndex:D2}",
            OverallRating = overall,
            Potential     = Mathf.Clamp(overall + _rng.Next(0, 16), 40, 99),
            MarketValue   = overall * overall * 150,
            Age           = _rng.Next(18, 35)
        };

        // Preenche base em overall e ajusta conforme o papel
        d.Pace      = Vary(overall);
        d.Shooting  = Vary(overall);
        d.Passing   = Vary(overall);
        d.Dribbling = Vary(overall);
        d.Defending = Vary(overall);
        d.Physical  = Vary(overall);
        d.Heading   = Vary(overall);

        if (role != null) ApplyRoleBoosts(d, role.Type, overall);

        d.OverallRating = d.ComputeOverall();
        return d;
    }

    // ── Distribuição de atributos por função ─────────────────────

    private static void ApplyRoleBoosts(PlayerData d, PlayerRole.RoleType type, int ovr)
    {
        switch (type)
        {
            case PlayerRole.RoleType.Goalkeeper:
                d.Reflexes      = Vary(ovr + 12);
                d.Diving        = Vary(ovr + 8);
                d.GkPositioning = Vary(ovr + 5);
                d.Passing       = Vary(ovr - 5);
                d.Shooting      = Vary(ovr - 30);
                d.Pace          = Vary(ovr - 18);
                d.Defending     = Vary(ovr + 5);
                d.Physical      = Vary(ovr + 8);
                d.OverallRating = d.ComputeGkOverall();
                break;

            case PlayerRole.RoleType.CenterBack:
            case PlayerRole.RoleType.FullBack:
                d.Defending = Vary(ovr + 14);
                d.Physical  = Vary(ovr + 10);
                d.Heading   = Vary(ovr + 10);
                d.Pace      = type == PlayerRole.RoleType.FullBack ? Vary(ovr + 8) : Vary(ovr - 4);
                d.Passing   = Vary(ovr + 2);
                d.Shooting  = Vary(ovr - 18);
                d.Dribbling = Vary(ovr - 8);
                break;

            case PlayerRole.RoleType.DefensiveMid:
                d.Defending = Vary(ovr + 10);
                d.Passing   = Vary(ovr + 8);
                d.Physical  = Vary(ovr + 8);
                d.Shooting  = Vary(ovr - 5);
                d.Pace      = Vary(ovr - 4);
                break;

            case PlayerRole.RoleType.CentralMid:
                d.Passing   = Vary(ovr + 12);
                d.Dribbling = Vary(ovr + 8);
                d.Defending = Vary(ovr + 4);
                d.Shooting  = Vary(ovr + 2);
                break;

            case PlayerRole.RoleType.AttackingMid:
                d.Passing   = Vary(ovr + 14);
                d.Dribbling = Vary(ovr + 12);
                d.Shooting  = Vary(ovr + 8);
                d.Defending = Vary(ovr - 14);
                d.Physical  = Vary(ovr - 4);
                break;

            case PlayerRole.RoleType.Winger:
                d.Pace      = Vary(ovr + 16);
                d.Dribbling = Vary(ovr + 14);
                d.Passing   = Vary(ovr + 4);
                d.Shooting  = Vary(ovr + 2);
                d.Defending = Vary(ovr - 16);
                d.Physical  = Vary(ovr - 6);
                break;

            case PlayerRole.RoleType.Striker:
                d.Shooting  = Vary(ovr + 16);
                d.Pace      = Vary(ovr + 10);
                d.Dribbling = Vary(ovr + 6);
                d.Heading   = Vary(ovr + 6);
                d.Physical  = Vary(ovr + 4);
                d.Defending = Vary(ovr - 20);
                d.Passing   = Vary(ovr - 4);
                break;
        }
    }

    private static int Vary(int base_)
        => Mathf.Clamp(base_ + _rng.Next(-5, 6), 25, 99);
}
