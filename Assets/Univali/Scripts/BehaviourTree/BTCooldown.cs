using System.Collections.Generic;
using UnityEngine;

namespace B4TGames.AI.BehaviourTree
{
    /// <summary>
    /// Decorator: wraps exactly one child and locks it for a few seconds after the
    /// child finishes.
    ///
    /// While the cooldown is counting down, the decorator returns Failure WITHOUT
    /// ticking the child, so a Selector above it hands the tick to a lower-priority
    /// branch. This is how a branch says "not now, let someone else go".
    ///
    /// The timer is armed when the child FINISHES — that is, when it returns
    /// Success or Failure. A child that keeps returning Running is never
    /// interrupted, and never arms the cooldown either: to be paced by this
    /// decorator, an action has to report when one repetition is done.
    ///
    /// Note: this is the only node in the library that depends on UnityEngine,
    /// because it reads Time.time.
    /// </summary>
    public sealed class BTCooldown : BTNode
    {
        private readonly BTNode child;
        private readonly float seconds;

        private float readyTime; // starts at 0, so the first tick always runs

        public BTCooldown(string name, float seconds, BTNode child) : base(name)
        {
            this.seconds = seconds;
            this.child = child;
        }

        protected override BTStatus OnTick(List<string> trace)
        {
            if (Time.time < readyTime)
            {
                return BTStatus.Failure;
            }

            var status = child.Tick(trace);

            if (status != BTStatus.Running)
            {
                readyTime = Time.time + seconds;
            }

            return status;
        }

        /// <summary>
        /// Passes the halt down but KEEPS its own timer on purpose. A cooldown is a
        /// wall-clock rule, not progress through a task: if leaving the branch reset
        /// it, an agent could fire every frame just by stepping out of the branch
        /// and back into it.
        /// </summary>
        public override void Halt()
        {
            child.Halt();
        }
    }
}
