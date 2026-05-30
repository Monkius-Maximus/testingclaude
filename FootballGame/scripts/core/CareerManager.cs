using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FootballGame;

/// <summary>
/// Autoload que gerencia todo o estado do modo carreira: calendário, classificação,
/// simulação de partidas e persistência em user://career.json.
/// </summary>
public partial class CareerManager : Node
{
    // ── Modelo de dados (serializado via System.Text.Json) ───────────────────

    public class CareerData
    {
        [JsonPropertyName("playerTeamId")]  public string           PlayerTeamId = "";
        [JsonPropertyName("season")]        public int              Season       = 1;
        [JsonPropertyName("fixtureIndex")]  public int              FixtureIndex = 0;
        [JsonPropertyName("balance")]       public int              Balance      = 1_000_000;
        [JsonPropertyName("standings")]     public List<StandingsRow> Standings  = new();
        [JsonPropertyName("fixtures")]      public List<Fixture>      Fixtures   = new();
    }

    public class StandingsRow
    {
        [JsonPropertyName("teamId")]  public string TeamId       = "";
        [JsonPropertyName("pts")]     public int    Points       = 0;
        [JsonPropertyName("pld")]     public int    Played       = 0;
        [JsonPropertyName("w")]       public int    Won          = 0;
        [JsonPropertyName("d")]       public int    Drawn        = 0;
        [JsonPropertyName("l")]       public int    Lost         = 0;
        [JsonPropertyName("gf")]      public int    GoalsFor     = 0;
        [JsonPropertyName("ga")]      public int    GoalsAgainst = 0;
        [JsonIgnore] public int GoalDiff => GoalsFor - GoalsAgainst;
    }

    public class Fixture
    {
        [JsonPropertyName("home")]     public string HomeTeamId  = "";
        [JsonPropertyName("away")]     public string AwayTeamId  = "";
        [JsonPropertyName("md")]       public int    Matchday    = 1;
        [JsonPropertyName("sh")]       public int    ScoreHome   = 0;
        [JsonPropertyName("sa")]       public int    ScoreAway   = 0;
        [JsonPropertyName("played")]   public bool   IsPlayed    = false;
        [JsonPropertyName("userMatch")] public bool  IsUserMatch = false;
    }

    // ── Estado ───────────────────────────────────────────────────────────────

    private const string SavePath = "user://career.json";

    public CareerData Current         { get; private set; }
    public bool       HasCareer       => Current != null;
    public bool       IsAwaitingResult { get; set; } = false;

