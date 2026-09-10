using System;
using System.Collections.Generic;

namespace B4TGames.AI.BehaviourTree
{
    /// <summary>
    /// Decorator: wraps exactly one child, ticks it, reports what it returned and
    /// forwards that status UNCHANGED.
    ///
    /// This is the odd decorator of the family: it does not alter behaviour at all.
    /// Remove it and the tree does exactly the same thing — only the reporting is
    /// lost. That makes it a good illustration that a decorator can observe rather
    /// than control.
    ///
    /// It complements the `trace` parameter of <see cref="BTNode.Tick"/> instead of
    /// replacing it. The parameter records EVERY node of the tree, automatically and
    /// forever; this decorator records only the one branch you wrapped, which is
    /// what you want while hunting a specific bug — signal instead of a wall of text.
    ///
    /// The destination is an Action&lt;string&gt; rather than a hard-coded Debug.Log,
    /// so the library stays independent of UnityEngine. From a MonoBehaviour:
    ///
    ///     new BTTrace("suspect", Debug.Log, mySuspiciousBranch)
    /// </summary>
    public sealed class BTTrace : BTNode
    {
        private readonly BTNode child;
        private readonly Action<string> report;

        public BTTrace(string name, Action<string> report, BTNode child) : base(name)
        {
            this.report = report;
            this.child = child;
        }

        protected override BTStatus OnTick(List<string> trace)
        {
            var status = child.Tick(trace);

            report?.Invoke($"[{Name}] {child.Name} -> {status}");

            return status; // untouched: the parent cannot tell this node is here
        }

        public override void Halt()
        {
            child.Halt(); // this decorator owns no state: just pass it down
        }
    }
}
