using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using ProjetoGnomoide.Procedural;

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

    private readonly List<GameObject> moedasAtivas = new List<GameObject>();
    private readonly List<GameObject> inimigosAtivos = new List<GameObject>();

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

        PlayerPrefs.SetInt("Round Atual", 1);
        PlayerPrefs.SetInt("MoedasAcumuladasTotais", 0);
        PlayerPrefs.Save();

        IniciarRound();
    }

    public void IniciarRound()
    {
        LimparSala();

        if (ProceduralMapGenerator.Instancia != null)
        {
            ProceduralMapGenerator.Instancia.GenerateMapForRound(roundAtual);
        }

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

        // Moedas são geradas exclusivamente aqui no GameManager
        GerarMoedasNoMapa(totalMoedasRound);

        // Inimigos são gerados exclusivamente aqui no GameManager
        GerarInimigosNoNavMesh(inimigosNoRound);

        Debug.Log($"--- ROUND {roundAtual} INICIADO ---");
    }

    private void GerarMoedasNoMapa(int quantidade)
    {
        if (prefabMoeda == null || quantidade <= 0) return;

        List<PrefabChunk> chunksValidos = new List<PrefabChunk>();
        if (ProceduralMapGenerator.Instancia != null)
        {
            var ativos = ProceduralMapGenerator.Instancia.ActiveChunks;
            for (int i = 0; i < ativos.Count; i++)
            {
                if (ativos[i] != null && ativos[i].nodeKind != ChunkNodeKind.SafeRoom)
                {
                    chunksValidos.Add(ativos[i]);
                }
            }
        }

        for (int i = 0; i < quantidade; i++)
        {
            Vector3 ponto = Vector3.zero;
            bool achou = false;

            if (chunksValidos.Count > 0)
            {
                PrefabChunk chunkEscolhido = chunksValidos[Random.Range(0, chunksValidos.Count)];
                chunkEscolhido.GetWorldOverlapBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rot);

                for (int t = 0; t < 20; t++)
                {
                    Vector3 candidato = center + (rot * new Vector3(
                        Random.Range(-halfExtents.x * 0.7f, halfExtents.x * 0.7f),
                        0f,
                        Random.Range(-halfExtents.z * 0.7f, halfExtents.z * 0.7f)
                    ));
                    candidato.y = chunkEscolhido.transform.position.y + 0.35f;

                    if (ProceduralMapGenerator.Instancia.IsPositionInsideSafeRoom(candidato, 0.5f)) continue;

                    if (NavMesh.SamplePosition(candidato, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    {
                        ponto = hit.position + Vector3.up * 0.35f;
                        achou = true;
                        break;
                    }

                    ponto = candidato;
                    achou = true;
                    break;
                }
            }

            if (!achou)
            {
                ponto = PegarPontoValidoNoNavMesh() + Vector3.up * 0.35f;
            }

            GameObject moeda = Instantiate(prefabMoeda, ponto, Quaternion.identity);
            moedasAtivas.Add(moeda);
        }
    }
    
    private void GerarInimigosNoNavMesh(int quantidade)
    {
        if (prefabInimigo == null || quantidade <= 0) return;

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
        List<PrefabChunk> chunksValidos = new List<PrefabChunk>();
        if (ProceduralMapGenerator.Instancia != null)
        {
            var ativos = ProceduralMapGenerator.Instancia.ActiveChunks;
            for (int i = 0; i < ativos.Count; i++)
            {
                if (ativos[i] != null && ativos[i].nodeKind != ChunkNodeKind.SafeRoom)
                {
                    chunksValidos.Add(ativos[i]);
                }
            }
        }

        // 1. Prioriza os cômodos gerados fora da Safe Room
        if (chunksValidos.Count > 0)
        {
            for (int tentativa = 0; tentativa < 30; tentativa++)
            {
                PrefabChunk chunkEscolhido = chunksValidos[Random.Range(0, chunksValidos.Count)];
                chunkEscolhido.GetWorldOverlapBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rot);

                Vector3 candidato = center + (rot * new Vector3(
                    Random.Range(-halfExtents.x * 0.65f, halfExtents.x * 0.65f),
                    0f,
                    Random.Range(-halfExtents.z * 0.65f, halfExtents.z * 0.65f)
                ));
                candidato.y = chunkEscolhido.transform.position.y;

                if (ProceduralMapGenerator.Instancia.IsPositionInsideSafeRoom(candidato, 1.0f))
                    continue;

                if (NavMesh.SamplePosition(candidato, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                {
                    if (!ProceduralMapGenerator.Instancia.IsPositionInsideSafeRoom(hit.position, 1.0f))
                        return hit.position;
                }
                else
                {
                    // Se o NavMesh ainda não tiver sido assado nesse chunk, usa o ponto interno do cômodo
                    return candidato;
                }
            }

            // Fallback garantido em um dos cômodos válidos (nunca na Safe Room)
            PrefabChunk fallbackChunk = chunksValidos[Random.Range(0, chunksValidos.Count)];
            return fallbackChunk.PatrolWorldPosition;
        }

        // Caso extremo: se nenhum chunk extra foi gerado no round
        Vector3 baseCenter = centroDaSala != null ? centroDaSala.position : transform.position;
        for (int tentativa = 0; tentativa < 20; tentativa++)
        {
            Vector3 pontoSorteado = baseCenter + Random.insideUnitSphere * raioDeSpawn;
            pontoSorteado.y = baseCenter.y;
            if (NavMesh.SamplePosition(pontoSorteado, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }

        return baseCenter;
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
        PlayerPrefs.SetInt("Moedas Acumuladas Totais", moedasAcumuladasTotais);
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
        PlayerPrefs.SetInt("Round Atual", roundAtual);
        PlayerPrefs.Save();
        IniciarRound();
    }
}