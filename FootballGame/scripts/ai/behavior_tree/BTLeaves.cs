using System;

namespace FootballGame;

/// <summary>
/// Folha de condição: avalia uma função booleana. Retorna Success se true, Failure se false.
/// Usada como guarda em sequences ("se tenho a bola, então atacar").
/// </summary>
public class BTCondition : BTNode
{
    private readonly Func<AIBrain, bool> _check;

    public BTCondition(Func<AIBrain, bool> check)
    {
        _check = check;
    }

    public override BTStatus Tick(AIBrain brain, float delta) =>
        _check(brain) ? BTStatus.Success : BTStatus.Failure;
}

/// <summary>
/// Folha de ação: executa uma função que retorna o status. Onde o comportamento
/// realmente acontece — escreve nas <c>Intended*</c> properties do Player.
/// </summary>
public class BTAction : BTNode
{
    private readonly Func<AIBrain, float, BTStatus> _action;

    public BTAction(Func<AIBrain, float, BTStatus> action)
    {
        _action = action;
    }

    public override BTStatus Tick(AIBrain brain, float delta) =>
        _action(brain, delta);
}
