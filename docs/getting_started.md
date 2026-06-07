# Primeiros passos no Godot

Guia rápido para sair do esqueleto e ter algo rodando.

## 1. Abrir o projeto

1. Baixe **Godot 4.3 .NET** em https://godotengine.org/download
2. Instale **.NET 8 SDK** em https://dotnet.microsoft.com/download
3. Abra o Godot → **Import** → selecione `project.godot` na raiz
4. Aguarde o Godot indexar os assets (primeiro abrir pode demorar)
5. Build solution: **Project → Tools → C# → Build** ou tecle `Alt+B`

> Se o build reclamar de `Godot.NET.Sdk`, abra `FootballGame.csproj` e
> ajuste a versão para a que está instalada. Para 4.3, deve ser
> `Godot.NET.Sdk/4.3.0`.

## 2. Registrar o GameManager como autoload

`Project → Project Settings → Autoload`:

| Path | Node Name | Singleton |
|---|---|---|
| `res://scripts/core/GameManager.cs` | `GameManager` | ✅ |

## 3. Criar a cena `Ball.tscn`

1. New Scene → 3D Scene
2. Renomeie a raiz para `Ball` e troque para `RigidBody3D`
3. Anexe o script `res://scripts/entities/Ball.cs`
4. Adicione filhos:
   - `CollisionShape3D` com `SphereShape3D` (radius 0.11m — bola tamanho real)
   - `MeshInstance3D` com `SphereMesh` ou um modelo .glb
5. Adicione ao grupo `ball` (ícone de link no inspetor → Groups)
6. Salve como `res://scenes/entities/Ball.tscn`

## 4. Criar a cena `Player.tscn`

1. New Scene → 3D Scene
2. Renomeie a raiz para `Player` e troque para `CharacterBody3D`
3. Anexe `res://scripts/entities/Player.cs`
4. Filhos:
   - `CollisionShape3D` com `CapsuleShape3D` (height ~1.8m, radius ~0.4m)
   - `MeshInstance3D` (modelo do jogador)
   - `AnimationPlayer` com as animações importadas
   - `AnimationTree` apontando para o AnimationPlayer
   - `Node` chamado `PlayerAnimator` com `PlayerAnimator.cs` anexado
   - `Node` chamado `AIBrain` com `AIBrain.cs` anexado
5. Adicione ao grupo `players`
6. Salve como `res://scenes/entities/Player.tscn`

### Configurar o AnimationTree

Tree Root → `New AnimationNodeStateMachine`:

- Estados a criar: `Idle`, `Locomotion` (`BlendSpace2D`), `Sprint`, `Kick`,
  `SlideTackle`, `Header`, `Stumble`, `Celebrate`, `Dive`
- Transições conforme `docs/architecture.md`
- Configure o BlendSpace2D `Locomotion` com pontos:
  - `(0, 0.1)` → walk_forward
  - `(-0.8, 0.5)` → walk_left
  - `(0.8, 0.5)` → walk_right
  - `(0, 1.0)` → run_forward
- Em cada animação de ação no AnimationPlayer, adicione **Method Track** no
  último frame chamando `OnActionFinished` no `PlayerAnimator`

## 5. Criar `Goalkeeper.tscn`

Herde de `Player.tscn` (Scene → Inherited Scene) ou crie do zero:
- Troque o script para `Goalkeeper.cs`
- Adicione referência à `Area3D` da grande área (atribuir depois quando a
  cena do Field existir)

## 6. Criar `Field.tscn`

1. New Scene → 3D Scene → `Node3D` raiz chamado `Field`
2. Filhos:
   - `MeshInstance3D` com o gramado (PlaneMesh ~105×68m)
   - `StaticBody3D` + `CollisionShape3D` para o chão
   - Dois `Node3D` chamados `GoalLeft`/`GoalRight` com:
     - `MeshInstance3D` com modelo da trave
     - `Area3D` (filho) com `GoalDetector.cs` anexado e shape cobrindo a
       boca do gol. Configure `Team=0` no esquerdo, `Team=1` no direito.
   - Duas `Area3D` para as **grandes áreas** (penalty area), uma de cada
     lado — usadas pelo Goalkeeper

