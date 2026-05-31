using Godot;

namespace FootballGame;

/// <summary>
/// Canvas 2D top-down do editor de estádio. Desenha o campo e os 12 slots
/// de construção; cliques toggleam slots e movimentos emitem hover para
/// o StadiumEditorUI atualizar o status.
///
/// Coordenadas do campo: origem no centro, +X para direita, +Z para baixo
/// (mesmo que o mundo 3D). O canvas mapeia essa área para pixels.
/// </summary>
public partial class StadiumPreviewUI : Control
{
    // Sinais para o editor pai
    [Signal] public delegate void SlotToggledEventHandler(int slotIndex);
    [Signal] public delegate void SlotHoveredEventHandler(int slotIndex);  // -1 = nenhum

    public StadiumData Stadium { get; set; }

    // ── Vista: campo + margens para os stands ────────────────────
    private const float ViewX1 = -66f, ViewX2 = 66f;
    private const float ViewZ1 = -46f, ViewZ2 = 46f;

    // ── Rects de cada slot em coordenadas de campo (x1,z1,x2,z2) ─
    // North = topo do canvas (Z negativo), South = base, West = esquerda, East = direita
    private static readonly (float x1, float z1, float x2, float z2)[] SlotRects =
    {
        (-52.5f, -42f, +52.5f, -34f),   // NorthStand
        (-52.5f, +34f, +52.5f, +42f),   // SouthStand
        (  -62f, -34f,   -54f, +34f),   // WestEnd
        (  +54f, -34f,   +62f, +34f),   // EastEnd
        (  -62f, -42f, -52.5f, -34f),   // CornerNW
        (+52.5f, -42f,   +62f, -34f),   // CornerNE
        (  -62f, +34f, -52.5f, +42f),   // CornerSW
        (+52.5f, +34f,   +62f, +42f),   // CornerSE
        (  -65f, -45f,   -61f, -41f),   // FloodlightNW
        (  +61f, -45f,   +65f, -41f),   // FloodlightNE
        (  -65f, +41f,   -61f, +45f),   // FloodlightSW
        (  +61f, +41f,   +65f, +45f),   // FloodlightSE
    };

    // ── Cores ────────────────────────────────────────────────────
    private static readonly Color ColBg        = new(0.10f, 0.12f, 0.15f);
    private static readonly Color ColInactive  = new(0.22f, 0.23f, 0.28f);
    private static readonly Color ColOutline   = new(0.38f, 0.40f, 0.46f);
    private static readonly Color ColHover     = new(0.80f, 0.80f, 0.30f);
    private static readonly Color ColWhite     = new(1.00f, 1.00f, 1.00f, 0.55f);
    private static readonly Color ColMarkings  = new(1.00f, 1.00f, 1.00f, 0.40f);

