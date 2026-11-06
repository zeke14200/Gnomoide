using UnityEngine;
using UnityEngine.AI;
using B4TGames.AI.StateMachine;
using B4TGames.AI.BehaviourTree;

public class IAInimigoHibrida : MonoBehaviour
{
    [Header("Referências")]
    public Transform jogador; 
    public NavMeshAgent agente;

    [Header("Configurações de Patrulha (NavMesh)")]
    public float raioDeBusca = 15f;
    public float tempoMinEspera = 1f;
    public float tempoMaxEspera = 3.5f;

    [Header("Configurações de Visão e Grupo")]
    public float raioVisao = 10f;
    public float raioPerdaDeVisao = 15f;
    public float raioAliados = 5f;
    public int quantidadeParaUnir = 2;

    [Header("Status do Grupo (Automático)")]
    public int AliadosProximos { get; private set; }
    public bool EhLider { get; private set; }
    
    public string estadoAtual = "";
    public string taticaAtual = "";

    [Header("Debug Visual")]
    public bool mostrarRaiosDeVisao = true;

    private FSM fsm;
    private BTNode arvorePatrulha;
    private BTNode arvorePerseguicao;
    private BTNode arvorePerseguicaoUnida;

    private Collider[] bufferAliados = new Collider[10];
    private PlayerHiding estadoStealth;

    private bool esperando = false;
    private float cronometroEspera = 0f;

    void Start()
    {
        if (agente == null) agente = GetComponent<NavMeshAgent>();

        agente.enabled = true;
        agente.isStopped = false;
        agente.Warp(transform.position);

        if (jogador == null)
        {
            GameObject objJogador = GameObject.FindGameObjectWithTag("Player");
            if (objJogador != null)
            {
                jogador = objJogador.transform;
            }
        }

        if (jogador != null)
        {
            estadoStealth = jogador.GetComponent<PlayerHiding>();
        }

        ConstruirArvoresDeComportamento();
        ConstruirMaquinaDeEstados();
        BuscarNovoPontoAleatorio();
    }

    void Update()
    {
        if (estadoStealth == null && jogador != null)
        {
            estadoStealth = jogador.GetComponent<PlayerHiding>();
        }

        AtualizarRadarDeAliados(); 
        fsm?.Tick();
    }

    private void LogEstado(string novoEstado)
    {
        if (estadoAtual != novoEstado)
        {
            estadoAtual = novoEstado;
            Debug.Log($"<color=cyan><b>[FSM]</b></color> {gameObject.name}: <b>{estadoAtual}</b>");
        }
    }

    private void LogTatica(string novaTatica)
    {
        if (taticaAtual != novaTatica)
        {
            taticaAtual = novaTatica;
        }
    }

