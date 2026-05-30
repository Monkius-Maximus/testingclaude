# Guia de Inserção de Assets 3D

Documento de referência completo: **onde colocar cada modelo no projeto**,
qual nó substituir, qual propriedade configurar no Inspector do Godot, e
o que o código espera de cada asset.

---

## 1. Jogador de Campo

### Arquivo esperado
`assets/models/characters/player_base.glb`

### Onde inserir no Godot
1. Abra `scenes/entities/Player.tscn`
2. Selecione o nó **MeshInstance3D** (atualmente tem uma CapsuleMesh placeholder)
3. No Inspector → **Mesh** → clique no ícone de pasta → selecione `player_base.glb`
4. O `.glb` deve conter o rig Mixamo embutido. Godot importa automaticamente
   como `Skeleton3D` + `MeshInstance3D` aninhados

### O que o código usa do nó
| Código | O que precisa |
|---|---|
| `MatchBootstrap.SetTeamColor()` | `MeshInstance3D` chamado **"MeshInstance3D"** |
| `PlayerAnimator.cs` | `AnimationTree` na raiz ou filho direto do Player |
| `AIBrain.cs` | Apenas `GlobalPosition` — não depende de mesh |

### Configuração obrigatória no Inspector após importar
- Nó raiz do `.glb` → renomear para `PlayerMesh`
- **Skeleton3D** → deixar como filho de `PlayerMesh`
- Criar nó **AnimationTree** como filho do Player:
  - `root_node = NodePath(".")`  
  - Adicionar **AnimationNodeStateMachine** e mapear os estados da seção 4 do `assets_proportions.md`

---

## 2. Goleiro

### Arquivo esperado
`assets/models/characters/goalkeeper_base.glb`

### Onde inserir
1. Abra `scenes/entities/Goalkeeper.tscn`
2. Mesmo processo do jogador de campo acima
3. O goleiro usa o mesmo rig — pode ser o mesmo arquivo `.glb` ou variante com manga longa

### Diferença vs jogador de campo
- `Goalkeeper.cs` chama `Animator.PlayDive()` — garanta que o clip `Dive` exista no AnimationTree
- O `PenaltyArea` (Area3D) já está no nó filho — **não remova esse filho ao substituir o mesh**

---

## 3. Variantes de Hair

### Arquivos esperados
```
assets/models/characters/hair_variants/
├── hair_00_short.glb
├── hair_01_curly.glb
├── hair_02_afro.glb
├── hair_03_long.glb
├── hair_04_bald.glb   (cap de goleiro)
├── hair_05_mohawk.glb
├── hair_06_dreadlocks.glb
└── hair_07_ponytail.glb
```

### Onde inserir
Adicione como **MeshInstance3D filhos** do nó PlayerMesh, com o mesmo
Skeleton3D como `Skeleton` em **Mesh → Skin**. No futuro `PlayerVisuals.cs`
(a criar) terá um `[Export] int HairVariant` que ativa/desativa o filho correto.

---

## 4. Variantes de Chuteira

### Arquivos esperados
```
assets/models/characters/boots_variants/
├── boots_A.glb   (chuteiras básicas)
├── boots_B.glb   (chuteiras de campo sintético)
├── boots_C.glb   (chuteiras de grama)
└── boots_D.glb   (chuteiras de goleiro/couro)
```

### Onde inserir
Mesmo processo do hair — filhos do PlayerMesh como MeshInstance3D skinned.
Os ossos `LeftFoot` e `RightFoot` do rig Mixamo devem existir no Skeleton3D.

---

## 5. Bola

### Arquivo esperado
`assets/models/ball/ball.glb`

### Onde inserir
1. Abra `scenes/entities/Ball.tscn`
2. Selecione o nó **MeshInstance3D** (atualmente tem SphereMesh de radius=0.11)
3. Inspector → **Mesh** → substituir pela mesh do `.glb`
4. Não altere o `CollisionShape3D` — a esfera de raio 0.11 é usada para física

### Atenção
- O modelo deve ter **pivot no centro geométrico** (não na base)
- Raio do modelo deve bater com 0.11 m (a bola tem ~22 cm de diâmetro)

---

## 6. Gramado / Campo Visual

### Arquivo esperado
`assets/models/stadium/pitch_surface.glb`

