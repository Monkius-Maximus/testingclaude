using Godot;
using System.Collections.Generic;

namespace FootballGame;

/// <summary>Fase de jogo do time, que orienta a movimentação sem bola.</summary>
public enum TeamPhase
{
    Attacking,  // este time tem a posse
    Defending,  // o adversário tem a posse
    Transition  // bola solta (ninguém domina)
}

/// <summary>
/// "Quadro-negro" do time: dados consultados pelos jogadores via <see cref="AIBrain"/>.
/// Atualizado uma vez por frame pelo <see cref="TeamController"/>. Sem lógica aqui —
/// apenas um agregador de estado.
/// </summary>
public partial class TeamBlackboard : Node
{
    // ── Estado da partida ─────────────────────────────────────────
    public Ball    Ball;
    public Vector3 BallPosition;
    public Vector3 BallVelocity;
    public Player  BallCarrier;
    public bool    TeamHasPossession;
    public TeamPhase Phase = TeamPhase.Transition;

    // ── Elencos (referências às listas do TeamController; sempre atuais) ──
    public IReadOnlyList<Player> Teammates;
    public IReadOnlyList<Player> Opponents;

    // ── Formação em tempo real ────────────────────────────────────
    public Dictionary<string, Vector3> FormationSlots = new();

    // ── Alvos táticos ────────────────────────────────────────────
    public Vector3 OwnGoalPosition;
    public Vector3 OpponentGoalPosition;

    // ── Ordens do humano (controle assistido) ────────────────────
    /// <summary>Adversário sendo pressionado coletivamente (botão LB).</summary>
    public Player  PressureTarget;

    /// <summary>Humano pediu para o goleiro sair (botão Y/Triângulo).</summary>
    public bool    GoalkeeperRush;
}
