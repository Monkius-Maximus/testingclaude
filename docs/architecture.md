# Arquitetura

Documento vivo. Atualizar conforme decisões evoluem.

## Visão de alto nível

O jogo é estruturado em **camadas independentes** que se comunicam por sinais e
propriedades, não por chamadas diretas. Isso é deliberado: queremos que
substituir, por exemplo, a IA por algo mais sofisticado não exija mexer no
controle humano, e vice-versa.

```
Match (cena raiz)
├── Ball                 (RigidBody3D — física)
├── MatchEventBus        (hub de eventos da partida)
├── MatchClock           (relógio do jogo)
├── Field                (campo + áreas + colisores)
│   ├── GoalDetectorL    (Area3D)
│   └── GoalDetectorR    (Area3D)
├── TeamA (TeamController, Team=0)
│   ├── TeamBlackboard   (contexto compartilhado)
│   ├── Goalkeeper       (Player + regras especiais)
│   │   ├── AIBrain
│   │   └── PlayerAnimator
│   └── OutfieldPlayer × 10  (cada um com AIBrain + PlayerAnimator)
├── TeamB                (mesma estrutura, Team=1)
├── GameplayCamera       (CameraController)
├── CinematicCamera      (CinematicCamera — usada em gol)
├── ControlSwitcher      (decide quem manda em cada jogador)
├── HumanInput × N       (1+ controles de humano)
├── RulesManager         (regras do futebol)
├── FoulSystem           (detecção de faltas e cartões)
├── CinematicDirector    (gol em câmera lenta)
└── HUD                  (placar)
```

## Princípio central: separação de "corpo" e "controle"

A classe `Player` é só um **corpo** que executa intenções:

```csharp
public Vector3 IntendedMovement { get; set; }
public bool    IntendsToKick    { get; set; }
public bool    IntendsToSprint  { get; set; }
// ...
```

Quem **preenche** essas intenções a cada frame é decidido pelo
`ControlSwitcher`:

| Modo | Quem escreve nas Intended* |
|---|---|
| `AI` | `AIBrain` (Behavior Tree) |
| `HumanDirect` | `HumanInput` (controle direto) |
| `HumanAssisted` | `AIBrain` com modificadores (ex: LB → pressão) |
| `ManualOverride` | `HumanInput` em jogador alternativo (ex: goleiro sai) |

A transição é invisível porque ambos escrevem nas mesmas propriedades. Trocar
de boneco (estilo FIFA) é apenas marcar `_modes[oldPlayer] = AI` e
`_modes[newPlayer] = HumanDirect`.

## Ordem de execução por frame

Importante para a coerência dos sistemas:

1. `TeamController._PhysicsProcess` — atualiza blackboard e slots
2. `ControlSwitcher._PhysicsProcess` — define quem manda em quem
3. `HumanInput._PhysicsProcess` — preenche intenções dos bonecos ativos
4. `AIBrain._PhysicsProcess` — preenche intenções dos demais (a 10 Hz)
5. `Player._PhysicsProcess` — lê intenções e executa

Em Godot, processos rodam na ordem da árvore. Para garantir isso na prática,
ajuste a ordem dos filhos na cena `Match.tscn` ou use `process_priority`.

## Pacotes de scripts

### `scripts/core/`
Glue do jogo: `GameManager` (singleton), `RulesManager` (regras), `MatchClock`.

### `scripts/entities/`
Atores: `Player`, `Goalkeeper`, `Ball`, `GoalDetector`. Apenas estado e
execução de intenções — nenhuma decisão acontece aqui.

### `scripts/ai/`
Inteligência: `AIBrain` (cérebro por jogador), `TeamController` (maestro do
time), `TeamBlackboard` (contexto compartilhado). A pasta
`ai/behavior_tree/` contém o framework mínimo de BT.

### `scripts/input/`
Controle humano: `HumanInput` (uma instância por controle físico),
`ControlSwitcher` (coordenador).

### `scripts/animation/`
`PlayerAnimator` — camada fina entre `Player` e `AnimationTree`.

