using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public abstract class GhostBase : MonoBehaviour
{
    // O Inky usa o Blinky como pivô. Um só para os dois cérebros.
    protected static GhostBase blinkyInstance;

    [SerializeField] protected GhostType ghostType;

    [SerializeField, Tooltip("Segundos preso na casa antes de entrar em jogo")]
    protected float releaseDelay = 0f;

    [SerializeField, Tooltip("Quão perto do destino conta como ter chegado")]
    protected float arrivalDistance = 0.2f;

    [Header("Speeds")]
    [SerializeField] protected float normalSpeed = 4.8f;
    [SerializeField] protected float frightenedSpeed = 3.4f;
    [SerializeField] protected float eatenSpeed = 9.4f;

    [Header("Colors")]
    [SerializeField] protected Color frightenedColor = Color.cyan;
    [SerializeField] protected Color blinkColor = Color.white;
    [SerializeField] protected Color eyesColor = new Color(1f, 1f, 1f, 0.35f);
    protected Color ghostColor;

    [Header("Debug")]
    [Tooltip("Escreve no console o caminho percorrido pelo cérebro. Custa caro: " +
             "a árvore do fantasma gera ~9 linhas por frame, e cada Debug.Log " +
             "captura um stack trace. Ligue só para depurar.")]
    public bool logTransitions = false;

    protected SpriteRenderer spriteRenderer;
    protected PacmanController pacMan;
    protected Vector2 initialPosition;

    protected float timer;
    protected float frightenedTimer;
    protected float frightenedTimerMax = 10f;

    // Destino de fuga do Clyde. Uma vez escolhido, ele vai até o fim.
    private Vector3 clydeRetreat;
    private bool clydeIsRetreating;

    // Último canto sorteado, para não repetir na escolha seguinte
    private int lastScatterIndex = -1;

    protected readonly List<Vector3> scatterOptions = new List<Vector3>(4);

    // Reaproveitado a cada tick para não alocar lixo todo frame
    private readonly List<string> trace = new();

    // Compartilhado: Update roda numa thread só, e assim o log não aloca
    private static readonly System.Text.StringBuilder logBuilder = new();

    protected virtual void Start()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        pacMan = FindAnyObjectByType<PacmanController>();

        // O NavGridAgent já centralizou no Awake dele, então isto é o centro
        // exato da célula — que é onde o agente volta a parar ao voltar para casa.
        initialPosition = transform.position;

        ghostColor = spriteRenderer.color;

        if (ghostType == GhostType.Blinky)
        {
            blinkyInstance = this;
        }

        scatterOptions.Clear();

        BuildBrain();

        GameEvents.OnPowerPelletEaten += OnPowerPelletEaten;
    }

    protected virtual void OnDestroy()
    {
        // Sem isto o evento estático continua segurando uma referência para um
        // fantasma destruído, e a próxima pílula chama um método de um objeto morto.
        GameEvents.OnPowerPelletEaten -= OnPowerPelletEaten;
    }

    protected virtual void Update()
    {
        trace.Clear();

        TickBrain(logTransitions ? trace : null);

        if (!logTransitions || trace.Count == 0) return;

        // Uma chamada só, não uma por linha. Debug.Log captura um stack trace a
        // cada chamada, então nove linhas por frame custam nove capturas — de
        // longe o item mais caro deste laço.
        logBuilder.Clear();
        logBuilder.Append(name);

        for (var i = 0; i < trace.Count; i++)
        {
            logBuilder.Append(i == 0 ? ": " : " | ").Append(trace[i]);
        }

        Debug.Log(logBuilder.ToString(), this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Pacman")) OnTouchedPacMan();
    }

    // ------------------------------------------------------------------
    // O que cada cérebro precisa implementar
    // ------------------------------------------------------------------

    /// <summary>Monta a FSM ou a árvore. Chamado no fim do Start.</summary>
    protected abstract void BuildBrain();

    /// <summary>Um passo do cérebro. O trace vem null quando o log está desligado.</summary>
    protected abstract void TickBrain(List<string> trace);

    /// <summary>Uma pílula foi comida em algum lugar do mapa.</summary>
    protected abstract void OnPowerPelletEaten();

    /// <summary>Encostou no Pac-Man. Quem decide o que isso significa é o cérebro.</summary>
    protected abstract void OnTouchedPacMan();

    // ------------------------------------------------------------------
    // Comportamento compartilhado
    // ------------------------------------------------------------------

    protected void EnterHouse()
    {
        spriteRenderer.color = ghostColor;

        // Zera aqui, e não no Start: um fantasma que volta comido precisa esperar
        // de novo. Com o timer do Start ele já teria passado de releaseDelay e
        // sairia na hora.
        timer = 0f;
    }

    protected void EnterChase()
    {
        spriteRenderer.color = ghostColor;
        clydeIsRetreating = false;
    }

    protected void ExitChase()
    {
        
    }

    protected void EnterFrightened()
    {
        spriteRenderer.color = frightenedColor;
        frightenedTimer = 0f;
    }

    protected void EnterEaten()
    {
        spriteRenderer.color = eyesColor;
    }

    /// <summary>Um passo de perseguição: aponta o agente para onde a personalidade manda.</summary>
    protected void ChaseStep()
    {
        
    }

    /// <summary>
    /// Um passo de fuga. Mesmo compromisso do Clyde: só escolhe outro canto
    /// depois de chegar neste. Entre uma escolha e outra, ninguém mexe no destino.
    /// </summary>
    protected void FleeStep()
    {
        if (HasArrived())
        {
            
        }
    }

    /// <summary>Para onde este fantasma mira durante a perseguição.</summary>
    protected virtual Vector3 GetChaseDestination()
    {
        var pacManPosition = pacMan.transform.position;
        var pacManDirection = (Vector3)pacMan.CurrentDirection;

        switch (ghostType)
        {
            case GhostType.Blinky:
                return pacManPosition;

            case GhostType.Pinky:
                return pacManPosition + pacManDirection * 4;

            case GhostType.Inky:
                var pivot = pacManPosition + pacManDirection * 2;
                return pivot + (pivot - blinkyInstance.transform.position);

            case GhostType.Clyde:
                return ClydeDestination(pacManPosition);

            default:
                return pacManPosition;
        }
    }

    private Vector3 ClydeDestination(Vector3 pacManPosition)
    {
        // Já está fugindo: mantém o rumo, sem reavaliar nada
        if (clydeIsRetreating)
        {
            if (!HasArrived()) return clydeRetreat;

            clydeIsRetreating = false;
        }

        // Longe do Pac-Man: persegue. Aqui o destino acompanha ele, que se mexe.
        if ((transform.position - pacManPosition).sqrMagnitude > 64)
        {
            return pacManPosition;
        }

        // Perto demais: sorteia um canto e se compromete com ele
        clydeIsRetreating = true;
        clydeRetreat = NextScatterCorner();

        return clydeRetreat;
    }

    /// <summary>Sorteia um canto de fuga, nunca repetindo o último.</summary>
    protected Vector3 NextScatterCorner()
    {
        if (scatterOptions.Count == 0) return transform.position;
        if (scatterOptions.Count == 1) return scatterOptions[0];

        // Sorteia num intervalo menor e pula o índice anterior: sai um sorteio
        // uniforme entre os outros cantos, sem laço de tentativa e erro.
        var index = Random.Range(0, scatterOptions.Count - 1);
        if (lastScatterIndex >= 0 && index >= lastScatterIndex) index++;

        lastScatterIndex = index;

        return scatterOptions[index];
    }

    /// <summary>
    /// Chegou ao destino atual?
    ///
    /// Mede pela rota do agente, não pela distância em linha reta até o alvo: um
    /// alvo inalcançável faz o agente parar na célula alcançável mais próxima, e
    /// a linha reta nunca fecharia.
    /// </summary>
    protected bool HasArrived()
    {
        return false;
    }

    protected bool TimeToLeaveHouse()
    {
        timer += Time.deltaTime;

        return timer >= releaseDelay;
    }

    protected bool IsFrightenedTimerOver()
    {
        frightenedTimer += Time.deltaTime;

        return frightenedTimer >= frightenedTimerMax;
    }
}
