using Godot;
using System.Collections.Generic;

namespace FootballGame;

/// <summary>
/// Centraliza o armazenamento de conteúdo criado pelo jogador no editor in-game:
/// jogadores (PlayerData) e clubes (TeamData) salvos como .tres em user://custom/.
/// Conteúdo persiste entre sessões e pode ser carregado no mercado/carreira.
/// </summary>
public static class CustomContent
{
    public const string PlayersDir  = "user://custom/players/";
    public const string TeamsDir    = "user://custom/teams/";
    public const string StadiumsDir = "user://custom/stadiums/";

    /// <summary>Garante que os diretórios de conteúdo customizado existam.</summary>
    public static void EnsureDirs()
    {
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(PlayersDir));
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(TeamsDir));
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(StadiumsDir));
    }

    // ── Jogadores ────────────────────────────────────────────────

    /// <summary>Salva um PlayerData; retorna o caminho res/user usado.</summary>
    public static string SavePlayer(PlayerData data)
    {
        EnsureDirs();
        string id   = SanitizeId(data.PlayerId, data.ShortName, "jogador");
        data.PlayerId = id;
        string path = $"{PlayersDir}{id}.tres";
        ResourceSaver.Save(data, path);
        return path;
    }

    /// <summary>Lista todos os jogadores customizados salvos.</summary>
    public static List<PlayerData> LoadAllPlayers()
        => LoadAll<PlayerData>(PlayersDir);

    public static void DeletePlayer(string id)
        => DeleteFile($"{PlayersDir}{id}.tres");

    // ── Clubes ───────────────────────────────────────────────────

    /// <summary>Salva um TeamData (com o elenco embutido) em user://custom/teams/.</summary>
    public static string SaveTeam(TeamData data)
    {
        EnsureDirs();
        string id = SanitizeId(data.TeamId, data.ShortName, "clube");
        data.TeamId = id;
        string path = $"{TeamsDir}{id}.tres";
        // BundleResources embute o Squad (sub-resources) no mesmo arquivo.
        ResourceSaver.Save(data, path, ResourceSaver.SaverFlags.BundleResources);
        return path;
    }

    public static List<TeamData> LoadAllTeams()
        => LoadAll<TeamData>(TeamsDir);

    public static void DeleteTeam(string id)
        => DeleteFile($"{TeamsDir}{id}.tres");

    // ── Estádios ──────────────────────────────────────────────────

    public static string SaveStadium(StadiumData data)
    {
        EnsureDirs();
        string id = SanitizeId(data.StadiumId, data.Name, "estadio");
        data.StadiumId = id;
        string path = $"{StadiumsDir}{id}.tres";
        ResourceSaver.Save(data, path);
        return path;
    }

    public static List<StadiumData> LoadAllStadiums()
        => LoadAll<StadiumData>(StadiumsDir);

    public static void DeleteStadium(string id)
        => DeleteFile($"{StadiumsDir}{id}.tres");

    // ── Internos ─────────────────────────────────────────────────

    private static List<T> LoadAll<T>(string dir) where T : Resource
    {
        var result = new List<T>();
        EnsureDirs();
        using var d = DirAccess.Open(dir);
        if (d == null) return result;
        d.ListDirBegin();
        string name = d.GetNext();
        while (!string.IsNullOrEmpty(name))
        {
            if (name.EndsWith(".tres"))
            {
                var res = GD.Load<Resource>($"{dir}{name}");
                if (res is T typed) result.Add(typed);
            }
            name = d.GetNext();
        }
        return result;
    }

    private static void DeleteFile(string path)
    {
        if (FileAccess.FileExists(path))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
    }

    /// <summary>Gera um id de arquivo seguro a partir do id/nome do recurso.</summary>
    private static string SanitizeId(string id, string fallbackName, string prefix)
    {
        string source = !string.IsNullOrWhiteSpace(id) ? id
                      : !string.IsNullOrWhiteSpace(fallbackName) ? fallbackName
                      : $"{prefix}_{Time.GetTicksMsec()}";

        var sb = new System.Text.StringBuilder();
        foreach (char c in source.ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');

        string clean = sb.ToString().Trim('_');
        return string.IsNullOrEmpty(clean) ? $"{prefix}_{Time.GetTicksMsec()}" : clean;
    }
}
