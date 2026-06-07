using Godot;
using System.Collections.Generic;

namespace FootballGame;

/// <summary>
/// Aplica as regras do futebol: laterais, escanteios, tiros de meta e gols.
/// Conecta-se aos sinais da bola e reage com os reposicionamentos adequados.
/// </summary>
public partial class RulesManager : Node
{
    [Export] private NodePath _ballPath;
    [Export] private NodePath _eventBusPath;
    [Export] private NodePath _matchClockPath;

    [Signal] public delegate void CornerKickEventHandler(int team, string corner);
    [Signal] public delegate void GoalKickEventHandler(int team);
    [Signal] public delegate void ThrowInEventHandler(int team, Vector3 position);
    [Signal] public delegate void GoalEventHandler(int team);

    private Ball          _ball;
    private MatchEventBus _bus;
    private MatchClock    _clock;
    private readonly int[] _score = { 0, 0 };

    // Posições de reposição (em metros). Ajustar ao campo real.
    private static readonly Dictionary<string, Vector3> CornerPositions = new()
    {
        { "top_left",     new Vector3(-52.5f, 0.5f,  34f) },
        { "top_right",    new Vector3(-52.5f, 0.5f, -34f) },
        { "bottom_left",  new Vector3( 52.5f, 0.5f,  34f) },
        { "bottom_right", new Vector3( 52.5f, 0.5f, -34f) },
    };

    private static readonly Vector3 KickoffPosition = new(0f, 0.5f, 0f);

    // Dimensões da grande área (metros, devem casar com o Field)
    private const float PenaltyAreaDepth   = 16.5f;
    private const float PenaltyAreaHalfZ   = 20.16f;
    private const float FieldHalfLength    = 52.5f;

    public override void _Ready()
    {
        _ball  = GetNode<Ball>(_ballPath);
        _clock = GetNodeOrNull<MatchClock>(_matchClockPath);

        _ball.BallOutLeft   += (last) => OnBallOutLateral(last, "left");
        _ball.BallOutRight  += (last) => OnBallOutLateral(last, "right");
        _ball.BallOutTop    += (last) => OnBallOutEndline(last, "top");
        _ball.BallOutBottom += (last) => OnBallOutEndline(last, "bottom");

        // Escuta faltas para montar o tiro livre / pênalti
        _bus = GetNodeOrNull<MatchEventBus>(_eventBusPath);
        if (_bus != null) _bus.EventOccurred += OnMatchEvent;
    }

    private void OnMatchEvent(MatchEvent e)
    {
        if (e.Type == MatchEventType.Foul)
            SetupRestartFromFoul(e);
    }

    // ── Saída pela lateral → Arremesso lateral ───────────────────
    private async void OnBallOutLateral(Node lastTouch, string side)
    {
        int opposingTeam = (lastTouch as Player)?.Team == 0 ? 1 : 0;
        var pos = _ball.GlobalPosition;
        pos.Z = Mathf.Clamp(pos.Z, -34f, 34f);

        EmitSignal(SignalName.ThrowIn, opposingTeam, pos);
        await ToSignal(GetTree().CreateTimer(1.5f), SceneTreeTimer.SignalName.Timeout);
        _ball.ResetBall(pos);
    }

    // ── Saída pela linha de fundo → Escanteio ou Tiro de Meta ────
    private void OnBallOutEndline(Node lastTouch, string side)
    {
        int attackerTeam = (lastTouch as Player)?.Team ?? 0;

        if (side == "top")
        {
            if (attackerTeam == 0) GrantCorner(0, side);
            else                   GrantGoalKick(1);
        }
        else
        {
            if (attackerTeam == 1) GrantCorner(1, side);
            else                   GrantGoalKick(0);
        }
    }

    private async void GrantCorner(int team, string side)
    {
        float bz = _ball.GlobalPosition.Z;
        string corner = side == "top"
            ? (bz > 0 ? "top_left"    : "top_right")
            : (bz > 0 ? "bottom_left" : "bottom_right");

        EmitSignal(SignalName.CornerKick, team, corner);
        await ToSignal(GetTree().CreateTimer(1.5f), SceneTreeTimer.SignalName.Timeout);
        _ball.ResetBall(CornerPositions[corner]);
    }

    private async void GrantGoalKick(int team)
    {
        var pos = new Vector3(team == 0 ? -45f : 45f, 0.5f, 0f);
        EmitSignal(SignalName.GoalKick, team);
        await ToSignal(GetTree().CreateTimer(1.5f), SceneTreeTimer.SignalName.Timeout);
        _ball.ResetBall(pos);
    }

    /// <summary>
    /// Chamado pelo <see cref="CinematicDirector"/> ao fim da cinemática.
    /// Atualiza placar e prepara kickoff.
    /// </summary>
    public async void OnGoalScored(int team)
    {
        _score[team]++;
        EmitSignal(SignalName.Goal, team);

        // Publica no bus para HUD, estatísticas, comentário, etc.
        int minute = _clock?.CurrentMinute ?? 0;
        _bus?.Publish(MatchEvent.Goal(minute, team, "", "", KickoffPosition));

        GD.Print($"GOL! Placar: {_score[0]} x {_score[1]}");
        await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
        _ball.ResetBall(KickoffPosition);
    }

    // ── Falta → tiro livre ou pênalti ────────────────────────────
    private async void SetupRestartFromFoul(MatchEvent foul)
    {
        var pos        = foul.Position;
        int fouledTeam = foul.Team == 0 ? 1 : 0; // quem sofreu a falta cobra
        int minute     = foul.Minute;

        bool isPenalty = IsInDefensivePenaltyArea(pos, defendingTeam: foul.Team);

        if (isPenalty)
        {
            // Marca da cal do time que cometeu a falta
            pos = new Vector3(foul.Team == 0 ? -41f : 41f, 0.5f, 0f);
            _bus?.Publish(MatchEvent.Simple(MatchEventType.Penalty, minute, fouledTeam));
        }
        else
        {
            pos.Y = 0.5f;
            _bus?.Publish(MatchEvent.Simple(MatchEventType.FreeKick, minute, fouledTeam));
        }

        await ToSignal(GetTree().CreateTimer(1.5f), SceneTreeTimer.SignalName.Timeout);
        _ball.ResetBall(pos);
    }

    /// <summary>Verdadeiro se <paramref name="pos"/> está na grande área do time que defende.</summary>
    private bool IsInDefensivePenaltyArea(Vector3 pos, int defendingTeam)
    {
        float ownGoalX = defendingTeam == 0 ? -FieldHalfLength : FieldHalfLength;
        bool nearOwnLine = defendingTeam == 0
            ? pos.X <= ownGoalX + PenaltyAreaDepth
            : pos.X >= ownGoalX - PenaltyAreaDepth;
        return nearOwnLine && Mathf.Abs(pos.Z) < PenaltyAreaHalfZ;
    }
}
