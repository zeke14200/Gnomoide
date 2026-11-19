using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ProjetoGnomoide.Procedural
{
    public class ProceduralMapGenerator : MonoBehaviour
    {
        public static ProceduralMapGenerator Instancia { get; private set; }

        [Header("Referências Fixas (Safe Room)")]
        [SerializeField] private PrefabChunk safeRoomChunk;
        [SerializeField] private Transform safeRoomExitSocket;
        [SerializeField] private Transform mapContainer;

        [Header("Cadeia de Markov & Tabelas Ponderadas")]
        [SerializeField] private ChunkMarkovChain markovChain;
        [SerializeField] private WeightedRandomTable<PrefabChunk> corridorTable = new WeightedRandomTable<PrefabChunk>();
        [SerializeField] private WeightedRandomTable<PrefabChunk> explorationRoomTable = new WeightedRandomTable<PrefabChunk>();
        [SerializeField] private WeightedRandomTable<PrefabChunk> deadEndRoomTable = new WeightedRandomTable<PrefabChunk>();
        [SerializeField] private GameObject blockerWallPrefab;

        [Header("Controle Exato por Round")]
        [Tooltip("Quantidade de corredores no Round 1 (ex: 1).")]
        [Min(1)] [SerializeField] private int corredoresRound1 = 1;
        [Tooltip("Quantos corredores a mais por round (ex: 1 -> Round 1: 1, Round 2: 2, Round 3: 3).")]
        [Min(0)] [SerializeField] private int corredoresExtrasPorRound = 1;
        [Min(1)] [SerializeField] private int maximoCorredores = 8;

        [Tooltip("Quantidade de quartos/salas no Round 1 (ex: 3).")]
        [Min(0)] [SerializeField] private int quartosRound1 = 3;
        [Tooltip("Quantos quartos/salas a mais por round (ex: 3 -> Round 1: 3, Round 2: 6, Round 3: 9).")]
        [Min(0)] [SerializeField] private int quartosExtrasPorRound = 3;
        [Min(1)] [SerializeField] private int maximoQuartos = 24;

        [Header("Detecção de Sobreposição e Conteúdo")]
        [SerializeField] private LayerMask chunkOverlapMask = ~0;
        [SerializeField] private MapContentSpawner contentSpawner;

        [Header("Integração com IA")]
        [SerializeField] private bool assignPatrolPointsToEnemies = true;

        public IReadOnlyList<PrefabChunk> ActiveChunks => activeChunks;
        public IReadOnlyList<Vector3> RoomPatrolPoints => roomPatrolPoints;

        private readonly List<PrefabChunk> activeChunks = new List<PrefabChunk>();
        private readonly List<GameObject> spawnedFurniture = new List<GameObject>();
        private readonly List<GameObject> spawnedCoins = new List<GameObject>();
        private readonly List<GameObject> spawnedBlockers = new List<GameObject>();
        private readonly List<Vector3> roomPatrolPoints = new List<Vector3>();

        private struct ExpansionTask
        {
            public Transform socket;
            public ChunkNodeKind parentKind;
            public int depth;

            public ExpansionTask(Transform socket, ChunkNodeKind parentKind, int depth)
            {
                this.socket = socket;
                this.parentKind = parentKind;
                this.depth = depth;
            }
        }

        private void Awake()
        {
            if (Instancia == null) Instancia = this;
            else Destroy(gameObject);

            if (mapContainer == null)
            {
                GameObject containerObj = new GameObject("MapContainer_Procedural");
                containerObj.transform.SetParent(transform);
                mapContainer = containerObj.transform;
            }
        }

        private void Start()
        {
            int round = GameManager.Instancia != null ? GameManager.Instancia.roundAtual : 1;
            GenerateMapForRound(round);
        }

        public bool IsPositionInsideSafeRoom(Vector3 worldPos, float margin = 1.5f)
        {
            if (safeRoomChunk == null) return false;
            safeRoomChunk.GetWorldOverlapBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rot);
            Vector3 localPos = Quaternion.Inverse(rot) * (worldPos - center);
            return Mathf.Abs(localPos.x) <= halfExtents.x + margin &&
                   Mathf.Abs(localPos.y) <= halfExtents.y + margin &&
                   Mathf.Abs(localPos.z) <= halfExtents.z + margin;
        }

        public void ClearGeneratedMap()
        {
            for (int i = activeChunks.Count - 1; i >= 0; i--)
            {
                if (activeChunks[i] != null && activeChunks[i] != safeRoomChunk)
                {
                    Destroy(activeChunks[i].gameObject);
                }
            }
            activeChunks.Clear();

            for (int i = spawnedFurniture.Count - 1; i >= 0; i--)
            {
                if (spawnedFurniture[i] != null) Destroy(spawnedFurniture[i]);
            }
            spawnedFurniture.Clear();

            for (int i = spawnedCoins.Count - 1; i >= 0; i--)
            {
                if (spawnedCoins[i] != null) Destroy(spawnedCoins[i]);
            }
            spawnedCoins.Clear();

            for (int i = spawnedBlockers.Count - 1; i >= 0; i--)
            {
                if (spawnedBlockers[i] != null) Destroy(spawnedBlockers[i]);
            }
            spawnedBlockers.Clear();

            roomPatrolPoints.Clear();
        }

        public void GenerateMapForRound(int roundNumber)
        {
            ClearGeneratedMap();

            int safeRound = Mathf.Max(1, roundNumber);
            int remainingCorridors = Mathf.Clamp(
                corredoresRound1 + ((safeRound - 1) * corredoresExtrasPorRound), 1, maximoCorredores
            );

            int remainingRooms = Mathf.Clamp(
                quartosRound1 + ((safeRound - 1) * quartosExtrasPorRound),
                0,
                maximoQuartos
            );

            int maxDepth = safeRound + 2;
            Queue<ExpansionTask> queue = new Queue<ExpansionTask>();

            Transform initialExit = safeRoomExitSocket != null
                ? safeRoomExitSocket
                : (safeRoomChunk != null && safeRoomChunk.exitSockets.Count > 0 ? safeRoomChunk.exitSockets[0] : null);

            if (safeRoomChunk != null)
            {
                activeChunks.Add(safeRoomChunk);
            }

            if (initialExit != null && remainingCorridors > 0)
            {
                PrefabChunk firstCorridorPrefab = corridorTable.Roll();
                if (firstCorridorPrefab != null)
                {
                    PrefabChunk spawnedCorridor = TrySpawnChunkAtSocket(firstCorridorPrefab, initialExit, out bool placed);
                    if (placed && spawnedCorridor != null)
                    {
                        remainingCorridors--;
                        RegisterChunkAndEnqueueExits(spawnedCorridor, 1, maxDepth, queue);
                    }
                    else
                    {
                        PlaceBlockerAtSocket(initialExit);
                    }
                }
            }

            while (queue.Count > 0)
            {
                ExpansionTask task = queue.Dequeue();
                if (task.socket == null) continue;

                // Se já atingiu o limite de quartos do round, fecha as saídas restantes
                if (remainingRooms <= 0 && remainingCorridors <= 0)
                {
                    PlaceBlockerAtSocket(task.socket);
                    continue;
                }

                ChunkNodeKind nextKind = markovChain != null
                    ? markovChain.GetNextKind(task.parentKind, safeRound, remainingCorridors, task.depth, maxDepth)
                    : (remainingCorridors > 0 ? ChunkNodeKind.Corridor : ChunkNodeKind.ExplorationRoom);

                // Se for corredor mas acabou o orçamento de corredores, vira quarto (se ainda houver quartos)
                if (nextKind == ChunkNodeKind.Corridor && remainingCorridors <= 0)
                {
                    nextKind = remainingRooms > 0 ? ChunkNodeKind.ExplorationRoom : ChunkNodeKind.DeadEnd;
                }

                // Se for quarto mas acabou o orçamento de quartos, fecha saída
                if ((nextKind == ChunkNodeKind.ExplorationRoom || nextKind == ChunkNodeKind.DeadEnd) && remainingRooms <= 0)
                {
                    if (remainingCorridors > 0)
                    {
                        nextKind = ChunkNodeKind.Corridor;
                    }
                    else
                    {
                        PlaceBlockerAtSocket(task.socket);
                        continue;
                    }
                }

                PrefabChunk chosenPrefab = PickPrefabForKind(nextKind);
                if (chosenPrefab == null)
                {
                    PlaceBlockerAtSocket(task.socket);
                    continue;
                }

                PrefabChunk spawned = TrySpawnChunkAtSocket(chosenPrefab, task.socket, out bool fits);
                if (fits && spawned != null)
                {
                    if (spawned.nodeKind == ChunkNodeKind.Corridor)
                    {
                        remainingCorridors = Mathf.Max(0, remainingCorridors - 1);
                    }
                    else
                    {
                        remainingRooms = Mathf.Max(0, remainingRooms - 1);
                    }

                    RegisterChunkAndEnqueueExits(spawned, task.depth + 1, maxDepth, queue);
                }
                else
                {
                    PlaceBlockerAtSocket(task.socket);
                }
            }

            if (contentSpawner != null)
            {
                contentSpawner.PopulateContent(
                    activeChunks,
                    mapContainer,
                    spawnedFurniture,
                    spawnedCoins,
                    safeRound
                );
            }

            if (assignPatrolPointsToEnemies && roomPatrolPoints.Count > 0)
            {
                ApplyPatrolPointsToEnemies();
            }
        }

        private PrefabChunk PickPrefabForKind(ChunkNodeKind kind)
        {
            switch (kind)
            {
                case ChunkNodeKind.Corridor:
                    return corridorTable.HasValidEntries() ? corridorTable.Roll() : explorationRoomTable.Roll();
                case ChunkNodeKind.DeadEnd:
                    return deadEndRoomTable.HasValidEntries() ? deadEndRoomTable.Roll() : explorationRoomTable.Roll();
                case ChunkNodeKind.ExplorationRoom:
                default:
                    return explorationRoomTable.HasValidEntries() ? explorationRoomTable.Roll() : deadEndRoomTable.Roll();
            }
        }

private PrefabChunk TrySpawnChunkAtSocket(PrefabChunk prefab, Transform targetExitSocket, out bool success)
        {
            success = false;
            if (prefab == null || targetExitSocket == null) return null;

            PrefabChunk instance = Instantiate(prefab, mapContainer);
            instance.AlignEntryToSocket(targetExitSocket);

            Physics.SyncTransforms();

            instance.GetWorldOverlapBox(out Vector3 center, out Vector3 halfExtents, out Quaternion orientation);

            float newRadiusXZ = Mathf.Max(halfExtents.x, halfExtents.z) * 0.9f;

            // 1. Impede que um chunk nasça sobre ou muito próximo do centro de outro chunk já ativo
            for (int i = 0; i < activeChunks.Count; i++)
            {
                PrefabChunk existing = activeChunks[i];
                if (existing == null) continue;

                existing.GetWorldOverlapBox(out Vector3 existingCenter, out Vector3 existingHalfExtents, out _);

                float existingRadiusXZ = Mathf.Max(existingHalfExtents.x, existingHalfExtents.z) * 0.9f;
                float minAllowedDist = (existingRadiusXZ + newRadiusXZ) * 0.85f;

                float flatDist = Vector2.Distance(
                    new Vector2(existingCenter.x, existingCenter.z),
                    new Vector2(center.x, center.z)
                );

                if (flatDist < minAllowedDist)
                {
                    Destroy(instance.gameObject);
                    return null;
                }
            }

            // 2. Checa colisão física com o ambiente já instanciado
            Collider[] overlaps = Physics.OverlapBox(center, halfExtents * 0.95f, orientation, chunkOverlapMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < overlaps.Length; i++)
            {
                if (overlaps[i] == null) continue;
                if (overlaps[i].transform.IsChildOf(instance.transform)) continue;

                Destroy(instance.gameObject);
                return null;
            }

            success = true;
            return instance;
        }

        private void RegisterChunkAndEnqueueExits(
            PrefabChunk chunk,
            int nextDepth,
            int maxDepth,
            Queue<ExpansionTask> queue)
        {
            activeChunks.Add(chunk);

            // Nunca adiciona SafeRoom na patrulha de inimigos
            if (chunk.nodeKind != ChunkNodeKind.SafeRoom && chunk.includeInEnemyPatrol)
            {
                roomPatrolPoints.Add(chunk.PatrolWorldPosition);
            }

            if (nextDepth > maxDepth || chunk.exitSockets == null) return;

            for (int i = 0; i < chunk.exitSockets.Count; i++)
            {
                Transform exit = chunk.exitSockets[i];
                if (exit == null) continue;
                queue.Enqueue(new ExpansionTask(exit, chunk.nodeKind, nextDepth));
            }
        }

        private void PlaceBlockerAtSocket(Transform socket)
        {
            if (blockerWallPrefab == null || socket == null) return;
            GameObject blocker = Instantiate(blockerWallPrefab, socket.position, socket.rotation, mapContainer);
            blocker.SetActive(true);
            spawnedBlockers.Add(blocker);
        }

        private void ApplyPatrolPointsToEnemies()
        {
            IAInimigoHibrida[] enemies = FindObjectsByType<IAInimigoHibrida>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null)
                {
                    enemies[i].DefinirPontosDePatrulha(roomPatrolPoints);
                }
            }
        }
    }
}