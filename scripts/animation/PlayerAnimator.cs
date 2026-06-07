using Godot;

namespace FootballGame;

/// <summary>
/// Camada de animação do jogador. Lê estado do <see cref="Player"/> e
/// atualiza o <see cref="AnimationTree"/>. Mantém uma flag de "ação em curso"
/// para que ações como chute e carrinho não sejam interrompidas pela locomoção.
/// </summary>
public partial class PlayerAnimator : Node
{
    // ── Referências (atribuir no editor) ──────────────────────────
    [Export] private AnimationTree _animTree;
    [Export] private NodePath      _playerPath;

    private AnimationNodeStateMachinePlayback _stateMachine;
    private Player                            _player;

    // ── StringName cacheados (zero alocação por frame) ───────────
    // Caminhos dentro do AnimationTree — devem bater com a config no editor
    private static readonly StringName ParamBlendPos  = "parameters/Locomotion/blend_position";
    private static readonly StringName ParamKickSpeed = "parameters/Kick/TimeScale/scale";

    // Nomes dos estados — passados ao StateMachine.Travel()
    private static readonly StringName StateIdle        = "Idle";
    private static readonly StringName StateLocomotion  = "Locomotion";
    private static readonly StringName StateSprint      = "Sprint";
    private static readonly StringName StateKick        = "Kick";
    private static readonly StringName StateSlideTackle = "SlideTackle";
    private static readonly StringName StateHeader      = "Header";
    private static readonly StringName StateStumble     = "Stumble";
    private static readonly StringName StateCelebrate   = "Celebrate";
    private static readonly StringName StateDive        = "Dive";

    private const float WalkThreshold   = 0.1f;
    private const float SprintThreshold = 0.85f;

    private StringName _currentState  = StateIdle;
    private Vector2    _blendVelocity = Vector2.Zero;
    private bool       _actionPending = false;

    public override void _Ready()
    {
        _player       = GetNode<Player>(_playerPath);
        _stateMachine = (AnimationNodeStateMachinePlayback)
                        _animTree.Get("parameters/playback");
        _animTree.Active = true;
    }

    public override void _Process(double delta)
    {
        if (_actionPending) return; // ação em curso bloqueia a locomoção
        UpdateLocomotion((float)delta);
    }

    private void UpdateLocomotion(float delta)
    {
        float speed = NormalizedSpeed();

        if (speed < WalkThreshold)
        {
            TryTransition(StateIdle);
            SmoothBlend(Vector2.Zero, delta);
            return;
        }

        if (speed >= SprintThreshold)
        {
            TryTransition(StateSprint);
        }
        else
        {
            TryTransition(StateLocomotion);
            var target = new Vector2(StrafeInput(), speed);
            SmoothBlend(target, delta);
        }
    }

    private float NormalizedSpeed()
    {
        if (_player == null) return 0f;
        var horiz = new Vector2(_player.Velocity.X, _player.Velocity.Z);
        // 13f = sprint speed alvo (deve casar com Player.GetSprintSpeed maxado)
        return Mathf.Clamp(horiz.Length() / 13f, 0f, 1f);
    }

    private float StrafeInput()
    {
        if (_player == null) return 0f;
        var dir = _player.IntendedMovement;
        if (dir.LengthSquared() < 0.01f) return 0f;
        // Decompõe em strafe relativo à orientação do jogador
        var basisRight = _player.GlobalTransform.Basis.X;
        return basisRight.Dot(dir);
    }

    private void SmoothBlend(Vector2 target, float delta)
    {
        _blendVelocity = _blendVelocity.Lerp(target, delta * 8f);
        _animTree.Set(ParamBlendPos, _blendVelocity);
    }

    // ── Ações one-shot (chamadas pelo Player) ────────────────────
    public void PlayKick(float forceMultiplier = 1f)
    {
        _animTree.Set(ParamKickSpeed, 0.8f + forceMultiplier * 0.4f);
        StartAction(StateKick);
    }

    public void PlaySlideTackle() => StartAction(StateSlideTackle);
    public void PlayHeader()      => StartAction(StateHeader);
    public void PlayStumble()     => StartAction(StateStumble);
    public void PlayCelebrate()   => StartAction(StateCelebrate);
    public void PlayDive()        => StartAction(StateDive);

    private void TryTransition(StringName state)
    {
        if (_currentState == state) return;
        _stateMachine.Travel(state);
        _currentState = state;
    }

    private void StartAction(StringName state)
    {
        _actionPending = true;
        _stateMachine.Travel(state);
        _currentState = state;
    }

    /// <summary>
    /// Chamada pelo AnimationPlayer via Method Track no último frame da animação de ação.
    /// </summary>
    public void OnActionFinished()
    {
        _actionPending = false;
        TryTransition(StateIdle);
    }
}
