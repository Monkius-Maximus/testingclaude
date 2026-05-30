# Estratégia de Assets Modulares

Objetivo: criar o **máximo de variação visual com o mínimo de assets manuais**.
A chave é separar *forma* (mesh), *cor* (shader) e *identidade* (textura/blend shape)
em camadas independentes, combinando-as em runtime pelo código C#.

---

## 1. Sistema de Kit (uniforme)

### O problema sem modularidade
Sem sistema modular, para 10 times × 3 kits × 22 jogadores precisaria de
**660 meshes únicos**. Isso é inviável.

### A solução: KitShader + 1 mesh universal

```
UniformeBase.glb
└── KitMesh  ← uma única malha: camisola + calções + meias + chuteiras
    └── KitShader.gdshader
            uniform vec3 primary_color;    ← cor 1 do time (do TeamData)
            uniform vec3 secondary_color;  ← cor 2 do time (do TeamData)
            uniform vec3 trim_color;       ← detalhes/barra/gola
            uniform float skin_tone;       ← 0.0 (escuro) .. 1.0 (claro)
            uniform sampler2D base_mask;   ← textura P&B que define "quem recebe qual cor"
            uniform sampler2D number_tex;  ← número gerado em runtime
```

**A `base_mask` é grayscale:**
- `R = 0` → zona recebe `primary_color`
- `R = 128` → zona recebe `secondary_color`
- `R = 255` → zona recebe `trim_color`
- Canal G → zone de pele (multiplicado por `skin_tone`)
- Canal B → zona sem tint (costura, logotipo, couro da chuteira)

Resultado: **1 mesh + 1 shader → variações ilimitadas** só mudando os três
`Color` no `TeamData`. O `MatchBootstrap.SetTeamColor()` já tem acesso a
`TeamData.PrimaryColor` e `SecondaryColor` — é só substituir a lógica de
cor sólida pela chamada ao shader.

### Número e nome dinâmicos no dorso

Use um **SubViewport pequeno (128×64)** que renderiza um `Label` com o número.
Converta para `ImageTexture` em runtime e passe como `number_tex` ao shader.
O código já tem `PlayerId` e `Role` por jogador — o número é derivado disso.

```csharp
// Em PlayerVisuals.cs (a criar)
void ApplyKitNumber(int number)
{
    var vp   = GetNode<SubViewport>("NumberViewport");
    var lbl  = vp.GetNode<Label>("NumberLabel");
    lbl.Text = number.ToString();
    vp.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    CallDeferred(nameof(UpdateNumberTexture));
}
```

---

## 2. Sistema de Peças Modulares do Personagem

Em vez de 22 meshes únicos de jogador, use **camadas de swap**:

```
Jogador (Node3D)
├── Skeleton3D  ← rig Mixamo compartilhado (todos os jogadores usam o mesmo)
├── MeshInstance3D "Body"      ← silhueta base (nunca visível sozinha)
├── MeshInstance3D "Kit"       ← camisola+calções (skin = universal, cor = shader)
├── MeshInstance3D "Boots"     ← 1 de 4 variantes (A/B/C/D)
├── MeshInstance3D "Hair"      ← 1 de 8 variantes (low/afro/careca/curto...)
└── MeshInstance3D "Head"      ← 1 cabeça com textura swappable (6 faces × 3 tons)
```

### Quantos assets você precisa produzir

| Peça | Variantes | Combinações geradas |
|---|---|---|
| Kit mesh | **1** (universal) | ∞ (cores via shader) |
| Boots | **4** malhas | 4 |
| Hair | **8** malhas | 8 |
| Head | **1** malha + **6** texturas de rosto | 6 |
| Skin tone | parâmetro de shader (não é mesh) | 3 |
| **Total assets** | **~20** | **4 × 8 × 6 × 3 = 576 personagens distintos** |

Compare com a abordagem ingênua: 576 assets manuais.

### Recurso PlayerAppearance (.tres)

```csharp
[GlobalClass]
public partial class PlayerAppearance : Resource
{
    [Export] public int  HairVariant  = 0;   // 0-7 → qual Hair mesh carregar
    [Export] public int  BootsVariant = 0;   // 0-3
    [Export] public int  FaceVariant  = 0;   // 0-5
    [Export] public float SkinTone    = 0.5f; // 0-1
    [Export] public int  KitNumber    = 9;
    [Export] public string KitName    = "";
}
```

