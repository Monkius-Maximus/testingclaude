using Godot;

namespace FootballGame;

/// <summary>
/// Goleiro. Estende <see cref="Player"/> com regra de pegar com as mãos
/// (apenas dentro da grande área), mergulho lateral e velocidade ligeiramente reduzida.
/// </summary>
public partial class Goalkeeper : Player
{
    /// <summary>Area3D que demarca a grande área do goleiro. Atribuir no editor.</summary>
    [Export] public Area3D PenaltyArea;

    /// <summary>Goleiro pode "mergulhar" — ação especial.</summary>
    public bool    IntendsToDive { get; set; } = false;
    public Vector3 DiveDirection { get; set; } = Vector3.Zero;

    public override bool CanHandleBallWithHands(Vector3 ballPosition)
    {
        if (PenaltyArea == null) return false;
        return PenaltyArea.OverlapsBody(this);
    }

    // Goleiro é levemente mais lento no campo aberto
    protected override float GetSprintSpeed() => 5f + Pace * 0.08f;

    protected override void ExecuteIntentions(float delta)
    {
        if (IntendsToDive)
        {
            var impulse = DiveDirection.Normalized() * 8f;
            Velocity = new Vector3(impulse.X, 3f, impulse.Z);
            Animator?.PlayDive();
            IntendsToDive = false;
            return; // mergulho ignora movimento normal
        }
        base.ExecuteIntentions(delta);
    }
}
