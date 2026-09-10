using System;
using System.Collections.Generic;

namespace B4TGames.AI.BehaviourTree
{
    /// <summary>Decision leaf: turns a bool into Success/Failure.</summary>
    public sealed class BTCondition : BTNode
    {
        private readonly Func<bool> test;

        public BTCondition(string name, Func<bool> test) : base(name)
        {
            this.test = test;
        }

        /// <summary>The test itself, which is exactly the guard question.</summary>
        public override bool CanTick() => test();

        protected override BTStatus OnTick(List<string> trace)
        {
            return test() ? BTStatus.Success : BTStatus.Failure;
        }
    }
}