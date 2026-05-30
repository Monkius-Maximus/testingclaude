using Godot;
using System.Collections.Generic;

namespace FootballGame;

/// <summary>
/// Gerencia uma disputa de pênaltis. Pode ser usada após empate em Copa ou
/// chamada diretamente ao final de uma partida.
///
/// Fluxo: 5 cobranças cada → sudden death se empate → emite ShootoutComplete(winner).
/// Para o humano: aguarda input de direção (esquerda/centro/direita) + timing de kick.
/// Para a IA: cálculo probabilístico baseado em Shooting/Reflexes.
/// </summary>
public partial class PenaltyShootout : Node
{
    [Signal] public delegate void ShootoutCompleteEventHandler(int winnerTeam);
    [Signal] public delegate void PenaltyResultEventHandler(int team, bool scored, string shooterId);

    private struct PenaltyKick
    {
        public int  Team;
        public bool Scored;
    }

    // Configuração
    private const int KicksEach        = 5;
    private const float GkAimTimeLimit = 3f;  // segundos para o humano escolher direção do GK
    private const float ShooterTimeLimit = 4f;

    // Estado
    private List<PenaltyKick> _results = new();
    private int  _currentTeam       = 0;
    private int  _kicksTeam0        = 0;
    private int  _kicksTeam1        = 0;
    private bool _suddenDeath       = false;
    private bool _waitingInput      = false;
    private float _inputTimer       = 0f;
    private bool _isShooterHuman    = false;
    private bool _isGkHuman         = false;
    private int  _humanChoice       = -1;  // -1=nenhum, 0=esq, 1=centro, 2=dir
    private int  _gkChoice          = -1;

    // Times (atribuídos antes de Start)
    public int HumanControlledTeam { get; set; } = 0;
    public List<Player> TeamPlayers0 { get; set; } = new();
    public List<Player> TeamPlayers1 { get; set; } = new();

    private enum ShootoutPhase { Idle, WaitingShooterInput, WaitingGkInput, Resolving }
    private ShootoutPhase _phase = ShootoutPhase.Idle;

    // ── Scores rápidos ────────────────────────────────────────────
    public int Score(int team)
    {
        int s = 0;
        foreach (var k in _results) if (k.Team == team && k.Scored) s++;
        return s;
    }

    // ── API ───────────────────────────────────────────────────────

    public void StartShootout()
    {
        _results.Clear();
        _currentTeam  = 0;
        _kicksTeam0   = 0;
        _kicksTeam1   = 0;
        _suddenDeath  = false;
        _phase        = ShootoutPhase.Idle;
        GD.Print("Disputa de pênaltis iniciada!");
        PrepareNextKick();
    }

    // Chamado pela UI quando o humano escolhe a direção de chute (0=esq, 1=ctr, 2=dir)
    public void SetShooterChoice(int direction)
    {
        if (_phase == ShootoutPhase.WaitingShooterInput)
            _humanChoice = direction;
    }

    // Chamado pela UI quando o GK humano escolhe direção de mergulho
    public void SetGkChoice(int direction)
    {
        if (_phase == ShootoutPhase.WaitingGkInput)
            _gkChoice = direction;
    }

    // ── Loop ──────────────────────────────────────────────────────

    public override void _Process(double delta)
    {
        if (_phase == ShootoutPhase.Idle) return;

        _inputTimer += (float)delta;

        switch (_phase)
        {
            case ShootoutPhase.WaitingShooterInput:
                if (_humanChoice >= 0 || !_isShooterHuman || _inputTimer > ShooterTimeLimit)
                {
                    if (_humanChoice < 0) _humanChoice = GD.RandRange(0, 2); // timeout → aleatório
                    _phase = ShootoutPhase.WaitingGkInput;
                    _inputTimer = 0f;
                }
                break;

            case ShootoutPhase.WaitingGkInput:
                if (_gkChoice >= 0 || !_isGkHuman || _inputTimer > GkAimTimeLimit)
                {
                    if (_gkChoice < 0) _gkChoice = GD.RandRange(0, 2);
                    _phase = ShootoutPhase.Resolving;
                    CallDeferred(nameof(ResolveKick));
                }
                break;
        }
    }

