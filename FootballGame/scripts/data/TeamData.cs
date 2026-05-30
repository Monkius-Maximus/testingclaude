using Godot;
using Godot.Collections;
using System.Linq;

namespace FootballGame;

/// <summary>
/// Dados estáticos de um clube ou seleção: nome, cores, escudo, força geral.
/// O elenco em si é armazenado separadamente (lista de PlayerData), permitindo
/// que jogadores transitem entre times.
/// </summary>
[GlobalClass]
public partial class TeamData : Resource
{
    [Export] public string  TeamId        = "";
    [Export] public string  ShortName     = "";
    [Export] public string  FullName      = "";
    [Export] public string  Country       = "";

    [Export] public Color   PrimaryColor   = new(1, 1, 1);
    [Export] public Color   SecondaryColor = new(0, 0, 0);

    /// <summary>Caminho para a textura do escudo, ex: <c>res://assets/textures/teams/foo.png</c></summary>
    [Export] public string  CrestPath      = "";

    /// <summary>Força geral 0..100 — usada para sortear adversários e calcular dificuldade.</summary>
    [Export] public int     OverallRating  = 70;

    /// <summary>IDs dos jogadores no elenco (legado — usado como fallback).</summary>
    [Export] public Array<string> SquadIds = new();

    /// <summary>
    /// Elenco completo com atributos individuais (11 jogadores em ordem de escalação).
    /// Quando preenchido, substitui SquadIds como fonte de verdade.
    /// </summary>
    [Export] public Array<PlayerData> Squad = new();

    /// <summary>Overall médio do elenco, calculado automaticamente se Squad estiver preenchido.</summary>
    public int ComputedOverall()
        => Squad.Count > 0 ? (int)Squad.Average(p => p.OverallRating) : OverallRating;
}
