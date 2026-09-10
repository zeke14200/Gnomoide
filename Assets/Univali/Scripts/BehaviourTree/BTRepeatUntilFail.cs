using System.Collections.Generic;

namespace B4TGames.AI.BehaviourTree
{
    /// <summary>
    /// Decorator: keeps running its child until the child finally fails, and then
    /// reports Success — the failure is the expected end of the loop, not an error.
    ///
    /// Typical use is a branch that should run "for as long as it still can":
    /// RepeatUntilFail(Sequence[ Condition "is there ammo left?", Action "shoot" ])
    /// keeps shooting and reports Success once the ammo runs out.
    ///
    /// Like <see cref="BTRepeater"/>, it does one repetition per tick, so it never
    /// blocks the frame. Note there is no upper bound: while the child keeps
    /// succeeding, this node keeps returning Running and the branch owns the tick.
    /// If that could go on forever, put a condition inside the child that
    /// eventually fails — that is the only way out.
    /// </summary>
    public sealed class BTRepeatUntilFail : BTNode
    {
        private readonly BTNode child;

        public BTRepeatUntilFail(string name, BTNode child) : base(name)
        {
            this.child = child;
        }

        protected override BTStatus OnTick(List<string> trace)
        {
            var status = child.Tick(trace);

            if (status == BTStatus.Failure)
            {
                return BTStatus.Success; // the loop ended exactly as intended
            }

            return BTStatus.Running; // Success or Running: keep going next tick
        }

        public override void Halt()
        {
            child.Halt(); // this decorator owns no state: just pass it down
        }
    }
}