Assim você cria um `.tres` por jogador no Godot Inspector e nunca toca no código.
`MatchBootstrap` passa o `PlayerAppearance` para o player ao spawnar.

---

## 3. Estádio Modular (Kit de Partes)

Em vez de um estádio monolítico, produza **peças que se encaixam**:

```
assets/models/stadium/
├── bleacher_straight.glb    ← arquibancada reta (105 m de comprimento)
├── bleacher_corner.glb      ← curva de canto
├── roof_canopy.glb          ← cobertura (encaixa sobre bleacher_straight)
├── floodlight_mast.glb      ← torre de iluminação
├── adboard.glb              ← placa de publicidade (textura swappable)
└── pitch_surface.glb        ← gramado visual (a física está no StaticBody3D)
```

**Montar um estádio** = 2× straight_long + 2× straight_short + 4× corner + 4×
floodlight. Diferentes combinações dão estádios pequenos, médios ou grandes,
sem precisar modelar um novo estádio inteiro.

A cena `Field.tscn` já tem o `StaticBody3D` de física — o estádio visual fica
como filho de um nó `StadiumVisuals` separado, com impacto zero na física.

---

## 4. Variação de Torcida com Billboards

Para simular torcida cheia sem custo de polígonos:

- Produza **8–12 sprites de torcedor** (PNG 128×256, vários tons de pele + roupa)
- Na cena, preencha as arquibancadas com `GPUParticles3D` ou `MultiMesh` de
  quadriláteros billboard — o Godot renderiza milhares a custo de draw call único
- Alterne aleatoriamente entre os sprites para dar variedade

---

## 5. Variação de Time sem Modelos Novos

Com o sistema acima, para adicionar um **novo time** você só precisa de:

1. Um arquivo `team_novotime.tres` com `PrimaryColor` e `SecondaryColor`
2. Opcionalmente, `crest_novotime.png` (512×512)
3. Nenhum modelo 3D novo

Os jogadores recebem `PlayerAppearance` aleatório do pool existente — igual
ao como times de futebol real têm jogadores visualmente variados sem ter
meshes únicos para cada um.

---

## 6. Pipeline de Produção Sugerido

### Fase 1 — Core visual (prioridade)
1. **1 jogador de campo** com todas as animações (seção 4.2 do guia de assets)
2. **1 mesh de kit** com a `base_mask` corretamente pintada
3. **KitShader.gdshader** (veja esquema acima)
4. **4 variantes de hair** + **1 variante de boots** (o resto vem depois)

→ Com isso você já tem 22 jogadores únicos na tela em 2 cores diferentes.

### Fase 2 — Variedade
5. +4 variantes de hair, +3 variantes de boots
6. +5 variantes de rosto (texturas)
7. Kit do goleiro (malha diferente — cobrindo os braços)

### Fase 3 — Ambiente
8. Gramado visual + goalposts modelo detalhado
9. Arquibancada modular (3 peças)

### Fase 4 — Polimento
10. Torcida billboard
11. Floodlights com iluminação real
12. Placares/letreiros texturizados

---

## 7. Estrutura de pastas recomendada

```
assets/
├── models/
│   ├── characters/
│   │   ├── player_base.glb          ← mesh base universal
│   │   ├── hair_variants/
│   │   │   ├── hair_00_short.glb
│   │   │   ├── hair_01_long.glb
│   │   │   └── ...
│   │   ├── boots_variants/
│   │   │   ├── boots_A.glb
│   │   │   └── ...
│   │   └── goalkeeper_base.glb      ← variante com braços cobertos
│   ├── ball/
│   │   └── ball.glb
│   └── stadium/
│       ├── pitch_surface.glb
│       ├── bleacher_straight.glb
│       ├── bleacher_corner.glb
│       └── floodlight_mast.glb
├── textures/
│   ├── characters/
│   │   ├── kit_base_mask.png        ← a "receita" do KitShader
│   │   ├── face_01.png .. face_06.png
│   │   └── boots_A_albedo.png ..
│   └── teams/
│       ├── crest_team_home.png      ← 512×512 PNG com alpha
│       └── crest_team_away.png
├── shaders/
│   └── kit_shader.gdshader
├── materials/                       ← .tres reutilizáveis
├── fonts/
└── audio/
    ├── music/
    └── sfx/
```
