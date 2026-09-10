using System.Collections.Generic;

namespace B4TGames.AI.BehaviourTree
{
    /// <summary>
    /// Decorator: runs its child a fixed number of times, then reports Success.
    ///
    /// One repetition per tick — NOT a loop inside a single tick. When the child
    /// completes, the repeater counts it and returns Running, which means "ask me
    /// again next frame". So a child that finishes instantly still takes N frames
    /// to be repeated N times. That is deliberate: looping inside one tick would
    /// freeze the editor the moment a child never returns Running.
    ///
    /// A repetition that comes back Failure aborts the whole thing — the repeater
    /// returns Failure and the counter resets. Pass repetitions &lt;= 0 to repeat
    /// forever (it then only ever returns Running, or Failure if the child fails).
    /// </summary>
    public sealed class BTRepeater : BTNode
    {
        private readonly BTNode child;
        private readonly int repetitions;

        private int completed;

        public BTRepeater(string name, int repetitions, BTNode child) : base(name)
        {
            this.repetitions = repetitions;
            this.child = child;
        }

        protected override BTStatus OnTick(List<string> trace)
        {
            var status = child.Tick(trace);

            if (status == BTStatus.Running)
            {
                return BTStatus.Running; // the current repetition is not over yet
            }

            if (status == BTStatus.Failure)
            {
                completed = 0;
                return BTStatus.Failure; // one repetition failed: give up on all of them
            }

            completed++;

            if (repetitions > 0 && completed >= repetitions)
            {
                completed = 0;
                return BTStatus.Success; // did them all
            }

            return BTStatus.Running; // more to go: come back next tick
        }

        /// <summary>
        /// Abandoned halfway: forget the repetitions already done, so next time this
        /// branch is reached the count starts at zero instead of resuming.
        /// </summary>
        public override void Halt()
        {
            completed = 0;
            child.Halt();
        }
    }
}
