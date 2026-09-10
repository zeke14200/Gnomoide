using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A MESMA árvore do <see cref="BehaviourTreeExample"/>, só que sem orientação a objetos:
/// nada de Node, Selector ou Sequence. Aqui a árvore é só um enum de status e um
/// punhado de métodos que retornam esse status.
///
///   TickBehaviorTree()            ← faz o papel do Selector (prioridade)
///   ├── ExecuteAttackSequence()   ← faz o papel de uma Sequence
///   ├── ExecuteChaseSequence()    ← idem
///   └── ExecutePatrolSequence()   ← fallback, nunca falha
///
/// O "Selector" é o encadeamento de ifs com return: o primeiro ramo que devolve
/// SUCCESS ou RUNNING interrompe o tick. A "Sequence" é o encadeamento de
/// guard clauses: qualquer condição falsa devolve FAILURE e passa a vez para o
/// próximo ramo.
///
/// Vantagem: dá para ler a IA inteira de cima a baixo.
/// Desvantagem: a árvore está codificada nos ifs — para reordenar prioridades
/// você edita código, enquanto na versão OO você reordena os filhos do Selector.
///
/// Uso: coloque em qualquer GameObject, arraste o Pac-Man em <see cref="player"/>
/// (ou deixe vazio para achar sozinho) e dê Play.
/// </summary>
public class SimpleBehaviourTree : MonoBehaviour
{
    /// <summary>O vocabulário da árvore. É o único "tipo" que este exemplo cria.</summary>
    public enum Status
    {
        FAILURE, // não deu certo: passe a vez para o próximo ramo
        SUCCESS, // terminou: o ramo cumpriu o objetivo dele
        RUNNING  // ainda acontecendo: reavalie no próximo frame
    }

    /// <summary>Qual ramo assumiu o frame. Serve só para o log e para os gizmos.</summary>
    public enum Branch
    {
        None,
        Patrol,
        Chase,
        Attack
    }

    [Header("Alvo")]
    [Tooltip("Quem é perseguido/atacado. Vazio: procura o PacmanController da cena.")]
    public Transform player;

    [Header("Distâncias (unidades do mundo)")]
    [Tooltip("Raio de visão: fora dele CanSeePlayer() é falso e a árvore cai no Patrol")]
    public float sightRange = 6f;

    [Tooltip("Raio de ataque: dentro dele o ramo Attack assume")]
    public float attackRange = 1.5f;

