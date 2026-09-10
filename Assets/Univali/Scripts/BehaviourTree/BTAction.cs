using System;
using System.Collections.Generic;

namespace B4TGames.AI.BehaviourTree
{
    /// <summary>Execution leaf: does something in the world and returns its status.</summary>
    public sealed class BTAction : BTNode
    {
        private readonly Func<BTStatus> act;

        public BTAction(string name, Func<BTStatus> act) : base(name)
        {
            this.act = act;
        }

        protected override BTStatus OnTick(List<string> trace) => act();
    }
}
