using System.Collections.Generic;
using B4TGames.AI.StateMachine;

/// <summary>
/// Cérebro de fantasma em máquina de estados finitos.
///
/// Um estado por comportamento, e uma tabela de transições dizendo quando trocar.
/// Todo o resto — mira, cantos, cores, velocidades — vem do <see cref="GhostBase"/>.
/// </summary>
public class GhostFSM : GhostBase
{
    private FSM fsm;

    // Guardados porque as transições e a colisão precisam apontar para eles
    private FSMDelegateState chase;
    private FSMDelegateState frightened;
    private FSMDelegateState eaten;

    protected override void BuildBrain()
    {
        var house = new FSMDelegateState("House",
            onEnter: EnterHouse);

        chase = new FSMDelegateState("Chase",
            onEnter: EnterChase,
            onUpdate: ChaseStep,
            onExit: ExitChase);

        frightened = new FSMDelegateState("Frightened",
            onEnter: EnterFrightened,
            onUpdate: FleeStep);

        eaten = new FSMDelegateState("Eaten",
            onEnter: EnterEaten);

        fsm = new FSM();
        fsm.AddTransition(house, chase, TimeToLeaveHouse);
        fsm.AddTransition(frightened, chase, IsFrightenedTimerOver);
        fsm.AddTransition(eaten, house, HasArrived);
        fsm.Start(house);
    }

    protected override void TickBrain(List<string> trace)
    {
        fsm.Tick(trace);
    }

    protected override void OnPowerPelletEaten()
    {
        // Equivale a um AddAnyTransition(frightened, ...): vale de qualquer estado
        fsm.ChangeState(frightened);
    }

    protected override void OnTouchedPacMan()
    {
        if (fsm.Current == frightened)
        {
            fsm.ChangeState(eaten);
        }
        else if (fsm.Current == chase)
        {
            pacMan.HasDied();
        }
    }
}
