using System;

namespace B4TGames.AI.StateMachine
{
    /// <summary>
    /// An edge of the machine: where to go, and what has to be true to go there.
    ///
    /// Keeping the condition in the transition rather than inside the state is what
    /// makes the graph readable — the set of transitions IS the diagram, and you can
    /// list every way out of a state without opening the state's code.
    /// </summary>
    public sealed class FSMTransition
    {
        /// <summary>The state this edge leads to.</summary>
        public FSMState To { get; }

        /// <summary>True when this edge should be taken.</summary>
        public Func<bool> Condition { get; }

        /// <summary>Optional label, only used for the trace.</summary>
        public string Name { get; }

        public FSMTransition(FSMState to, Func<bool> condition, string name = null)
        {
            To = to;
            Condition = condition;
            Name = name ?? $"-> {to.Name}";
        }

        public override string ToString() => Name;
    }
}
