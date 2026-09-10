using UnityEngine;
using UnityEngine.AI; // 1. Obrigatório para usar o NavMesh

public enum EstadoInimigo
{
    Patrulha,
    Perseguicao,
    PerseguicaoUnida
}

public class IAInimigo : MonoBehaviour
{
    public EstadoInimigo estadoAtual = EstadoInimigo.Patrulha;

    [Header("Referências")]
    public Transform jogador;
    private NavMeshAgent agente; 

    [Header("Configurações de Visão e Grupo")]
    public float distanciaDeteccao = 10f;
    public float raioAliados = 5f;
    public int quantidadeAliadosParaUnir = 2;

    [Header("Configurações de Patrulha")]
    public Transform[] pontosPatrulha; 
    private int indicePatrulhaAtual = 0;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        TomarDecisao();
        ExecutarEstado();
    }

    void TomarDecisao()
    {
        float distanciaParaJogador = Vector3.Distance(transform.position, jogador.position);

        if (distanciaParaJogador <= distanciaDeteccao)
        {
            int aliadosProximos = ContarAliadosProximos();

            if (aliadosProximos >= quantidadeAliadosParaUnir)
            {
                estadoAtual = EstadoInimigo.PerseguicaoUnida;
            }
            else
            {
                estadoAtual = EstadoInimigo.Perseguicao;
            }
        }
        else
        {
            estadoAtual = EstadoInimigo.Patrulha;
        }
    }

    void ExecutarEstado()
    {
        switch (estadoAtual)
        {
            case EstadoInimigo.Patrulha:
                ComportamentoPatrulha();
                break;
                
            case EstadoInimigo.Perseguicao:
                ComportamentoPerseguicao();
                break;
                
            case EstadoInimigo.PerseguicaoUnida:
                ComportamentoPerseguicaoUnida();
                break;
        }
    }

    void ComportamentoPatrulha()
    {
        agente.speed = 1.5f; 
        agente.stoppingDistance = 0f;

        if (pontosPatrulha.Length == 0) return;

        if (agente.remainingDistance < 0.5f && !agente.pathPending)
        {
            indicePatrulhaAtual = (indicePatrulhaAtual + 1) % pontosPatrulha.Length;
        }

        agente.SetDestination(pontosPatrulha[indicePatrulhaAtual].position);
    }

    void ComportamentoPerseguicao()
    {
        agente.speed = 5f; 
        agente.stoppingDistance = 1.5f; 
        
        agente.SetDestination(jogador.position);
    }

    void ComportamentoPerseguicaoUnida()
    {
        agente.speed = 4f; 
        agente.stoppingDistance = 3f; 
        
        agente.SetDestination(jogador.position);
    }

    int ContarAliadosProximos()
    {
        Collider[] aliados = Physics.OverlapSphere(transform.position, raioAliados);
        int contagem = 0;
        
        foreach (var aliado in aliados)
        {
            if (aliado.CompareTag("Inimigo") && aliado.gameObject != this.gameObject)
            {
                contagem++;
            }
        }
        return contagem;
    }
}