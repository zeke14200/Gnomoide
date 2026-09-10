namespace B4TGames.AI.StateMachine
{
    /// <summary>
    /// One state of the machine. Enter and Exit fire exactly once per visit;
    /// Update fires every tick for as long as this state is the current one.
    ///
    /// That Enter/Exit pair is the whole point of a state machine: it is the hook
    /// a plain switch statement does not have, and the reason you can set a colour,
    /// reverse a direction or start a timer once instead of re-doing it every frame.
    ///
    /// Subclass this when a state carries its own fields. For a state you can
    /// express in a couple of lambdas, use <see cref="FSMDelegateState"/> instead.
    /// </summary>
    public abstract class FSMState
    {
        public string Name { get; }

        protected FSMState(string name) => Name = name;

        /// <summary>Called once, when the machine switches into this state.</summary>
        public virtual void Enter()
        {
        }

        /// <summary>Called every tick while this state is current.</summary>
        public virtual void Update()
        {
        }

        /// <summary>Called once, when the machine switches out of this state.</summary>
        public virtual void Exit()
        {
        }

        public override string ToString() => Name;
    }
}
