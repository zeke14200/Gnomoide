using UnityEngine;
using UnityEngine.AI; // OBRIGATÓRIO PARA USAR O NAVMESH
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instancia { get; private set; }

    [Header("Progressão de Rounds")]
    public int roundAtual = 1;

    [Header("Configurações de Spawn (NavMesh)")]
    [Tooltip("Um Empty GameObject bem no meio do seu quarto/mapa")]
    public Transform centroDaSala;
    [Tooltip("Até quantos metros a partir do centro as coisas podem nascer")]
    public float raioDeSpawn = 15f; 

    [Header("Configurações de Moedas")]
    public int moedasIniciais = 5;
    public int moedasAdicionaisPorRound = 2;
    public GameObject prefabMoeda;

    [Header("Configurações de Inimigos")]
    public int inimigosIniciais = 2;
    public int inimigosAdicionaisPorRound = 1;
    public int limiteMaximoInimigos = 10;
    public GameObject prefabInimigo;

    [Header("Referências da Sala")]
    public Transform pontoSpawnJogador;
    private GameObject jogador;

    [Header("Status do Round Atual (Somente Leitura)")]
    public int totalMoedasRound;
    public int moedasColetadasRound;
    public bool allCoinsCollected;
    public int inimigosNoRound;

    [Header("Dados Salvos")]
    public int moedasAcumuladasTotais;

    public int moedasRestantes => totalMoedasRound - moedasColetadasRound;

    // Listas para limpar o cenário antes de começar o próximo round
    private List<GameObject> moedasAtivas = new List<GameObject>();
    private List<GameObject> inimigosAtivos = new List<GameObject>();

    void Awake()
    {
        if (Instancia == null)
        {
            Instancia = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        jogador = GameObject.FindGameObjectWithTag("Player");
        
        // Carrega o banco global de moedas e o round atual
        moedasAcumuladasTotais = PlayerPrefs.GetInt("MoedasAcumuladasTotais", 0);
        roundAtual = PlayerPrefs.GetInt("RoundAtual", 1); 

        IniciarRound();
    }

    public void IniciarRound()
    {
        LimparSala();

        // 1. Cálculos de Matemática da Progressão
        totalMoedasRound = moedasIniciais + ((roundAtual - 1) * moedasAdicionaisPorRound);
        inimigosNoRound = inimigosIniciais + ((roundAtual - 1) * inimigosAdicionaisPorRound);
        if (inimigosNoRound > limiteMaximoInimigos) inimigosNoRound = limiteMaximoInimigos;

        moedasColetadasRound = 0;
        allCoinsCollected = (totalMoedasRound == 0);

        // 2. Teleporta o Jogador para o Início
        if (jogador != null && pontoSpawnJogador != null)
        {
            CharacterController cc = jogador.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            
            jogador.transform.position = pontoSpawnJogador.position;
            jogador.transform.rotation = pontoSpawnJogador.rotation;
            
            if (cc != null) cc.enabled = true;
        }

        // 3. Geração Procedural via NavMesh
        GerarObjetosNoNavMesh(totalMoedasRound, prefabMoeda, moedasAtivas);
        GerarObjetosNoNavMesh(inimigosNoRound, prefabInimigo, inimigosAtivos);

        Debug.Log($"--- ROUND {roundAtual} INICIADO ---");
        Debug.Log($"Moedas: {totalMoedasRound} | Inimigos: {inimigosNoRound}");
    }

    // A Mágica do NavMesh acontece aqui
    private void GerarObjetosNoNavMesh(int quantidade, GameObject prefab, List<GameObject> listaRegistro)
    {
        if (prefab == null || centroDaSala == null) return;

        for (int i = 0; i < quantidade; i++)
        {
            Vector3 pontoAleatorio = PegarPontoValidoNoNavMesh();
            
            // Instancia o objeto no ponto exato caminhável que o NavMesh validou
            GameObject novoObjeto = Instantiate(prefab, pontoAleatorio, Quaternion.identity);
            listaRegistro.Add(novoObjeto);
        }
    }

    private Vector3 PegarPontoValidoNoNavMesh()
    {
        // O script tenta achar um ponto válido até 30 vezes. Se não achar (o que é raríssimo), nasce no centro.
        for (int tentativa = 0; tentativa < 30; tentativa++)
        {
            // Cria um ponto 100% aleatório dentro de uma esfera invisível
            Vector3 pontoSorteado = centroDaSala.position + Random.insideUnitSphere * raioDeSpawn;
            
            NavMeshHit hit;
            // O SamplePosition checa se num raio de 2 metros desse ponto aleatório existe chão do NavMesh.
            if (NavMesh.SamplePosition(pontoSorteado, out hit, 2f, NavMesh.AllAreas))
            {
                return hit.position; // Retorna a posição exata, segura e livre de paredes
            }
        }
        
        Debug.LogWarning("Não foi possível achar um lugar no NavMesh! Spawnando no centro.");
        return centroDaSala.position; 
    }

    private void LimparSala()
    {
        foreach (GameObject obj in moedasAtivas)
        {
            if (obj != null) Destroy(obj);
        }
        moedasAtivas.Clear();

        foreach (GameObject obj in inimigosAtivos)
        {
            if (obj != null) Destroy(obj);
        }
        inimigosAtivos.Clear();
    }

    public void RegistrarColeta()
    {
        moedasColetadasRound++;
        
        moedasAcumuladasTotais++;
        PlayerPrefs.SetInt("MoedasAcumuladasTotais", moedasAcumuladasTotais);
        PlayerPrefs.Save();

        Debug.Log($"Moeda coletada! Faltam {moedasRestantes} no Round {roundAtual}. Total: {moedasAcumuladasTotais}");

        if (moedasColetadasRound >= totalMoedasRound)
        {
            allCoinsCollected = true;
            Debug.Log("Todas as moedas do round pegas! Corra para o Spawn!");
        }
    }

    public void AvancarRound()
    {
        roundAtual++;
        PlayerPrefs.SetInt("RoundAtual", roundAtual);
        PlayerPrefs.Save();
        
        IniciarRound();
    }
    
    // Mostra a área de spawn na janela Scene (bolha amarela) para facilitar o ajuste do Raio
    void OnDrawGizmosSelected()
    {
        if (centroDaSala != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(centroDaSala.position, raioDeSpawn);
        }
    }
}