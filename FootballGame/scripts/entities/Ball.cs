using Godot;

namespace FootballGame;

/// <summary>
/// Bola. RigidBody3D com física e detecção das linhas do campo.
/// Emite sinais quando sai do campo para que o RulesManager decida
/// se é lateral, escanteio ou tiro de meta.
/// </summary>
public partial class Ball : RigidBody3D
{
    // ── Parâmetros físicos ────────────────────────────────────────
    [Export] public float Friction       = 0.4f;
    [Export] public float Bounce         = 0.6f;
    [Export] public float MaxSpeed       = 30f;
    [Export] public float AirResistance  = 0.02f;

    // ── Limites do campo (metros — ajustar ao modelo do estádio) ─
    [Export] public float FieldHalfLength = 52.5f;
    [Export] public float FieldHalfWidth  = 34.0f;

    [Signal] public delegate void BallOutLeftEventHandler(Node lastTouch);
    [Signal] public delegate void BallOutRightEventHandler(Node lastTouch);
    [Signal] public delegate void BallOutTopEventHandler(Node lastTouch);
    [Signal] public delegate void BallOutBottomEventHandler(Node lastTouch);

    public Node LastTouchedBy { get; set; }
    public bool IsInPlay      { get; private set; } = true;

    public override void _Ready()
    {
        AddToGroup("ball");

        // Material físico configurado em runtime
        var mat = new PhysicsMaterial
        {
            Friction = Friction,
            Bounce   = Bounce
        };
        PhysicsMaterialOverride = mat;

        ContactMonitor = true;
        MaxContactsReported = 4;
        BodyEntered += OnBodyEntered;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsInPlay) return;
        ApplyAirResistance();
        ClampSpeed();
        CheckFieldBounds();
    }

    private void ApplyAirResistance()
    {
        LinearVelocity -= LinearVelocity * AirResistance;
    }

    private void ClampSpeed()
    {
        if (LinearVelocity.Length() > MaxSpeed)
            LinearVelocity = LinearVelocity.Normalized() * MaxSpeed;
    }

    private void CheckFieldBounds()
    {
        var pos = GlobalPosition;

        if (pos.Z > FieldHalfWidth)
        {
            EmitSignal(SignalName.BallOutRight, LastTouchedBy);
            IsInPlay = false;
        }
        else if (pos.Z < -FieldHalfWidth)
        {
            EmitSignal(SignalName.BallOutLeft, LastTouchedBy);
            IsInPlay = false;
        }

        if (pos.X > FieldHalfLength)
        {
            EmitSignal(SignalName.BallOutTop, LastTouchedBy);
            IsInPlay = false;
        }
        else if (pos.X < -FieldHalfLength)
        {
            EmitSignal(SignalName.BallOutBottom, LastTouchedBy);
            IsInPlay = false;
        }
    }

    /// <summary>Aplica impulso na bola na direção indicada.</summary>
    public void Kick(Vector3 direction, float force)
    {
        if (!IsInPlay) return;
        ApplyCentralImpulse(direction.Normalized() * force);
    }

    private void OnBodyEntered(Node body)
    {
        if (body.IsInGroup("players"))
            LastTouchedBy = body;
    }

    public void ResetBall(Vector3 position)
    {
        IsInPlay = true;
        GlobalPosition  = position;
        LinearVelocity  = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
    }

    /// <summary>Congela a bola durante cinemáticas. Reverter com <see cref="UnfreezeBall"/>.</summary>
    public void FreezeBall()
    {
        Freeze   = true;   // propriedade do RigidBody3D
        IsInPlay = false;
    }

    public void UnfreezeBall()
    {
        Freeze   = false;
        IsInPlay = true;
    }
}
