# Design da IA

## Camadas

A IA acontece em **três níveis** que conversam entre si:

```
┌──────────────────────────────────────────────┐
│  TeamController (decisão de time)            │
│  - Atualiza blackboard a cada frame          │
│  - Calcula slots de formação                 │
│  - Aplica escalações e substituições         │
└────────────────┬─────────────────────────────┘
                 │
                 ▼
       ┌─────────────────────┐
       │   TeamBlackboard    │  (estado compartilhado)
       │  posse, bola, alvos │
       └─────────────────────┘
                 ▲
                 │ (consultado por todos)
                 │
┌────────────────┴─────────────────────────────┐
│  AIBrain × 11 (decisão individual)           │
│  - Tickia a 10 Hz                            │
│  - Behavior Tree montada conforme o role     │
│  - Escreve nas Intended* do Player           │
└──────────────────────────────────────────────┘
```

## Behavior Trees por role

### Jogador de linha
```
Selector
├── Sequence: HasBall → AttackOrPass
├── Sequence: TeamHasPossession → SupportAttack
├── Sequence: PressureTarget != null && CloseToBall → PressBallCarrier
└── HoldFormationSlot
```

### Goleiro
```
Selector
├── Sequence: GoalkeeperRush → RushOutOfBox
├── Sequence: BallApproachingGoal → SetupDive
└── HoldGoalLine
```

A árvore é **construída uma vez no `_Ready`** e reutilizada — os nós são
stateless. Folhas (`BTAction`, `BTCondition`) recebem o `AIBrain` como
contexto e leem do blackboard.

## Por que 10 Hz?

Decisões de futebol são lentas (centenas de ms para um humano também). Frame
perfect não acrescenta nada e custa caro com 22 agentes. Em testes, 10 ticks
por segundo é o ponto onde:

- A jogada parece reativa (200ms de latência média)
- O custo de CPU some no profiler (de ~6ms/frame para <1ms)

Se precisar reagir mais rápido a algo (ex: defender um chute), o
`AIBrain` pode ter "tick imediato" disparado por sinal específico — não
implementado ainda, mas a arquitetura permite.

## Blackboard: o que entra ali

| Dado | Atualizado por | Lido por |
|---|---|---|
| `BallPosition`, `BallVelocity` | TeamController | Todo brain |
| `BallCarrier` | TeamController | Todo brain |
| `TeamHasPossession` | TeamController | Todo brain |
| `FormationSlots` | TeamController | Todo brain |
| `PressureTarget` | ControlSwitcher (humano) | Brains do time |
| `GoalkeeperRush` | ControlSwitcher (humano) | Brain do goleiro |

A regra é: blackboard **só guarda fatos**, nunca decisões. "A bola está na
posição X" é fato; "este jogador deve correr para a bola" é decisão e fica
no brain.

## Controle assistido (estilo FIFA)

O conceito de "humano controla 1 boneco direto + manda em 2-3 indiretamente"
é implementado puramente via blackboard:

- **Botão LB (pressão coletiva):** ControlSwitcher detecta o pressed,
  encontra colegas próximos do boneco ativo, e seta
  `Blackboard.PressureTarget` para o adversário com a bola. Brains dos
  colegas, na próxima decisão, veem `PressureTarget != null && CloseToBall` e
  acionam o nó `PressBallCarrier`.

- **Botão Y/Triângulo (goleiro sai):** Seta `Blackboard.GoalkeeperRush = true`.
  Brain do goleiro toma a decisão de sair.

Note que o ControlSwitcher **não chama métodos** na IA — só escreve fatos no
blackboard. A IA decide. Isso é importante: significa que a IA pode
"discordar" da ordem do humano se outro fator pesar mais (ex: o goleiro está
sem stamina e o nó `RushOutOfBox` poderia ter um guard de stamina mínima).

## Decisões futuras

- **Marcação por zona vs individual:** atualmente todo defensor segue o slot
  da formação. Adicionar nó `MarkOpponent` que pega o adversário mais
  próximo do slot e o "trava" enquanto durar a posse.
- **Linhas de passe inteligentes:** `AttackOrPass` hoje só chuta para o gol.
  Adicionar cálculo de melhor opção de passe (lance de ângulo + distância +
  marcação no recebedor).
- **Estados táticos do time:** "atacando", "transitando", "defendendo",
  "bola parada". Cada um pode alterar pesos no brain.

## Movimentação sem bola

A movimentação sem bola é o que diferencia "correr atrás da bola" de "criar
jogadas". É orientada por três peças:

### 1. Fase do time (`TeamPhase`)
O `TeamController` define a fase a cada frame, gravada no blackboard:

| Fase | Quando | Comportamento sem bola |
|---|---|---|
| `Attacking` | este time tem a posse | busca espaço, oferece linha de passe, faz corridas |
| `Defending` | o adversário tem a posse | recompõe, marca goalside |
| `Transition` | bola solta | mantém o slot (fallback) |

### 2. Avaliação de espaço (`SpaceEvaluator`)
Funções puras (sem estado, fáceis de testar) que pontuam uma posição:

```
score = 0.30·espaço_livre        (distância ao adversário mais próximo)
      + 0.25·linha_de_passe       (do portador até a posição, sem interceptação)
      + 0.30·avanço               (progresso em direção ao gol)
      − 0.25·aglomeração          (proximidade de companheiros)
      − 1.00·impedimento          (além da linha do 2º último defensor)
```

A linha de impedimento é o X do **segundo defensor mais avançado** (inclui o
goleiro) — a regra real do futebol. Posições além dela e à frente da bola são
fortemente penalizadas, então a IA aprende a "segurar a corrida" até o momento
certo, em vez de ficar impedida.

### 3. Amostragem de candidatos (no `AIBrain.OffBallAttack`)
Em vez de uma posição fixa, o jogador amostra ~9 posições ao redor (uma corrida
reta em profundidade + um anel radial com viés pra frente), pontua cada uma com
o `SpaceEvaluator`, e vai para a melhor. O `DefensiveBias` do role modula o
quanto ele avança: atacantes fazem corridas longas, zagueiros quase não sobem.

**Custo:** ~9 candidatos × ~22 jogadores avaliados = ~200 operações por tick, a
10 Hz com stagger. Irrelevante no profiler. Amostragem é mais barata que um
mapa de influência completo e suficiente para futebol.

### Por que amostragem e não influence map?
Um influence map (grade de campo com "calor" de controle) é o padrão AAA, mas
é mais caro de manter e atualizar. A amostragem local dá 90% do resultado com
10% da complexidade — exatamente o tipo de troca que faz sentido nesta fase.
Reavaliar se quisermos movimentação coletiva coordenada (ex: linha de
impedimento sobe junta, blocos defensivos).

### O que ainda não está modelado
- **Corridas cronometradas:** o jogador segura na linha de impedimento, mas não
  dispara a corrida sincronizada com o passe do companheiro. Precisa de um sinal
  "vou passar" do portador.
- **Overlap/underlap:** laterais ultrapassando pontas — exige coordenação entre
  dois jogadores específicos.
- **Arrastar marcação:** mover-se para abrir espaço para outro, não para si.
