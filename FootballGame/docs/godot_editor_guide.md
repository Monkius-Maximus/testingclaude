# Guia Completo do Editor Godot 4 — FootballGame

> Tutorial para quem está começando do zero no Godot 4.3 com C#.
> Específico para este projeto — não é um tutorial genérico.

---

## Parte 1 — Instalação

### O que você precisa (2 itens obrigatórios)

**Atenção:** este projeto usa C#. Existem duas versões do Godot 4 — você precisa
da versão **.NET**, não da versão padrão.

**A) Godot 4.3 .NET**
- Site oficial: `https://godotengine.org/download`
- "Other versions" → versão **4.3** → **Godot Engine - .NET**
- É portátil (não precisa instalar), basta extrair e executar

**B) .NET 8 SDK**
- Site: `https://dotnet.microsoft.com/download/dotnet/8.0`
- Baixe e instale o **SDK** (não o Runtime)
- Confirme no terminal: `dotnet --version` → deve mostrar `8.x.x`

> O Godot roda o motor. O .NET SDK compila o C#. São separados — o Godot chama
> o compilador, mas ele precisa estar instalado.

---

## Parte 2 — Abrindo o Projeto

### 2.1 Clonar o repositório

```bash
git clone https://github.com/Monkius-Maximus/testingclaude.git
cd testingclaude
git checkout claude/game-implementation-roadmap-r7vr0
```

Ou baixe como ZIP no GitHub → Code → Download ZIP → extraia.

### 2.2 Importar no Godot

1. Abra o executável do Godot 4.3 .NET
2. Tela inicial → clique **Import**
3. Navegue até `testingclaude/FootballGame/`
4. Selecione `project.godot` → **Open**
5. Clique **Import & Edit**

---

## Parte 3 — Interface do Editor

```
┌─────────────────────────────────────────────────────────────────┐
│  BARRA DE MENU  (File, Edit, Project, Debug…)          ▶ ■ ⏸  │
├──────────┬──────────────────────────────────────┬───────────────┤
│          │                                      │               │
│  SCENE   │         VIEWPORT (área 3D/2D)        │  INSPECTOR    │
│  (árvore │                                      │  (propriedades│
│  de nós) │                                      │   do nó)      │
│          │                                      │               │
├──────────┴──────────────────────────────────────┴───────────────┤
│                         FILESYSTEM                              │
│         (todos os arquivos: scripts, cenas, recursos)          │
└─────────────────────────────────────────────────────────────────┘
```

- **Scene** (esquerda): hierarquia de nós da cena aberta
- **Inspector** (direita): propriedades do nó selecionado — edite aqui sem mexer no código
- **FileSystem** (baixo): todos os arquivos do projeto
- **Viewport** (centro): visualização e edição da cena
- **Botão ▶ (F5)**: roda o jogo

---

## Parte 4 — Compilando o C# (obrigatório antes de tudo)

Na primeira vez, compile manualmente:

**Menu → Project → Build** (ou `Alt+B`)

Aguarde na aba Output:
```
Build succeeded.
```

Se der erro:
- `.NET SDK não instalado` → instale o SDK 8 e reinicie o Godot
- `Versão errada` → precisa do SDK 8 (não 6, não 9)
- `Godot versão errada` → baixe a versão .NET do Godot 4.3

> Regra de ouro: toda vez que editar um `.cs`, faça Build antes de F5.

---

## Parte 5 — Estrutura de Arquivos

```
FootballGame/
├── docs/               ← documentação
├── resources/
│   ├── leagues/        ← times (team_home.tres, team_away.tres)
│   ├── lineups/        ← formações (lineup_433.tres, lineup_442.tres…)
│   └── roles/          ← posições (role_gk.tres, role_st.tres…)
├── scenes/
│   ├── cameras/        ← CameraController.tscn
│   ├── entities/       ← Ball.tscn, Player.tscn, Goalkeeper.tscn
│   ├── match/          ← Field.tscn, Match.tscn (cena principal)
│   └── ui/             ← MainMenu.tscn, TeamSelect.tscn, HUD.tscn…
├── scripts/            ← código C#
│   ├── ai/             ← AIBrain, TeamController, Behavior Tree
│   ├── core/           ← GameManager, MatchClock, RulesManager, GameStateManager
│   ├── data/           ← PlayerRole, TeamData, Lineup, LeagueData
│   ├── entities/       ← Player, Goalkeeper, Ball, GoalDetector
│   ├── input/          ← HumanInput, ControlSwitcher
│   ├── match/          ← MatchBootstrap
│   └── ui/             ← HUD, MainMenuUI, SettingsUI, PauseUI…
└── assets/             ← vazio — aqui entram modelos 3D, texturas, áudio
```

---

## Parte 6 — Explorando as Cenas

Clique duplo em qualquer `.tscn` no FileSystem para abrir.

