using System.Collections.Generic;
using System.Text;
using UnityEngine;
using B4TGames.AI.BehaviourTree;

/// <summary>
/// Segundo exemplo didático: a mesma ideia do <see cref="BehaviourTreeExample"/>,
/// agora com DECORATORS. Decorator é o nó que tem exatamente UM filho e modifica
/// o que ele devolve (Composite tem vários filhos, Leaf não tem nenhum).
///
/// A árvore montada em Start() é esta:
///
///   Selector "Raiz"
///   ├── Sequence "Atacar"
///   │   ├── Condition "Alvo ao alcance de ataque?"
///   │   └── Cooldown  "Cooldown do golpe"      → Action "Morder"
///   ├── Sequence "Provocar"
///   │   ├── Condition "Alvo avistado?"
///   │   ├── Inverter  "Alvo LONGE do alcance"  → Condition "Alvo ao alcance de ataque?"
///   │   └── Cooldown  "Cooldown da provocação" → Action "Provocar"
///   ├── Sequence "Perseguir"
///   │   ├── Condition "Alvo avistado?"
///   │   └── Action    "Perseguir"
///   └── Action "Patrulhar"                                    (fallback, nunca falha)
///
/// Os dois decorators fazem coisas diferentes:
///
/// 1) Cooldown segura o filho por alguns segundos DEPOIS que ele termina. Enquanto
///    está contando, devolve Failure sem tocar no filho — e é aí que está a graça:
///    o Selector então entrega o tick para um ramo de prioridade menor. Resultado:
///    entre uma mordida e outra o agente continua se reposicionando (Perseguir),
///    em vez de ficar parado esperando o relógio.
///
/// 2) Inverter troca Success por Failure. Aqui ele reaproveita a MESMA condição do
///    ataque para dizer o contrário dela: "eu vejo o alvo, mas ele NÃO está ao
///    alcance" — ou seja, provoco enquanto persigo, nunca enquanto mordo.
///
/// Repare que "Morder" e "Provocar" devolvem Success, não Running: uma ação só
/// pode ser controlada por um Cooldown se ela avisar quando terminou uma repetição.
///
/// 3) BTTrace (opcional, ligue <see cref="traceTauntBranch"/> no Inspector) é o
///    terceiro decorator, e o mais estranho deles: ele não muda comportamento
///    nenhum. Envolve o ramo "Provocar" e apenas conta no console o que aquele
///    ramo devolveu, repassando o status intacto. Serve para depurar UM ramo sem
///    ler o caminho da árvore inteira.
///
/// Uso: coloque em qualquer GameObject, arraste um Transform em <see cref="target"/>
/// (por exemplo o Pac-Man) e dê Play.
/// </summary>
public class BehaviourTreeDecoratorsExample : MonoBehaviour
{
    /// <summary>Os ramos deste exemplo, em ordem de prioridade.</summary>
    public enum Behaviour
    {
        None,
        Patrol,
        Chase,
        Taunt,
        Attack
    }

    [Header("Alvo")]
    [Tooltip("Quem é perseguido/atacado")]
    public Transform target;

    [Header("Distâncias (unidades do mundo)")]
    [Tooltip("Dentro deste raio o agente ataca")]
    public float attackRange = 1.5f;

    [Tooltip("Dentro deste raio o agente persegue; fora dele, patrulha")]
    public float chaseRange = 6f;

