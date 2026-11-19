using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ProjetoGnomoide.Procedural
{
    public class MapContentSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class FurnitureEntry
        {
            public GameObject prefab;
            [Min(0.01f)] public float weight = 1f;
            [Min(0.2f)] public float clearanceRadius = 0.9f;
            [Tooltip("Distância de afastamento da face interna da parede (para móveis de parede).")]
            [Min(0f)] public float wallOffset = 0.55f;
        }

        [Header("1. Móveis Obrigatórios no Centro")]
        [SerializeField] private List<FurnitureEntry> centerFurniturePrefabs = new List<FurnitureEntry>();
        [Min(0)] [SerializeField] private int minCenterFurniturePerRoom = 1;
        [Min(0)] [SerializeField] private int maxCenterFurniturePerRoom = 1;

        [Header("2. Móveis Exclusivos de Parede (Aleatória)")]
        [SerializeField] private List<FurnitureEntry> wallFurniturePrefabs = new List<FurnitureEntry>();
        [Min(0)] [SerializeField] private int minWallFurniturePerRoom = 1;
        [Min(0)] [SerializeField] private int maxWallFurniturePerRoom = 3;
        [Tooltip("Margem para não encostar nas quinas onde ficam as aberturas/sockets.")]
        [Min(0.5f)] [SerializeField] private float wallCornerPadding = 2.2f;

        [Header("Regras Gerais de Sala e Obstáculos")]
        [SerializeField] private bool allowFurnitureInCorridors = false;
        [SerializeField] private LayerMask obstacleMask = ~0;
        [Min(5)] [SerializeField] private int attemptsPerItem = 25;
        [Min(0.2f)] [SerializeField] private float navMeshSampleRadius = 2.0f;

        [Header("Moedas (Spawn Dinâmico)")]
        [SerializeField] private GameObject coinPrefab;
        [Min(0)] [SerializeField] private int coinsPerRoomRound1 = 2;
        [Min(0)] [SerializeField] private int extraCoinsPerRound = 1;
        [Min(0.1f)] [SerializeField] private float coinClearanceRadius = 0.55f;

        public void PopulateContent(
            IReadOnlyList<PrefabChunk> activeChunks,
            Transform parentContainer,
            List<GameObject> spawnedFurniture,
            List<GameObject> spawnedCoins,
            int currentRound)
        {
            if (activeChunks == null || activeChunks.Count == 0) return;

            int coinsTargetPerRoom = Mathf.Max(0, coinsPerRoomRound1 + ((Mathf.Max(1, currentRound) - 1) * extraCoinsPerRound));

            for (int i = 0; i < activeChunks.Count; i++)
            {
                PrefabChunk chunk = activeChunks[i];
                if (chunk == null || chunk.nodeKind == ChunkNodeKind.SafeRoom) continue;

                bool isRoom = chunk.nodeKind == ChunkNodeKind.ExplorationRoom || chunk.nodeKind == ChunkNodeKind.DeadEnd;
                bool canSpawnFurniture = isRoom || (allowFurnitureInCorridors && chunk.nodeKind == ChunkNodeKind.Corridor);

                if (canSpawnFurniture)
                {
                    SpawnCenterFurnitureInChunk(chunk, parentContainer, spawnedFurniture);
                    SpawnWallFurnitureInChunk(chunk, parentContainer, spawnedFurniture);
                }

                if (isRoom && coinPrefab != null && coinsTargetPerRoom > 0)
                {
                    for (int c = 0; c < coinsTargetPerRoom; c++)
                    {
                        if (TryFindFreePointInChunk(chunk, coinClearanceRadius, 0.72f, out Vector3 coinPos))
                        {
                            coinPos.y += 0.35f;
                            GameObject coin = Instantiate(coinPrefab, coinPos, Quaternion.identity, parentContainer);
                            spawnedCoins.Add(coin);
                        }
                    }
                }
            }
        }

        private void SpawnCenterFurnitureInChunk(PrefabChunk chunk, Transform parentContainer, List<GameObject> spawnedFurniture)
        {
            if (centerFurniturePrefabs == null || centerFurniturePrefabs.Count == 0) return;

            int count = Random.Range(minCenterFurniturePerRoom, maxCenterFurniturePerRoom + 1);
            for (int i = 0; i < count; i++)
            {
                FurnitureEntry chosen = PickWeighted(centerFurniturePrefabs);
                if (chosen == null || chosen.prefab == null) continue;

                chunk.GetWorldOverlapBox(out Vector3 center, out _, out Quaternion rot);
                Vector3 centerPos = new Vector3(center.x, chunk.transform.position.y, center.z);

                if (NavMesh.SamplePosition(centerPos, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
                {
                    centerPos = hit.position;
                }

                // Se houver leve obstrução no centro exato, tenta um pequeno deslocamento ao redor do centro
                if (IsBlocked(centerPos, chosen.clearanceRadius))
                {
                    bool foundNearby = false;
                    for (int a = 0; a < attemptsPerItem; a++)
                    {
                        Vector2 offset2D = Random.insideUnitCircle * 1.4f;
                        Vector3 candidate = centerPos + (rot * new Vector3(offset2D.x, 0f, offset2D.y));
                        if (NavMesh.SamplePosition(candidate, out NavMeshHit altHit, navMeshSampleRadius, NavMesh.AllAreas))
                        {
                            candidate = altHit.position;
                        }

                        if (!IsBlocked(candidate, chosen.clearanceRadius))
                        {
                            centerPos = candidate;
                            foundNearby = true;
                            break;
                        }
                    }

                    if (!foundNearby) continue;
                }

                Quaternion yawRot = rot * Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f);
                GameObject obj = Instantiate(chosen.prefab, centerPos, yawRot, parentContainer);
                spawnedFurniture.Add(obj);
            }
        }

        private void SpawnWallFurnitureInChunk(PrefabChunk chunk, Transform parentContainer, List<GameObject> spawnedFurniture)
        {
            if (wallFurniturePrefabs == null || wallFurniturePrefabs.Count == 0) return;

            int count = Random.Range(minWallFurniturePerRoom, maxWallFurniturePerRoom + 1);
            if (count <= 0) return;

            // Sorteia uma parede inicial aleatória (0: +Z, 1: -Z, 2: +X, 3: -X)
            int primaryWallSide = Random.Range(0, 4);

            for (int i = 0; i < count; i++)
            {
                FurnitureEntry chosen = PickWeighted(wallFurniturePrefabs);
                if (chosen == null || chosen.prefab == null) continue;

                // Prioriza a parede aleatória sorteada; se não couber ali, testa as outras paredes
                int wallSide = (primaryWallSide + i) % 4;
                if (TryFindWallPointInChunk(chunk, wallSide, chosen, out Vector3 wallPos, out Quaternion wallRot))
                {
                    GameObject obj = Instantiate(chosen.prefab, wallPos, wallRot, parentContainer);
                    spawnedFurniture.Add(obj);
                }
            }
        }

        private bool TryFindWallPointInChunk(
            PrefabChunk chunk,
            int wallSide,
            FurnitureEntry entry,
            out Vector3 worldPos,
            out Quaternion worldRot)
        {
            worldPos = Vector3.zero;
            worldRot = Quaternion.identity;

            chunk.GetWorldOverlapBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rot);

            float maxSpanX = Mathf.Max(0.4f, halfExtents.x - wallCornerPadding);
            float maxSpanZ = Mathf.Max(0.4f, halfExtents.z - wallCornerPadding);

            for (int attempt = 0; attempt < attemptsPerItem; attempt++)
            {
                Vector3 localCandidate = Vector3.zero;
                // Rotação local olhando para o interior da sala (-Z se estiver na parede +Z, etc.)
                Quaternion localFacingRot = Quaternion.identity;

                switch (wallSide)
                {
                    case 0: // Parede +Z (frente)
                        localCandidate = new Vector3(
                            Random.Range(-maxSpanX, maxSpanX),
                            0f,
                            Mathf.Max(0.4f, halfExtents.z - entry.wallOffset)
                        );
                        localFacingRot = Quaternion.Euler(0f, 180f, 0f);
                        break;

                    case 1: // Parede -Z (trás)
                        localCandidate = new Vector3(
                            Random.Range(-maxSpanX, maxSpanX),
                            0f,
                            -Mathf.Max(0.4f, halfExtents.z - entry.wallOffset)
                        );
                        localFacingRot = Quaternion.Euler(0f, 0f, 0f);
                        break;

                    case 2: // Parede +X (direita)
                        localCandidate = new Vector3(
                            Mathf.Max(0.4f, halfExtents.x - entry.wallOffset),
                            0f,
                            Random.Range(-maxSpanZ, maxSpanZ)
                        );
                        localFacingRot = Quaternion.Euler(0f, -90f, 0f);
                        break;

                    case 3: // Parede -X (esquerda)
                    default:
                        localCandidate = new Vector3(
                            -Mathf.Max(0.4f, halfExtents.x - entry.wallOffset),
                            0f,
                            Random.Range(-maxSpanZ, maxSpanZ)
                        );
                        localFacingRot = Quaternion.Euler(0f, 90f, 0f);
                        break;
                }

                Vector3 candidate = center + (rot * localCandidate);
                candidate.y = chunk.transform.position.y;

                if (ProceduralMapGenerator.Instancia != null &&
                    ProceduralMapGenerator.Instancia.IsPositionInsideSafeRoom(candidate, 0.5f))
                {
                    continue;
                }

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
                {
                    candidate = hit.position;
                }

                if (IsBlocked(candidate, entry.clearanceRadius))
                {
                    continue;
                }

                worldPos = candidate;
                worldRot = rot * localFacingRot;
                return true;
            }

            return false;
        }

        private bool TryFindFreePointInChunk(PrefabChunk chunk, float clearanceRadius, float innerFactor, out Vector3 result)
        {
            result = Vector3.zero;
            chunk.GetWorldOverlapBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rot);

            float usableHalfX = Mathf.Max(0.5f, halfExtents.x * innerFactor);
            float usableHalfZ = Mathf.Max(0.5f, halfExtents.z * innerFactor);

            for (int attempt = 0; attempt < attemptsPerItem; attempt++)
            {
                Vector3 candidate = center + (rot * new Vector3(
                    Random.Range(-usableHalfX, usableHalfX),
                    0f,
                    Random.Range(-usableHalfZ, usableHalfZ)
                ));
                candidate.y = chunk.transform.position.y;

                if (ProceduralMapGenerator.Instancia != null &&
                    ProceduralMapGenerator.Instancia.IsPositionInsideSafeRoom(candidate, 0.5f))
                {
                    continue;
                }

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
                {
                    candidate = hit.position;
                }

                if (IsBlocked(candidate, clearanceRadius))
                {
                    continue;
                }

                result = candidate;
                return true;
            }

            return false;
        }

        private bool IsBlocked(Vector3 worldPos, float clearanceRadius)
        {
            Vector3 checkPos = worldPos + Vector3.up * Mathf.Max(0.35f, clearanceRadius * 0.5f);
            return Physics.CheckSphere(checkPos, clearanceRadius, obstacleMask, QueryTriggerInteraction.Ignore);
        }

        private FurnitureEntry PickWeighted(List<FurnitureEntry> list)
        {
            float total = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].prefab != null && list[i].weight > 0f)
                {
                    total += list[i].weight;
                }
            }

            if (total <= 0f) return null;

            float roll = Random.Range(0f, total);
            float cumulative = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                FurnitureEntry entry = list[i];
                if (entry == null || entry.prefab == null || entry.weight <= 0f) continue;

                cumulative += entry.weight;
                if (roll <= cumulative) return entry;
            }

            return null;
        }
    }
}