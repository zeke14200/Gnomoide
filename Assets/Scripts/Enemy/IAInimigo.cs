using UnityEngine;
using UnityEngine.AI;

public class IAInimigo : MonoBehaviour
{
    [Header("Máquina de Estados")]
    private EstadoInimigoBase estadoAtual; 

    [Header("Referências")]
    public Transform jogador;
    public NavMeshAgent agente;
    public Transform[] pontosPatrulha;

    [Header("Configurações de Visão")]
    public float raioVisao = 5f;
    public float raioPerdaDeVisao = 10f; 
    
    [Header("Configurações de Grupo")]
    public float raioAliados = 5f;
    public int quantidadeParaUnir = 2;
    private Collider[] bufferAliados = new Collider[10]; 

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        MudarEstado(new EstadoPatrulha(this)); 
    }

    void Update()
    {
        if (estadoAtual != null)
        {
            estadoAtual.Atualizar();
        }
    }

    // Função central que gerencia a transição 
    public void MudarEstado(EstadoInimigoBase novoEstado)
    {
        if (estadoAtual != null)
        {
            estadoAtual.Sair();
        }

        estadoAtual = novoEstado;
        estadoAtual.Entrar();
        
        Debug.Log("Inimigo mudou para o estado: " + estadoAtual.GetType().Name);
    }

    //metodos de interrupção
    public void ReceberStun()
    {
        MudarEstado(new EstadoAtordoado(this));
    }

    public void Morrer()
    {
        MudarEstado(new EstadoMorto(this));
    }

    public int ContarAliadosProximos()
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

//--- ESTADO PATRULHA ---
public class EstadoPatrulha : EstadoInimigoBase
{
    private int indiceAtual = 0;

    public EstadoPatrulha(IAInimigo ia) : base(ia) { }

    public override void Entrar()
    {
        ia.agente.speed = 2f;
        ia.agente.stoppingDistance = 0f;
    }

    public override void Atualizar()
    {
        float distanciaAoQuadrado = (ia.transform.position - ia.jogador.position).sqrMagnitude;
        
        if (distanciaAoQuadrado <= (ia.raioVisao * ia.raioVisao))
        {
            if (ia.ContarAliadosProximos() >= ia.quantidadeParaUnir)
                ia.MudarEstado(new EstadoPerseguicaoUnida(ia));
            else
                ia.MudarEstado(new EstadoPerseguicao(ia));
            return;
        }

        if (ia.pontosPatrulha.Length == 0) return;

        if (ia.agente.remainingDistance < 0.5f && !ia.agente.pathPending)
        {
            indiceAtual = (indiceAtual + 1) % ia.pontosPatrulha.Length;
        }
        ia.agente.SetDestination(ia.pontosPatrulha[indiceAtual].position);
    }

    public override void Sair() { }
}

// --- ESTADO: PERSEGUIÇÃO ---
public class EstadoPerseguicao : EstadoInimigoBase
{
    public EstadoPerseguicao(IAInimigo ia) : base(ia) { }

    public override void Entrar()
    {
        ia.agente.speed = 5f;
        ia.agente.stoppingDistance = 1.5f;
    }

    public override void Atualizar()
    {
        float distanciaAoQuadrado = (ia.transform.position - ia.jogador.position).sqrMagnitude;

        // Transição: Perdeu o jogador de vista 
        if (distanciaAoQuadrado > (ia.raioPerdaDeVisao * ia.raioPerdaDeVisao))
        {
            ia.MudarEstado(new EstadoPatrulha(ia));
            return;
        }

        // Transição: Ganhou reforços enquanto corria
        if (ia.ContarAliadosProximos() >= ia.quantidadeParaUnir)
        {
            ia.MudarEstado(new EstadoPerseguicaoUnida(ia));
            return;
        }

        ia.agente.SetDestination(ia.jogador.position);
    }

    public override void Sair() { }
}

// --- classes das taticas  de perseguição---
public abstract class TaticaGrupoBase
{
    protected IAInimigo ia;
    public TaticaGrupoBase(IAInimigo ia) { this.ia = ia; }
    public abstract void Entrar();
    public abstract void Atualizar();
    public abstract void Sair();
}

// --- O ESTADO PRINCIPAL ---
public class EstadoPerseguicaoUnida : EstadoInimigoBase
{
    private TaticaGrupoBase taticaAtual; 

    public EstadoPerseguicaoUnida(IAInimigo ia) : base(ia) { }

    public override void Entrar()
    {
        AvaliarTatica();
    }

