using Godot;

namespace FootballGame;

/// <summary>
/// Classe base de qualquer jogador em campo. Não lê input nem chama IA:
/// apenas executa o que foi "intencionado" via propriedades <c>Intended*</c>.
/// Isso permite que tanto o <see cref="HumanInput"/> quanto o <see cref="AIBrain"/>
/// controlem o mesmo corpo sem que o Player precise saber quem manda.
/// </summary>
public partial class Player : CharacterBody3D
{
    // ── Identidade ────────────────────────────────────────────────
    [Export] public string         PlayerId = "p_001";
    [Export] public int            Team     = 0;
    [Export] public PlayerRole     Role;
    [Export] public PlayerAnimator Animator;

    // ── Atributos do jogador (estilo FIFA, 0..100) ───────────────
    [Export] public int Pace     = 70;
    [Export] public int Stamina  = 80;
    [Export] public int Passing  = 70;
    [Export] public int Shooting = 70;
    [Export] public int Tackling = 70;

    // ── Estado em tempo real ─────────────────────────────────────
    public float CurrentStamina { get; private set; } = 100f;
    public bool  HasBall        { get; set; }         = false;

    // ── Intenções (preenchidas por Brain OU HumanInput) ──────────
    public Vector3 IntendedMovement  { get; set; } = Vector3.Zero;
    public bool    IntendsToSprint   { get; set; } = false;
    public bool    IntendsToKick     { get; set; } = false;
    public float   IntendedKickPower { get; set; } = 1f;
    public bool    IntendsToTackle   { get; set; } = false;
    public Vector3 LookTarget        { get; set; } = Vector3.Zero;

    protected float Gravity = ProjectSettings
        .GetSetting("physics/3d/default_gravity").AsSingle();

    public override void _Ready()
    {
        AddToGroup("players");
    }

    public override void _PhysicsProcess(double delta)
    {
        ApplyGravity((float)delta);
        ExecuteIntentions((float)delta);
        MoveAndSlide();
    }

    /// <summary>Lê as intenções e converte em movimento + animação.</summary>
    protected virtual void ExecuteIntentions(float delta)
    {
        var moveDir = IntendedMovement;
        float currentSpeed = IntendsToSprint ? GetSprintSpeed() : GetWalkSpeed();

        if (moveDir.Length() > 0.1f)
        {
            Velocity = new Vector3(
                moveDir.X * currentSpeed,
                Velocity.Y,
                moveDir.Z * currentSpeed
            );
            float angle = Mathf.Atan2(moveDir.X, moveDir.Z);
            Rotation = new Vector3(
                Rotation.X,
                Mathf.LerpAngle(Rotation.Y, angle, 10f * delta),
                Rotation.Z
            );
        }
        else
        {
            Velocity = new Vector3(
                Mathf.MoveToward(Velocity.X, 0, currentSpeed),
                Velocity.Y,
                Mathf.MoveToward(Velocity.Z, 0, currentSpeed)
            );
        }

        if (IntendsToKick)   PerformKick();
        if (IntendsToTackle) PerformTackle();

        // Stamina
        if (IntendsToSprint) CurrentStamina -= 5f * delta;
        else                 CurrentStamina += 1f * delta;
        CurrentStamina = Mathf.Clamp(CurrentStamina, 0f, 100f);

        // Limpa intenções one-shot
        IntendsToKick   = false;
        IntendsToTackle = false;
    }

    // ── Pontos de extensão (Goalkeeper sobrescreve) ──────────────
    protected virtual float GetWalkSpeed()   => 4f + Pace * 0.05f;
    protected virtual float GetSprintSpeed() => 6f + Pace * 0.09f;

    /// <summary>Regra geral: jogador de linha NUNCA pode usar mãos. Goalkeeper sobrescreve.</summary>
    public virtual bool CanHandleBallWithHands(Vector3 ballPosition) => false;

    protected virtual void PerformKick()   => Animator?.PlayKick(IntendedKickPower);
    protected virtual void PerformTackle() => Animator?.PlaySlideTackle();

    private void ApplyGravity(float delta)
    {
        if (!IsOnFloor())
        {
            Velocity = new Vector3(Velocity.X, Velocity.Y - Gravity * delta, Velocity.Z);
        }
    }
}