## 7. Criar `Match.tscn`

A cena raiz da partida. Estrutura sugerida:

```
Match (Node3D)
├── Field (instanced)
├── Ball (instanced)
├── MatchEventBus (Node + MatchEventBus.cs)   ← hub de eventos
├── MatchClock (Node + MatchClock.cs)
├── TeamA (Node + TeamController.cs, Team=0)
│   ├── Goalkeeper (instanced)
│   └── Player × 10 (instanced, posicionados na formação inicial)
├── TeamB (Node + TeamController.cs, Team=1)
│   ├── Goalkeeper (instanced)
│   └── Player × 10
├── GameplayCamera (Camera3D + CameraController.cs)
├── CinematicCamera (Camera3D + CinematicCamera.cs, Current=false)
├── RulesManager (Node + RulesManager.cs)
├── FoulSystem (Node + FoulSystem.cs)          ← detecção de faltas/cartões
├── ControlSwitcher (Node + ControlSwitcher.cs)
├── HumanInput_P1 (Node + HumanInput.cs, ControllerIndex=0)
├── CinematicDirector (Node + CinematicDirector.cs)
└── HUD (instanced)
```

Configure todos os `NodePath` exportados de cada script para apontarem
para os nós corretos. Atenção aos NodePaths do **sistema de eventos**:

| Nó | Propriedade | Aponta para |
|---|---|---|
| `TeamA` / `TeamB` | `Event Bus Path` | `../MatchEventBus` |
| `RulesManager` | `Event Bus Path`, `Match Clock Path` | `../MatchEventBus`, `../MatchClock` |
| `FoulSystem` | `Bus Path`, `Match Clock Path`, `Team A Path`, `Team B Path` | respectivos nós |
| `HUD` | `Event Bus Path` | o `MatchEventBus` |
| `HumanInput_P1` | `Team Controller Path`, `Reference Camera` | `../TeamA`, `../GameplayCamera` |

## 8. Criar os PlayerRoles em `resources/roles/`

No FileSystem dock: clique direito em `resources/roles/` → **New Resource**
→ `PlayerRole` → preencha:

| Arquivo | Type | BasePosition (X, Y) |
|---|---|---|
| `role_gk.tres` | Goalkeeper | `(0.0, -0.95)` |
| `role_cb_l.tres` | CenterBack | `(-0.15, -0.6)` |
| `role_cb_r.tres` | CenterBack | `(0.15, -0.6)` |
| `role_fb_l.tres` | FullBack | `(-0.7, -0.55)` |
| `role_fb_r.tres` | FullBack | `(0.7, -0.55)` |
| `role_dm.tres` | DefensiveMid | `(0.0, -0.3)` |
| `role_cm_l.tres` | CentralMid | `(-0.3, -0.1)` |
| `role_cm_r.tres` | CentralMid | `(0.3, -0.1)` |
| `role_wg_l.tres` | Winger | `(-0.75, 0.3)` |
| `role_wg_r.tres` | Winger | `(0.75, 0.3)` |
| `role_st.tres` | Striker | `(0.0, 0.55)` |

Marque `CanUseHands = true` apenas no `role_gk.tres`.

Atribua o role correto a cada Player na cena `Match.tscn`.

## 9. Rodar

Pressione **F5**. Se for a primeira vez, o Godot pede para selecionar a cena
principal — escolha `Match.tscn`.

## Problemas comuns

- **"NodePath não encontrado"** — algum `[Export] NodePath` está vazio na cena.
  Abra o nó no editor e atribua o caminho.
- **Jogadores não se movem** — verifique se o `ControlSwitcher` e o
  `HumanInput` têm `NodePath`s preenchidos. O `Team` do `HumanInput` precisa
  bater com o `Team` de algum `TeamController`.
- **Bola atravessa o chão** — `Ball.tscn` precisa de `CollisionShape3D` e o
  campo precisa de `StaticBody3D + CollisionShape3D`. Verifique também as
  camadas de colisão.
- **Animações não tocam** — `AnimationTree.Active = true` e os nomes dos
  estados devem bater exatamente com os strings em `PlayerAnimator.cs`.
