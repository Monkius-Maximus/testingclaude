using Godot;
using System.Collections.Generic;

namespace FootballGame;

/// <summary>
/// Detecta faltas a partir de tentativas de desarme e gradua cartões.
/// NÃO mexe na física: apenas decide e publica eventos no <see cref="MatchEventBus"/>.
/// A reposição da bola (tiro livre / pênalti) é responsabilidade de quem escuta
/// o evento de falta — ver <see cref="RulesManager"/>.
/// </summary>
public partial class FoulSystem : Node
{
    [Export] private NodePath _busPath;
    [Export] private NodePath _matchClockPath;
    [Export] private NodePath _teamAPath;
    [Export] private NodePath _teamBPath;

    // ── Parâmetros ajustáveis no editor ──────────────────────────
    [Export] public float TackleRadius   = 1.8f;  // alcance do desarme (m)
    [Export] public float DangerousSpeed = 9.0f;  // m/s acima disso → risco de cartão

    private MatchEventBus  _bus;
    private MatchClock     _clock;
    private TeamController _teamA;
    private TeamController _teamB;

    // Estado: amarelos por jogador e quem já foi expulso
    private readonly Dictionary<string, int> _yellowCards = new();
    private readonly HashSet<string>         _sentOff     = new();

    public override void _Ready()
    {
        _bus   = GetNode<MatchEventBus>(_busPath);
        _clock = GetNodeOrNull<MatchClock>(_matchClockPath);
        _teamA = GetNode<TeamController>(_teamAPath);
        _teamB = GetNode<TeamController>(_teamBPath);

        // Conecta às tentativas de desarme depois que todos os jogadores
        // já entraram na árvore.
        CallDeferred(MethodName.ConnectToPlayers);
    }

    private void ConnectToPlayers()
    {
        foreach (var node in GetTree().GetNodesInGroup("players"))
            if (node is Player p)
                p.TackleAttempted += OnTackleAttempted;
    }

    // ─────────────────────────────────────────────────────────────
    // Entrada: alguém tentou um desarme
    // ─────────────────────────────────────────────────────────────
    private void OnTackleAttempted(Player tackler)
    {
        if (_sentOff.Contains(tackler.PlayerId)) return;

        var victim = FindTackleVictim(tackler);
        if (victim == null)   return;  // desarme no vazio
        if (!victim.HasBall)  return;  // só resolvemos sobre o portador da bola

        ResolveTackle(tackler, victim);
    }

    private void ResolveTackle(Player tackler, Player victim)
    {
        float skill       = tackler.Tackling / 100f;
        bool  fromBehind  = IsFromBehind(tackler, victim);
        float relSpeed    = (tackler.Velocity - victim.Velocity).Length();
        float speedFactor = Mathf.Clamp(relSpeed / 12f, 0f, 1f);

        // Chance de desarme limpo: habilidade penalizada por vir de trás e por velocidade
        float cleanChance = skill
                          * (fromBehind ? 0.6f : 1.0f)
                          * (1f - 0.4f * speedFactor);

        if (GD.Randf() < cleanChance)
        {
            // Limpo: transfere a posse, sem falta
            victim.HasBall  = false;
            tackler.HasBall = true;
            return;
        }

        // É falta
        int minute = _clock?.CurrentMinute ?? 0;
        var pos    = victim.GlobalPosition;

        _bus.Publish(MatchEvent.Foul(minute, tackler.Team, tackler.PlayerId, victim.PlayerId, pos));

        // Graduação do cartão
        bool dangerous = fromBehind || relSpeed > DangerousSpeed;
        bool lastMan   = IsDenyingClearChance(tackler, victim);

        if (lastMan)
            IssueRedCard(tackler, minute, pos);     // DOGSO → vermelho direto
        else if (dangerous)
            IssueYellowCard(tackler, minute, pos);
        // senão: falta simples, sem cartão
    }

    // ─────────────────────────────────────────────────────────────
    // Cartões
    // ─────────────────────────────────────────────────────────────
    private void IssueYellowCard(Player player, int minute, Vector3 pos)
    {
        int count = _yellowCards.GetValueOrDefault(player.PlayerId, 0) + 1;
        _yellowCards[player.PlayerId] = count;

        _bus.Publish(MatchEvent.Card(MatchEventType.YellowCard, minute, player.Team, player.PlayerId, pos));

        if (count >= 2)
            IssueRedCard(player, minute, pos); // segundo amarelo → vermelho
    }

    private void IssueRedCard(Player player, int minute, Vector3 pos)
    {
        if (_sentOff.Contains(player.PlayerId)) return;
        _sentOff.Add(player.PlayerId);

        _bus.Publish(MatchEvent.Card(MatchEventType.RedCard, minute, player.Team, player.PlayerId, pos));
        // A remoção física do jogador é feita pelo TeamController, que escuta o evento.
    }

    // ─────────────────────────────────────────────────────────────
    // Heurísticas
    // ─────────────────────────────────────────────────────────────
    private Player FindTackleVictim(Player tackler)
    {
        var opponents = tackler.Team == 0 ? _teamB.Players : _teamA.Players;
        Player best = null;
        float minDist = TackleRadius * TackleRadius;

        foreach (var o in opponents)
        {
            if (_sentOff.Contains(o.PlayerId)) continue;
            float d = o.GlobalPosition.DistanceSquaredTo(tackler.GlobalPosition);
            if (d < minDist) { minDist = d; best = o; }
        }
        return best;
    }

    /// <summary>Desarme "por trás" = tackler está atrás da direção que a vítima encara.</summary>
    private bool IsFromBehind(Player tackler, Player victim)
    {
        var victimForward = -victim.GlobalTransform.Basis.Z;
        var toTackler     = (tackler.GlobalPosition - victim.GlobalPosition).Normalized();
        return victimForward.Dot(toTackler) < -0.3f;
    }

    /// <summary>
    /// Aproxima a regra de "negar oportunidade clara de gol" (DOGSO):
    /// se há no máximo 1 defensor (≈ o goleiro) entre a vítima e o gol que ela
    /// ataca, e ela está em campo ofensivo, então o desarme falhado a impediu
    /// de uma chance clara → vermelho.
    /// </summary>
    private bool IsDenyingClearChance(Player tackler, Player victim)
    {
        float attackingGoalX = victim.Team == 0 ? 52.5f : -52.5f;
        var   defenders      = tackler.Team == 0 ? _teamA.Players : _teamB.Players;

        int blockers = 0;
        foreach (var d in defenders)
        {
            if (d == tackler) continue;
            if (_sentOff.Contains(d.PlayerId)) continue;

            bool betweenVictimAndGoal = victim.Team == 0
                ? d.GlobalPosition.X > victim.GlobalPosition.X
                : d.GlobalPosition.X < victim.GlobalPosition.X;
            if (betweenVictimAndGoal) blockers++;
        }

        bool inAttackingHalf = Mathf.Abs(victim.GlobalPosition.X - attackingGoalX) < 40f;
        return blockers <= 1 && inAttackingHalf;
    }
}
