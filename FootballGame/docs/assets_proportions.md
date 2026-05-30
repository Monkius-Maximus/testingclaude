# Guia de Proporções e Especificação de Assets

Documento de referência para a produção/importação de **modelos 3D, texturas,
UI e áudio**. Todas as medidas aqui estão **amarradas às constantes que já
existem no código** — seguir estes números garante que os assets encaixem sem
retrabalho de escala.

> **Regra de ouro da escala:** `1 unidade do Godot = 1 metro`.
> Configure o import de todo modelo (`.glb`/`.gltf`/`.fbx`) com **Scale = 1.0**
> e modele em metros. Nunca importe em centímetros (erro clássico do FBX, que
> entra 100× maior).

---

## 1. Campo / Estádio

As dimensões batem com `Ball.cs` (`FieldHalfLength = 52.5`, `FieldHalfWidth = 34`)
e com `Field.tscn`. São as medidas oficiais FIFA.

| Elemento | Medida (metros) | Constante no código |
|---|---|---|
| Comprimento do gramado | **105.0** (X: −52.5 a +52.5) | `FieldHalfLength = 52.5` |
| Largura do gramado | **68.0** (Z: −34 a +34) | `FieldHalfWidth = 34` |
| Linha central | X = 0 | `KickoffPosition (0,0.5,0)` |
| Grande área (profundidade × largura) | **16.5 × 40.32** | `BoxShape3D_penaltyA` |
| Pequena área (profundidade × largura) | 5.5 × 18.32 | (a desenhar na textura/linhas) |
| Marca do pênalti | 11.0 do gol | — |
| Círculo central (raio) | 9.15 | — |

**Origem e orientação:**
- O **centro do campo é a origem (0,0,0)**. Modele o estádio centralizado nela.
- Eixo **X = comprimento** (gol a gol). Eixo **Z = largura** (laterais). **Y = altura**.
- Time 0 (casa) defende o gol em **X = −52.5**; Time 1 (visitante) defende em **X = +52.5**.

**Recomendação de produção:** entregue o estádio em **3 LODs separados** — o
gramado+linhas precisa ser malha simples (a física do chão já está no
`StaticBody3D` do `Field.tscn`, então o modelo visual pode ser só estético).
Arquibancadas/torcida podem ser planos com textura (billboard) para performance.

---

## 2. Gol (trave)

Medidas oficiais, já refletidas em `Field.tscn`.

| Elemento | Medida (metros) | Constante |
|---|---|---|
| Distância entre postes (largura) | **7.32** | `BoxMesh_crossbar` size.Z = 7.32 |
| Altura (travessão) | **2.44** | `BoxMesh_postV` height = 2.44 |
| Espessura do poste | 0.12 | `BoxMesh_postV` (0.12 × 2.44 × 0.12) |
| Posição dos postes (Z) | ±3.66 | nós `PostLeft`/`PostRight` |

A rede pode ser modelada como malha com alpha ou simulada com `SoftBody3D`
(opcional, fase posterior). O modelo do gol deve ter sua **origem no centro da
linha do gol, no chão**, para encaixar nos nós `GoalA`/`GoalB`.

---

## 3. Bola

Bate com `Ball.tscn` (`radius = 0.11`) e com a massa oficial.

| Propriedade | Valor |
|---|---|
| Raio do modelo | **0.11 m** (≈ circunferência 69 cm) |
| Diâmetro | 0.22 m |
| Massa | 0.43 kg (já em `Ball.tscn`) |
| Origem do pivô | **centro geométrico da esfera** |
| Textura recomendada | 1024×1024, mapa UV esférico equiretangular |

---

## 4. Jogadores

Bate com os placeholders de `Player.tscn`/`Goalkeeper.tscn` (cápsula de
`radius 0.3`, `height 1.2`, posicionada a `y = 0.9`, dando ~1.8 m de pé).

| Propriedade | Valor alvo |
|---|---|
| **Altura total** | **1.80 m** (faixa aceitável 1.70–1.95) |
| Largura de ombros | ~0.45 m |
| **Pivô / origem do modelo** | **entre os pés, no chão (Y = 0)** |
| Orientação "frente" | olhando para **+Z** na pose de bind |