    private int  _hovered = -1;
    private Font _font;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(460, 320);
        _font = GetThemeFont("font");
    }

    // ── Desenho ──────────────────────────────────────────────────

    public override void _Draw()
    {
        if (Stadium == null) return;
        var sz = Size;

        DrawRect(new Rect2(Vector2.Zero, sz), ColBg);

        // Campo
        DrawRect(Fr(-52.5f, -34f, +52.5f, +34f, sz), Stadium.PitchColor);

        // Marcações do campo
        DrawRect(Fr(-52.5f, -34f, +52.5f, +34f, sz), ColWhite, false, 1.5f);
        DrawLine(Fp(0f, -34f, sz), Fp(0f, +34f, sz), ColMarkings, 1f);
        DrawCircle(Fp(0f, 0f, sz), FLen(9.15f, sz), ColMarkings, false, 1f);
        DrawCircle(Fp(0f, 0f, sz), FLen(1.0f, sz), ColMarkings);
        DrawRect(Fr(-52.5f, -20.16f, -36f, +20.16f, sz), ColMarkings, false, 1f);
        DrawRect(Fr(  +36f, -20.16f, +52.5f, +20.16f, sz), ColMarkings, false, 1f);

        // Stands
        var activeColor = Stadium.StandColor;
        var brightEdge  = new Color(
            Mathf.Min(activeColor.R * 1.5f, 1f),
            Mathf.Min(activeColor.G * 1.5f, 1f),
            Mathf.Min(activeColor.B * 1.5f, 1f));

        for (int i = 0; i < StadiumData.SlotCount; i++)
        {
            var r = SlotRects[i];
            var rect = Fr(r.x1, r.z1, r.x2, r.z2, sz);
            bool active  = Stadium.HasSlot((StadiumSlot)i);
            bool hovered = i == _hovered;

            if (active)
            {
                DrawRect(rect, activeColor);
                DrawRect(rect, hovered ? ColHover : brightEdge, false, hovered ? 2.5f : 1.5f);
            }
            else
            {
                DrawRect(rect, ColInactive);
                DrawRect(rect, hovered ? ColHover : ColOutline, false, hovered ? 2.5f : 1f);
            }

            // Ícone de holofote: pequeno X amarelo quando ativo
            if (i >= (int)StadiumSlot.FloodlightNW && active)
            {
                var c = rect.GetCenter();
                float h = 4f;
                DrawLine(c - new Vector2(h, h), c + new Vector2(h, h), ColHover, 1.5f);
                DrawLine(c - new Vector2(h, -h), c + new Vector2(h, -h), ColHover, 1.5f);
            }
        }

        // Legenda de direções
        var lblColor = new Color(0.7f, 0.7f, 0.75f);
        if (_font != null)
        {
            DrawString(_font, Fp(-52f, -44f, sz), "N", HorizontalAlignment.Left, -1, 11, lblColor);
            DrawString(_font, Fp(-52f, +43f, sz), "S", HorizontalAlignment.Left, -1, 11, lblColor);
            DrawString(_font, Fp(-64f,   0f, sz), "O", HorizontalAlignment.Left, -1, 11, lblColor);
            DrawString(_font, Fp(+59f,   0f, sz), "L", HorizontalAlignment.Left, -1, 11, lblColor);
        }
    }

    // ── Input ─────────────────────────────────────────────────────

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mm)
        {
            int prev = _hovered;
            _hovered = HitTest(mm.Position);
            if (_hovered != prev)
            {
                QueueRedraw();
                EmitSignal(SignalName.SlotHovered, _hovered);
            }
        }
        else if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
        {
            int hit = HitTest(mb.Position);
            if (hit >= 0) EmitSignal(SignalName.SlotToggled, hit);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────

    private int HitTest(Vector2 canvasPos)
    {
        var fp = CanvasToField(canvasPos, Size);
        for (int i = 0; i < StadiumData.SlotCount; i++)
        {
            var r = SlotRects[i];
            if (fp.X >= r.x1 && fp.X <= r.x2 && fp.Y >= r.z1 && fp.Y <= r.z2)
                return i;
        }
        return -1;
    }

    private Vector2 FieldToCanvas(float fx, float fz, Vector2 sz)
        => new((fx - ViewX1) / (ViewX2 - ViewX1) * sz.X,
               (fz - ViewZ1) / (ViewZ2 - ViewZ1) * sz.Y);

    private Vector2 CanvasToField(Vector2 p, Vector2 sz)
        => new(p.X / sz.X * (ViewX2 - ViewX1) + ViewX1,
               p.Y / sz.Y * (ViewZ2 - ViewZ1) + ViewZ1);

    // Ponto no canvas
    private Vector2 Fp(float fx, float fz, Vector2 sz) => FieldToCanvas(fx, fz, sz);

    // Rect no canvas
    private Rect2 Fr(float x1, float z1, float x2, float z2, Vector2 sz)
    {
        var tl = FieldToCanvas(x1, z1, sz);
        var br = FieldToCanvas(x2, z2, sz);
        return new Rect2(tl, br - tl);
    }

    // Comprimento em píxeis a partir de metros de campo
    private float FLen(float meters, Vector2 sz)
        => meters / (ViewX2 - ViewX1) * sz.X;
}