### Onde inserir
1. Abra `scenes/match/Field.tscn`
2. O `StaticBody3D` chamado **PitchBody** tem um BoxMesh apenas para física — **não substituir esse nó**
3. Crie um nó irmão: **Node3D** chamado `PitchVisual`
4. Adicione o `.glb` importado como filho de `PitchVisual`
5. O BoxMesh de física permanece invisível (desative `Visible` nele se quiser)

### Dimensões exatas esperadas pelo campo
```
Comprimento total: 105 m   (X: -52.5 a +52.5)
Largura total:      68 m   (Z: -34 a +34)
Espessura:         0.2 m   (Y: -0.1 a +0.1)
Origem do modelo: (0, 0, 0)
```

---

## 7. Traves e Rede (Gol)

### Arquivos esperados
```
assets/models/stadium/
├── goalpost.glb     ← um par de traves + travessão + rede
```

### Onde inserir
1. `scenes/match/Field.tscn` → nó **GoalA** (em X = -52.5)
2. Os `BoxMesh` atuais nos filhos PostLeft/PostRight/Crossbar são colisores — mantê-los
3. Adicione o `.glb` visual como filho de GoalA, posicionado em `Vector3(0, 0, 0)` local
4. Repita para **GoalB** (espelhe: `scale.X = -1` ou use versão virada)

### Origem do modelo
O centro da **linha do gol ao nível do chão** — igual ao nó GoalA/B em código.

### Dimensões (FIFA)
| Parte | Medida |
|---|---|
| Largura interna (poste a poste) | 7.32 m |
| Altura (chão ao travessão) | 2.44 m |
| Espessura dos postes | 0.12 m |
| Profundidade da rede | 2.0 m (opcional) |

---

## 8. Arquibancada / Estádio

### Arquivos esperados (modulares)
```
assets/models/stadium/
├── bleacher_straight.glb   ← segmento reto de arquibancada
├── bleacher_corner.glb     ← curva de canto
├── roof_canopy.glb         ← cobertura (opcional)
└── floodlight_mast.glb     ← torre de iluminação
```

### Onde inserir
1. `scenes/match/Field.tscn` → crie um nó **Node3D** chamado `StadiumVisuals`
2. Importe os `.glb` e adicione como filhos de `StadiumVisuals`
3. Posicione manualmente pelo Inspector:
   - Bleacher lateral longo: `position.X = 0`, `position.Z = ±38` (fora do campo)
   - Bleacher curto de fundo: `position.X = ±58`, `rotation.Y = 90°`
   - Corners nos 4 cantos: `position = (±56, 0, ±36)`, `rotation.Y = 0/90/180/270°`

### Não afeta física
`StadiumVisuals` é puramente visual — a física do chão está no `StaticBody3D` do PitchBody.

---

## 9. Escudos dos Times

### Arquivos esperados
```
assets/textures/teams/
├── crest_team_home.png   ← 512×512 PNG com canal alpha
└── crest_team_away.png
```

### Onde configurar
1. Abra `resources/leagues/team_home.tres` no Inspector
2. Propriedade **CrestPath** → clique na pasta → selecione `crest_team_home.png`
3. Repita para `team_away.tres`

### Onde o código usa
`HUD.cs` e telas de resultado leem `TeamData.CrestPath` e carregam com `GD.Load<Texture2D>()`.
Enquanto o path estiver vazio, simplesmente não aparece escudo.

---

## 10. Rostos dos Jogadores

### Arquivos esperados
```
assets/textures/characters/
├── face_01.png   ← 512×512 PNG
├── face_02.png
├── face_03.png
├── face_04.png
├── face_05.png
└── face_06.png
```

### Como aplicar
O futuro `PlayerVisuals.cs` lerá `PlayerData.FaceVariant` (int 0–5) e fará:
```csharp
var mat = mesh.GetActiveMaterial(0) as StandardMaterial3D;
mat.AlbedoTexture = GD.Load<Texture2D>($"res://assets/textures/characters/face_{faceVariant+1:D2}.png");
```
Por enquanto não há código de face — o slot está reservado.

---

## 11. KitShader (Uniforme com Cores Dinâmicas)

### Arquivo esperado
`assets/shaders/kit_shader.gdshader`

### Arquivos de textura
```
assets/textures/characters/
└── kit_base_mask.png   ← 1024×1024 — R=primária, G=pele, B=neutro
```

### Onde configurar
1. No `Player.tscn` → MeshInstance3D → **Material Override**
2. Crie um `ShaderMaterial` → **Shader** → selecione `kit_shader.gdshader`
3. Parâmetros do shader:
   - `base_mask`: a `kit_base_mask.png`
   - `primary_color`: deixe em branco (será preenchido pelo `MatchBootstrap.SetTeamColor()`)
   - `secondary_color`: idem

