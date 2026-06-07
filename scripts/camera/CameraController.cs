using Godot;

namespace FootballGame;

/// <summary>
/// Câmera principal do gameplay. Modo orbital (segue a bola) ou tático (vista aérea).
/// </summary>
public partial class CameraController : Camera3D
{
    public enum CameraMode { FollowBall, Tactical }

    [Export] public CameraMode Mode          = CameraMode.FollowBall;
    [Export] public float      FollowSpeed   = 5f;
    [Export] public float      ZoomMin       = 10f;
    [Export] public float      ZoomMax       = 40f;
    [Export] public float      ZoomSpeed     = 3f;
    [Export] public float      HeightOffset  = 18f;
    [Export] public float      DistanceOffset = 22f;

    private Node3D _ball;
    private float  _zoomTarget = 20f;
    private float  _yaw        = 0f;
    private float  _pitch      = -45f;

    public override void _Ready()
    {
        _ball = GetTree().GetFirstNodeInGroup("ball") as Node3D;
        _zoomTarget = HeightOffset;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp)
                _zoomTarget = Mathf.Clamp(_zoomTarget - ZoomSpeed, ZoomMin, ZoomMax);
            else if (mb.ButtonIndex == MouseButton.WheelDown)
                _zoomTarget = Mathf.Clamp(_zoomTarget + ZoomSpeed, ZoomMin, ZoomMax);
        }

        if (@event is InputEventMouseMotion mm
            && Input.IsMouseButtonPressed(MouseButton.Right))
        {
            _yaw   -= mm.Relative.X * 0.3f;
            _pitch  = Mathf.Clamp(_pitch - mm.Relative.Y * 0.2f, -80f, -10f);
        }

        if (@event.IsActionPressed("cam_tactical")) Mode = CameraMode.Tactical;
        else if (@event.IsActionPressed("cam_follow")) Mode = CameraMode.FollowBall;
    }

    public override void _PhysicsProcess(double delta)
    {
        switch (Mode)
        {
            case CameraMode.FollowBall: FollowBall((float)delta); break;
            case CameraMode.Tactical:   TacticalView((float)delta); break;
        }
    }

    private void FollowBall(float delta)
    {
        if (_ball == null) return;

        float pitchRad = Mathf.DegToRad(_pitch);
        float yawRad   = Mathf.DegToRad(_yaw);

        var offset = new Vector3(
            Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
            -Mathf.Sin(pitchRad),
            Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
        ) * _zoomTarget;

        var target = _ball.GlobalPosition + offset;
        GlobalPosition = GlobalPosition.Lerp(target, FollowSpeed * delta);
        LookAt(_ball.GlobalPosition, Vector3.Up);
    }

    private void TacticalView(float delta)
    {
        var target = new Vector3(0f, 35f, 0f);
        GlobalPosition = GlobalPosition.Lerp(target, FollowSpeed * delta);
        LookAt(Vector3.Zero, Vector3.Forward);
    }
}
