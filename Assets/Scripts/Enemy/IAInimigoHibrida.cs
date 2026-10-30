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

    [Header("Status do Grupo (Automático)")]
    public int AliadosProximos { get; private set; }
    public bool EhLider { get; private set; }
    
    [Header("Debug Visual (Monitor da IA)")]
    public string estadoAtual = "";
    public string taticaAtual = "";

    private FSM fsm;
    private BTNode arvorePatrulha;
    private BTNode arvorePerseguicao;
    private BTNode arvorePerseguicaoUnida;

    private int indicePatrulha = 0;
    private Collider[] bufferAliados = new Collider[10];

    // Referência crucial ao Stealth do jogador
    private PlayerHiding estadoStealth;

    void Start()
    {
        if (agente == null) agente = GetComponent<NavMeshAgent>();
        if (jogador != null) estadoStealth = jogador.GetComponent<PlayerHiding>();

        ConstruirArvoresDeComportamento();
        ConstruirMaquinaDeEstados();
    }

    void Update()
    {
        AtualizarRadarDeAliados(); 
        fsm?.Tick();
    }

    private void LogEstado(string novoEstado)
    {
        if (estadoAtual != novoEstado)
        {
            estadoAtual = novoEstado;
            Debug.Log($"<color=cyan><b>[FSM]</b></color> {gameObject.name} mudou para o Estado: <b>{estadoAtual}</b>");
        }
    }

    private void LogTatica(string novaTatica)
    {
        if (taticaAtual != novaTatica)
        {
            taticaAtual = novaTatica;
            Debug.Log($"<color=orange><b>[BT]</b></color> {gameObject.name} mudou para a Tática: <b>{taticaAtual}</b>");
        }
    }

    private void ConstruirArvoresDeComportamento()
    {
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

        arvorePerseguicao = new BTAction("CorrerAtrasDoJogador", () =>
        {
            agente.speed = 5f;
            agente.stoppingDistance = 1.5f;
            agente.SetDestination(jogador.position);
            return BTStatus.Running;
        });

        arvorePerseguicaoUnida = new BTSelector("TaticasDeGrupo",
            new BTSequence("TaticaFrenetica",
                new BTCondition("MuitosAliados", () => AliadosProximos >= 6),
                new BTAction("EsmagarJogador", () =>
                {
                    LogTatica("Frenética (Enxame)");
                    agente.speed = 8f;
                    agente.stoppingDistance = 0f;
                    agente.SetDestination(jogador.position);
                    return BTStatus.Running;
                })
            ),
            new BTSequence("TaticaMista",
                new BTCondition("AliadosMedianos", () => AliadosProximos >= 3),
                new BTAction("AtaqueMistoTriplo", () =>
                {
                    int papel = Mathf.Abs(gameObject.GetInstanceID()) % 3;

                    if (papel == 0)
                    {
                        LogTatica("Mista (Agressor - Ataca direto)");
                        agente.speed = 5.5f;
                        agente.stoppingDistance = 1f;
                        agente.SetDestination(jogador.position);
                    }
                    else if (papel == 1)
                    {
                        LogTatica("Mista (Interceptador - Vai pela frente)");
                        agente.speed = 4.5f;
                        agente.stoppingDistance = 2f;
                        Vector3 pontoFrente = jogador.position + (jogador.forward * 4f);
                        agente.SetDestination(pontoFrente);
                    }
                    else
                    {
                        LogTatica("Mista (Bloqueador - Cobre as costas)");
                        agente.speed = 4f;
                        agente.stoppingDistance = 2.5f;
                        Vector3 pontoCostas = jogador.position - (jogador.forward * 4f);
                        agente.SetDestination(pontoCostas);
                    }
                    return BTStatus.Running;
                })
            ),
            new BTAction("TaticaEmboscada", () =>
            {
                float distanciaAtual = Vector3.Distance(transform.position, jogador.position);

                if (distanciaAtual < 3.5f)
                {
                    LogTatica("Emboscada (Defesa Corpo-a-Corpo)");
                    agente.speed = 5f;
                    agente.stoppingDistance = 1.5f;
                    agente.SetDestination(jogador.position);
                }
                else
                {
                    LogTatica("Emboscada (Recuo de Cerco)");
                    agente.speed = 4f;
                    agente.stoppingDistance = 3f;
                    Vector3 direcaoAfastamento = (transform.position - jogador.position).normalized;
                    agente.SetDestination(jogador.position + (direcaoAfastamento * 3f));
                }
                return BTStatus.Running;
            })
        );
    }

    private void ConstruirMaquinaDeEstados()
    {
        var estadoPatrulha = new FSMDelegateState("Estado_Patrulha",
            onEnter: () => { LogEstado("Patrulha"); LogTatica("Nenhuma (Aguardando)"); },
            onUpdate: () => arvorePatrulha.Tick()
        );

        var estadoPerseguicao = new FSMDelegateState("Estado_Perseguicao",
            onEnter: () => { LogEstado("Perseguição (Sozinho)"); LogTatica("Correndo atrás"); },
            onUpdate: () => arvorePerseguicao.Tick()
        );

        var estadoPerseguicaoUnida = new FSMDelegateState("Estado_PerseguicaoUnida",
            onEnter: () => { LogEstado("Perseguição Unida (Grupo)"); },
            onUpdate: () => arvorePerseguicaoUnida.Tick()
        );

        fsm = new FSM();

        fsm.AddTransition(estadoPatrulha, estadoPerseguicaoUnida, () => VeJogador() && AliadosProximos >= quantidadeParaUnir);
        fsm.AddTransition(estadoPatrulha, estadoPerseguicao, () => VeJogador() && AliadosProximos < quantidadeParaUnir);

        fsm.AddTransition(estadoPerseguicao, estadoPatrulha, () => PerdeuJogador());
        fsm.AddTransition(estadoPerseguicao, estadoPerseguicaoUnida, () => AliadosProximos >= quantidadeParaUnir);

        fsm.AddTransition(estadoPerseguicaoUnida, estadoPatrulha, () => PerdeuJogador());
        fsm.AddTransition(estadoPerseguicaoUnida, estadoPerseguicao, () => AliadosProximos < quantidadeParaUnir);

        fsm.Start(estadoPatrulha);
    }

    private bool VeJogador()
    {
        if (estadoStealth != null && estadoStealth.isHidden) return false;
        return (transform.position - jogador.position).sqrMagnitude <= (raioVisao * raioVisao);
    }

    private bool PerdeuJogador()
    {
        if (estadoStealth != null && estadoStealth.isHidden) return true;
        return (transform.position - jogador.position).sqrMagnitude > (raioPerdaDeVisao * raioPerdaDeVisao);
    }

    private void AtualizarRadarDeAliados()
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
        AliadosProximos = contagem;

        EhLider = true; 
        for (int i = 0; i < hits; i++)
        {
            if (bufferAliados[i].CompareTag("Inimigo") && bufferAliados[i].gameObject != this.gameObject)
            {
                if (bufferAliados[i].TryGetComponent<IAInimigoHibrida>(out var outro))
                {
                    if (outro.AliadosProximos > this.AliadosProximos || 
                       (outro.AliadosProximos == this.AliadosProximos && outro.gameObject.GetInstanceID() > this.gameObject.GetInstanceID()))
                    {
                        EhLider = false;
                        break; 
                    }
                }
            }
        }
    }

    void OnDrawGizmos()
    {
        if (EhLider)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2.5f, 0.5f);
        }
    }
}