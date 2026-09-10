using System.Collections.Generic;
using B4TGames.AI.BehaviourTree;

/// <summary>
/// Cérebro de fantasma em Behaviour Tree.
///
/// A ordem do Selector substitui a tabela de transições da FSM: o que lá era
/// AddAnyTransition, aqui é "ficar mais perto do topo". Todo o resto vem do
/// <see cref="GhostBase"/>.
/// </summary>
public class GhostBT : GhostBase
{
    public enum Behaviour
    {
        House,
        Chase,
        Frightened,
        Eaten
    }

    private BTNode root;
    private Behaviour tickBehaviour = Behaviour.House;

    protected override void BuildBrain()
    {
        // Cada ramo é Sequence(guarda, BTStateful(ação)). A guarda vem NA FRENTE
        // do BTStateful: dentro dele, o onEnter dispararia todo tick em que a
        // guarda fosse falsa.
        root = new BTSelector("Root",

            new BTSequence("Eaten",
                new BTCondition("IsEaten", () => tickBehaviour == Behaviour.Eaten),
                new BTStateful("ReturnHome",
                    child: new BTAction("GoHome", GoHomeTick),
                    onEnter: EnterEaten)),

            new BTSequence("Frightened",
                new BTCondition("IsFrightened", () => tickBehaviour == Behaviour.Frightened),
                new BTStateful("Flee",
                    child: new BTAction("Flee", FleeTick),
                    onEnter: EnterFrightened)),

            new BTSequence("House",
                new BTCondition("InHouse", () => tickBehaviour == Behaviour.House),
                new BTStateful("WaitInHouse",
                    child: new BTAction("Wait", WaitInHouseTick),
                    onEnter: EnterHouse)),

            // Sem guarda: perseguir é o que sobra quando nenhum ramo acima quer o tick
            new BTStateful("Chase",
                child: new BTAction("Chase", ChaseTick),
                onEnter: EnterChase,
                onExit: ExitChase)
        );
    }

    protected override void TickBrain(List<string> trace)
    {
        root.Tick(trace);
    }

    protected override void OnPowerPelletEaten()
    {
        // Olhos voltando para casa não se assustam
        if (tickBehaviour == Behaviour.Eaten) return;

        tickBehaviour = Behaviour.Frightened;
    }

    protected override void OnTouchedPacMan()
    {
        switch (tickBehaviour)
        {
            case Behaviour.Frightened:
                tickBehaviour = Behaviour.Eaten;
                break;

            case Behaviour.Eaten:
                break; // só os olhos: não mata nem é comido de novo

            default:
                pacMan.HasDied();
                break;
        }
    }

    // ------------------------------------------------------------------
    // As folhas. Running = "ainda estou nisto, me pergunte de novo";
    // Success = "terminei", e é o que libera o modo para o ramo seguinte.
    // ------------------------------------------------------------------

    private BTStatus GoHomeTick()
    {
        if (!HasArrived()) return BTStatus.Running;

        // Chegou: solta o modo, senão o ramo Eaten venceria para sempre
        tickBehaviour = Behaviour.House;
        return BTStatus.Success;
    }

    private BTStatus FleeTick()
    {
        if (IsFrightenedTimerOver())
        {
            tickBehaviour = Behaviour.Chase;
            return BTStatus.Success;
        }

        FleeStep();
        return BTStatus.Running;
    }

    private BTStatus WaitInHouseTick()
    {
        if (!TimeToLeaveHouse()) return BTStatus.Running;

        tickBehaviour = Behaviour.Chase;
        return BTStatus.Success;
    }

    private BTStatus ChaseTick()
    {
        ChaseStep();

        // Perseguir nunca "termina". Running é o que dá ao ramo um intervalo de
        // tempo, e é nesse intervalo que EnterChase/ExitChase se penduram.
        return BTStatus.Running;
    }
}
