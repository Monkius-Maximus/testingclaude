using Godot;

namespace FootballGame;

/// <summary>Slot de construção do estádio — identifica cada seção.</summary>
public enum StadiumSlot
{
    NorthStand   = 0,
    SouthStand   = 1,
    WestEnd      = 2,
    EastEnd      = 3,
    CornerNW     = 4,
    CornerNE     = 5,
    CornerSW     = 6,
    CornerSE     = 7,
    FloodlightNW = 8,
    FloodlightNE = 9,
    FloodlightSW = 10,
    FloodlightSE = 11,
}

/// <summary>
/// Dados de um estádio criado no editor in-game. Armazena identidade,
/// aparência e layout (quais slots estão ativos) como bitmask de 12 bits.
/// SlotTransforms e SlotModelPaths provêm os dados 3D necessários para o
/// StadiumLoader instanciar as peças modulares na cena de partida.
/// </summary>
[GlobalClass]
public partial class StadiumData : Resource
{
    public const int SlotCount = 12;

    // ── Identidade ───────────────────────────────────────────────
    [Export] public string StadiumId  = "";
    [Export] public string Name       = "Meu Estádio";
    [Export] public string City       = "";
    [Export] public int    BuiltYear  = 2000;

    // ── Aparência ────────────────────────────────────────────────
    [Export] public Color PitchColor = new(0.13f, 0.55f, 0.13f);
    [Export] public Color StandColor = new(0.28f, 0.30f, 0.36f);

    // ── Layout (bitmask de 12 slots) ─────────────────────────────
    /// <summary>Bit i = 1 → slot i está ativo.</summary>
    [Export] public int SlotMask = 0;

    // ── Tabelas estáticas ────────────────────────────────────────

    /// <summary>Contribuição de capacidade de cada slot (em torcedores).</summary>
    public static readonly int[] SlotCapacity =
    {
        8000, 8000, 3500, 3500,   // NorthStand, SouthStand, WestEnd, EastEnd
        1500, 1500, 1500, 1500,   // CornerNW, CornerNE, CornerSW, CornerSE
        0, 0, 0, 0,               // Floodlights não adicionam capacidade
    };

    /// <summary>Nomes legíveis de cada slot (para UI e tooltips).</summary>
    public static readonly string[] SlotNames =
    {
        "Tribuna Norte", "Tribuna Sul", "Gol Oeste", "Gol Leste",
        "Canto NO",      "Canto NE",   "Canto SO",  "Canto SE",
        "Holofote NO",   "Holofote NE","Holofote SO","Holofote SE",
    };

    /// <summary>
    /// Transform 3D de cada slot: Position em metros (eixo X = comprimento do
    /// campo, Z = largura) e RotationY em graus. Usado pelo StadiumLoader.
    /// </summary>
    public static readonly (Vector3 Position, float RotationY)[] SlotTransforms =
    {
        (new Vector3(   0, 0, -38), 0f),    // NorthStand  (tribuna longa, norte)
        (new Vector3(   0, 0, +38), 180f),  // SouthStand  (tribuna longa, sul)
        (new Vector3( -58, 0,   0), 90f),   // WestEnd     (fundo, oeste)
        (new Vector3( +58, 0,   0), 270f),  // EastEnd     (fundo, leste)
        (new Vector3( -57, 0, -38), 0f),    // CornerNW
        (new Vector3( +57, 0, -38), 90f),   // CornerNE
        (new Vector3( -57, 0, +38), 270f),  // CornerSW
        (new Vector3( +57, 0, +38), 180f),  // CornerSE
        (new Vector3( -60, 0, -42), 0f),    // FloodlightNW
        (new Vector3( +60, 0, -42), 0f),    // FloodlightNE
        (new Vector3( -60, 0, +42), 0f),    // FloodlightSW
        (new Vector3( +60, 0, +42), 0f),    // FloodlightSE
    };

    /// <summary>Caminho do modelo .glb para cada slot (relativo a res://).</summary>
    public static readonly string[] SlotModelPaths =
    {
        "res://assets/models/stadium/bleacher_straight.glb",
        "res://assets/models/stadium/bleacher_straight.glb",
        "res://assets/models/stadium/bleacher_straight.glb",
        "res://assets/models/stadium/bleacher_straight.glb",
        "res://assets/models/stadium/bleacher_corner.glb",
        "res://assets/models/stadium/bleacher_corner.glb",
        "res://assets/models/stadium/bleacher_corner.glb",
        "res://assets/models/stadium/bleacher_corner.glb",
        "res://assets/models/stadium/floodlight_mast.glb",
        "res://assets/models/stadium/floodlight_mast.glb",
        "res://assets/models/stadium/floodlight_mast.glb",
        "res://assets/models/stadium/floodlight_mast.glb",
    };

    // ── API pública ──────────────────────────────────────────────

    public bool HasSlot(StadiumSlot s) => (SlotMask & (1 << (int)s)) != 0;

    public void SetSlot(StadiumSlot s, bool active)
    {
        if (active) SlotMask |=  (1 << (int)s);
        else        SlotMask &= ~(1 << (int)s);
    }

    public void ToggleSlot(StadiumSlot s) => SetSlot(s, !HasSlot(s));

    public int ComputeCapacity()
    {
        int total = 0;
        for (int i = 0; i < SlotCount; i++)
            if ((SlotMask & (1 << i)) != 0) total += SlotCapacity[i];
        return total;
    }
}