    [Header("Movimento")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 3.5f;

    [Tooltip("Pontos da patrulha. Vazio: vai e volta em torno da posição inicial.")]
    public Transform[] patrolPath;

    [Tooltip("Amplitude do vaivém quando não há pontos de patrulha")]
    public float patrolRadius = 3f;

    [Tooltip("Distância para considerar que chegou no ponto de patrulha")]
    public float patrolTolerance = 0.15f;

    [Header("Combate")]
    [Tooltip("Vida do alvo. Zerou, PlayerIsDead() vira true e o ramo Attack dá SUCCESS.")]
    public float playerHealth = 100f;

    public float maxPlayerHealth = 100f;
    public float attackDamage = 25f;

    [Tooltip("Segundos entre um golpe e outro")]
    public float attackInterval = 1f;

    [Tooltip("Segundos até o alvo voltar à vida (para a demonstração poder recomeçar)")]
    public float respawnDelay = 3f;

    [Header("Console")]
    [Tooltip("Escreve no console cada troca de ramo")]
    public bool logTransitions = true;

    [Tooltip("Junto com a troca, mostra por que cada ramo falhou ou venceu")]
    public bool logBranchTrace = true;

    [Header("Ramo Atual (somente leitura)")]
    [SerializeField] private Branch currentBranch = Branch.None;
    [SerializeField] private Status currentStatus = Status.FAILURE;

    // Estado da patrulha
    private Vector3 homePosition;
    private int patrolIndex;
    private int patrolDirection = 1; // usado só no vaivém sem pontos

    // Estado do combate
    private float nextAttackTime;
    private float respawnTime;
    private bool victoryTauntPlayed;

    public Branch CurrentBranch => currentBranch;

    private void Start()
    {
        homePosition = transform.position;
        maxPlayerHealth = Mathf.Max(maxPlayerHealth, playerHealth);

        Log($"Pronto. sightRange={sightRange}, attackRange={attackRange}, vida do alvo={playerHealth}.");
    }

    private void Update()
    {
        BeginTick();          // limpa o registro deste frame
        TickBehaviorTree();   // a árvore em si
        EndTick();            // console: só escreve quando o ramo muda

        TickRespawn();        // devolve o alvo à vida, para a demo poder recomeçar
    }

    // ------------------------------------------------------------------
    // A árvore
    // ------------------------------------------------------------------

    /// <summary>
    /// O Selector. A ordem dos ifs É a ordem de prioridade da árvore.
    /// </summary>
    private void TickBehaviorTree()
    {
        // Criar uma condição para Pegar uma Chave
        // Abrir uma porta, e aí sim começar a seguir o Player
        
        // Prioridade 1: ramo de ataque
        Status attackStatus = ExecuteAttackSequence();
        if (attackStatus is Status.SUCCESS or Status.RUNNING) return; // para aqui!

        // Prioridade 2: ramo de perseguição
        Status chaseStatus = ExecuteChaseSequence();
        if (chaseStatus is Status.SUCCESS or Status.RUNNING) return; // para aqui!

        // Prioridade 3: patrulha, o fallback que nunca falha
        ExecutePatrolSequence();
    }

    /// <summary>Sequence: ver o alvo E estar ao alcance E então bater nele.</summary>
    private Status ExecuteAttackSequence()
    {
        if (!IsPlayerInAttackRange())
        {
            return Record(Branch.Attack, Status.FAILURE, "longe demais, deixa o Chase tentar");
        }

        if (PlayerIsDead())
        {
            PlayVictoryTaunt();
            return Record(Branch.Attack, Status.SUCCESS, "alvo derrotado");
        }

        PlayAttackAnimation();
        return Record(Branch.Attack, Status.RUNNING, "atacando");
    }

    /// <summary>Sequence: ver o alvo E caminhar até ele.</summary>
    private Status ExecuteChaseSequence()
    {
        if (!CanSeePlayer())
        {
            return Record(Branch.Chase, Status.FAILURE, "não vejo o alvo, deixa o Patrol tentar");
        }

        if (IsPlayerInAttackRange())
        {
            // Cheguei. Devolver SUCCESS encerra o Chase; no próximo frame o
            // Selector já vai entregar o tick para o ramo Attack.
            return Record(Branch.Chase, Status.SUCCESS, "cheguei ao alcance de ataque");
        }

        MoveWithPathfindingTo(player.position);
        return Record(Branch.Chase, Status.RUNNING, "perseguindo");
    }

    /// <summary>Fallback: patrulhar para sempre. Nunca devolve FAILURE.</summary>
    private Status ExecutePatrolSequence()
    {
        MoveAlongPatrolPath();
        return Record(Branch.Patrol, Status.RUNNING, "patrulhando");
    }

    // ------------------------------------------------------------------
    // Condições
    // ------------------------------------------------------------------

    private bool CanSeePlayer()
    {
        return player != null && DistanceToPlayer() <= sightRange;
    }

    private bool IsPlayerInAttackRange()
    {
        return player != null && DistanceToPlayer() <= attackRange;
    }

    private bool PlayerIsDead()
    {
        return playerHealth <= 0f;
    }

    private float DistanceToPlayer()
    {
        return Vector3.Distance(transform.position, player.position);
    }

    // ------------------------------------------------------------------
    // Ações
    // ------------------------------------------------------------------

    /// <summary>Bate no alvo no ritmo de <see cref="attackInterval"/>.</summary>
    private void PlayAttackAnimation()
    {
        if (Time.time < nextAttackTime) return;

        nextAttackTime = Time.time + attackInterval;
        playerHealth = Mathf.Max(0f, playerHealth - attackDamage);

        Log($"Attack: golpe em {player.name}! vida do alvo = {playerHealth:0}.");

        if (PlayerIsDead())
        {
            respawnTime = Time.time + respawnDelay;
        }
    }

    /// <summary>Comemora uma vez só — sem isto o SUCCESS logaria a cada frame.</summary>
    private void PlayVictoryTaunt()
    {
        if (victoryTauntPlayed) return;

        victoryTauntPlayed = true;
        Log($"Attack: SUCCESS — {player.name} caiu. Provocação!");
    }

    /// <summary>
    /// Aqui entraria o NavMesh/BFS do projeto. Para o exemplo, linha reta basta:
    /// o que importa é a árvore decidir, não o algoritmo de caminho.
    /// </summary>
    private void MoveWithPathfindingTo(Vector3 destination)
    {
        transform.position = Vector3.MoveTowards(
            transform.position, destination, chaseSpeed * Time.deltaTime);
    }

    private void MoveAlongPatrolPath()
    {
        var destination = CurrentPatrolPoint();

        transform.position = Vector3.MoveTowards(
            transform.position, destination, patrolSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, destination) > patrolTolerance) return;

        if (HasPatrolPath())
        {
            patrolIndex = (patrolIndex + 1) % patrolPath.Length;
            Log($"Patrol: próximo ponto → {patrolPath[patrolIndex].name}.");
        }
        else
        {
            patrolDirection = -patrolDirection; // vaivém em torno da posição inicial
        }
    }