    public override void Atualizar()
    {
        float distanciaAoQuadrado = (ia.transform.position - ia.jogador.position).sqrMagnitude;
        if (distanciaAoQuadrado > (ia.raioPerdaDeVisao * ia.raioPerdaDeVisao))
        {
            ia.MudarEstado(new EstadoPatrulha(ia));
            return;
        }

        if (ia.ContarAliadosProximos() < ia.quantidadeParaUnir)
        {
            ia.MudarEstado(new EstadoPerseguicao(ia));
            return;
        }

        AvaliarTatica();

        if (taticaAtual != null)
        {
            taticaAtual.Atualizar();
        }
    }

    private void AvaliarTatica()
    {
        int aliados = ia.ContarAliadosProximos();
        TaticaGrupoBase novaTatica = null;

        // POUCOS INIMIGOS ex: 2 a 3 no total -> Emboscada
        if (aliados <= 2) 
        {
            if (!(taticaAtual is TaticaEmboscada)) novaTatica = new TaticaEmboscada(ia);
        }
        else if (aliados >= 3 && aliados <= 5)
        {
            if (!(taticaAtual is TaticaMista)) novaTatica = new TaticaMista(ia);
        }
        else 
        {
            if (!(taticaAtual is TaticaFrenetica)) novaTatica = new TaticaFrenetica(ia);
        }

        if (novaTatica != null)
        {
            if (taticaAtual != null) taticaAtual.Sair();
            taticaAtual = novaTatica;
            taticaAtual.Entrar();
            
            Debug.Log(ia.gameObject.name + " mudou a tática de grupo para: " + taticaAtual.GetType().Name);
        }
    }

    public override void Sair() 
    {
        if (taticaAtual != null) taticaAtual.Sair();
    }
}

// EMBOSCADA 
public class TaticaEmboscada : TaticaGrupoBase
{
    public TaticaEmboscada(IAInimigo ia) : base(ia) { }

    public override void Entrar()
    {
        ia.agente.speed = 4f;
        ia.agente.stoppingDistance = 2f;
    }

    public override void Atualizar()
    {
       
        Vector3 direcaoAfastamento = (ia.transform.position - ia.jogador.position).normalized;
        Vector3 pontoCerco = ia.jogador.position + (direcaoAfastamento * 3f);
        
        ia.agente.SetDestination(pontoCerco);
    }
    public override void Sair() { }
}

// PERSEGUIÇÃO FRENETICA + EMBOSCADA ---
public class TaticaMista : TaticaGrupoBase
{
    private bool agirComoEmboscada;

    public TaticaMista(IAInimigo ia) : base(ia) { }

    public override void Entrar()
    {
        agirComoEmboscada = (ia.gameObject.GetInstanceID() % 2 == 0);

        ia.agente.speed = agirComoEmboscada ? 4f : 5.5f;
        ia.agente.stoppingDistance = agirComoEmboscada ? 2.5f : 1f;
    }

    public override void Atualizar()
    {
        if (agirComoEmboscada)
        {
            Vector3 direcao = (ia.transform.position - ia.jogador.position).normalized;
            ia.agente.SetDestination(ia.jogador.position + (direcao * 3f));
        }
        else
        {
            ia.agente.SetDestination(ia.jogador.position);
        }
    }
    public override void Sair() { }
}
// MEGA PERSSEGUIÇÃO FRENÉTICA ---
public class TaticaFrenetica : TaticaGrupoBase
{
    public TaticaFrenetica(IAInimigo ia) : base(ia) { }

    public override void Entrar()
    {
        ia.agente.speed = 8f; 
        ia.agente.stoppingDistance = 0f; 
    }

    public override void Atualizar()
    {
        // Enxame: Todo mundo corre em linha reta para esmagar o jogador ao mesmo tempo
        ia.agente.SetDestination(ia.jogador.position);
    }
    public override void Sair() { }
}

public class EstadoAtordoado : EstadoInimigoBase
{
    private float tempoAtordoado = 2f;
    private float cronometro = 0f;

    public EstadoAtordoado(IAInimigo ia) : base(ia) { }

    public override void Entrar()
    {
        ia.agente.isStopped = true; 
    }

    public override void Atualizar()
    {
        cronometro += Time.deltaTime;
        if (cronometro >= tempoAtordoado)
        {
            ia.MudarEstado(new EstadoPatrulha(ia));
        }
    }

    public override void Sair()
    {
        ia.agente.isStopped = false; 
    }
}

public class EstadoMorto : EstadoInimigoBase
{
    public EstadoMorto(IAInimigo ia) : base(ia) { }

    public override void Entrar()
    {
        ia.agente.isStopped = true;
        ia.agente.enabled = false; 
    }

    public override void Atualizar() { } 
    public override void Sair() { }
}

