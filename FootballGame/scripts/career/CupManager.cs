using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FootballGame;

/// <summary>
/// Copa eliminatória (mata-mata) ligada ao modo carreira. Gera um chaveamento
/// de 8 times, avança rodadas conforme partidas são disputadas e determina
/// o campeão. O usuário joga suas partidas; as demais são simuladas.
/// </summary>
public partial class CupManager : Node
{
    public class CupData
    {
        [JsonPropertyName("teamIds")]   public List<string>   TeamIds    = new();
        [JsonPropertyName("rounds")]    public List<CupRound> Rounds     = new();
        [JsonPropertyName("roundIdx")]  public int            RoundIndex = 0;
        [JsonPropertyName("champion")]  public string         Champion   = "";
    }

    public class CupRound
    {
        [JsonPropertyName("matches")] public List<CupMatch> Matches = new();
        [JsonPropertyName("name")]    public string         Name    = "";
    }

    public class CupMatch
    {
        [JsonPropertyName("home")]    public string HomeTeamId  = "";
        [JsonPropertyName("away")]    public string AwayTeamId  = "";
        [JsonPropertyName("sh")]      public int    ScoreHome   = 0;
        [JsonPropertyName("sa")]      public int    ScoreAway   = 0;
        [JsonPropertyName("pen_sh")]  public int    PenHome     = 0;
        [JsonPropertyName("pen_sa")]  public int    PenAway     = 0;
        [JsonPropertyName("played")]  public bool   IsPlayed    = false;
        [JsonPropertyName("user")]    public bool   IsUserMatch = false;
        [JsonPropertyName("winner")]  public string WinnerId    = "";
    }

    private static readonly string[] RoundNames = { "Quartas de Final", "Semifinal", "Final" };

    public CupData Current { get; private set; }
    public bool    HasCup  => Current != null && Current.Champion == "";

    private CareerManager _career;
    private readonly Random _rng = new();

    public override void _Ready() =>
        _career = GetNodeOrNull<CareerManager>("/root/CareerManager");

    // ── API ───────────────────────────────────────────────────────

    public void GenerateCup(List<string> teamIds)
    {
        var shuffled = teamIds.OrderBy(_ => _rng.Next()).Take(8).ToList();
        while (shuffled.Count < 8) shuffled.Add($"ghost_{shuffled.Count}");

        Current = new CupData { TeamIds = shuffled };
        Current.Rounds.Add(BuildRound(shuffled, 0));
    }

    public CupMatch GetNextUserMatch()
    {
        if (Current == null || Current.RoundIndex >= Current.Rounds.Count) return null;
        return Current.Rounds[Current.RoundIndex].Matches
            .FirstOrDefault(m => !m.IsPlayed && m.IsUserMatch);
    }

    public void SimulateNonUserMatches()
    {
        if (Current == null) return;
        var round = Current.Rounds[Current.RoundIndex];
        foreach (var m in round.Matches)
        {
            if (!m.IsPlayed && !m.IsUserMatch) SimulateMatch(m);
        }
    }

    public void ApplyUserMatchResult(int scoreHome, int scoreAway, int penHome = 0, int penAway = 0)
    {
        var m = GetNextUserMatch();
        if (m == null) return;

        m.ScoreHome = scoreHome; m.ScoreAway = scoreAway;
        m.PenHome   = penHome;   m.PenAway   = penAway;
        m.IsPlayed  = true;
        SetWinner(m);
        TryAdvanceRound();
    }

    // ── Lógica interna ────────────────────────────────────────────

    private void TryAdvanceRound()
    {
        var round = Current.Rounds[Current.RoundIndex];
        if (round.Matches.Any(m => !m.IsPlayed)) return;

        var winners = round.Matches.Select(m => m.WinnerId).ToList();

        if (winners.Count == 1)
        {
            Current.Champion = winners[0];
            GD.Print($"Campeão da Copa: {Current.Champion}");
            return;
        }

        Current.RoundIndex++;
        Current.Rounds.Add(BuildRound(winners, Current.RoundIndex));
    }

    private CupRound BuildRound(List<string> teams, int roundIdx)
    {
        var round = new CupRound
        {
            Name = roundIdx < RoundNames.Length ? RoundNames[roundIdx] : $"Rodada {roundIdx + 1}"
        };

        string playerTeamId = _career?.Current?.PlayerTeamId ?? "";

        for (int i = 0; i < teams.Count; i += 2)
        {
            if (i + 1 >= teams.Count) break;
            bool isUser = teams[i] == playerTeamId || teams[i + 1] == playerTeamId;
            round.Matches.Add(new CupMatch
            {
                HomeTeamId  = teams[i],
                AwayTeamId  = teams[i + 1],
                IsUserMatch = isUser
            });
        }
        return round;
    }

    private void SimulateMatch(CupMatch m)
    {
        int homeR = GetRating(m.HomeTeamId) + 3;
        int awayR = GetRating(m.AwayTeamId);
        float diff = (homeR - awayR) / 20f;

        m.ScoreHome = PoissonGoals(1.3f + diff);
        m.ScoreAway = PoissonGoals(1.1f - diff);
        m.IsPlayed  = true;

        if (m.ScoreHome == m.ScoreAway)
        {
            // Pênaltis simulados
            m.PenHome = _rng.Next(3, 6);
            m.PenAway = _rng.Next(3, 6);
            while (m.PenHome == m.PenAway) m.PenAway = _rng.Next(3, 6);
        }
        SetWinner(m);
    }

    private static void SetWinner(CupMatch m)
    {
        if (m.ScoreHome != m.ScoreAway)
            m.WinnerId = m.ScoreHome > m.ScoreAway ? m.HomeTeamId : m.AwayTeamId;
        else
            m.WinnerId = m.PenHome > m.PenAway ? m.HomeTeamId : m.AwayTeamId;
    }

    private int GetRating(string teamId)
    {
        var td = _career?.GetTeamData(teamId);
        return td?.OverallRating ?? 65;
    }

    private int PoissonGoals(float lambda)
    {
        lambda = Math.Max(0.1f, lambda);
        double L = Math.Exp(-lambda);
        int k = 0; double p = 1.0;
        do { k++; p *= _rng.NextDouble(); } while (p > L);
        return k - 1;
    }

    public string GetRoundName() =>
        Current?.RoundIndex < RoundNames.Length ? RoundNames[Current.RoundIndex] : "Copa";
}