### `scripts/camera/`
Câmeras: `CameraController` (jogo) e `CinematicCamera` (gol).

### `scripts/cinematics/`
`CinematicDirector` — orquestra a cinemática de gol em fases.

### `scripts/ui/`
`HUD` — placar e banner de gol.

### `scripts/data/`
Tipos de `Resource`: `PlayerRole`, `Lineup`, `LeagueData`, `TeamData`.
Instâncias (`.tres`) ficam em `/resources/`.

## Decisões de design (e por que)

**BT custom, sem plugin externo.** Behavior Trees são simples o suficiente
para um framework de ~100 linhas. Plugins (LimboAI, Beehave, Yet Another BT)
tornam-se obrigações de manutenção. Reavaliar se a complexidade do
comportamento crescer muito.

**AIBrain tickando a 10 Hz, não a 60.** Decisões de IA em futebol não
precisam de frame-perfect — 10 ticks/segundo são suficientes para reagir e
economizam 83% do CPU de IA com 22 jogadores em campo.

**Blackboard como Node, não como autoload.** Cada time tem o seu próprio,
permitindo testar partidas isoladas e potencialmente rodar simulações
paralelas (futuro: simulação rápida de partidas em segundo plano para
liga/copa).

**Resources para roles e formações.** Permite editar dados táticos sem
recompilar, e abre porta para mods.

**Namespace único `FootballGame`.** Simplicidade no MVP. Se a base crescer
muito (>50 arquivos), considerar subnamespaces por pacote.

## Pontos abertos / TODO arquitetural

- Sistema de eventos da partida (faltas, cartões, substituições) — provável
  novo `MatchEventBus`.
- Replay buffer — gravar últimos 10s de transformes para "instant replays".
- Multiplayer online — `MultiplayerSynchronizer` nas intenções dos jogadores
  e `AIBrain` rodando authoritative no servidor.
- Simulação de partidas em background (para modo carreira).

## Performance baseline

Decisões de otimização aplicadas desde o esqueleto inicial (todas com
custo zero — são mudanças cirúrgicas, não adicionam complexidade):

| Otimização | Onde | Por quê |
|---|---|---|
| `StringName` para parâmetros do AnimationTree | `PlayerAnimator` | Strings literais em `Set()`/`Travel()` alocam em todo frame. `StringName` é imutável e cacheado. |
| Lista cacheada de jogadores | `TeamController.Players`, `OpponentPlayers`, `_allPlayers` | `GetNodesInGroup` varre toda a SceneTree. Caro com 22 agentes × 60 fps. |
| `DistanceSquaredTo` em vez de `DistanceTo` | `ControlSwitcher`, `TeamController`, `HumanInput` | Evita `sqrt()` quando só queremos comparar distâncias. |
| Stagger no tick da IA | `AIBrain._Ready` | 22 brains decidindo no mesmo frame causaria spike. Espalhar pelo ciclo de 100ms suaviza o frame time. |
| Tick de IA a 10 Hz | `AIBrain` | Decisões de futebol não precisam de 60 Hz. Reduz 83% do custo. |
| Buffer reutilizável | `ControlSwitcher._nearbyBuffer` | Evita alocar `List<Player>` a cada frame em queries vizinhas. |
| Câmera de referência cacheada | `HumanInput.ReferenceCamera` | `GetViewport().GetCamera3D()` faz lookup na árvore. |
| `CallDeferred` na coleta inicial | `TeamController.CollectPlayers` | Garante que todos os jogadores (ambos os times) estão na árvore antes de cachear. |

**Otimizações reservadas para depois** (não fazer ainda — esperar profiler apontar):

- LOD de modelos e animação (jogadores distantes)
- Object pooling de partículas e efeitos
- Tick adaptativo de IA (mais rápido perto da bola)
- Decisões em cascata (estratégica > tática > mecânica com TTLs diferentes)
- Crowd no estádio via `MultiMeshInstance3D`

A regra geral: **profile primeiro, otimize depois**. Veja `docs/getting_started.md`
para os primeiros passos com o Profiler do Godot.
