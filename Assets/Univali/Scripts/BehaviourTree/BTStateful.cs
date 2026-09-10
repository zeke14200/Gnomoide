using System;
using System.Collections.Generic;

namespace B4TGames.AI.BehaviourTree
{
    /// <summary>
    /// Decorator that gives its child a LIFETIME, and fires a callback at each end
    /// of it. This is the Behaviour Tree answer to a state machine's Enter/Exit.
    ///
    /// A tree has no "current state" to hook: it re-evaluates from the root every
    /// tick, so nothing is ever "entered". What it does have is a child that keeps
    /// answering Running — and the first and last tick of that stretch are exactly
    /// the two edges a state machine calls Enter and Exit.
    ///
    /// onExit fires on BOTH ways out, which is what makes it equivalent to Exit:
    ///   - the child finished on its own (returned Success or Failure)
    ///   - the child was abandoned, and a parent called Halt()
    ///
    /// Unity's own com.unity.behavior does the same thing with OnStart/OnEnd, and
    /// still calls OnEnd when a running node is interrupted.
    ///
    /// USAGE — the guard condition goes IN FRONT of this node, never inside it:
    ///
    ///     new BTSequence("Flee",
    ///         new BTCondition("IsScared", () => scared),
    ///         new BTStateful("FleeRun",
    ///             onEnter: () => { speed = slow; colour = blue; },
    ///             onExit:  () => colour = normal,
    ///             child:   new BTAction("Run", RunAway)));
    ///
    /// Wrapping the condition instead would fire onEnter and onExit on every tick
    /// the condition is false, because onEnter runs before the child is ticked.
    ///
    /// Note the child must return Running while it is still working. A child that
    /// returns Success every tick has no lifetime, so onEnter/onExit would fire
    /// on every single tick.
    ///
    /// ORDERING — BTSelector halts an abandoned branch BEFORE ticking the branch
    /// that replaces it (its "pass one"), so onExit always lands before the next
    /// onEnter. An onExit that restores shared state is therefore safe.
    /// </summary>
    public sealed class BTStateful : BTNode
    {
        private readonly BTNode child;
        private readonly Action onEnter;
        private readonly Action onExit;

        // Whether the child is in the middle of a Running stretch right now
        private bool active;

        public BTStateful(
            string name,
            BTNode child,
            Action onEnter = null,
            Action onExit = null) : base(name)
        {
            this.child = child;
            this.onEnter = onEnter;
            this.onExit = onExit;
        }

        /// <summary>Is the child in the middle of a run? Handy for debugging.</summary>
        public bool IsActive => active;

        /// <summary>Passes the question straight through: the decorator has no guard.</summary>
        public override bool CanTick() => child.CanTick();

        protected override BTStatus OnTick(List<string> trace)
        {
            // First tick of a new stretch
            if (!active)
            {
                active = true;
                onEnter?.Invoke();
            }

            var status = child.Tick(trace);

            // Finished by itself. That counts as leaving, just as much as being halted.
            if (status != BTStatus.Running)
            {
                active = false;
                onExit?.Invoke();
            }

            return status;
        }

        /// <summary>
        /// Abandoned before finishing — a higher-priority branch took the tick, or
        /// a guard in front of us turned false. Same exit, different reason.
        /// </summary>
        public override void Halt()
        {
            if (active)
            {
                active = false;
                onExit?.Invoke();
            }

            child.Halt();
        }
    }
}
