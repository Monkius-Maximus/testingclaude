using Godot;
using System.Collections.Generic;

namespace FootballGame;

/// <summary>
/// Árbitro da partida. Detecta faltas por colisão de alta velocidade entre
/// adversários, emite sinais, gerencia cartões amarelos/vermelhos e
/// dispara penaltis quando a falta é na grande área.
/// </summary>
public partial class RefereeManager : Node
{
    [Signal] public delegate void FoulCommittedEventHandler(int foulerTeam, Vector3 position);
    [Signal] public delegate void PenaltyAwardedEventHandler(int fouledTeam);
    [Signal] public delegate void YellowCardEventHandler(string playerId, int team);
    [Signal] public delegate void RedCardEventHandler(string playerId, int team);

    // Velocidade mínima de impacto (m/s) para registrar falta
    private const float FoulSpeedThreshold = 4.5f;
    // Cooldown entre faltas do mesmo jogador (segundos)
    private const float FoulCooldown = 8f;

    private readonly Dictionary<string, int>   _yellowCards = new();
    private readonly Dictionary<string, float> _foulCooldowns = new();

    private MatchStats _stats;
    private AudioManager _audio;

    // Limites da grande área (absolutos, sem sinal de X)
    private const float PenaltyAreaDepth = 16.5f;
    private const float PenaltyAreaWidth = 20.16f; // metade = ±20.16

    public override void _Ready()
    {
        AddToGroup("referee");
        _stats = GetTree().GetFirstNodeInGroup("match_stats") as MatchStats;
        _audio = GetNodeOrNull<AudioManager>("/root/AudioManager");
    }

    public override void _Process(double delta)
    {
        // Decrementa cooldowns
        var keys = new List<string>(_foulCooldowns.Keys);
        foreach (var k in keys)
        {
            _foulCooldowns[k] -= (float)delta;
            if (_foulCooldowns[k] <= 0f) _foulCooldowns.Remove(k);
        }
    }

    /// <summary>
    /// Chamado pelo Player quando sofre um carrinho (PerformTackle colide com
    /// um oponente a alta velocidade). Registra a falta e emite sinal.
    /// </summary>
    public void ReportCollision(Player fouler, Player fouled, float impactSpeed)
    {
        if (impactSpeed < FoulSpeedThreshold) return;
        if (_foulCooldowns.ContainsKey(fouler.PlayerId)) return;

        _foulCooldowns[fouler.PlayerId] = FoulCooldown;

        _stats?.RegisterFoul(fouler.Team);
        _audio?.PlaySfx("whistle");

        var pos = fouled.GlobalPosition;

        // Verificar se está dentro de uma das grandes áreas
        bool inPenaltyArea = IsFoulInPenaltyArea(pos, fouled.Team);
        if (inPenaltyArea)
        {
            _audio?.PlaySfx("whistle_long");
            EmitSignal(SignalName.PenaltyAwarded, fouled.Team);
            GD.Print($"Pênalti! Time {fouled.Team}");
        }
        else
        {
            EmitSignal(SignalName.FoulCommitted, fouler.Team, pos);
        }

        // Decidir cartão
        float cardRoll = GD.Randf();
        bool dangerousFoul = impactSpeed > 7f;
        bool yellowChance  = dangerousFoul ? 0.65f : 0.2f;

        if (cardRoll < (dangerousFoul ? 0.15f : 0f))
            IssueRedCard(fouler);
        else if (cardRoll < yellowChance)
            IssueYellowCard(fouler);
    }

    // ── Cartões ───────────────────────────────────────────────────

    private void IssueYellowCard(Player player)
    {
        _audio?.PlaySfx("card");
        if (!_yellowCards.TryGetValue(player.PlayerId, out int count))
            count = 0;

        _yellowCards[player.PlayerId] = count + 1;
        _stats?.RegisterYellowCard(player.Team);
        EmitSignal(SignalName.YellowCard, player.PlayerId, player.Team);
        GD.Print($"Amarelo: {player.PlayerId}");

        if (_yellowCards[player.PlayerId] >= 2)
            IssueRedCard(player);
    }

    private void IssueRedCard(Player player)
    {
        _audio?.PlaySfx("card");
        _stats?.RegisterRedCard(player.Team);
        EmitSignal(SignalName.RedCard, player.PlayerId, player.Team);
        GD.Print($"Vermelho: {player.PlayerId} — expulso!");

        // Remove o jogador do campo
        player.QueueFree();
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static bool IsFoulInPenaltyArea(Vector3 pos, int fouledTeam)
    {
        // Área A (time 0 defende X < -52.5+16.5 = -36)
        // Área B (time 1 defende X >  52.5-16.5 =  36)
        float absX = Mathf.Abs(pos.X);
        bool inDepth = absX >= (52.5f - PenaltyAreaDepth);
        bool inWidth = Mathf.Abs(pos.Z) <= PenaltyAreaWidth;
        bool correctEnd = fouledTeam == 0 ? pos.X < 0 : pos.X > 0;
        return inDepth && inWidth && correctEnd;
    }

    public bool HasYellowCard(string playerId) => _yellowCards.GetValueOrDefault(playerId) >= 1;
    public bool HasRedCard(string playerId)    => _yellowCards.GetValueOrDefault(playerId) >= 2;
}
