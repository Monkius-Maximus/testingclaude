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

    // ── Atributos do jogador (estilo FIFA, 0–100) ─────────────────
    // Atribuídos pelo PlayerData.Apply(); podem ser sobrescritos no editor.
    [Export] public int Pace      = 70;
    [Export] public int Shooting  = 70;
    [Export] public int Passing   = 70;
    [Export] public int Dribbling = 70;
    [Export] public int Defending = 70;
    [Export] public int Physical  = 70;
    [Export] public int Heading   = 70;

    // ── Estado em tempo real ─────────────────────────────────────
    public float CurrentStamina { get; private set; } = 100f;
    public bool  HasBall        { get; set; }         = false;

    // ── Source de dados (opcional) ────────────────────────────────
    private PlayerData _stats;
    public PlayerData Stats
    {
        get => _stats;
        set { _stats = value; if (value != null) ApplyStats(value); }
    }

    // ── Intenções (preenchidas por Brain OU HumanInput) ──────────
    public Vector3 IntendedMovement  { get; set; } = Vector3.Zero;
    public bool    IntendsToSprint   { get; set; } = false;
    public bool    IntendsToKick     { get; set; } = false;
    public float   IntendedKickPower { get; set; } = 1f;
    public bool    IntendsToTackle   { get; set; } = false;
    public Vector3 LookTarget        { get; set; } = Vector3.Zero;

    protected float Gravity = ProjectSettings
        .GetSetting("physics/3d/default_gravity").AsSingle();

    // Referência cacheada à bola para evitar busca por grupo a cada frame
    private Ball _ball;

    public override void _Ready()
    {
        AddToGroup("players");
        _ball = GetTree().GetFirstNodeInGroup("ball") as Ball;
    }

    public override void _PhysicsProcess(double delta)
    {
        ApplyGravity((float)delta);
        ExecuteIntentions((float)delta);
        MoveAndSlide();
        UpdateBallPossession();
    }

    private void UpdateBallPossession()
    {
        HasBall = _ball != null && _ball.IsInPlay &&
                  GlobalPosition.DistanceTo(_ball.GlobalPosition) < 1.2f;
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

        // Stamina: Physical alto → drena mais devagar, recupera mais rápido
        float drainRate    = Mathf.Lerp(7f,  3f,  Physical / 100f);
        float recoveryRate = Mathf.Lerp(0.8f, 2.5f, Physical / 100f);
        if (IntendsToSprint) CurrentStamina -= drainRate  * delta;
        else                 CurrentStamina += recoveryRate * delta;
        CurrentStamina = Mathf.Clamp(CurrentStamina, 0f, 100f);

        // Limpa intenções one-shot
        IntendsToKick   = false;
        IntendsToTackle = false;
    }

    // ── Pontos de extensão (Goalkeeper sobrescreve) ──────────────
    protected virtual float GetWalkSpeed()   => 4f + Pace * 0.04f;
    protected virtual float GetSprintSpeed()
    {
        float base_ = 6f + Pace * 0.08f;
        // Fadiga reduz sprint gradualmente abaixo de 40% de stamina
        float stamFactor = CurrentStamina < 40f ? 0.7f + (CurrentStamina / 40f) * 0.3f : 1f;
        return base_ * stamFactor;
    }

    /// <summary>Regra geral: jogador de linha NUNCA pode usar mãos. Goalkeeper sobrescreve.</summary>
    public virtual bool CanHandleBallWithHands(Vector3 ballPosition) => false;

    protected virtual void PerformKick()
    {
        Animator?.PlayKick(IntendedKickPower);

        if (_ball == null || !_ball.IsInPlay) return;
        if (GlobalPosition.DistanceTo(_ball.GlobalPosition) > 1.5f) return;

        var dir = Vector3.Zero;
        if (LookTarget != Vector3.Zero && LookTarget.DistanceTo(GlobalPosition) > 0.1f)
            dir = (LookTarget - GlobalPosition);
        else
            dir = -GlobalTransform.Basis.Z;

        dir.Y = 0f;
        if (dir.LengthSquared() < 0.01f) dir = Vector3.Forward;
        dir = dir.Normalized();
        dir += Vector3.Up * 0.12f;

        // Shooting alto → mais força e levemente mais preciso
        float shootFactor = 0.7f + Shooting * 0.006f;   // 70→1.12, 90→1.24
        float force = (7f + IntendedKickPower * 13f) * shootFactor;
        _ball.Kick(dir.Normalized(), force);
        _ball.LastTouchedBy = this;
        HasBall = false;
    }

    protected virtual void PerformTackle()
    {
        Animator?.PlaySlideTackle();

        if (_ball == null || !_ball.IsInPlay) return;
        // Defending alto → alcance de carrinho ligeiramente maior
        float tackleRange = 1.5f + Defending * 0.01f;  // Def 50→2.0m, Def 90→2.4m
        if (GlobalPosition.DistanceTo(_ball.GlobalPosition) > tackleRange) return;

        var dir = (_ball.GlobalPosition - GlobalPosition).Normalized();
        dir.Y = 0.05f;
        _ball.Kick(dir.Normalized(), 5f);
        _ball.LastTouchedBy = this;
    }

    /// <summary>Copia os atributos do PlayerData para as propriedades de física do jogador.</summary>
    public void ApplyStats(PlayerData data)
    {
        if (data == null) return;
        PlayerId  = string.IsNullOrEmpty(data.PlayerId) ? PlayerId : data.PlayerId;
        Pace      = data.Pace;
        Shooting  = data.Shooting;
        Passing   = data.Passing;
        Dribbling = data.Dribbling;
        Defending = data.Defending;
        Physical  = data.Physical;
        Heading   = data.Heading;
    }

    private void ApplyGravity(float delta)
    {
        if (!IsOnFloor())
        {
            Velocity = new Vector3(Velocity.X, Velocity.Y - Gravity * delta, Velocity.Z);
        }
    }
}
