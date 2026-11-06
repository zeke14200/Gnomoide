using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instancia { get; private set; }

    [Header("Progressão de Rounds")]
    public int roundAtual = 1;

    [Header("Configurações de Spawn (NavMesh)")]
    public Transform centroDaSala;
    public float raioDeSpawn = 15f; 

    [Header("Configurações do Jogador")]
    public GameObject prefabJogador; 
    [HideInInspector] public GameObject jogadorNaCena;

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

    [Header("Status do Round Atual")]
    public int totalMoedasRound;
    public int moedasColetadasRound;
    public bool allCoinsCollected;
    public int inimigosNoRound;

    [Header("Dados Salvos")]
    public int moedasAcumuladasTotais;

    public int moedasRestantes => totalMoedasRound - moedasColetadasRound;

    private List<GameObject> moedasAtivas = new List<GameObject>();
    private List<GameObject> inimigosAtivos = new List<GameObject>();

    void Awake()
    {
        if (Instancia == null) Instancia = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        moedasAcumuladasTotais = PlayerPrefs.GetInt("Moedas Acumuladas Totais", 0);
        roundAtual = PlayerPrefs.GetInt("Round Atual", 1); 

        IniciarRound();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5))
        {
            ResetarProgresso();
        }
    }

    public void ResetarProgresso()
    {
        roundAtual = 1;
        moedasAcumuladasTotais = 0;
        
        PlayerPrefs.SetInt("RoundAtual", 1);
        PlayerPrefs.SetInt("MoedasAcumuladasTotais", 0);
        PlayerPrefs.Save();
        
        IniciarRound();
    }

    public void IniciarRound()
    {
        LimparSala();

        totalMoedasRound = moedasIniciais + ((roundAtual - 1) * moedasAdicionaisPorRound);
        inimigosNoRound = inimigosIniciais + ((roundAtual - 1) * inimigosAdicionaisPorRound);
        if (inimigosNoRound > limiteMaximoInimigos) inimigosNoRound = limiteMaximoInimigos;

        moedasColetadasRound = 0;
        allCoinsCollected = (totalMoedasRound == 0);

        if (jogadorNaCena != null)
        {
            Destroy(jogadorNaCena);
        }

        if (prefabJogador != null && pontoSpawnJogador != null)
        {
            jogadorNaCena = Instantiate(prefabJogador, pontoSpawnJogador.position, pontoSpawnJogador.rotation);
        }

        GerarObjetosNoNavMesh(totalMoedasRound, prefabMoeda, moedasAtivas);
        GerarInimigosNoNavMesh(inimigosNoRound);

        Debug.Log($"--- ROUND {roundAtual} INICIADO ---");
    }

    private void GerarObjetosNoNavMesh(int quantidade, GameObject prefab, List<GameObject> listaRegistro)
    {
        if (prefab == null || centroDaSala == null) return;

        for (int i = 0; i < quantidade; i++)
        {
            Vector3 pontoAleatorio = PegarPontoValidoNoNavMesh();
            GameObject novoObjeto = Instantiate(prefab, pontoAleatorio, Quaternion.identity);
            listaRegistro.Add(novoObjeto);
        }
    }

    private void GerarInimigosNoNavMesh(int quantidade)
    {
        if (prefabInimigo == null || centroDaSala == null) return;

        for (int i = 0; i < quantidade; i++)
        {
            Vector3 pontoAleatorio = PegarPontoValidoNoNavMesh();
            GameObject novoInimigo = Instantiate(prefabInimigo, pontoAleatorio, Quaternion.identity);
            inimigosAtivos.Add(novoInimigo);

            if (jogadorNaCena != null && novoInimigo.TryGetComponent<IAInimigoHibrida>(out var ia))
            {
                ia.jogador = jogadorNaCena.transform;
            }
        }
    }

    private Vector3 PegarPontoValidoNoNavMesh()
    {
        for (int tentativa = 0; tentativa < 30; tentativa++)
        {
            Vector3 pontoSorteado = centroDaSala.position + Random.insideUnitSphere * raioDeSpawn;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(pontoSorteado, out hit, 2f, NavMesh.AllAreas))
            {
                return hit.position; 
            }
        }
        return centroDaSala.position; 
    }

    private void LimparSala()
    {
        foreach (GameObject obj in moedasAtivas) if (obj != null) Destroy(obj);
        moedasAtivas.Clear();

        foreach (GameObject obj in inimigosAtivos) if (obj != null) Destroy(obj);
        inimigosAtivos.Clear();
    }

    public void RegistrarColeta()
    {
        moedasColetadasRound++;
        moedasAcumuladasTotais++;
        PlayerPrefs.SetInt("MoedasAcumuladasTotais", moedasAcumuladasTotais);
        PlayerPrefs.Save();

        if (moedasColetadasRound >= totalMoedasRound)
        {
            allCoinsCollected = true;
            Debug.Log("Todas as moedas pegas");
        }
    }

    public void AvancarRound()
    {
        roundAtual++;
        PlayerPrefs.SetInt("RoundAtual", roundAtual);
        PlayerPrefs.Save();
        IniciarRound();
    }
}