    private void PrepareNextKick()
    {
        _humanChoice  = -1;
        _gkChoice     = -1;
        _inputTimer   = 0f;

        _isShooterHuman = _currentTeam == HumanControlledTeam;
        _isGkHuman      = _currentTeam != HumanControlledTeam; // GK do time adversário

        _phase = ShootoutPhase.WaitingShooterInput;
        GD.Print($"Próxima cobrança: Time {_currentTeam} — isHuman={_isShooterHuman}");
    }

    private void ResolveKick()
    {
        _phase = ShootoutPhase.Idle;

        // Obtém atributos do cobrador e do goleiro
        var shooterList = _currentTeam == 0 ? TeamPlayers0 : TeamPlayers1;
        var gkList      = _currentTeam == 0 ? TeamPlayers1 : TeamPlayers0;
        int shooterIdx  = _currentTeam == 0 ? _kicksTeam0 : _kicksTeam1;

        int shootingAttr = GetAttr(shooterList, shooterIdx % Mathf.Max(shooterList.Count, 1), "Shooting", 70);
        int reflexes     = GetAttr(gkList, 0, "GkReflexes", 70); // GK é sempre índice 0

        // Probabilidade base de gol
        float baseProb = 0.75f + (shootingAttr - 70) * 0.003f;
        baseProb = Mathf.Clamp(baseProb, 0.55f, 0.92f);

        // Se atirador e GK escolheram o mesmo lado → chance de defesa aumenta
        bool sameDirection = _humanChoice == _gkChoice;
        if (sameDirection)
            baseProb -= 0.30f + (reflexes - 70) * 0.004f;

        baseProb = Mathf.Clamp(baseProb, 0.10f, 0.95f);
        bool scored = GD.Randf() < baseProb;

        string shooterId = shooterList.Count > 0 ? shooterList[shooterIdx % shooterList.Count].PlayerId : $"p{_currentTeam}";

        _results.Add(new PenaltyKick { Team = _currentTeam, Scored = scored });
        EmitSignal(SignalName.PenaltyResult, _currentTeam, scored, shooterId);
        GD.Print($"Pênalti time {_currentTeam}: {(scored ? "GOL" : "DEFENDIDO")} — {shooterId}");

        // Avança contadores
        if (_currentTeam == 0) _kicksTeam0++; else _kicksTeam1++;
        _currentTeam = 1 - _currentTeam; // alterna time

        // Verifica encerramento
        if (IsShootoutOver())
        {
            int winner = Score(0) > Score(1) ? 0 : 1;
            EmitSignal(SignalName.ShootoutComplete, winner);
            GD.Print($"Pênaltis: {Score(0)} x {Score(1)} — Vencedor: Time {winner}");
        }
        else
        {
            PrepareNextKick();
        }
    }

    private bool IsShootoutOver()
    {
        int k0 = _kicksTeam0, k1 = _kicksTeam1;
        int s0 = Score(0),    s1 = Score(1);

        if (!_suddenDeath)
        {
            // Verifica se alguém já não pode mais alcançar o adversário
            if (k0 < KicksEach && MathematicallyOver(k0, k1, s0, s1)) return true;
            if (k0 >= KicksEach && k1 >= KicksEach)
            {
                if (s0 != s1) return true;
                _suddenDeath = true;
            }
        }
        else
        {
            // Sudden death: ambos cobram e quem marcar e outro não → vence
            if (k0 > k1) return false; // time 1 ainda não cobrou nesta rodada
            if (s0 != s1) return true;
        }
        return false;
    }

    private static bool MathematicallyOver(int k0, int k1, int s0, int s1)
    {
        int rem0 = KicksEach - k0;
        int rem1 = KicksEach - k1;
        return (s0 + rem0 < s1) || (s1 + rem1 < s0);
    }

    private static int GetAttr(List<Player> players, int idx, string attr, int def)
    {
        if (players == null || players.Count == 0) return def;
        var p = players[Mathf.Clamp(idx, 0, players.Count - 1)];
        return attr switch
        {
            "Shooting"   => p.Shooting,
            "GkReflexes" => p is Goalkeeper gk ? gk.Reflexes : def,
            _            => def
        };
    }
}