    private readonly Dictionary<string, TeamData> _teamCache = new();
    private readonly JsonSerializerOptions        _jsonOpts  = new() { WriteIndented = false };

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        TryLoad();
    }

    // ── API pública ──────────────────────────────────────────────────────────

    /// <summary>Inicia uma nova carreira para o time indicado e gera a primeira temporada.</summary>
    public void StartNewCareer(string playerTeamId)
    {
        ScanTeams();
        Current = new CareerData { PlayerTeamId = playerTeamId };
        GenerateSeason();
        Save();
    }

    /// <summary>Retorna o próximo jogo do usuário ainda não disputado.</summary>
    public Fixture GetNextUserFixture()
    {
        if (Current == null) return null;
        for (int i = Current.FixtureIndex; i < Current.Fixtures.Count; i++)
            if (!Current.Fixtures[i].IsPlayed && Current.Fixtures[i].IsUserMatch)
                return Current.Fixtures[i];
        return null;
    }

    /// <summary>Simula automaticamente todas as partidas anteriores ao próximo jogo do usuário.</summary>
    public void SimulateUpToNextUserMatch()
    {
        if (Current == null) return;
        var rng = new Random();
        foreach (var f in Current.Fixtures)
        {
            if (f.IsPlayed) continue;
            if (f.IsUserMatch) break;
            SimulateFixture(f, rng);
        }
        Save();
    }

    /// <summary>Aplica o placar do jogo do usuário e avança o calendário.</summary>
    public void ApplyUserMatchResult(int scoreHome, int scoreAway)
    {
        if (Current == null) return;
        var f = GetNextUserFixture();
        if (f == null) return;

        f.ScoreHome = scoreHome;
        f.ScoreAway = scoreAway;
        f.IsPlayed  = true;
        UpdateStandings(f);

        Current.FixtureIndex = Current.Fixtures.IndexOf(f) + 1;
        IsAwaitingResult = false;

        if (IsSeasonOver()) AdvanceSeason();
        Save();
    }

    /// <summary>Retorna TeamData para o ID dado (usa cache interno).</summary>
    public TeamData GetTeamData(string teamId)
    {
        if (_teamCache.TryGetValue(teamId, out var td)) return td;
        ScanTeams();
        return _teamCache.GetValueOrDefault(teamId);
    }

    // ── Geração de temporada ─────────────────────────────────────────────────

    private void GenerateSeason()
    {
        ScanTeams();
        var teamIds = new List<string>(_teamCache.Keys);

        // Garante ao menos 4 times para ter liga interessante
        int extra = 0;
        while (teamIds.Count < 4) teamIds.Add($"ghost_{extra++}");

        Current.Standings = teamIds
            .Select(id => new StandingsRow { TeamId = id })
            .ToList();
        Current.Fixtures  = BuildRoundRobin(teamIds, Current.PlayerTeamId);
        Current.FixtureIndex = 0;
    }

    private void AdvanceSeason()
    {
        Current.Season++;
        Current.Standings.Clear();
        Current.Fixtures.Clear();
        GenerateSeason();
    }

    private bool IsSeasonOver()
        => Current.Fixtures.All(f => f.IsPlayed);

    private List<Fixture> BuildRoundRobin(List<string> teams, string playerTeamId)
    {
        var result = new List<Fixture>();
        var pool   = new List<string>(teams);

        if (pool.Count % 2 != 0) pool.Add("__bye__");
        int n = pool.Count;

        // Algoritmo de torneio com pivot fixo (pool[0])
        for (int round = 0; round < n - 1; round++)
        {
            int md = round + 1;
            AddPair(result, pool[0], pool[n - 1], md, playerTeamId);
            for (int i = 1; i < n / 2; i++)
                AddPair(result, pool[i], pool[n - 1 - i], md, playerTeamId);

            string last = pool[n - 1];
            for (int i = n - 1; i > 1; i--) pool[i] = pool[i - 1];
            pool[1] = last;
        }

        // Segunda volta: mandante/visitante invertidos
        int half = result.Count;
        for (int i = 0; i < half; i++)
        {
            var f = result[i];
            AddPair(result, f.AwayTeamId, f.HomeTeamId, f.Matchday + (n - 1), playerTeamId);
        }

        result.Sort((a, b) => a.Matchday.CompareTo(b.Matchday));
        result.RemoveAll(f => f.HomeTeamId == "__bye__" || f.AwayTeamId == "__bye__");
        return result;
    }

    private static void AddPair(List<Fixture> list, string home, string away, int md, string playerTeamId)
    {
        if (home == "__bye__" || away == "__bye__") return;
        list.Add(new Fixture
        {
            HomeTeamId  = home,
            AwayTeamId  = away,
            Matchday    = md,
            IsUserMatch = home == playerTeamId || away == playerTeamId
        });
    }

    // ── Simulação ────────────────────────────────────────────────────────────

    private void SimulateFixture(Fixture f, Random rng)
    {
        float homeAdv = (GetRating(f.HomeTeamId) + 5 - GetRating(f.AwayTeamId)) / 20f;
        f.ScoreHome = PoissonGoals(1.3f + homeAdv, rng);
        f.ScoreAway = PoissonGoals(1.1f - homeAdv, rng);
        f.IsPlayed  = true;
        UpdateStandings(f);
    }

    private static int PoissonGoals(float lambda, Random rng)
    {
        lambda = Math.Max(0.1f, lambda);
        double L = Math.Exp(-lambda);
        int k = 0; double p = 1.0;
        do { k++; p *= rng.NextDouble(); } while (p > L);
        return k - 1;
    }

    private void UpdateStandings(Fixture f)
    {
        var home = GetRow(f.HomeTeamId);
        var away = GetRow(f.AwayTeamId);
        if (home == null || away == null) return;

        home.Played++; away.Played++;
        home.GoalsFor  += f.ScoreHome; home.GoalsAgainst += f.ScoreAway;
        away.GoalsFor  += f.ScoreAway; away.GoalsAgainst += f.ScoreHome;

        if (f.ScoreHome > f.ScoreAway) { home.Won++; home.Points += 3; away.Lost++; }
        else if (f.ScoreAway > f.ScoreHome) { away.Won++; away.Points += 3; home.Lost++; }
        else { home.Drawn++; home.Points++; away.Drawn++; away.Points++; }
    }

    private StandingsRow GetRow(string teamId)
        => Current?.Standings.Find(r => r.TeamId == teamId);

    private int GetRating(string teamId)
        => _teamCache.TryGetValue(teamId, out var td) ? td.OverallRating : 65;

    // ── Scan de times ────────────────────────────────────────────────────────

    private void ScanTeams()
    {
        if (_teamCache.Count > 0) return;
        using var dir = DirAccess.Open("res://resources/leagues/");
        if (dir == null) return;
        dir.ListDirBegin();
        string name = dir.GetNext();
        while (!string.IsNullOrEmpty(name))
        {
            if (name.EndsWith(".tres"))
            {
                var res = GD.Load<Resource>($"res://resources/leagues/{name}");
                if (res is TeamData td && !string.IsNullOrEmpty(td.TeamId))
                    _teamCache[td.TeamId] = td;
            }
            name = dir.GetNext();
        }
    }

    // ── Persistência ─────────────────────────────────────────────────────────

    public void Save()
    {
        if (Current == null) return;
        string json = JsonSerializer.Serialize(Current, _jsonOpts);
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        file?.StoreString(json);
    }

    public bool TryLoad()
    {
        if (!FileAccess.FileExists(SavePath)) return false;
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        if (file == null) return false;
        try
        {
            Current = JsonSerializer.Deserialize<CareerData>(file.GetAsText(), _jsonOpts);
            if (Current != null) { ScanTeams(); return true; }
        }
        catch (Exception e) { GD.PrintErr($"CareerManager: falha ao carregar save — {e.Message}"); }
        return false;
    }

    public void DeleteSave()
    {
        Current = null;
        if (FileAccess.FileExists(SavePath))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SavePath));
    }
}
