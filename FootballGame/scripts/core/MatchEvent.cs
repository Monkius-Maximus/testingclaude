using Godot;

namespace FootballGame;

/// <summary>Categorias de evento que podem ocorrer durante uma partida.</summary>
public enum MatchEventType
{
    KickOff,
    Goal,
    Foul,
    YellowCard,
    RedCard,
    Substitution,
    CornerKick,
    ThrowIn,
    GoalKick,
    Penalty,
    FreeKick,
    Offside,
    HalfTime,
    FullTime,
    Injury,
    Save,
    ShotOnTarget,
    ShotOffTarget
}

/// <summary>
/// Um evento ocorrido na partida. RefCounted para trafegar pelo
/// <see cref="MatchEventBus"/> via sinal e poder ser armazenado em logs
/// (estatísticas, replay, comentário pós-jogo).
///
/// Identifica jogadores por <see cref="MainPlayerId"/> (string), nunca por
/// referência de nó — assim o evento é seguro para persistir e imune a
/// referências penduradas de jogadores já removidos (ex: expulsos).
/// </summary>
public partial class MatchEvent : RefCounted
{
    public MatchEventType Type              { get; set; }
    public int            Minute            { get; set; }
    public int            Team              { get; set; } = -1; // time principal (-1 = neutro)
    public string         MainPlayerId      { get; set; } = "";
    public string         SecondaryPlayerId { get; set; } = ""; // assistência, vítima da falta, etc
    public Vector3        Position          { get; set; } = Vector3.Zero;
    public string         Description       { get; set; } = "";

    // ── Factories (criação consistente e legível) ────────────────

    public static MatchEvent Goal(int minute, int team, string scorerId, string assistId, Vector3 pos) =>
        new()
        {
            Type = MatchEventType.Goal,
            Minute = minute,
            Team = team,
            MainPlayerId = scorerId,
            SecondaryPlayerId = assistId,
            Position = pos,
            Description = $"GOL aos {minute}'"
        };

    public static MatchEvent Foul(int minute, int offendingTeam, string offenderId, string victimId, Vector3 pos) =>
        new()
        {
            Type = MatchEventType.Foul,
            Minute = minute,
            Team = offendingTeam,
            MainPlayerId = offenderId,
            SecondaryPlayerId = victimId,
            Position = pos,
            Description = $"Falta aos {minute}'"
        };

    public static MatchEvent Card(MatchEventType cardType, int minute, int team, string offenderId, Vector3 pos)
    {
        string label = cardType == MatchEventType.RedCard ? "Cartão vermelho" : "Cartão amarelo";
        return new MatchEvent
        {
            Type = cardType,
            Minute = minute,
            Team = team,
            MainPlayerId = offenderId,
            Position = pos,
            Description = $"{label} aos {minute}'"
        };
    }

    /// <summary>Evento sem jogador específico (kickoff, intervalo, escanteio...).</summary>
    public static MatchEvent Simple(MatchEventType type, int minute, int team = -1) =>
        new()
        {
            Type = type,
            Minute = minute,
            Team = team,
            Description = $"{type} aos {minute}'"
        };
}
