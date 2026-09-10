namespace B4TGames.AI.BehaviourTree
{
    /// <summary>
    /// Result of evaluating a node. This is the vocabulary of the whole tree.
    /// </summary>
    public enum BTStatus
    {
        Success,    // finished, and it worked
        Failure,    // did not work (or the condition is false)
        Running     // still happening, evaluate it again on the next tick
    }
}