### 4.1 LODs (Level of Detail)

| LOD | Polígonos (tris) | Distância de switch | Uso |
|---|---|---|---|
| **LOD 0** | **8k–15k** | 0 – 20 m | Jogador em foco, câmera próxima |
| **LOD 1** | **2k–4k** | 20 – 50 m | Jogadores no campo, câmera normal |
| **LOD 2** | **300–600** | 50+ m | Substitutos no banco, perspectiva aérea |
| **Shadow mesh** | **200–400** | todos | Sombra projetada (só silhueta) |

O Godot 4 usa `VisibilityNotifier3D` + LODs automáticos em `.glb` quando o modelo
é exportado com LODs no mesmo arquivo (nomes `_lod1`, `_lod2` no Blender/Maya).

### 4.2 Rig / Esqueleto
- Use **esqueleto humanoide compatível com Mixamo** (mesma nomenclatura de
  bones). O `PlayerAnimator.cs` espera um `AnimationTree` com uma máquina de
  estados; quanto mais padrão o rig, mais direto o retarget.
- **Root motion desligado** — o movimento é dirigido por código
  (`Player.ExecuteIntentions`). As animações devem ser "in place".
- Goleiro precisa de bones extras: `Glove_L`, `Glove_R` (para luvas).

### 4.3 Animações necessárias (nomes esperados pelo código)
O `PlayerAnimator.cs` e o `CinematicDirector.cs` referenciam estes estados:

| Estado/clip | Tipo | Duração aprox. | Usado por |
|---|---|---|---|
| `Idle` | loop | — | locomoção parada |
| `Locomotion` (BlendSpace2D) | loop | — | andar/correr direcionais |
| `Sprint` | loop | — | corrida máxima |
| `Kick` | one-shot | 0.5–0.8 s | `PlayKick()` |
| `SlideTackle` | one-shot | 0.8–1.0 s | `PlaySlideTackle()` |
| `Header` | one-shot | 0.6–0.8 s | `PlayHeader()` |
| `Stumble` | one-shot | 0.6 s | `PlayStumble()` (quem sofre o gol) |
| `Celebrate` | one-shot | 2–3 s | `PlayCelebrate()` (quem marca) |
| `Dive` | one-shot | 0.5–0.7 s | `PlayDive()` (goleiro) |

> As one-shot precisam de um **Method Track** no último frame chamando
> `OnActionFinished()` (já implementado), senão a locomoção não retorna.

> O BlendSpace2D de `Locomotion` usa eixo **X = strafe** e **Y = velocidade
> normalizada (0..1)**, com sprint normalizado por **13 m/s** (ver
> `PlayerAnimator.NormalizedSpeed`).

---

## 5. Uniformes / Texturas de jogador

| Asset | Resolução | Canais | Observação |
|---|---|---|---|
| `kit_base_mask.png` | **1024×1024** | RGB | Máscara do KitShader (ver guia de modulares) |
| Face texture (`face_01.png`...) | **512×512** | RGBA | 1 textura por variante de rosto (6 sugeridas) |
| Normal map (corpo) | 1024×1024 | RG | Detalhes de tecido/músculo |
| ORM (occlusion/rough/metal) | 512×512 | RGB | 1 arquivo = 3 mapas |

**Máscara `kit_base_mask.png` (como pintar):**
- Canal **R = 0 (preto)** → zona fica com `PrimaryColor` do time
- Canal **R = 127 (cinza)** → zona fica com `SecondaryColor`
- Canal **R = 255 (branco)** → zona neutra (costura, logo, sola)
- Canal **G** → pele humana (multiplicado pelo parâmetro `skin_tone`)

Hoje o `MatchBootstrap.SetTeamColor()` aplica cor sólida (azul/vermelho).
O sistema de KitShader substitui isso sem mudar o mesh — veja `docs/modular_assets_guide.md`.

---

## 6. Escudos / Bandeiras (crests)

