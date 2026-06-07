# Sistema de Eventos da Partida

## Por quê

Muitos sistemas precisam reagir aos mesmos acontecimentos: um gol interessa ao
placar (HUD), ao comentário, às estatísticas e ao replay. Se cada emissor
chamasse cada consumidor diretamente, teríamos um emaranhado de dependências.

O **event bus** resolve isso: emissores publicam eventos sem saber quem escuta,
e consumidores se inscrevem sem saber quem emite.

```
EMISSORES                    BUS                      CONSUMIDORES
─────────                    ───                      ───────────
FoulSystem    ──┐                              ┌──►  HUD (placar, cartões)
RulesManager  ──┤                              ├──►  TeamController (expulsão)
MatchClock    ──┼──►  MatchEventBus  ──────────┼──►  (futuro) Comentário
CinematicDir  ──┘     [EventOccurred]          ├──►  (futuro) Estatísticas
                       + histórico             └──►  (futuro) Replay
```

## Peças

### `MatchEvent` (RefCounted)
O dado de um evento: tipo, minuto, time, jogadores (por `PlayerId`), posição e
descrição. Identifica jogadores por **string**, nunca por referência de nó —
seguro para logar e imune a referências penduradas (ex: jogador expulso).

Tem factories estáticas para criação legível:
```csharp
MatchEvent.Goal(minute, team, scorerId, assistId, pos);
MatchEvent.Foul(minute, offendingTeam, offenderId, victimId, pos);
MatchEvent.Card(MatchEventType.YellowCard, minute, team, playerId, pos);
MatchEvent.Simple(MatchEventType.HalfTime, minute);
```

### `MatchEventBus` (Node)
Um único sinal `EventOccurred(MatchEvent)` e um histórico. Publica:
```csharp
_bus.Publish(MatchEvent.Goal(23, 0, "p_09", "p_10", pos));
```
Inscreve:
```csharp
_bus.EventOccurred += OnMatchEvent;

private void OnMatchEvent(MatchEvent e)
{
    switch (e.Type) { /* ... */ }
}
```

É um Node de cena (não autoload), por consistência com `TeamBlackboard` —
cada partida tem o seu, permitindo simulações paralelas no futuro.

### `FoulSystem` (Node)
Escuta o sinal `TackleAttempted` de cada jogador e decide o desfecho:

```
desarme tentado
   │
   ▼
 vítima é o portador da bola? ──não──► ignora
   │ sim
   ▼
 chance limpa = habilidade × penalidade(por trás) × penalidade(velocidade)
   │
   ├─ sorteio < chance ─► desarme LIMPO (transfere posse)
   │
   └─ senão ─► FALTA  ─► publica Foul
                 │
                 ├─ último homem?  ─► vermelho direto (DOGSO)
                 ├─ perigoso?       ─► amarelo (2º amarelo → vermelho)
                 └─ senão           ─► falta simples
```

O `FoulSystem` **não toca na bola** — só decide e publica. A reposição é de
quem escuta (o `RulesManager`).

## Fluxo de um gol (ponta a ponta)

```
GoalDetector.GoalScored
   → CinematicDirector.OnGoalDetected
       → (slow-mo, câmera, celebração)
       → RulesManager.OnGoalScored(team)
           → incrementa placar interno
           → _bus.Publish(Goal)        ◄── ponto único de verdade
               → HUD.OnMatchEvent → AnimateGoalScored (banner + placar visual)
               → (futuro) Estatísticas, Comentário...
```

## Fluxo de uma falta com expulsão

```
Player.PerformTackle → emite TackleAttempted
   → FoulSystem.OnTackleAttempted
       → ResolveTackle → é falta + último homem
           → _bus.Publish(Foul)
               → RulesManager → monta tiro livre/pênalti
               → HUD → toast "Falta"
           → _bus.Publish(RedCard)
               → TeamController (do time infrator) → remove o jogador
               → HUD → toast "Cartão vermelho"
```

## Como adicionar um novo tipo de evento

1. Adicione o valor em `MatchEventType`.
2. (Opcional) Crie uma factory em `MatchEvent`.
3. Publique de onde faz sentido: `_bus.Publish(...)`.
4. Trate no `switch` de quem se interessa.

Nenhum dos passos exige mexer no `MatchEventBus`. É essa a vantagem.

## Extensões previstas

- **MatchClock → bus:** publicar `HalfTime`, `FullTime`, `KickOff`. Hoje o
  `MatchClock` tem sinais próprios; basta um adaptador fino ou dar a ele uma
  referência ao bus.
- **Offside:** exige detectar o segundo-último defensor no momento do passe.
  Provável novo `OffsideSystem` que escuta passes e publica `Offside`.
- **StatsTracker:** assina `EventOccurred`, acumula tudo, gera o resumo a
  partir de `MatchEventBus.History`.
- **CommentarySystem:** assina `EventOccurred`, mapeia tipo → falas.
- **Substituições:** já há `MatchEventType.Substitution`; o
  `TeamController.Substitute` pode publicar o evento.

## Decisões de design

**Um sinal só, não um por tipo.** `EventOccurred(MatchEvent)` mantém uma forma
única de inscrição (consistência) e permite que loggers recebam tudo com uma
conexão. Consumidores específicos filtram por `e.Type` — o volume de eventos é
baixo (dezenas por jogo), então o custo é irrelevante.

**Jogadores por ID, não por referência.** Eventos podem ser logados e revistos
muito depois de o jogador sair de campo. String é segura; referência de nó não.

**FoulSystem decide, RulesManager repõe.** Detecção e reposição são
responsabilidades distintas, conectadas pelo bus. Trocar uma não afeta a outra.
