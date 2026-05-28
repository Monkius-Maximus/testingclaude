using Godot;

namespace FootballGame;

/// <summary>
/// Relógio da partida. Avança em tempo de jogo (não real). Emite sinais
/// nos minutos importantes (45', 90', etc).
/// </summary>
public partial class MatchClock : Node
{
    /// <summary>Quantos segundos reais valem 1 minuto de jogo. Default: 60s real = 1 min.</summary>
    [Export] public float RealSecondsPerMatchMinute = 60f;

    [Export] public int MatchMinutesPerHalf = 45;
    [Export] public int InjuryTimeMaxMinutes = 5;

    public int   CurrentMinute { get; private set; } = 0;
    public int   CurrentHalf   { get; private set; } = 1;
    public bool  IsRunning     { get; private set; } = false;

    [Signal] public delegate void HalfTimeEventHandler();
    [Signal] public delegate void FullTimeEventHandler();
    [Signal] public delegate void MinuteTickEventHandler(int minute);

    private float _accumulator = 0f;

    public override void _Process(double delta)
    {
        if (!IsRunning) return;
        _accumulator += (float)delta;
        if (_accumulator >= RealSecondsPerMatchMinute)
        {
            _accumulator = 0f;
            CurrentMinute++;
            EmitSignal(SignalName.MinuteTick, CurrentMinute);

            if (CurrentHalf == 1 && CurrentMinute >= MatchMinutesPerHalf)
            {
                IsRunning = false;
                EmitSignal(SignalName.HalfTime);
            }
            else if (CurrentHalf == 2 && CurrentMinute >= MatchMinutesPerHalf * 2)
            {
                IsRunning = false;
                EmitSignal(SignalName.FullTime);
            }
        }
    }

    public void Start()        => IsRunning = true;
    public void Pause()        => IsRunning = false;
    public void StartSecondHalf()
    {
        CurrentHalf = 2;
        CurrentMinute = MatchMinutesPerHalf;
        IsRunning = true;
    }
}
