using Godot;
using System.Collections.Generic;

namespace FootballGame;

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
