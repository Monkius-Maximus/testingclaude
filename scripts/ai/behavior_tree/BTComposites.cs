using System.Collections.Generic;

namespace FootballGame;

/// <summary>
/// Selector: tenta cada filho na ordem; retorna no primeiro Success ou Running.
/// Falha apenas se todos os filhos falharem. Análogo a um "OR" curto-circuitado.
/// </summary>
public class BTSelector : BTNode
{
    private readonly List<BTNode> _children;

    public BTSelector(params BTNode[] children)
    {
        _children = new List<BTNode>(children);
    }

    public override BTStatus Tick(AIBrain brain, float delta)
    {
        foreach (var child in _children)
        {
            var status = child.Tick(brain, delta);
            if (status != BTStatus.Failure)
                return status;
        }
        return BTStatus.Failure;
    }
}

/// <summary>
/// Sequence: executa filhos em ordem; falha no primeiro Failure ou retorna Running.
/// Sucesso só se todos forem Success. Análogo a um "AND" curto-circuitado.
/// </summary>
public class BTSequence : BTNode
{
    private readonly List<BTNode> _children;

    public BTSequence(params BTNode[] children)
    {
        _children = new List<BTNode>(children);
    }

    public override BTStatus Tick(AIBrain brain, float delta)
    {
        foreach (var child in _children)
        {
            var status = child.Tick(brain, delta);
            if (status != BTStatus.Success)
                return status;
        }
        return BTStatus.Success;
    }
}
