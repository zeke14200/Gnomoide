using System;
using System.Collections.Generic;

namespace B4TGames.AI.StateMachine
{
    /// <summary>
    /// A finite state machine: one current state, and a set of transitions that
    /// decide when to leave it.
    ///
    ///     var idle  = new FSMDelegateState("Idle");
    ///     var chase = new FSMDelegateState("Chase", onUpdate: ChasePlayer);
    ///     var dead  = new FSMDelegateState("Dead");
    ///
    ///     var fsm = new FSM();
    ///     fsm.AddTransition(idle, chase, () => CanSeePlayer());
    ///     fsm.AddTransition(chase, idle, () => !CanSeePlayer());
    ///     fsm.AddAnyTransition(dead, () => health &lt;= 0);   // wins from anywhere
    ///     fsm.Start(idle);
    ///
    ///     void Update() => fsm.Tick();
    ///
    /// Transitions are checked before Update, and only ONE fires per tick, so a
    /// chain of conditions cannot loop the machine through several states in a
    /// single frame.
    /// </summary>
    public sealed class FSM
    {
        // Outgoing edges of each state
        private readonly Dictionary<FSMState, List<FSMTransition>> transitions =
            new Dictionary<FSMState, List<FSMTransition>>();

        // Edges valid from ANY state. Checked first, so they always win:
        // "died", "was stunned", "game over" belong here.
        private readonly List<FSMTransition> anyTransitions = new List<FSMTransition>();

        // Shared empty list, so a state with no outgoing edges allocates nothing
        private static readonly List<FSMTransition> NoTransitions = new List<FSMTransition>();

        private List<FSMTransition> currentTransitions = NoTransitions;

        /// <summary>The state being ticked right now, or null before Start.</summary>
        public FSMState Current { get; private set; }

        /// <summary>Raised after every switch, with (previous state, new state).</summary>
        public event Action<FSMState, FSMState> Transitioned;

        /// <summary>Adds an edge from one state to another.</summary>
        public void AddTransition(FSMState from, FSMState to, Func<bool> condition, string name = null)
        {
            if (!transitions.TryGetValue(from, out var list))
            {
                list = new List<FSMTransition>();
                transitions[from] = list;
            }

            list.Add(new FSMTransition(to, condition, name));
        }

        /// <summary>
        /// Adds an edge that can fire from any state. These are tested before the
        /// current state's own edges, so they take priority.
        /// </summary>
        public void AddAnyTransition(FSMState to, Func<bool> condition, string name = null)
        {
            anyTransitions.Add(new FSMTransition(to, condition, name));
        }

        /// <summary>Enters the first state. Its Enter() runs immediately.</summary>
        public void Start(FSMState state)
        {
            Current = state;
            currentTransitions = TransitionsOf(state);

            Current.Enter();
            Transitioned?.Invoke(null, Current);
        }

        /// <summary>
        /// Advances the machine: first look for an edge to take, then update
        /// whichever state ended up current.
        ///
        /// <paramref name="trace"/> is optional and defaults to null: pass a list
        /// and the machine writes what it did into it.
        /// </summary>
        public void Tick(List<string> trace = null, bool logUpdates = false)
        {
            if (Current == null) return;

            var next = NextTransition();
            if (next != null)
            {
                trace?.Add($"{Current.Name} {next.Name}");
                ChangeState(next.To);
            }

            if (logUpdates)
            {
                trace?.Add($"{Current.Name}: update");
            }

            Current.Update();
        }

        /// <summary>
        /// Switches state by hand, without going through a condition. Useful when an
        /// outside event already knows where the machine should go — a collision, a
        /// button, a finished animation.
        ///
        /// Returns false when the machine is already in that state.
        /// </summary>
        public bool ChangeState(FSMState next)
        {
            if (next == null || next == Current) return false;

            var previous = Current;

            previous?.Exit();
            Current = next;
            currentTransitions = TransitionsOf(next);
            Current.Enter();

            Transitioned?.Invoke(previous, Current);
            return true;
        }

        /// <summary>The first edge whose condition holds, or null if none does.</summary>
        private FSMTransition NextTransition()
        {
            // "From anywhere" first: these outrank whatever the current state wants
            for (var i = 0; i < anyTransitions.Count; i++)
            {
                var transition = anyTransitions[i];
                if (transition.To == Current) continue;
                if (transition.Condition()) return transition;
            }

            for (var i = 0; i < currentTransitions.Count; i++)
            {
                var transition = currentTransitions[i];
                if (transition.Condition()) return transition;
            }

            return null;
        }

        private List<FSMTransition> TransitionsOf(FSMState state)
        {
            return transitions.GetValueOrDefault(state, NoTransitions);
        }
    }
}
