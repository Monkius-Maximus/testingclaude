using Godot;

namespace FootballGame;

/// <summary>
/// Coleta estatísticas ao vivo de uma partida (posse, chutes, passes, faltas,
/// cartões). Deve ser adicionado como filho do nó Match e registra-se no
/// grupo "match_stats" para que outros nós o localizem sem referência direta.
/// </summary>
public partial class MatchStats : Node
{
    // ── Contadores por time (índice 0 = casa, 1 = visitante) ─────
    private readonly int[] _possession      = { 0, 0 };  // frames com posse
    private readonly int[] _shots           = { 0, 0 };
    private readonly int[] _shotsOnTarget   = { 0, 0 };
    private readonly int[] _passAttempts    = { 0, 0 };
    private readonly int[] _passCompleted   = { 0, 0 };
    private readonly int[] _tackles         = { 0, 0 };
    private readonly int[] _fouls           = { 0, 0 };
    private readonly int[] _yellowCards     = { 0, 0 };
    private readonly int[] _redCards        = { 0, 0 };
    private readonly int[] _corners         = { 0, 0 };

    private int _totalPossessionFrames = 0;

    public override void _Ready() => AddToGroup("match_stats");

    // ── API de registro (chamada por Player, RefereeManager etc.) ──

    public void RegisterPossession(int team)
    {
        _possession[Clamp(team)]++;
        _totalPossessionFrames++;
    }

    public void RegisterShot(int team, bool onTarget)
    {
        _shots[Clamp(team)]++;
        if (onTarget) _shotsOnTarget[Clamp(team)]++;
    }

    public void RegisterPassAttempt(int team)  => _passAttempts[Clamp(team)]++;
    public void RegisterPassCompleted(int team) => _passCompleted[Clamp(team)]++;
    public void RegisterTackle(int team)        => _tackles[Clamp(team)]++;
    public void RegisterFoul(int team)          => _fouls[Clamp(team)]++;
    public void RegisterYellowCard(int team)    => _yellowCards[Clamp(team)]++;
    public void RegisterRedCard(int team)       => _redCards[Clamp(team)]++;
    public void RegisterCorner(int team)        => _corners[Clamp(team)]++;

    // ── Leitores ──────────────────────────────────────────────────

    public float PossessionPct(int team)
    {
        if (_totalPossessionFrames == 0) return 50f;
        return _possession[Clamp(team)] * 100f / _totalPossessionFrames;
    }

    public int  Shots(int team)          => _shots[Clamp(team)];
    public int  ShotsOnTarget(int team)  => _shotsOnTarget[Clamp(team)];
    public int  PassAttempts(int team)   => _passAttempts[Clamp(team)];
    public int  PassCompleted(int team)  => _passCompleted[Clamp(team)];
    public int  Tackles(int team)        => _tackles[Clamp(team)];
    public int  Fouls(int team)          => _fouls[Clamp(team)];
    public int  YellowCards(int team)    => _yellowCards[Clamp(team)];
    public int  RedCards(int team)       => _redCards[Clamp(team)];
    public int  Corners(int team)        => _corners[Clamp(team)];

    public float PassAccuracy(int team)
    {
        int att = _passAttempts[Clamp(team)];
        return att == 0 ? 0f : _passCompleted[Clamp(team)] * 100f / att;
    }

    private static int Clamp(int t) => t == 0 ? 0 : 1;
}
