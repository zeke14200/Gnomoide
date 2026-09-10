using System.Collections.Generic;

namespace B4TGames.AI.BehaviourTree
{
    /// <summary>
    /// A Behaviour Tree node. Every node knows how to do one thing only: answer a
    /// BTStatus when it receives a tick. Composites (Selector/Sequence) forward the
    /// tick to their children; leaves (Condition/Action) resolve it on the spot.
    /// </summary>
    public abstract class BTNode
    {
        public string Name { get; }

        protected BTNode(string name) => Name = name;

        /// <summary>
        /// Evaluates the node. <paramref name="trace"/> is optional and defaults to
        /// null: when a list is passed, every visited node writes its result into it
        /// so the console can show the path. Call Tick() with no argument to skip it.
        /// </summary>
        public BTStatus Tick(List<string> trace = null)
        {
            var status = OnTick(trace);
            trace?.Add($"{Name}: {status}");
            return status;
        }

        protected abstract BTStatus OnTick(List<string> trace);

        /// <summary>
        /// Could this node take the tick, WITHOUT starting anything?
        ///
        /// Composites use this to work out who is going to win before any node
        /// actually runs, so an abandoned branch can be halted before the branch
        /// that replaces it enters. Without it, Tick() both decides and starts,
        /// and the exit of the old branch lands after the enter of the new one.
        ///
        /// MUST be free of side effects: it is called in addition to Tick(), and
        /// possibly several times per frame. Only override it on guard-like nodes.
        ///
        /// The default answer is "yes, probably". That is deliberately
        /// conservative: a wrong yes costs one needless halt, a wrong no would
        /// hide a branch that should have run.
        /// </summary>
        public virtual bool CanTick() => true;

        /// <summary>
        /// Tells this node it was ABANDONED before finishing, so it can drop any
        /// half-done state — a repeater forgets how many repetitions it had done,
        /// a composite forgets which child was running.
        ///
        /// Called by a parent that stops ticking a subtree it was ticking before:
        /// a Selector whose higher-priority branch took over, or a Sequence that
        /// failed on an earlier child. Composites and decorators MUST pass it down
        /// to their children, or the state deeper in the subtree survives.
        ///
        /// Not the same as finishing. A node that returned Success or Failure on
        /// its own is done, not abandoned, and is never halted for that.
        ///
        /// Leaves usually have nothing to forget, so the default does nothing.
        /// </summary>
        public virtual void Halt()
        {
        }
    }
}