using Godot;
using System.Threading.Tasks;

namespace FootballGame;

/// <summary>
/// Câmera usada durante cinemáticas (ex: gol). Pode voar até uma posição
/// alvo com tween e orbitar ao redor de um ponto por um tempo definido.
/// </summary>
public partial class CinematicCamera : Camera3D
{
    [Export] public float OrbitSpeed  = 18f;   // graus/segundo
    [Export] public float OrbitRadius = 5f;
    [Export] public float OrbitHeight = 2.5f;

    private float   _orbitAngle  = 0f;
    private bool    _isOrbiting  = false;
    private Vector3 _orbitTarget = Vector3.Zero;

    public override void _Process(double delta)
    {
        if (!_isOrbiting) return;

        _orbitAngle += OrbitSpeed * (float)delta;
        float rad = Mathf.DegToRad(_orbitAngle);

        GlobalPosition = _orbitTarget + new Vector3(
            Mathf.Sin(rad) * OrbitRadius,
            OrbitHeight,
            Mathf.Cos(rad) * OrbitRadius
        );
        LookAt(_orbitTarget, Vector3.Up);
    }

    /// <summary>Voa suavemente até uma posição e olha para um alvo.</summary>
    public async Task FlyTo(Vector3 pos, Vector3 lookTarget, float duration)
    {
        _isOrbiting = false;

        var tw = CreateTween().SetParallel();
        tw.TweenProperty(this, "global_position", pos, duration)
          .SetTrans(Tween.TransitionType.Sine)
          .SetEase(Tween.EaseType.InOut);
        tw.TweenProperty(this, "fov", 55f, duration)
          .SetTrans(Tween.TransitionType.Sine);

        await ToSignal(tw, Tween.SignalName.Finished);
        LookAt(lookTarget, Vector3.Up);
    }

    /// <summary>Orbita ao redor de um ponto por <paramref name="duration"/> segundos.</summary>
    public async Task OrbitAround(Vector3 center, float duration)
    {
        _orbitTarget = center;
        _orbitAngle = Mathf.RadToDeg(
            Mathf.Atan2(
                GlobalPosition.X - center.X,
                GlobalPosition.Z - center.Z
            )
        );
        _isOrbiting = true;

        await ToSignal(
            GetTree().CreateTimer(duration, true),
            SceneTreeTimer.SignalName.Timeout
        );

        _isOrbiting = false;
    }
}
