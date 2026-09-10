using System;

namespace B4TGames.AI.StateMachine
{
    /// <summary>
    /// A state built from lambdas, so simple states need no subclass at all:
    ///
    ///     var patrol = new FSMDelegateState("Patrol",
    ///         onEnter:  () => agent.SetDestination(NextWaypoint()),
    ///         onUpdate: () => animator.SetFloat("speed", agent.Velocity.magnitude));
    ///
    /// Every callback is optional; the ones you leave out simply do nothing.
    /// State that lives between ticks is captured by the closure, the same way
    /// BTAction and BTCondition capture theirs.
    /// </summary>
    public sealed class FSMDelegateState : FSMState
    {
        private readonly Action onEnter;
        private readonly Action onUpdate;
        private readonly Action onExit;

        public FSMDelegateState(
            string name,
            Action onEnter = null,
            Action onUpdate = null,
            Action onExit = null) : base(name)
        {
            this.onEnter = onEnter;
            this.onUpdate = onUpdate;
            this.onExit = onExit;
        }

        public override void Enter() => onEnter?.Invoke();

        public override void Update() => onUpdate?.Invoke();

        public override void Exit() => onExit?.Invoke();
    }
}