Referenciado por `TeamData.CrestPath`.

| Asset | Proporção | Resolução | Formato |
|---|---|---|---|
| Escudo do clube | **1:1 (quadrado)** | 512×512 | PNG com alpha |
| Bandeira de país | 3:2 | 300×200 | PNG |
| Versão "mini" (HUD) | 1:1 | 64×64 | PNG com alpha |

Caminho sugerido: `res://assets/textures/teams/<team_id>.png`.

---

## 7. UI / HUD

**Resolução base de design: 1920×1080 (16:9).** Configure o projeto com
`stretch mode = canvas_items` e `aspect = expand` para escalar bem em outras
telas.

| Elemento | Tamanho de referência (px @1080p) | Observação |
|---|---|---|
| Placar (já em `HUD.tscn`) | bloco ~240×40, topo-centro | fonte 32 px |
| Banner de gol | ~400×60, a 30% da altura | fonte 36 px |
| Ícone de time no HUD | 64×64 | usa o crest mini |
| Botão de menu | 320×72 | padding 16 px |
| Margem segura (safe area) | 5% das bordas | evita corte em TVs |

### 7.1 Fontes
- Coloque fontes em `res://assets/fonts/`.
- Use **`.ttf`/`.otf`** (o Godot rasteriza em qualquer tamanho — não precisa de
  bitmap). Recomendo uma família condensada estilo esportivo para placar +
  uma neutra para corpo de texto.

### 7.2 Ícones / sprites de UI
- Exporte em **PNG com alpha**, em **@1x e @2x** (ex.: 32 e 64) ou um único SVG.
- Para nitidez, prefira **múltiplos de 4 px**.

---

## 8. Áudio

Pastas já existem: `assets/audio/music` e `assets/audio/sfx`.

| Tipo | Formato | Sample rate | Observação |
|---|---|---|---|
| Música (menu, comemoração) | `.ogg` (Vorbis) | 44.1 kHz | loopável; marque loop no import |
| SFX curtos (chute, apito, rede) | `.wav` | 44.1 kHz | latência baixa |
| Ambiente de torcida | `.ogg` | 44.1 kHz | loop longo, estéreo |
| Locução/narração (futuro) | `.ogg` | 44.1 kHz | mono por frase |

Evite MP3 (o `.ogg` tem melhor loop e licença livre).

---

## 9. Formatos de importação preferidos (resumo)

| Categoria | Formato recomendado | Por quê |
|---|---|---|
| Modelos 3D + animação | **`.glb`** (glTF binário) | melhor suporte no Godot 4, traz malha+rig+anim num arquivo |
| Texturas | **PNG** (UI/alpha) / **WebP** (mundo) | qualidade + tamanho |
| Áudio | **OGG** (longo) / **WAV** (curto) | loop + latência |
| Fontes | **TTF/OTF** | rasterização dinâmica |

### Checklist por modelo antes de mandar pro projeto
- [ ] Escala em metros (Scale=1.0 no import)
- [ ] Pivô na origem correta (pés no chão / centro da bola / linha do gol)
- [ ] Eixo "frente" = +Z
- [ ] Sem root motion nas animações de jogador
- [ ] Nomes de clips batendo com a tabela da seção 4.2

---

## 10. Onde cada coisa vai no repositório

```
assets/
├── models/
│   ├── players/      ← jogadores .glb (rig humanoide)
│   ├── ball/         ← bola .glb (opcional; já há placeholder)
│   └── stadiums/     ← estádio/gramado .glb
├── textures/
│   ├── teams/        ← escudos <team_id>.png (512×512)
│   └── kits/         ← uniformes (2048²)
├── materials/        ← .tres de StandardMaterial3D reutilizáveis
├── fonts/            ← .ttf/.otf
└── audio/
    ├── music/        ← .ogg
    └── sfx/          ← .wav
```

Quando você subir os assets nessas pastas, eu faço o wiring: troco os
placeholders (cápsula/esfera/cor sólida) pelos modelos reais, configuro o
`AnimationTree` e ligo escudos/uniformes ao `TeamData`.
