using System.Collections.Generic;

namespace B4TGames.AI.BehaviourTree
{
    /// <summary>
    /// Selector = OR. Runs the children in order and stops at the first one that does
    /// NOT fail. This is the node that gives priority: Attack before Chase, Chase
    /// before Patrol.
    /// </summary>
    public sealed class BTSelector : BTNode
    {
        private readonly BTNode[] children;

        private int runningIndex = -1; // which child was left running last tick

        public BTSelector(string name, params BTNode[] children) : base(name)
        {
            this.children = children;
        }

        /// <summary>A selector runs if ANY of its children can.</summary>
        public override bool CanTick()
        {
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].CanTick()) return true;
            }

            return false;
        }

        protected override BTStatus OnTick(List<string> trace)
        {
            // Pass one, decide. Ask the higher-priority children whether one of
            // them is about to take over, and if so end the running branch NOW.
            //
            // This has to happen before anything is ticked. Tick() both decides
            // and starts, so halting afterwards would run the old branch's exit
            // after the new branch's enter — and the exit would undo what the
            // enter had just set up.
            if (runningIndex > 0)
            {
                for (int i = 0; i < runningIndex; i++)
                {
                    if (!children[i].CanTick()) continue;

                    children[runningIndex].Halt();
                    runningIndex = -1;
                    break;
                }
            }

            // Pass two, execute.
            for (int i = 0; i < children.Length; i++)
            {
                var status = children[i].Tick(trace);
                if (status == BTStatus.Failure) continue;

                // Child [i] took the tick, so children after it were not ticked at all
                // this frame. If one of them was running, it has just been abandoned.
                // Pass one already dealt with the higher-priority case; what is left
                // here is a branch that gave up on its own.
                if (runningIndex > i) children[runningIndex].Halt();

                runningIndex = status == BTStatus.Running ? i : -1;
                return status; // Success or Running: it already found something to do
            }

            // Everyone was ticked and everyone failed: nobody was skipped, so there
            // is nothing to abandon.
            runningIndex = -1;
            return BTStatus.Failure; // no child wanted to take over
        }

        public override void Halt()
        {
            if (runningIndex >= 0) children[runningIndex].Halt();
            runningIndex = -1;
        }
    }
}