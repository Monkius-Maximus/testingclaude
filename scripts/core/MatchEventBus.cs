using Godot;
using System.Collections.Generic;

namespace FootballGame;

/// <summary>
/// Hub central de eventos da partida. Emissores publicam via <see cref="Publish"/>
/// e consumidores se inscrevem no sinal <see cref="EventOccurred"/>. Desacopla
/// quem gera o evento (FoulSystem, RulesManager...) de quem reage (HUD,
/// estatísticas, comentário...).
///
/// É um Node de cena (não autoload), por consistência com TeamBlackboard:
/// cada partida tem o seu próprio bus, o que mantém simulações paralelas
/// isoladas no futuro.
/// </summary>
public partial class MatchEventBus : Node
{
    [Signal] public delegate void EventOccurredEventHandler(MatchEvent matchEvent);

    /// <summary>Histórico ordenado de todos os eventos. Base para o resumo pós-jogo.</summary>
    public IReadOnlyList<MatchEvent> History => _history;
    private readonly List<MatchEvent> _history = new();

    public void Publish(MatchEvent matchEvent)
    {
        _history.Add(matchEvent);
        EmitSignal(SignalName.EventOccurred, matchEvent);
    }

    public void Clear() => _history.Clear();
}
