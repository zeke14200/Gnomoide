using System.Collections.Generic;

namespace B4TGames.AI.BehaviourTree
{
    /// <summary>
    /// Decorator: wraps exactly one child and flips its result — Success becomes
    /// Failure and Failure becomes Success.
    ///
    /// Useful to reuse a condition you already have instead of writing its
    /// opposite: Inverter(Condition "target in attack range?") reads as
    /// "target is NOT in attack range".
    /// </summary>
    public sealed class BTInverter : BTNode
    {
        private readonly BTNode child;

        public BTInverter(string name, BTNode child) : base(name)
        {
            this.child = child;
        }

        protected override BTStatus OnTick(List<string> trace)
        {
            var status = child.Tick(trace);

            if (status == BTStatus.Success) return BTStatus.Failure;
            if (status == BTStatus.Failure) return BTStatus.Success;

            // Running is not an outcome yet, so there is nothing to invert:
            // it goes through untouched and the child is asked again next tick.
            return BTStatus.Running;
        }

        public override void Halt()
        {
            child.Halt(); // a decorator owns no state here: just pass it down
        }
    }
}
