using Godot;
using Godot.Collections;

namespace FootballGame;

/// <summary>
/// Escalação de um time. Os arrays <c>Roles</c> e <c>PlayerIds</c> são
/// paralelos: o role no índice <c>i</c> é atribuído ao jogador no índice <c>i</c>.
/// Essa separação permite escalar um jogador fora da posição natural dele.
/// </summary>
[GlobalClass]
public partial class Lineup : Resource
{
    public enum FormationKind
    {
        F_4_4_2,
        F_4_3_3,
        F_3_5_2,
        F_4_2_3_1,
        F_5_3_2
    }

    [Export] public Array<PlayerRole> Roles     = new();
    [Export] public Array<string>     PlayerIds = new();
    [Export] public FormationKind     Formation = FormationKind.F_4_3_3;
}
