namespace FootballGame;

/// <summary>Resultado de um tick de nó da Behavior Tree.</summary>
public enum BTStatus
{
    Success,
    Failure,
    Running
}

/// <summary>
/// Nó base da Behavior Tree. Todos os composites, decoradores e folhas
/// herdam disto e implementam <see cref="Tick"/>.
/// </summary>
public abstract class BTNode
{
    /// <summary>
    /// Executa um passo do nó.
    /// </summary>
    /// <param name="brain">O cérebro que está executando a árvore (acesso ao jogador e blackboard).</param>
    /// <param name="delta">Tempo em segundos desde o último tick.</param>
    public abstract BTStatus Tick(AIBrain brain, float delta);
}