### Árvore de nós de Match.tscn

```
Match (Node3D) ← script: MatchBootstrap.cs
├── Field         ← campo verde + gols
├── Ball          ← bola (esfera 11cm)
├── TeamA         ← IA do time azul (TeamController.cs)
├── TeamB         ← IA do time vermelho
├── ControlSwitcher ← arbitro humano vs IA
├── HumanInput_P1   ← seu controle (WASD)
├── RulesManager    ← laterais, escanteios, gols
├── MatchClock      ← relógio (45+45 min)
├── CinematicDirector ← câmera de gol
├── HUD             ← placar
├── MainCamera      ← câmera seguindo a bola
└── CinematicCamera ← câmera de replay
```

**Selecionar um nó + ver propriedades:** clique no nó → Inspector mostra
os campos `[Export]` do script. Tudo editável sem tocar no código.

---

## Parte 7 — Rodando o Jogo

**F5** (ou botão ▶)

O jogo inicia no Menu Principal (`scenes/ui/MainMenu.tscn`).

### Fluxo de telas

```
Menu Principal → Jogar → Seleção de Time → Partida
                              ↑                ↕ Escape = Pausa
                 Configurações ┘               ↕ 45' = Intervalo overlay
                                               ↕ 90' = Resultado → Menu
```

### O que você verá na partida (sem modelos)

- Campo verde plano com traves brancas
- Cápsulas azuis (casa) e vermelhas (visitante) como jogadores
- Esfera cinza pequena como bola

Isso é correto — os placeholders são funcionais. Arte real entra depois.

---

## Parte 8 — Controles do Jogador 1

| Tecla | Ação |
|-------|------|
| W / A / S / D | Mover |
| Shift Esquerdo | Sprint |
| Espaço | Chutar |
| Q | Carrinho (tackle) |
| Tab | Trocar jogador controlado |
| LShift | Pressão coletiva do time |
| Y | Goleiro sai da área |
| Escape | Pausar |
| F2 | Câmera tática (vista aérea) |
| F | Câmera seguindo a bola |
| Scroll mouse | Zoom |
| Botão direito + arrastar | Orbitar câmera |

---

## Parte 9 — Verificando os Autoloads

**Project → Project Settings → Autoload**

Deve haver exatamente dois:

| Nome | Caminho |
|------|---------|
| `GameManager` | `res://scripts/core/GameManager.cs` |
| `GameStateManager` | `res://scripts/core/GameStateManager.cs` |

Se não estiverem: clique na pasta → selecione o script → coloque o nome → **Add**.

---

## Parte 10 — Propriedades Exportadas no Inspector

As mais importantes para verificar:

**Match.tscn → nó "Match" (MatchBootstrap.cs):**
- `Player Scene` → `scenes/entities/Player.tscn`
- `Goalkeeper Scene` → `scenes/entities/Goalkeeper.tscn`
- `_teamAPath` → `TeamA`
- `_teamBPath` → `TeamB`
- `_ballPath` → `Ball`

> O código carrega automaticamente se não estiver preenchido,
> mas preencher acelera o carregamento.

**Como preencher um NodePath:** clique no campo → digite o caminho (`../Ball`)
ou arraste o nó da árvore diretamente para o campo.

**Como preencher uma PackedScene:** arraste o `.tscn` do FileSystem para o campo.

---

## Parte 11 — Importando Modelos 3D

### 11.1 Formato preferido

Use **`.glb`** (glTF binário) — melhor suporte Godot 4. Une malha + rig + animações.

### 11.2 Onde colocar

```
assets/models/players/    ← jogadores .glb
assets/models/ball/       ← bola .glb
assets/models/stadiums/   ← estádio .glb
```

O Godot detecta novos arquivos automaticamente.

### 11.3 Configurando o import

Clique no `.glb` → aba **Import** (ao lado de "Scene"):
- **Scale**: `1.0` (se o modelo está em metros)
- Clique **Re-import**

### 11.4 Substituindo o placeholder do jogador

1. Abra `scenes/entities/Player.tscn`
2. Arraste o `.glb` do FileSystem para **dentro** do nó Player na árvore
3. Isso cria uma instância com malha + rig como filho do Player
4. Delete o nó `MeshInstance3D` antigo (o capsule)

### 11.5 Configurando o AnimationTree

Necessário para o `PlayerAnimator.cs` funcionar com as animações.

1. Em Player.tscn, clique direito na árvore → **Add Child Node** → `AnimationPlayer`
2. Adicione outro filho: `AnimationTree`
3. Clique em **AnimationTree** → Inspector:
   - `Anim Player` → arraste o nó AnimationPlayer
   - `Tree Root` → "New AnimationNodeStateMachine"
   - `Active` → ligue o toggle
