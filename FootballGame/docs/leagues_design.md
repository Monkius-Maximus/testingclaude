# Sistema de Ligas e Prestígio

Documento de planejamento. Reflete decisões discutidas em
[`futeboldocs1.md`](futeboldocs1.md) sobre o sistema de "rankings" de liga e
multiplicadores de XP.

## Objetivo

Brilhar em uma partida da Premier League deve render muito mais XP no modo
carreira do que uma performance idêntica na 3ª divisão do Brasileirão.
A diferença é modelada por **multiplicador de partida**:

```
XP_ganha = Nota_da_partida × Multiplicador_da_partida

Multiplicador_da_partida = Prestígio_da_liga + Bônus_da_competição
```

## Tiers de prestígio

A enum `LeagueData.PrestigeTier` resume:

| Tier | Multiplicador base | Exemplos |
|---|---|---|
| `SuperLeague` | 1.0 | Premier League, La Liga, Bundesliga, Serie A, Ligue 1, Brasileirão A |
| `EliteLeague` | 0.7 | Primeira Liga (PT), Eredivisie, Jupiler (BE), MLS |
| `RegionalForce` | 0.4 | Liga Profesional (AR), Primera División (PY), Liga MX |
| `DevelopingLeague` | 0.2 | Demais |

Estes valores são **chutes iniciais** baseados em coeficientes UEFA e
relevância de mercado. Ajustar com playtesting.

## Bônus de competição

Adicionados ao multiplicador da liga durante torneios:

| Competição | Bônus |
|---|---|
| Amistoso | -0.5 (ou multiplicador final mínimo 0.1) |
| Fase de grupos Champions League | +1.0 |
| Mata-mata Champions | +1.5 |
| Final Champions | +2.0 |
| Fase de grupos Copa do Mundo | +1.0 |
| Mata-mata Copa do Mundo | +2.0 |
| Final Copa do Mundo | +3.0 |
| Copa nacional (jogo regular) | +0.2 |
| Final de copa nacional | +1.0 |

## Geração de ligas para países sem dados

Para um país sem liga modelada, o jogador pode pedir "gerar liga". O
algoritmo proposto:

1. **Banco de prefixos genéricos:** `FC`, `Sporting`, `Atlético`, `Real`,
   `Club`, `Deportivo`, `União`, `Nacional`, etc.
2. **Sufixos regionais:** nome de capital, cidade conhecida, ou termos
   geográficos do país.
3. **Combinação:** `prefix + sufixo regional` → "Sporting Capitalino",
   "Atlético del Norte", "FC União Brasiliense".
4. **Tier:** automaticamente `DevelopingLeague` (multiplicador 0.2),
   editável pelo jogador.
5. **Força dos clubes:** distribuição em sino — 1 favorito (overall 75-80),
   2-3 médios (65-72), restante zebras (50-65).

A flag `LeagueData.IsGenerated = true` marca essas ligas para que o sistema
saiba que pode regenerá-las (re-roll) se o jogador quiser.

## Principais ligas e seleções a modelar no MVP

### Ligas — implementar primeiro
1. Premier League (ENG)
2. La Liga (ESP)
3. Bundesliga (GER)
4. Serie A (ITA)
5. Ligue 1 (FRA)
6. Brasileirão Série A (BRA)
7. Primeira Liga (POR)
8. Eredivisie (NED)
9. Major League Soccer (USA/CAN)
10. Liga Profesional (ARG)

### Seleções — implementar primeiro

**UEFA:** França, Espanha, Itália, Alemanha, Portugal, Holanda, Bélgica,
Inglaterra, Croácia

**CONMEBOL:** Argentina, Brasil, Uruguai, Colômbia, Equador

**CONCACAF:** México, Estados Unidos, Canadá

**CAF:** Marrocos, Senegal, Egito, Nigéria, Camarões, Gana

**AFC:** Japão, Coreia do Sul, Irã, Arábia Saudita, Austrália

## Cálculo da "Nota da partida"

Ainda em design. Provável heurística:

```
Nota = 6.0 (baseline)
     + 1.0 por gol
     + 0.5 por assistência
     + 0.5 a 1.5 por defesa importante (goleiro)
     + 0.2 por desarme bem-sucedido
     - 0.5 por erro grave (perde a bola na defesa)
     - 1.0 por cartão amarelo
     - 2.0 por cartão vermelho
```

Limitada a 0..10.

Multiplicador final aplicado:

```csharp
float xp = nota * GameManager.CurrentExperienceMultiplier;
```

`GameManager.CurrentExperienceMultiplier` é preenchido pelo sistema de
competição antes da partida começar.

## Open questions

- Como tratar partidas internacionais entre clubes de tiers diferentes
  (PSG vs time de Andorra na Champions)? Provável: usar o tier do
  **adversário**, não da própria liga.
- Stamina/forma como fator multiplicador adicional?
- Bônus para jogos decisivos no campeonato (último jogo, decisão de título)?
