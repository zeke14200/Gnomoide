using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ProjetoGnomoide.Procedural
{
    [System.Serializable]
    public class CategoryFurnitureConfig
    {
        public FurnitureCategory category;
        public WeightedRandomTable<GameObject> furnitureTable = new WeightedRandomTable<GameObject>();
        [Range(0f, 1f)] public float spawnChancePerPoint = 0.7f;
        [Min(0.1f)] public float minSpacingBetweenFurniture = 1.6f;
    }

    public class MapContentSpawner : MonoBehaviour
    {
        [Header("Configuração de Móveis por Categoria")]
        [SerializeField] private List<CategoryFurnitureConfig> furnitureConfigs = new List<CategoryFurnitureConfig>();
        [SerializeField] private LayerMask furnitureOverlapMask = ~0;

        [Header("Configuração de Moedas")]
        [Tooltip("Prefab da moeda (ex: Prefab com SecretCoin).")]
        [SerializeField] private GameObject coinPrefab;

        [Min(0)] [SerializeField] private int minCoinsPerRoom = 1;
        [Min(0)] [SerializeField] private int maxCoinsPerRoom = 3;

        [Range(0f, 1f)]
        [Tooltip("Chance base de um ponto de moeda ser selecionado durante a distribuição.")]
        [SerializeField] private float coinPointChance = 0.65f;

        [Tooltip("Distância mínima entre duas moedas geradas para evitar aglomeração.")]
        [SerializeField] private float minDistanceBetweenCoins = 1.25f;

        [Tooltip("Se verdadeiro, sincroniza totalMoedasRound no GameManager com a quantidade real gerada.")]
        [SerializeField] private bool syncRoundCoinCountWithGameManager = true;

        private readonly List<Vector3> placedFurniturePositions = new List<Vector3>();
        private readonly List<Vector3> placedCoinPositions = new List<Vector3>();

        public void PopulateContent(
            List<PrefabChunk> generatedChunks,
            Transform contentParent,
            List<GameObject> spawnedFurnitureRegistry,
            List<GameObject> spawnedCoinsRegistry,
            int currentRound)
        {
            placedFurniturePositions.Clear();
            placedCoinPositions.Clear();

            if (generatedChunks == null || generatedChunks.Count == 0) return;

            // 1. Móveis por chunk e categoria
            for (int i = 0; i < generatedChunks.Count; i++)
            {
                PrefabChunk chunk = generatedChunks[i];
                if (chunk == null) continue;

                SpawnFurnitureForChunk(chunk, contentParent, spawnedFurnitureRegistry);
            }

            // 2. Moedas distribuídas entre as salas de forma equilibrada
            SpawnCoinsAcrossChunks(generatedChunks, contentParent, spawnedCoinsRegistry, currentRound);
        }

        private void SpawnFurnitureForChunk(
            PrefabChunk chunk,
            Transform contentParent,
            List<GameObject> registry)
        {
            for (int c = 0; c < furnitureConfigs.Count; c++)
            {
                CategoryFurnitureConfig cfg = furnitureConfigs[c];
                if (!cfg.furnitureTable.HasValidEntries()) continue;

                List<Transform> points = chunk.GetPointsForCategory(cfg.category);
                if (points == null || points.Count == 0) continue;

                int maxAllowed = chunk.GetMaxItemsForCategory(cfg.category);
                if (maxAllowed <= 0) maxAllowed = points.Count;

                List<Transform> shuffledPoints = new List<Transform>(points);
                ShuffleList(shuffledPoints);

                int spawnedCount = 0;
                for (int p = 0; p < shuffledPoints.Count; p++)
                {
                    if (spawnedCount >= maxAllowed) break;

                    Transform pt = shuffledPoints[p];
                    if (pt == null) continue;

                    if (Random.value > cfg.spawnChancePerPoint) continue;

                    if (IsTooCloseToExisting(pt.position, placedFurniturePositions, cfg.minSpacingBetweenFurniture))
                        continue;

                    if (Physics.CheckSphere(pt.position + Vector3.up * 0.4f, cfg.minSpacingBetweenFurniture * 0.45f, furnitureOverlapMask, QueryTriggerInteraction.Ignore))
                        continue;

                    GameObject prefab = cfg.furnitureTable.Roll();
                    if (prefab == null) continue;

                    Vector3 spawnPos = SnapToNavMeshOrPoint(pt.position, 1.5f);
                    GameObject instance = Instantiate(prefab, spawnPos, pt.rotation, contentParent);

                    EnsureNavMeshObstacle(instance);

                    placedFurniturePositions.Add(spawnPos);
                    registry.Add(instance);
                    spawnedCount++;
                }
            }
        }

        private void SpawnCoinsAcrossChunks(
            List<PrefabChunk> chunks,
            Transform contentParent,
            List<GameObject> coinRegistry,
            int currentRound)
        {
            if (coinPrefab == null) return;

            // Filtra chunks elegíveis para moedas (prioriza salas de exploração e becos sem saída, excluindo SafeRoom)
            List<PrefabChunk> eligibleRooms = new List<PrefabChunk>();
            for (int i = 0; i < chunks.Count; i++)
            {
                PrefabChunk ch = chunks[i];
                if (ch == null || ch.nodeKind == ChunkNodeKind.SafeRoom) continue;
                if (ch.coinSpawnPoints != null && ch.coinSpawnPoints.Count > 0)
                {
                    eligibleRooms.Add(ch);
                }
            }

            if (eligibleRooms.Count == 0) return;

            ShuffleList(eligibleRooms);

            int totalSpawned = 0;
            int effectiveMaxPerRoom = Mathf.Max(minCoinsPerRoom, maxCoinsPerRoom + ((currentRound - 1) / 3));

            for (int r = 0; r < eligibleRooms.Count; r++)
            {
                PrefabChunk room = eligibleRooms[r];
                List<Transform> candidatePoints = new List<Transform>(room.coinSpawnPoints);
                ShuffleList(candidatePoints);

                int targetForThisRoom = Random.Range(minCoinsPerRoom, effectiveMaxPerRoom + 1);
                targetForThisRoom = Mathf.Clamp(targetForThisRoom, 0, candidatePoints.Count);

                int roomCoinsSpawned = 0;

                for (int p = 0; p < candidatePoints.Count; p++)
                {
                    if (roomCoinsSpawned >= targetForThisRoom) break;

                    Transform pt = candidatePoints[p];
                    if (pt == null) continue;

                    // Garante pelo menos minCoinsPerRoom se possível; nos extras aplica a chance
                    if (roomCoinsSpawned >= minCoinsPerRoom && Random.value > coinPointChance)
                        continue;

                    if (IsTooCloseToExisting(pt.position, placedCoinPositions, minDistanceBetweenCoins))
                        continue;

                    if (IsTooCloseToExisting(pt.position, placedFurniturePositions, 0.7f))
                        continue;

                    Vector3 coinPos = SnapToNavMeshOrPoint(pt.position, 1.5f) + Vector3.up * 0.25f;
                    GameObject coinObj = Instantiate(coinPrefab, coinPos, Quaternion.identity, contentParent);

                    placedCoinPositions.Add(coinPos);
                    coinRegistry.Add(coinObj);
                    roomCoinsSpawned++;
                    totalSpawned++;
                }
            }

            if (syncRoundCoinCountWithGameManager && GameManager.Instancia != null && totalSpawned > 0)
            {
                GameManager.Instancia.totalMoedasRound = totalSpawned;
                GameManager.Instancia.moedasColetadasRound = 0;
                GameManager.Instancia.allCoinsCollected = false;
            }
        }

        private void EnsureNavMeshObstacle(GameObject furniture)
        {
            if (furniture.TryGetComponent<NavMeshObstacle>(out var obstacle))
            {
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
            }
        }

        private Vector3 SnapToNavMeshOrPoint(Vector3 origin, float maxDistance)
        {
            if (NavMesh.SamplePosition(origin, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
            {
                return hit.position;
            }
            return origin;
        }

        private bool IsTooCloseToExisting(Vector3 pos, List<Vector3> existing, float minDistance)
        {
            float minSqr = minDistance * minDistance;
            for (int i = 0; i < existing.Count; i++)
            {
                if ((existing[i] - pos).sqrMagnitude < minSqr)
                    return true;
            }
            return false;
        }

        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}