### Atualização de código necessária
Quando o shader estiver pronto, edite `MatchBootstrap.SetTeamColor()`:
```csharp
// Substitua a linha mat.AlbedoColor por:
var shaderMat = mesh.GetActiveMaterial(0) as ShaderMaterial;
shaderMat?.SetShaderParameter("primary_color", color);
shaderMat?.SetShaderParameter("secondary_color", secondaryColor);
```

---

## 12. Áudio

### Estrutura de pastas e arquivos esperados
```
assets/audio/
├── music/
│   ├── menu_theme.ogg        ← loop do menu principal
│   ├── match_ambient.ogg     ← música de fundo da partida
│   ├── halftime.ogg          ← intervalo
│   ├── victory.ogg           ← tela de resultado (vitória)
│   └── defeat.ogg            ← tela de resultado (derrota)
└── sfx/
    ├── kick.wav              ← chute
    ├── goal.ogg              ← gol (curto)
    ├── whistle.wav           ← apito curto (falta)
    ├── whistle_long.wav      ← apito longo (pênalti/fim de jogo)
    ├── card.wav              ← som de cartão
    ├── crowd_loop.ogg        ← loop de torcida
    ├── crowd_goal.ogg        ← torcida gritando gol
    └── save.wav              ← defesa do goleiro
```

### Como funciona
O **AudioManager** (autoload) monitora essas pastas. Quando você colocar qualquer
arquivo nesses caminhos, o som começa a funcionar automaticamente sem nenhuma
mudança de código. As chamadas já estão nos lugares certos:
- `MatchBootstrap` → `PlayMusic("match")`
- `RefereeManager` → `PlaySfx("whistle")`, `PlaySfx("card")`
- `CinematicDirector` → `PlaySfx("goal")`, `PlaySfx("crowd_goal")`

---

## 13. Fontes

### Onde colocar
`assets/fonts/`

### Como configurar
1. No Godot: **Project → Project Settings → Theme** → crie um `Theme` global
2. Ou por nó: qualquer Label/Button → Inspector → **Custom Fonts** → selecione o `.ttf`

---

## Resumo: O que implementar primeiro para ver resultado imediato

| Prioridade | Asset | Impacto visual |
|---|---|---|
| 1 | `player_base.glb` | 22 jogadores com modelo real em campo |
| 2 | `ball.glb` | Bola com visual correto |
| 3 | `pitch_surface.glb` | Campo com grama e linhas |
| 4 | `goalpost.glb` | Traves reais (substitui BoxMesh branco) |
| 5 | `crest_home/away.png` | Escudos no HUD e menus |
| 6 | `kit_base_mask.png` + shader | Times em cores reais do `TeamData` |
| 7 | Variantes de hair/boots | Variedade visual nos jogadores |
| 8 | `bleacher_straight.glb` | Arquibancada ao redor do campo |
| 9 | SFX (kick.wav, whistle.wav) | Som imediato sem outros assets |
| 10 | `crowd_loop.ogg` | Atmosfera de estádio |

---

## Editor In-Game (Roadmap)

A pedido, o projeto será expandido futuramente com um editor in-game para:

### Editor de Jogadores
- Editar `PlayerData` (nome, atributos, aparência) de um jogador do elenco
- Selecionar variante de hair/boots/face
- Preview em tempo real do jogador 3D com o kit do time

**Nós envolvidos:** `PlayerData.cs`, futuro `PlayerVisuals.cs`, `SubViewport` para preview

### Editor de Clubes
- Editar `TeamData` (nome, cores primária/secundária, escudo)
- O shader de kit atualiza em tempo real no preview
- Importar PNG como escudo diretamente (via `FileDialog`)

**Nós envolvidos:** `TeamData.cs`, `KitShaderMaterial`, `TextureRect` para preview

### Editor de Estádios
- Posicionar as peças modulares de estádio em grid
- Definir capacidade, nome, e cidade
- Salvar como recurso `.tres` de `StadiumData` (a criar)

**Nós envolvidos:** `StadiumData.cs` (a criar), `Node3D` com peças modulares,
`GridMap` ou posicionamento livre com snapping

> O editor in-game é a etapa mais complexa. A base modular do código e dos
> assets (especialmente kit shader + peças de estádio) deve estar sólida antes
> de começar o editor, pois ele apenas expõe visualmente o que já existe em dados.
