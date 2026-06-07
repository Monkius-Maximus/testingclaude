using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FootballGame;

/// <summary>
/// Orquestra a cinemática de gol em fases: slow-mo → câmera dramática →
/// celebração → HUD + reset. Conecta-se aos <see cref="GoalDetector"/>s
/// via grupo "goal_detectors".
/// </summary>
public partial class CinematicDirector : Node
{
    // ── Referências exportadas ────────────────────────────────────
    [Export] private NodePath _gameplayCameraPath;
    [Export] private NodePath _cinematicCameraPath;
    [Export] private NodePath _ballPath;
    [Export] private NodePath _rulesManagerPath;
    [Export] private NodePath _hudPath;

    /// <summary>Caminhos para os 22 jogadores em campo.</summary>
    [Export] private NodePath[] _playerPaths = System.Array.Empty<NodePath>();

    // ── Parâmetros de timing (segundos) ──────────────────────────
    [Export] public float SlowMoDuration    = 1.2f;
    [Export] public float SlowMoScale       = 0.15f;
    [Export] public float OrbitDuration     = 3.0f;
    [Export] public float CelebrateDuration = 3.5f;
    [Export] public float CameraReturnTime  = 1.2f;

    [Signal] public delegate void CinematicStartedEventHandler(int team);
    [Signal] public delegate void CinematicFinishedEventHandler();

    private Camera3D        _gameplayCam;
    private CinematicCamera _cinematicCam;
    private Ball            _ball;
    private RulesManager    _rules;
    private HUD             _hud;
    private readonly List<Player> _players = new();

    private bool _isPlaying = false;

    public override void _Ready()
    {
        _gameplayCam  = GetNode<Camera3D>(_gameplayCameraPath);
        _cinematicCam = GetNode<CinematicCamera>(_cinematicCameraPath);
        _ball         = GetNode<Ball>(_ballPath);
        _rules        = GetNode<RulesManager>(_rulesManagerPath);
        _hud          = GetNode<HUD>(_hudPath);

        foreach (var path in _playerPaths)
            _players.Add(GetNode<Player>(path));

        foreach (var node in GetTree().GetNodesInGroup("goal_detectors"))
        {
            if (node is GoalDetector gd)
                gd.GoalScored += OnGoalDetected;
        }
    }

    private async void OnGoalDetected(int team, Vector3 ballPos)
    {
        if (_isPlaying) return;
        _isPlaying = true;

        EmitSignal(SignalName.CinematicStarted, team);

        _ball.FreezeBall();
        FreezeAllPlayers();

        await Phase_SlowMo();
        await Phase_CinematicCamera(ballPos);
        await Phase_Celebrate(team);
        await Phase_ReturnToGame(team, ballPos);

        _isPlaying = false;
        EmitSignal(SignalName.CinematicFinished);
    }

    // ─────────────────────────────────────────────────────────────
    private async Task Phase_SlowMo()
    {
        var tw = CreateTween();
        tw.TweenMethod(
            Callable.From<float>(v => Engine.TimeScale = v),
            1.0f, SlowMoScale, 0.3f
        ).SetTrans(Tween.TransitionType.Sine);

        await ToSignal(tw, Tween.SignalName.Finished);

        await ToSignal(
            GetTree().CreateTimer(SlowMoDuration, true, true),
            SceneTreeTimer.SignalName.Timeout
        );

        var tw2 = CreateTween();
        tw2.TweenMethod(
            Callable.From<float>(v => Engine.TimeScale = v),
            SlowMoScale, 1.0f, 0.5f
        ).SetTrans(Tween.TransitionType.Sine);

        await ToSignal(tw2, Tween.SignalName.Finished);
    }

    private async Task Phase_CinematicCamera(Vector3 ballPos)
    {
        _gameplayCam.Current  = false;
        _cinematicCam.Current = true;

        var targetPos = ballPos + new Vector3(0f, 2.5f, -4f);
        await _cinematicCam.FlyTo(targetPos, ballPos, 0.8f);
        await _cinematicCam.OrbitAround(ballPos, OrbitDuration);
    }

    private async Task Phase_Celebrate(int scoringTeam)
    {
        UnfreezeAllPlayers();

        foreach (var player in _players)
        {
            if (player.Team == scoringTeam)
                player.Animator?.PlayCelebrate();
            else
                player.Animator?.PlayStumble();
        }

        var scorers = _players.FindAll(p => p.Team == scoringTeam);
        if (scorers.Count > 0)
        {
            var center = Vector3.Zero;
            foreach (var s in scorers) center += s.GlobalPosition;
            center /= scorers.Count;

            var camPos = center + new Vector3(0f, 4f, -6f);
            await _cinematicCam.FlyTo(camPos, center, 0.6f);
            await _cinematicCam.OrbitAround(center, CelebrateDuration * 0.7f);
        }

        await ToSignal(
            GetTree().CreateTimer(CelebrateDuration, true),
            SceneTreeTimer.SignalName.Timeout
        );
    }

    private async Task Phase_ReturnToGame(int team, Vector3 ballPos)
    {
        // O HUD reage ao gol via MatchEventBus (publicado por RulesManager.OnGoalScored).
        // Aqui só cuidamos da câmera e do reset.

        var tw = CreateTween();
        tw.TweenProperty(_cinematicCam, "fov", 75f, CameraReturnTime);
        await ToSignal(tw, Tween.SignalName.Finished);

        _gameplayCam.Current  = true;
        _cinematicCam.Current = false;

        await ToSignal(
            GetTree().CreateTimer(1.5f, true),
            SceneTreeTimer.SignalName.Timeout
        );

        foreach (var node in GetTree().GetNodesInGroup("goal_detectors"))
            (node as GoalDetector)?.Reactivate();

        _rules?.OnGoalScored(team);
    }

    // ── Helpers ──────────────────────────────────────────────────
    private void FreezeAllPlayers()
    {
        foreach (var p in _players) p.SetPhysicsProcess(false);
    }

    private void UnfreezeAllPlayers()
    {
        foreach (var p in _players) p.SetPhysicsProcess(true);
    }
}
