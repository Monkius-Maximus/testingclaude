using Godot;

namespace FootballGame;

/// <summary>
/// Instancia as peças modulares do estádio na cena de partida a partir de
/// um StadiumData. Cada slot ativo tenta carregar o .glb correspondente;
/// se o arquivo não existir, é ignorado silenciosamente (silent no-op).
///
/// Chamado pelo MatchBootstrap depois de instanciar o campo.
/// As peças são adicionadas como filhas de um nó StadiumVisuals (criado
/// automaticamente) abaixo do nó Field.
/// </summary>
public static class StadiumLoader
{
    private const string VisualsNodeName = "StadiumVisuals";

    /// <summary>
    /// Aplica um StadiumData em <paramref name="fieldRoot"/>. Se
    /// <paramref name="data"/> for null usa o estádio padrão vazio.
    /// Também ajusta a cor do gramado se o PitchBody existir.
    /// </summary>
    public static void Apply(Node3D fieldRoot, StadiumData data)
    {
        if (fieldRoot == null) return;

        ApplyPitchColor(fieldRoot, data?.PitchColor);

        if (data == null || data.SlotMask == 0) return;

        // Cria ou limpa o nó de visuais
        var visuals = fieldRoot.GetNodeOrNull<Node3D>(VisualsNodeName);
        if (visuals == null)
        {
            visuals = new Node3D { Name = VisualsNodeName };
            fieldRoot.AddChild(visuals);
        }
        else
        {
            foreach (Node child in visuals.GetChildren())
                child.QueueFree();
        }

        for (int i = 0; i < StadiumData.SlotCount; i++)
        {
            if (!data.HasSlot((StadiumSlot)i)) continue;

            string path = StadiumData.SlotModelPaths[i];
            if (!ResourceLoader.Exists(path)) continue;

            var scene = GD.Load<PackedScene>(path);
            if (scene == null) continue;

            var piece = scene.Instantiate<Node3D>();
            var (pos, rotY) = StadiumData.SlotTransforms[i];
            piece.Position         = pos;
            piece.RotationDegrees  = new Vector3(0, rotY, 0);
            visuals.AddChild(piece);
        }
    }

    private static void ApplyPitchColor(Node3D root, Color? color)
    {
        if (color == null) return;
        var pitchMesh = root.GetNodeOrNull<MeshInstance3D>("PitchBody/PitchMesh");
        if (pitchMesh?.GetActiveMaterial(0) is StandardMaterial3D mat)
            mat.AlbedoColor = color.Value;
    }
}