    [Header("Movimento")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 3.5f;

    [Tooltip("Pontos da patrulha. Vazio: vai e volta em torno da posição inicial.")]
    public Transform[] waypoints;

    [Tooltip("Amplitude do vaivém quando não há waypoints")]
    public float patrolRadius = 3f;

    [Tooltip("Distância para considerar que chegou no ponto de patrulha")]
    public float waypointTolerance = 0.15f;

    [Header("Decorators")]
    [Tooltip("Segundos entre uma mordida e outra (Cooldown do ramo Atacar)")]
    public float attackInterval = 1f;

    [Tooltip("Segundos entre uma provocação e outra (Cooldown do ramo Provocar)")]
    public float tauntInterval = 2f;

    [Header("Console")]
    [Tooltip("Escreve no console cada troca de ramo")]
    public bool logTransitions = true;

    [Tooltip("Junto com a troca, mostra o caminho percorrido na árvore")]
    public bool logNodePath = true;

    [Tooltip("Envolve o ramo Provocar num BTTrace, para acompanhar só ele de perto")]
    public bool traceTauntBranch = false;

    [Header("Ramo Atual (somente leitura)")]
    [SerializeField] private Behaviour currentBehaviour = Behaviour.None;

    private BTNode root;

    // Estado interno das folhas (a "blackboard" deste exemplo)
    private Behaviour tickBehaviour;   // quem assumiu o tick corrente
    private BTStatus rootStatus;       // o que a raiz devolveu no último tick
    private Vector3 homePosition;
    private int waypointIndex;
    private int patrolDirection = 1;   // usado só no vaivém sem waypoints
    private string lastTraceMessage;   // evita repetir a mesma linha do BTTrace

    // Reaproveitados a cada tick para não alocar lixo todo frame
    private readonly List<string> trace = new();
    private readonly StringBuilder pathBuilder = new();

    private static readonly string[] Taunts =
    {
        "não vai escapar!",
        "eu vejo você!",
        "pode correr, mas eu sou mais rápido!"
    };

    public Behaviour CurrentBehaviour => currentBehaviour;

    private void Start()
    {
        homePosition = transform.position;

        // A árvore é montada uma vez só. Os decorators entram aqui, na montagem:
        // nenhuma ação precisou saber que existe um Cooldown em volta dela.
        // O ramo Provocar é o mais sutil da árvore: na maior parte dos frames ele
        // falha DE PROPÓSITO (o Cooldown está contando) para o Perseguir assumir.
        // Guardá-lo numa variável permite envolvê-lo — ou não — num BTTrace.
        BTNode provocar = new BTSequence("Provocar",
            children: new BTNode[]
            {
                new BTCondition("Alvo avistado?", IsTargetVisible),
                new BTInverter("Alvo LONGE do alcance",
                    new BTCondition("Alvo ao alcance de ataque?", IsTargetInAttackRange)),
                new BTCooldown("Cooldown da provocação", tauntInterval,
                    new BTAction("Provocar", DoTaunt))
            });

        if (traceTauntBranch)
        {
            // O BTTrace não muda nada no comportamento: tire-o daqui e a árvore
            // decide exatamente as mesmas coisas. Ele só relata o que passou.
            provocar = new BTTrace("suspeito", LogTraceOnChange, provocar);
        }

        root = new BTSelector(name: "Raiz",
            children: new[]
            {
                new BTSequence(name: "Atacar",
                    children: new BTNode[]
                    {
                        new BTCondition("Alvo ao alcance de ataque?", IsTargetInAttackRange),
                        new BTCooldown("Cooldown do golpe", attackInterval,
                            new BTAction("Morder", DoBite))
                    }),

                provocar,

                new BTSequence(name: "Perseguir",
                    children: new BTNode[]
                    {
                        new BTCondition("Alvo avistado?", IsTargetVisible),
                        new BTAction("Perseguir", DoChase)
                    }),

                new BTAction("Patrulhar", DoPatrol)
            });

        Log($"Árvore montada. attackRange={attackRange}, chaseRange={chaseRange}, " +
            $"golpe a cada {attackInterval}s, provocação a cada {tauntInterval}s.");
    }

    private void Update()
    {
        if (root == null) return;

        tickBehaviour = Behaviour.None;
        trace.Clear();

        rootStatus = logNodePath ? root.Tick(trace) : root.Tick();

        ReportIfChanged(tickBehaviour);
    }

    // ------------------------------------------------------------------
    // Condições
    // ------------------------------------------------------------------

    private bool IsTargetInAttackRange()
    {
        return target != null && DistanceToTarget() <= attackRange;
    }

    private bool IsTargetVisible()
    {
        return target != null && DistanceToTarget() <= chaseRange;
    }

    private float DistanceToTarget()
    {
        return Vector3.Distance(transform.position, target.position);
    }

    // ------------------------------------------------------------------
    // Ações
    // ------------------------------------------------------------------

    /// <summary>
    /// Um golpe, e só. Devolve Success porque a repetição TERMINOU — é isso que
    /// arma o Cooldown em volta. Se devolvesse Running, o Cooldown nunca contaria.
    /// </summary>
    private BTStatus DoBite()
    {
        tickBehaviour = Behaviour.Attack;
        Log($"Morder: golpe em {target.name} (dist {DistanceToTarget():0.00}).");
        return BTStatus.Success;
    }

    /// <summary>Uma provocação. Também termina na hora, pelo mesmo motivo do golpe.</summary>
    private BTStatus DoTaunt()
    {
        tickBehaviour = Behaviour.Taunt;
        Log($"Provocar: \"{Taunts[Random.Range(0, Taunts.Length)]}\"");
        return BTStatus.Success;
    }

    /// <summary>Anda em direção ao alvo. Nunca termina: devolve Running.</summary>
    private BTStatus DoChase()
    {
        tickBehaviour = Behaviour.Chase;
        MoveTowards(target.position, chaseSpeed);
        return BTStatus.Running;
    }

    /// <summary>Fallback da árvore: circula pelos waypoints (ou vai e volta). Sempre Running.</summary>
    private BTStatus DoPatrol()
    {
        tickBehaviour = Behaviour.Patrol;

        var destination = CurrentPatrolPoint();
        MoveTowards(destination, patrolSpeed);

        if (Vector3.Distance(transform.position, destination) <= waypointTolerance)
        {
            AdvancePatrolPoint();
        }

        return BTStatus.Running;
    }

    private Vector3 CurrentPatrolPoint()
    {
        if (HasWaypoints())
        {
            return waypoints[waypointIndex].position;
        }

        return homePosition + Vector3.right * (patrolRadius * patrolDirection);
    }

    private void AdvancePatrolPoint()
    {
        if (HasWaypoints())
        {
            waypointIndex = (waypointIndex + 1) % waypoints.Length;
            Log($"Patrulhar: próximo ponto → {waypoints[waypointIndex].name}.");
            return;
        }

        patrolDirection = -patrolDirection;
    }

    private bool HasWaypoints()
    {
        return waypoints != null && waypoints.Length > 0 && waypoints[waypointIndex] != null;
    }

    private void MoveTowards(Vector3 destination, float speed)
    {
        transform.position = Vector3.MoveTowards(
            transform.position, destination, speed * Time.deltaTime);
    }

    // ------------------------------------------------------------------
    // Feedback no console
    // ------------------------------------------------------------------

    /// <summary>
    /// Só escreve quando o ramo vencedor muda. Com os decorators ligados, o
    /// console mostra o vaivém entre Chase e Attack/Taunt: é o Cooldown devolvendo
    /// Failure e deixando o ramo de baixo assumir.
    /// </summary>
    private void ReportIfChanged(Behaviour next)
    {
        if (next == currentBehaviour) return;

        var previous = currentBehaviour;
        currentBehaviour = next;

        if (!logTransitions) return;

        var distance = target != null ? $"{DistanceToTarget():0.00}" : "sem alvo";
        Log($"TRANSIÇÃO {previous} -> {next}  (dist {distance})");

        if (logNodePath && trace.Count > 0)
        {
            Log($"caminho: {BuildPath()}");
        }
    }

    private string BuildPath()
    {
        pathBuilder.Clear();

        for (int i = 0; i < trace.Count; i++)
        {
            if (i > 0) pathBuilder.Append("  |  ");
            pathBuilder.Append(trace[i]);
        }

        return pathBuilder.ToString();
    }

    private void Log(string message)
    {
        Debug.Log($"[BT-Decorators:{name}] {message}", this);
    }

    /// <summary>
    /// O destino do BTTrace. Ele é um Action&lt;string&gt; justamente para caber
    /// política aqui: sem o filtro abaixo seriam 60 linhas por segundo no console,
    /// já que o decorator relata TODO tick do ramo que ele envolve.
    /// </summary>
    private void LogTraceOnChange(string message)
    {
        if (message == lastTraceMessage) return;

        lastTraceMessage = message;
        Log(message);
    }

    // ------------------------------------------------------------------
    // Gizmos: os dois raios que decidem toda a árvore
    // ------------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        DrawPatrolPathGizmo();

        if (!Application.isPlaying) return;

        if (currentBehaviour == Behaviour.Patrol)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, CurrentPatrolPoint());
        }

#if UNITY_EDITOR
        // Mostra na Scene View qual ramo venceu o tick e com que status
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.6f,
            $"{currentBehaviour} / {rootStatus}");
#endif
    }

    private void DrawPatrolPathGizmo()
    {
        Gizmos.color = Color.cyan;

        if (waypoints == null || waypoints.Length == 0)
        {
            // Sem pontos: desenha as duas pontas do vaivém
            var origin = Application.isPlaying ? homePosition : transform.position;
            Gizmos.DrawLine(origin + Vector3.left * patrolRadius,
                origin + Vector3.right * patrolRadius);
            return;
        }

        for (int i = 0; i < waypoints.Length; i++)
        {
            var from = waypoints[i];
            var to = waypoints[(i + 1) % waypoints.Length];
            if (from == null || to == null) continue;

            Gizmos.DrawWireSphere(from.position, waypointTolerance);
            Gizmos.DrawLine(from.position, to.position);
        }
    }
}
