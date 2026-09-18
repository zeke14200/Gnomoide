using UnityEngine;
using UnityEngine.AI;
using B4TGames.AI.StateMachine;
using B4TGames.AI.BehaviourTree;

public class IAInimigoHibrida : MonoBehaviour
{
    [Header("Referências")]
    public Transform jogador;
    public NavMeshAgent agente;
    public Transform[] pontosPatrulha;

    [Header("Configurações")]
    public float raioVisao = 10f;
    public float raioPerdaDeVisao = 15f;
    public float raioAliados = 5f;
    public int quantidadeParaUnir = 2;

    // A Máquina de Estados 
    private FSM fsm;

    // As Árvores de Comportamento 
    private BTNode arvorePatrulha;
    private BTNode arvorePerseguicao;
    private BTNode arvorePerseguicaoUnida;

    private int indicePatrulha = 0;
    private Collider[] bufferAliados = new Collider[10];

    void Start()
    {
        if (agente == null) agente = GetComponent<NavMeshAgent>();

        //Constrói as Árvores de Comportamento 
        ConstruirArvoresDeComportamento();

        //Constrói a Máquina de Estados 
        ConstruirMaquinaDeEstados();
    }

    void Update()
    {
        fsm?.Tick();
    }

    private void ConstruirArvoresDeComportamento()
    {
        // --- ÁRVORE DE PATRULHA ---
        arvorePatrulha = new BTAction("AndarPelosPontos", () =>
        {
            if (pontosPatrulha.Length == 0) return BTStatus.Failure;

            agente.speed = 2f;
            agente.stoppingDistance = 0f;

            if (agente.remainingDistance < 0.5f && !agente.pathPending)
            {
                indicePatrulha = (indicePatrulha + 1) % pontosPatrulha.Length;
            }
            agente.SetDestination(pontosPatrulha[indicePatrulha].position);
            
            return BTStatus.Running;
        });

        // --- ÁRVORE DE PERSEGUIÇÃO SOZINHO ---
        arvorePerseguicao = new BTAction("CorrerAtrasDoJogador", () =>
        {
            agente.speed = 5f;
            agente.stoppingDistance = 1.5f;
            agente.SetDestination(jogador.position);
            
            return BTStatus.Running;
        });

        // --- ÁRVORE DE PERSEGUIÇÃO UNIDA ---
        // O BTSelector testa de cima para baixo. O primeiro que não falhar, ele executa.
        arvorePerseguicaoUnida = new BTSelector("TaticasDeGrupo",
            
            // Tática 1: Frenética 
            new BTSequence("TaticaFrenetica",
                new BTCondition("MuitosAliados", () => ContarAliados() >= 6),
                new BTAction("EsmagarJogador", () =>
                {
                    agente.speed = 8f;
                    agente.stoppingDistance = 0f;
                    agente.SetDestination(jogador.position);
                    return BTStatus.Running;
                })
            ),

            // Tática 2: Mista 
            new BTSequence("TaticaMista",
                new BTCondition("AliadosMedianos", () => ContarAliados() >= 3),
                new BTAction("AtaqueMisto", () =>
                {
                    bool agirComoEmboscada = (gameObject.GetInstanceID() % 2 == 0);
                    agente.speed = agirComoEmboscada ? 4f : 5.5f;
                    agente.stoppingDistance = agirComoEmboscada ? 2.5f : 1f;

                    if (agirComoEmboscada)
                    {
                        Vector3 direcao = (transform.position - jogador.position).normalized;
                        agente.SetDestination(jogador.position + (direcao * 3f));
                    }
                    else
                    {
                        agente.SetDestination(jogador.position);
                    }
                    return BTStatus.Running;
                })
            ),

            // Tática 3: Emboscada 
            new BTAction("TaticaEmboscada", () =>
            {
                agente.speed = 4f;
                agente.stoppingDistance = 3f;
                Vector3 direcaoAfastamento = (transform.position - jogador.position).normalized;
                agente.SetDestination(jogador.position + (direcaoAfastamento * 3f));
                
                return BTStatus.Running;
            })
        );
    }

    private void ConstruirMaquinaDeEstados()
    {
        // Cria os estados baseados em delegate. 
        // O onUpdate de cada estado simplesmente repassa o pulso (Tick) para a sua respectiva árvore.
        var estadoPatrulha = new FSMDelegateState("Estado_Patrulha",
            onUpdate: () => arvorePatrulha.Tick()
        );

        var estadoPerseguicao = new FSMDelegateState("Estado_Perseguicao",
            onUpdate: () => arvorePerseguicao.Tick()
        );

        var estadoPerseguicaoUnida = new FSMDelegateState("Estado_PerseguicaoUnida",
            onUpdate: () => arvorePerseguicaoUnida.Tick()
        );

        fsm = new FSM();

        // Condições de transição 
        fsm.AddTransition(estadoPatrulha, estadoPerseguicaoUnida, () => VeJogador() && ContarAliados() >= quantidadeParaUnir);
        fsm.AddTransition(estadoPatrulha, estadoPerseguicao, () => VeJogador() && ContarAliados() < quantidadeParaUnir);

        fsm.AddTransition(estadoPerseguicao, estadoPatrulha, () => PerdeuJogador());
        fsm.AddTransition(estadoPerseguicao, estadoPerseguicaoUnida, () => ContarAliados() >= quantidadeParaUnir);

        fsm.AddTransition(estadoPerseguicaoUnida, estadoPatrulha, () => PerdeuJogador());
        fsm.AddTransition(estadoPerseguicaoUnida, estadoPerseguicao, () => ContarAliados() < quantidadeParaUnir);

        fsm.Start(estadoPatrulha);
    }

    // --- FUNÇÕES DE SENSOR ---
    private bool VeJogador()
    {
        return (transform.position - jogador.position).sqrMagnitude <= (raioVisao * raioVisao);
    }

    private bool PerdeuJogador()
    {
        return (transform.position - jogador.position).sqrMagnitude > (raioPerdaDeVisao * raioPerdaDeVisao);
    }

    private int ContarAliados()
    {
        int hits = Physics.OverlapSphereNonAlloc(transform.position, raioAliados, bufferAliados);
        int contagem = 0;
        for (int i = 0; i < hits; i++)
        {
            if (bufferAliados[i].CompareTag("Inimigo") && bufferAliados[i].gameObject != this.gameObject)
            {
                contagem++;
            }
        }
        return contagem;
    }
}