4. Duplo clique no AnimationTree para abrir o editor de estados
5. Crie estados com estes nomes exatos (usados pelo código):

   | Estado | Tipo | Loop |
   |--------|------|------|
   | `Idle` | Animation | sim |
   | `Locomotion` | BlendSpace2D | sim |
   | `Sprint` | Animation | sim |
   | `Kick` | Animation | não |
   | `SlideTackle` | Animation | não |
   | `Header` | Animation | não |
   | `Stumble` | Animation | não |
   | `Celebrate` | Animation | não |
   | `Dive` | Animation | não |

6. No estado `Locomotion`, configure o BlendSpace2D:
   - Eixo X: strafe (-1 a 1)
   - Eixo Y: velocidade (0 a 1)

7. Nas animações one-shot (Kick, Tackle etc.), adicione um **Method Track** no
   último frame chamando `OnActionFinished()` no nó PlayerAnimator.
   Isso sinaliza ao código que a ação terminou e libera a locomoção.

### 11.6 Configurando o PlayerAnimator

Em Player.tscn, o nó `AIBrain` não precisa de ajuste. Mas se você adicionar o
`PlayerAnimator`, conecte-o ao Player:
1. Adicione `PlayerAnimator` como filho do Player
2. No Inspector do PlayerAnimator: campo `_animTree` → arraste o AnimationTree
3. No Inspector do Player: campo `Animator` → arraste o nó PlayerAnimator

---

## Parte 12 — Adicionando Escudos e Uniformes

**Escudos (TeamData):**
1. Coloque PNGs em `assets/textures/teams/` (512×512, com alpha)
2. Abra `resources/leagues/team_home.tres`
3. Campo `Crest Path` → `res://assets/textures/teams/nome.png`

**Uniformes (Material override nos jogadores):**
Atualmente o `MatchBootstrap.SetTeamColor()` aplica cor sólida. Quando
os uniformes texturizados chegarem, avise — ajusto o código para usar
a textura em vez da cor.

---

## Parte 13 — Erros Frequentes

| Erro | Causa | Solução |
|------|-------|---------|
| `Build failed` | .NET não instalado / versão errada | Instale SDK 8, reinicie Godot |
| Cenas não carregam | Script não compilou | Project → Build |
| Jogadores não aparecem | MatchBootstrap sem cenas de jogador | Abra Match.tscn → Inspector → atribua Player/Goalkeeper Scene |
| IA parada nos primeiros frames | Normal — TeamController usa CallDeferred | Aguarde 1-2 segundos |
| Bola não se move | ContactMonitor desligado | Ball.tscn → Inspector → `Contact Monitor = On` |
| Mouse preso na tela | Câmera captura o mouse | Alt+Tab ou rode em janela flutuante |
| `Can't cast to Player` | Nó no grupo "players" com script errado | Verifique Player.tscn e Goalkeeper.tscn |

---

## Parte 14 — Fluxo de Trabalho Diário

```
1. Abrir Godot 4.3 .NET → abrir projeto
2. Editar scripts .cs no VS Code (recomendado)
3. Voltar ao Godot → Project → Build
4. F5 para testar
5. Ajustar propriedades no Inspector (sem editar código)
6. Ctrl+S para salvar a cena
```

### Configurar VS Code como editor externo

**Editor → Editor Settings → Text Editor → External Editor → Visual Studio Code**

Clique duplo em qualquer `.cs` no FileSystem → abre no VS Code com IntelliSense.

---

## Parte 15 — Estado Atual do Projeto

| Funcionalidade | Status |
|----------------|--------|
| Física da bola (chute, passe, quique) | ✅ Pronto |
| Movimento e corrida dos jogadores | ✅ Pronto |
| IA com behavior tree (ataque, passe, formação, pressão) | ✅ Pronto |
| Controle humano (teclado) | ✅ Pronto |
| Câmera orbital + vista tática | ✅ Pronto |
| Regras (lateral, escanteio, tiro de meta, gol) | ✅ Pronto |
| Relógio de partida (1º e 2º tempo) | ✅ Pronto |
| Menu principal, seleção de times, configurações, pausa | ✅ Pronto |
| 5 formações (4-3-3, 4-4-2, 3-5-2, 4-2-3-1, 5-3-2) | ✅ Pronto |
| HUD com placar e banner de gol | ✅ Pronto |
| Cinemática de gol (slow-mo + câmera orbital) | ✅ Pronto |
| Configurações salvas (user://settings.cfg) | ✅ Pronto |
| Modelos 3D dos jogadores | ⏳ Você fornece |
| Modelos 3D do estádio e bola | ⏳ Você fornece |
| Animações (Idle, Run, Kick…) | ⏳ Você fornece |
| Áudio (aplausos, apito, chute) | ⏳ Você fornece |
| Modo carreira | 🔜 Próxima etapa de código |