    private void ConstruirArvoresDeComportamento()
    {
        arvorePatrulha = new BTAction("VasculharArea", () =>
        {
            agente.speed = 2f;
            agente.stoppingDistance = 0f;
            agente.isStopped = false;

            if (esperando)
            {
                cronometroEspera -= Time.deltaTime;
                if (cronometroEspera <= 0f)
                {
                    esperando = false;
                    BuscarNovoPontoAleatorio();
                }
                return BTStatus.Running;
            }

            if (!agente.pathPending)
            {
                bool chegou = agente.remainingDistance <= 0.5f;
                bool caminhoInvalido = agente.pathStatus == NavMeshPathStatus.PathInvalid || agente.pathStatus == NavMeshPathStatus.PathPartial;
                bool travadoEmParede = agente.velocity.sqrMagnitude < 0.1f && agente.remainingDistance > 0.5f;

                if (chegou || caminhoInvalido || travadoEmParede)
                {
                    esperando = true;
                    cronometroEspera = Random.Range(tempoMinEspera, tempoMaxEspera);
                }
            }
            
            return BTStatus.Running;
        });

        arvorePerseguicao = new BTAction("CorrerAtrasDoJogador", () =>
        {
            if (jogador == null) return BTStatus.Failure;

            agente.speed = 5f;
            agente.stoppingDistance = 1.5f;
            agente.isStopped = false;

            if (Vector3.Distance(agente.destination, jogador.position) > 1f)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(jogador.position, out hit, 3f, NavMesh.AllAreas))
                {
                    agente.SetDestination(hit.position);
                }
                else
                {
                    agente.SetDestination(jogador.position);
                }
            }
            
            return BTStatus.Running;
        });
        
        arvorePerseguicaoUnida = new BTSelector("TaticasDeGrupo",
            new BTSequence("TaticaFrenetica",
                new BTCondition("MuitosAliados", () => AliadosProximos >= 4),
                new BTAction("EsmagarJogador", () =>
                {
                    if (jogador == null) return BTStatus.Failure;
                    LogTatica("Frenética (Enxame Total)");
                    agente.speed = 7f;
                    agente.stoppingDistance = 0f;
                    agente.isStopped = false;
                    agente.SetDestination(jogador.position);
                    return BTStatus.Running;
                })
            ),
            new BTSequence("TaticaMista",
                new BTCondition("AliadosMedianos", () => AliadosProximos >= 2),
                new BTAction("AtaqueMistoTriplo", () =>
                {
                    if (jogador == null) return BTStatus.Failure;
                    int papel = Mathf.Abs(gameObject.GetInstanceID()) % 3;

                    agente.isStopped = false;
                    if (papel == 0)
                    {
                        LogTatica("Mista (Agressor Direto)");
                        agente.speed = 5.5f;
                        agente.stoppingDistance = 1f;
                        agente.SetDestination(jogador.position);
                    }
                    else if (papel == 1)
                    {
                        LogTatica("Mista (Interceptador de Flanco)");
                        agente.speed = 5f;
                        agente.stoppingDistance = 2f;
                        Vector3 pontoFrente = jogador.position + (jogador.forward * 3f);
                        agente.SetDestination(pontoFrente);
                    }
                    else
                    {
                        LogTatica("Mista (Bloqueador de Retaguarda)");
                        agente.speed = 4.5f;
                        agente.stoppingDistance = 2f;
                        Vector3 pontoCostas = jogador.position - (jogador.forward * 3f);
                        agente.SetDestination(pontoCostas);
                    }
                    return BTStatus.Running;
                })
            ),
            new BTAction("TaticaPadraoGrupo", () =>
            {
                if (jogador == null) return BTStatus.Failure;
                LogTatica("Grupo (Apoio Mútuo)");
                agente.speed = 5f;
                agente.stoppingDistance = 1.5f;
                agente.isStopped = false;
                agente.SetDestination(jogador.position);
                return BTStatus.Running;
            })
        );
    }

    private void BuscarNovoPontoAleatorio()
    {
        Vector3 direcaoAleatoria = Random.insideUnitSphere * raioDeBusca;
        direcaoAleatoria += transform.position;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(direcaoAleatoria, out hit, raioDeBusca, NavMesh.AllAreas))
        {
            agente.SetDestination(hit.position);
        }
    }

    private void ConstruirMaquinaDeEstados()
    {
        var estadoPatrulha = new FSMDelegateState("Patrulha",
            onEnter: () => { LogEstado("Vasculhando"); esperando = false; },
            onUpdate: () => arvorePatrulha.Tick()
        );

        var estadoPerseguicao = new FSMDelegateState("Perseguicao",
            onEnter: () => { LogEstado("Caçando (Sozinho)"); esperando = false; },
            onUpdate: () => arvorePerseguicao.Tick()
        );

        var estadoPerseguicaoUnida = new FSMDelegateState("PerseguicaoGrupo",
            onEnter: () => { LogEstado("Caçando (Grupo com Táticas)"); esperando = false; },
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
        if (jogador == null) return false;

        if (estadoStealth != null && estadoStealth.isHidden) return false;

        return (transform.position - jogador.position).sqrMagnitude <= (raioVisao * raioVisao);
    }

    private bool PerdeuJogador()
    {
        if (jogador == null) return true;

        if (estadoStealth != null && estadoStealth.isHidden) return true;

        return (transform.position - jogador.position).sqrMagnitude > (raioPerdaDeVisao * raioPerdaDeVisao);
    }

    private void AtualizarRadarDeAliados()
    {
        int hits = Physics.OverlapSphereNonAlloc(transform.position, raioAliados, bufferAliados);
        
        int contagem = 0;
        for (int i = 0; i < hits; i++)
        {
            if (bufferAliados[i] != null && bufferAliados[i].CompareTag("Inimigo") && bufferAliados[i].gameObject != this.gameObject)
            {
                contagem++;
            }
        }
        AliadosProximos = contagem;

        EhLider = true; 
        for (int i = 0; i < hits; i++)
        {
            if (bufferAliados[i] != null && bufferAliados[i].CompareTag("Inimigo") && bufferAliados[i].gameObject != this.gameObject)
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
        if (mostrarRaiosDeVisao)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, raioVisao);
            
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, raioPerdaDeVisao);
        }

        if (EhLider)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2.5f, 0.5f);
        }
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (estadoAtual == "PerseguicaoGrupo" && taticaAtual == "Frenética (Enxame Total)")
        {
            if (collision.gameObject.TryGetComponent<FurniturePhysics>(out var movel))
            {
                Vector3 dirImpacto = (collision.transform.position - transform.position).normalized;
                
                movel.IA_ForceReleasePhysics(dirImpacto, 20f, collision.contacts[0].point);
            }
        }
    }
}