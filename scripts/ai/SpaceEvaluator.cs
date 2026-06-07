using Godot;
using System.Collections.Generic;

namespace FootballGame;

/// <summary>
/// Funções puras de avaliação espacial usadas pela IA para movimentação sem bola.
/// Tudo no plano do gramado (Y ignorado). Sem estado — só matemática, fácil de testar.
/// </summary>
public static class SpaceEvaluator
{
    // Pesos do score de uma posição de ataque sem bola. Ajustar com playtesting.
    private const float WeightOpenness    = 0.30f;
    private const float WeightLane        = 0.25f;
    private const float WeightAdvancement = 0.30f;
    private const float WeightClustering  = 0.25f;
    private const float OffsidePenalty    = 1.00f;

    private const float MaxFieldLength    = 105f;  // para normalizar avanço
    private const float OpennessCap       = 10f;   // distância (m) a partir da qual "aberto" satura

    private static Vector3 Flat(Vector3 v) => new(v.X, 0f, v.Z);

    /// <summary>Distância de um ponto ao segmento AB (no plano).</summary>
    public static float DistancePointToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        p = Flat(p); a = Flat(a); b = Flat(b);
        var ab = b - a;
        float lenSq = ab.LengthSquared();
        if (lenSq < 0.0001f) return p.DistanceTo(a);
        float t = Mathf.Clamp((p - a).Dot(ab) / lenSq, 0f, 1f);
        return p.DistanceTo(a + ab * t);
    }

    /// <summary>Distância ao adversário mais próximo de <paramref name="pos"/>.</summary>
    public static float NearestOpponentDistance(Vector3 pos, IReadOnlyList<Player> opponents)
    {
        pos = Flat(pos);
        float min = float.MaxValue;
        foreach (var o in opponents)
        {
            float d = Flat(o.GlobalPosition).DistanceSquaredTo(pos);
            if (d < min) min = d;
        }
        return min == float.MaxValue ? 100f : Mathf.Sqrt(min);
    }

    /// <summary>Linha de passe de <paramref name="from"/> a <paramref name="to"/> está livre?</summary>
    public static bool IsPassingLaneClear(Vector3 from, Vector3 to,
                                          IReadOnlyList<Player> opponents, float interceptRadius)
    {
        foreach (var o in opponents)
            if (DistancePointToSegment(o.GlobalPosition, from, to) < interceptRadius)
                return false;
        return true;
    }

    /// <summary>
    /// Linha de impedimento (eixo X) para o time atacante: posição X do
    /// segundo defensor mais avançado (inclui o goleiro). Atacar além disso é impedimento.
    /// </summary>
    public static float OffsideLineX(int attackingTeam, IReadOnlyList<Player> defenders)
    {
        if (defenders.Count < 2)
            return attackingTeam == 0 ? 52.5f : -52.5f;

        float first, second;
        if (attackingTeam == 0)
        {
            first = second = float.NegativeInfinity;
            foreach (var d in defenders)
            {
                float x = d.GlobalPosition.X;
                if (x > first)  { second = first; first = x; }
                else if (x > second) second = x;
            }
        }
        else
        {
            first = second = float.PositiveInfinity;
            foreach (var d in defenders)
            {
                float x = d.GlobalPosition.X;
                if (x < first)  { second = first; first = x; }
                else if (x < second) second = x;
            }
        }
        return second;
    }

    /// <summary>Posição está além da linha de impedimento E à frente da bola?</summary>
    public static bool IsBeyondOffside(float posX, float ballX, float offsideLineX, int attackingTeam)
    {
        return attackingTeam == 0
            ? posX > offsideLineX && posX > ballX
            : posX < offsideLineX && posX < ballX;
    }

    /// <summary>
    /// Pontua uma posição candidata para movimentação ofensiva sem bola.
    /// Quanto maior, melhor. Combina espaço livre, linha de passe, avanço,
    /// e penaliza aglomeração e impedimento.
    /// </summary>
    public static float ScoreAttackingPosition(
        Vector3 pos, Vector3 ballPos, float attackingGoalX, float offsideLineX,
        int attackingTeam, IReadOnlyList<Player> opponents,
        IReadOnlyList<Player> teammates, Player self)
    {
        float openness = Mathf.Clamp(NearestOpponentDistance(pos, opponents) / OpennessCap, 0f, 1f);
        float lane     = IsPassingLaneClear(ballPos, pos, opponents, 1.5f) ? 1f : 0f;

        float advancement = 1f - Mathf.Abs(pos.X - attackingGoalX) / MaxFieldLength;
        advancement = Mathf.Clamp(advancement, 0f, 1f);

        float clustering = ClusteringPenalty(pos, teammates, self);

        float score = WeightOpenness    * openness
                    + WeightLane        * lane
                    + WeightAdvancement * advancement
                    - WeightClustering  * clustering;

        if (IsBeyondOffside(pos.X, ballPos.X, offsideLineX, attackingTeam))
            score -= OffsidePenalty;

        return score;
    }

    /// <summary>Penalidade 0..1 por estar perto de um companheiro (evita aglomeração).</summary>
    private static float ClusteringPenalty(Vector3 pos, IReadOnlyList<Player> teammates, Player self)
    {
        pos = Flat(pos);
        float min = float.MaxValue;
        foreach (var t in teammates)
        {
            if (t == self) continue;
            float d = Flat(t.GlobalPosition).DistanceSquaredTo(pos);
            if (d < min) min = d;
        }
        if (min == float.MaxValue) return 0f;
        float dist = Mathf.Sqrt(min);
        return Mathf.Clamp(1f - dist / 8f, 0f, 1f); // dentro de 8m → penaliza
    }
}
