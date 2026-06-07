# FootballGame

Jogo de futebol 3D em desenvolvimento, construído em **Godot 4.3** com **C#**.

## Status atual

Esqueleto inicial. Estrutura de pastas, scripts de gameplay e documentação de arquitetura. **Ainda sem cenas (`.tscn`) montadas** — a maior parte do trabalho seguinte é no editor do Godot.

## Estrutura

```
FootballGame/
├── scripts/           Toda a lógica em C#
│   ├── core/          GameManager, RulesManager, MatchClock
│   ├── entities/      Player, Goalkeeper, Ball, GoalDetector
│   ├── ai/            Brain, TeamController, Blackboard, Behavior Tree
│   ├── input/         HumanInput, ControlSwitcher
│   ├── animation/     PlayerAnimator
│   ├── camera/        CameraController, CinematicCamera
│   ├── cinematics/    CinematicDirector
│   ├── ui/            HUD
│   └── data/          Resources: PlayerRole, Lineup, LeagueData...
│
├── scenes/            Cenas do Godot (a criar no editor)
├── resources/         .tres — instâncias de roles, formações, etc
├── assets/            Modelos 3D, materiais, áudio
└── docs/              Decisões de arquitetura e design
```

## Como abrir

1. Instale o **[Godot 4.3 .NET](https://godotengine.org/download)** (a versão Mono/C#).
2. Instale o **[.NET 8 SDK](https://dotnet.microsoft.com/download)**.
3. Abra o Godot e clique em **Import** → selecione `project.godot` na raiz do repositório.
4. Aceite quando o Godot pedir para gerar o `.csproj` (se ainda não houver). Já existe um aqui.
5. Build: **Project → Tools → C# → Create C# solution** se a build não disparar sozinha, ou pressione **Alt+B** (Build Solution).

## Próximos passos (roadmap curto)

- [ ] Criar `Field.tscn` com o campo, gols, áreas e laterais
- [ ] Criar `Player.tscn` e `Goalkeeper.tscn` com `CharacterBody3D` + `AnimationTree`
- [ ] Criar `Ball.tscn` (`RigidBody3D` + `PhysicsMaterial`)
- [ ] Criar `Match.tscn` que orquestra tudo
- [ ] Configurar `AnimationTree` com BlendSpace2D de locomoção
- [ ] Importar/gerar modelos 3D e animações (Mixamo para protótipo)
- [ ] Criar os `.tres` de PlayerRole para cada posição

## Documentação

- [`docs/architecture.md`](docs/architecture.md) — visão geral dos módulos e como conversam
- [`docs/ai_design.md`](docs/ai_design.md) — decisões de IA, behavior tree e blackboard
- [`docs/match_events.md`](docs/match_events.md) — sistema de eventos da partida (bus, faltas, cartões)
- [`docs/leagues_design.md`](docs/leagues_design.md) — sistema de ligas e prestígio
- [`docs/getting_started.md`](docs/getting_started.md) — passo a passo no editor do Godot
- [`docs/futeboldocs1.md`](docs/futeboldocs1.md) — conversa fundadora (escopo inicial e decisões)

## Convenções

- **Linguagem:** C# (Mono/.NET). Sem GDScript no projeto principal.
- **Namespace:** `FootballGame` em todos os arquivos.
- **Nomes:** PascalCase para tipos e métodos, camelCase com `_` para campos privados.
- **Tabs:** 4 espaços.
- **Estilo:** prioridade à clareza. Sem fallbacks silenciosos, sem early returns escondidos. `throw` quando precondições falham.

## Licença

A definir.
