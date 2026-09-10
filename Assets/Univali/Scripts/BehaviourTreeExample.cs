using System.Collections.Generic;
using System.Text;
using UnityEngine;
using B4TGames.AI.BehaviourTree;

/// <summary>
/// Exemplo didático de Behaviour Tree com três comportamentos: Attack, Chase e Patrol.
///
/// A árvore montada em Start() é esta:
///
///   Selector "Raiz"
///   ├── Sequence "Attack"  → Condition "Alvo ao alcance de ataque?" + Action "Attack"
///   ├── Sequence "Chase"   → Condition "Alvo avistado?"             + Action "Chase"
///   └── Action   "Patrol"                                     (fallback, nunca falha)
///
/// A cada tick o Selector tenta Attack; se a condição falhar tenta Chase; se
/// falhar também, cai no Patrol. Toda vez que o ramo vencedor muda, a troca é
/// escrita no console — inclusive o caminho percorrido na árvore, se
/// <see cref="logNodePath"/> estiver ligado.
///
/// Uso: coloque em qualquer GameObject, arraste um Transform em <see cref="target"/>
/// (por exemplo o Pac-Man) e dê Play.
/// </summary>
public class BehaviourTreeExample : MonoBehaviour
{
    /// <summary>Os três comportamentos deste exemplo, em ordem de prioridade.</summary>
    public enum Behaviour
    {
        None,
        Patrol,
        Chase,
        Attack
    }

    [Header("Alvo")]
    [Tooltip("Quem é perseguido/atacado. Vazio: procura o PacmanController da cena.")]
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

    [Header("Ataque")]
    [Tooltip("Segundos entre um golpe e outro")]
    public float attackInterval = 1f;

    [Header("Console")]
    [Tooltip("Escreve no console cada troca de comportamento")]
    public bool logTransitions = true;

    [Tooltip("Junto com a troca, mostra o caminho percorrido na árvore")]
    public bool logNodePath = true;

    [Header("Comportamento Atual (somente leitura)")]
    [SerializeField] private Behaviour currentBehaviour = Behaviour.None;

    private BTNode root;

    // Estado interno das folhas (a "blackboard" deste exemplo)
    private Behaviour tickBehaviour;   // quem assumiu o tick corrente
    private BTStatus rootStatus;         // o que a raiz devolveu no último tick
    private Vector3 homePosition;
    private int waypointIndex;
    private int patrolDirection = 1;     // usado só no vaivém sem waypoints
    private float nextAttackTime;

    // Reaproveitados a cada tick para não alocar lixo todo frame
    private readonly List<string> trace = new();
    private readonly StringBuilder pathBuilder = new();

    public Behaviour CurrentBehaviour => currentBehaviour;

    private void Start()
    {
        homePosition = transform.position;

        // A árvore é montada uma vez só. Depois disso o Update apenas dá ticks nela.
        root = new BTSelector("Raiz",
            children: new BTNode[]
            {
                new BTSequence("Attack",
                    children: new BTNode[]
                    {
                        new BTCondition("Alvo ao alcance de ataque?", IsTargetInAttackRange),
                        new BTAction("Attack", DoAttack)
                    }),
                new BTSequence("Chase",
                    children: new BTNode[]
                    {
                        new BTCondition("Alvo avistado?", IsTargetVisible),
                        new BTAction("Chase", DoChase)
                    }),
                new BTAction("Patrol", DoPatrol)
            });

        Log($"Árvore montada. attackRange={attackRange}, chaseRange={chaseRange}.");
    }

    private void Update()
    {
        if (root == null) return;

        tickBehaviour = Behaviour.None;
        trace.Clear();

        rootStatus = root.Tick(logNodePath ? trace : null);

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

    /// <summary>Fica parado batendo no alvo em intervalos fixos. Nunca "termina": devolve Running.</summary>
    private BTStatus DoAttack()
    {
        tickBehaviour = Behaviour.Attack;

        if (Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackInterval;
            Log($"Attack: golpe em {target.name} (dist {DistanceToTarget():0.00}).");
        }

        return BTStatus.Running;
    }

    /// <summary>Anda em direção ao alvo. Success quando chega ao alcance de ataque.</summary>
    private BTStatus DoChase()
    {
        tickBehaviour = Behaviour.Chase;

        MoveTowards(target.position, chaseSpeed);

        return DistanceToTarget() <= attackRange ? BTStatus.Success : BTStatus.Running;
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
            Log($"Patrol: próximo ponto → {waypoints[waypointIndex].name}.");
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
    /// O ponto central do exemplo: só escreve quando o ramo vencedor muda, para o
    /// console mostrar transições e não um log por frame.
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
        Debug.Log($"[BT:{name}] {message}", this);
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