    private Vector3 CurrentPatrolPoint()
    {
        if (HasPatrolPath())
        {
            return patrolPath[patrolIndex].position;
        }

        return homePosition + Vector3.right * (patrolRadius * patrolDirection);
    }

    private bool HasPatrolPath()
    {
        if (patrolPath == null || patrolPath.Length == 0) return false;

        // O array pode encolher no Inspector durante o Play
        patrolIndex = Mathf.Clamp(patrolIndex, 0, patrolPath.Length - 1);

        return patrolPath[patrolIndex] != null;
    }

    /// <summary>
    /// Enquanto o alvo estiver morto e ao alcance, o ramo Attack continua vencendo
    /// com SUCCESS e o agente fica parado — é exatamente o que o código acima manda
    /// fazer. O respawn é o que devolve a árvore para Chase/Patrol.
    /// </summary>
    private void TickRespawn()
    {
        if (!PlayerIsDead() || Time.time < respawnTime) return;

        playerHealth = maxPlayerHealth;
        victoryTauntPlayed = false;
        Log($"{player.name} renasceu com {playerHealth:0} de vida. A árvore volta ao normal.");
    }

    // ------------------------------------------------------------------
    // Feedback no console
    // ------------------------------------------------------------------

    // Registro do frame: quem venceu o tick e com que status
    private Branch tickBranch;
    private Status tickStatus;
    private readonly List<string> trace = new();

    private void BeginTick()
    {
        tickBranch = Branch.None;
        tickStatus = Status.FAILURE;
        trace.Clear();
    }

    /// <summary>
    /// Anota o resultado de um ramo e devolve o mesmo status, para dar para
    /// escrever "return Record(...)" sem quebrar a leitura da sequence.
    /// </summary>
    private Status Record(Branch branch, Status status, string reason)
    {
        if (logBranchTrace)
        {
            trace.Add($"{branch}: {status} ({reason})");
        }

        // FAILURE não assume o frame: quem assume é quem devolve SUCCESS/RUNNING
        if (status != Status.FAILURE)
        {
            tickBranch = branch;
            tickStatus = status;
        }

        return status;
    }

    /// <summary>Só escreve quando o ramo ou o status muda — nada de um log por frame.</summary>
    private void EndTick()
    {
        if (tickBranch == currentBranch && tickStatus == currentStatus) return;

        var previousBranch = currentBranch;
        var previousStatus = currentStatus;

        currentBranch = tickBranch;
        currentStatus = tickStatus;

        if (!logTransitions) return;

        var distance = player != null ? $"{DistanceToPlayer():0.00}" : "sem alvo";
        Log($"TRANSIÇÃO {previousBranch}/{previousStatus} -> {tickBranch}/{tickStatus}  (dist {distance})");

        if (logBranchTrace && trace.Count > 0)
        {
            Log($"avaliação: {string.Join("  |  ", trace)}");
        }
    }

    private void Log(string message)
    {
        Debug.Log($"[SimpleBT:{name}] {message}", this);
    }

    // ------------------------------------------------------------------
    // Gizmos: os dois raios que decidem a árvore inteira
    // ------------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        DrawPatrolPathGizmo();

        if (!Application.isPlaying) return;

        // Linha até quem está sendo perseguido/atacado
        if (player != null && currentBranch != Branch.Patrol && currentBranch != Branch.None)
        {
            Gizmos.color = currentBranch == Branch.Attack ? Color.red : Color.yellow;
            Gizmos.DrawLine(transform.position, player.position);
        }

        if (currentBranch == Branch.Patrol)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, CurrentPatrolPoint());
        }

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.6f,
            $"{currentBranch} / {currentStatus}");
#endif
    }

    private void DrawPatrolPathGizmo()
    {
        Gizmos.color = Color.cyan;

        if (patrolPath == null || patrolPath.Length == 0)
        {
            // Sem pontos: desenha as duas pontas do vaivém
            var origin = Application.isPlaying ? homePosition : transform.position;
            Gizmos.DrawLine(origin + Vector3.left * patrolRadius,
                            origin + Vector3.right * patrolRadius);
            return;
        }

        for (int i = 0; i < patrolPath.Length; i++)
        {
            var from = patrolPath[i];
            var to = patrolPath[(i + 1) % patrolPath.Length];
            if (from == null || to == null) continue;

            Gizmos.DrawWireSphere(from.position, patrolTolerance);
            Gizmos.DrawLine(from.position, to.position);
        }
    }
}
