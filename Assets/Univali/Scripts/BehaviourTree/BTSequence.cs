using System.Collections.Generic;

namespace B4TGames.AI.BehaviourTree
{
    /// <summary>
    /// Sequence = AND. Runs the children in order and stops at the first one that does
    /// NOT return Success. Used as "condition + action": it only attacks if the target
    /// is within range.
    /// </summary>
    public sealed class BTSequence : BTNode
    {
        private readonly BTNode[] children;

        private int runningIndex = -1; // which child was left running last tick

        public BTSequence(string name, params BTNode[] children) : base(name)
        {
            this.children = children;
        }

        /// <summary>A sequence só arranca se o primeiro filho arrancar: é a guarda.</summary>
        public override bool CanTick()
        {
            return children.Length == 0 || children[0].CanTick();
        }

        protected override BTStatus OnTick(List<string> trace)
        {
            for (int i = 0; i < children.Length; i++)
            {
                var status = children[i].Tick(trace);
                if (status == BTStatus.Success) continue;

                // The sequence stops at child [i], so children after it were not
                // ticked this frame. This is the case that matters: a condition at
                // the front turning false while a later action was still running.
                if (runningIndex > i) children[runningIndex].Halt();

                runningIndex = status == BTStatus.Running ? i : -1;
                return status; // Failure or Running interrupt the sequence
            }

            runningIndex = -1;
            return BTStatus.Success; // all of them passed
        }

        public override void Halt()
        {
            if (runningIndex >= 0) children[runningIndex].Halt();
            runningIndex = -1;
        }
    }
}