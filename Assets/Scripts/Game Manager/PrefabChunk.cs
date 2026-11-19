using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ProjetoGnomoide.Procedural
{
    public enum FurnitureCategory
    {
        Centro,
        Parede,
        Canto,
        Especial
    }

    [Serializable]
    public class ChunkFurnitureOption
    {
        public GameObject prefab;
        [Min(0.01f)] public float weight = 1f;
        [Min(0.2f)] public float clearanceRadius = 0.9f;
        [Tooltip("Distância de afastamento da face interna da parede (usado para móveis de Parede/Canto).")]
        [Min(0f)] public float wallOffset = 0.55f;
    }

    public class PrefabChunk : MonoBehaviour
    {
        [Header("Identificação do Chunk")]
        public ChunkNodeKind nodeKind = ChunkNodeKind.ExplorationRoom;
        public string chunkId = "Chunk_Padrao";

        [Header("Conectores (Entrada e Saídas)")]
        [Tooltip("Ponto de encaixe de entrada (onde a saída do chunk anterior se conecta). Deixe nulo na SafeRoom se ela for fixa.")]
        public Transform entrySocket;

        [Tooltip("Saídas disponíveis (ex: 4 saídas em um corredor padrão).")]
        public List<Transform> exitSockets = new List<Transform>();

        [Header("Patrulha & Navegação")]
        [Tooltip("Ponto central da sala/corredor usado como alvo de patrulha pela IA.")]
        public Transform patrolCenter;

        [Tooltip("Se verdadeiro, este chunk entra na lista de patrulha das salas.")]
        public bool includeInEnemyPatrol = true;

        [Header("Móveis Obrigatórios no Centro")]
        public List<ChunkFurnitureOption> centerFurnitureOptions = new List<ChunkFurnitureOption>();
        [Min(0)] public int minCenterItems = 1;
        [Min(0)] public int maxCenterItems = 1;

        [Header("Móveis Exclusivos de Parede (Aleatória)")]
        public List<ChunkFurnitureOption> wallFurnitureOptions = new List<ChunkFurnitureOption>();
        [Min(0)] public int minWallItems = 1;
        [Min(0)] public int maxWallItems = 3;
        [Tooltip("Margem para evitar encostar nas quinas/saídas das paredes.")]
        [Min(0.5f)] public float wallCornerPadding = 2.0f;

        [Header("Regras de Spawn no Chunk")]
        public bool spawnFurnitureInThisChunk = true;
        public LayerMask obstacleMask = ~0;
        [Min(5)] public int maxAttemptsPerFurniture = 25;
        [Min(0.2f)] public float navMeshSampleRadius = 2.0f;

        [Header("Volume para Evitar Sobreposição")]
        public Vector3 boundsLocalCenter = new Vector3(0f, 1.25f, 0f);
        public Vector3 boundsSize = new Vector3(6f, 2.5f, 6f);

        public Vector3 PatrolWorldPosition => patrolCenter != null ? patrolCenter.position : transform.position;

        public void SpawnChunkFurniture(Transform parentContainer, List<GameObject> spawnedList)
        {
            if (!spawnFurnitureInThisChunk || nodeKind == ChunkNodeKind.SafeRoom) return;

            Transform container = parentContainer != null ? parentContainer : transform;

            SpawnCenterFurniture(container, spawnedList);
            SpawnWallFurniture(container, spawnedList);
        }

        private void SpawnCenterFurniture(Transform container, List<GameObject> spawnedList)
        {
            if (centerFurnitureOptions == null || centerFurnitureOptions.Count == 0) return;

            int count = UnityEngine.Random.Range(minCenterItems, maxCenterItems + 1);
            if (count <= 0) return;

            GetWorldOverlapBox(out Vector3 boxCenter, out _, out Quaternion rot);
            Vector3 baseCenter = new Vector3(boxCenter.x, transform.position.y, boxCenter.z);

            for (int i = 0; i < count; i++)
            {
                ChunkFurnitureOption chosen = PickWeighted(centerFurnitureOptions);
                if (chosen == null || chosen.prefab == null) continue;

                Vector3 targetPos = baseCenter;
                if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
                {
                    targetPos = hit.position;
                }

                if (IsBlocked(targetPos, chosen.clearanceRadius))
                {
                    bool foundAlt = false;
                    for (int a = 0; a < maxAttemptsPerFurniture; a++)
                    {
                        Vector2 offset2D = UnityEngine.Random.insideUnitCircle * 1.3f;
                        Vector3 candidate = baseCenter + (rot * new Vector3(offset2D.x, 0f, offset2D.y));
                        if (NavMesh.SamplePosition(candidate, out NavMeshHit altHit, navMeshSampleRadius, NavMesh.AllAreas))
                        {
                            candidate = altHit.position;
                        }

                        if (!IsBlocked(candidate, chosen.clearanceRadius))
                        {
                            targetPos = candidate;
                            foundAlt = true;
                            break;
                        }
                    }

                    if (!foundAlt) continue;
                }

                Quaternion yawRot = rot * Quaternion.Euler(0f, UnityEngine.Random.Range(0, 4) * 90f, 0f);
                GameObject instance = Instantiate(chosen.prefab, targetPos, yawRot, container);
                spawnedList?.Add(instance);
            }
        }

        private void SpawnWallFurniture(Transform container, List<GameObject> spawnedList)
        {
            if (wallFurnitureOptions == null || wallFurnitureOptions.Count == 0) return;

            int count = UnityEngine.Random.Range(minWallItems, maxWallItems + 1);
            if (count <= 0) return;

            int primaryWallSide = UnityEngine.Random.Range(0, 4);

            for (int i = 0; i < count; i++)
            {
                ChunkFurnitureOption chosen = PickWeighted(wallFurnitureOptions);
                if (chosen == null || chosen.prefab == null) continue;

                int wallSide = (primaryWallSide + i) % 4;
                if (TryFindWallSpot(wallSide, chosen, out Vector3 worldPos, out Quaternion worldRot))
                {
                    GameObject instance = Instantiate(chosen.prefab, worldPos, worldRot, container);
                    spawnedList?.Add(instance);
                }
            }
        }

        private bool TryFindWallSpot(int wallSide, ChunkFurnitureOption option, out Vector3 worldPos, out Quaternion worldRot)
        {
            worldPos = Vector3.zero;
            worldRot = Quaternion.identity;

            GetWorldOverlapBox(out Vector3 boxCenter, out Vector3 halfExtents, out Quaternion rot);

            float spanX = Mathf.Max(0.4f, halfExtents.x - wallCornerPadding);
            float spanZ = Mathf.Max(0.4f, halfExtents.z - wallCornerPadding);

            for (int attempt = 0; attempt < maxAttemptsPerFurniture; attempt++)
            {
                Vector3 localPos;
                Quaternion localRot;

                switch (wallSide)
                {
                    case 0: // +Z
                        localPos = new Vector3(
                            UnityEngine.Random.Range(-spanX, spanX),
                            0f,
                            Mathf.Max(0.4f, halfExtents.z - option.wallOffset)
                        );
                        localRot = Quaternion.Euler(0f, 180f, 0f);
                        break;

                    case 1: // -Z
                        localPos = new Vector3(
                            UnityEngine.Random.Range(-spanX, spanX),
                            0f,
                            -Mathf.Max(0.4f, halfExtents.z - option.wallOffset)
                        );
                        localRot = Quaternion.Euler(0f, 0f, 0f);
                        break;

                    case 2: // +X
                        localPos = new Vector3(
                            Mathf.Max(0.4f, halfExtents.x - option.wallOffset),
                            0f,
                            UnityEngine.Random.Range(-spanZ, spanZ)
                        );
                        localRot = Quaternion.Euler(0f, -90f, 0f);
                        break;

                    case 3: // -X
                    default:
                        localPos = new Vector3(
                            -Mathf.Max(0.4f, halfExtents.x - option.wallOffset),
                            0f,
                            UnityEngine.Random.Range(-spanZ, spanZ)
                        );
                        localRot = Quaternion.Euler(0f, 90f, 0f);
                        break;
                }

                Vector3 candidate = boxCenter + (rot * localPos);
                candidate.y = transform.position.y;

                if (ProceduralMapGenerator.Instancia != null &&
                    ProceduralMapGenerator.Instancia.IsPositionInsideSafeRoom(candidate, 0.5f))
                {
                    continue;
                }

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
                {
                    candidate = hit.position;
                }

                if (IsBlocked(candidate, option.clearanceRadius))
                {
                    continue;
                }

                worldPos = candidate;
                worldRot = rot * localRot;
                return true;
            }

            return false;
        }

        private bool IsBlocked(Vector3 worldPos, float clearanceRadius)
        {
            Vector3 checkCenter = worldPos + Vector3.up * Mathf.Max(0.35f, clearanceRadius * 0.5f);
            return Physics.CheckSphere(checkCenter, clearanceRadius, obstacleMask, QueryTriggerInteraction.Ignore);
        }

        private ChunkFurnitureOption PickWeighted(List<ChunkFurnitureOption> list)
        {
            float totalWeight = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].prefab != null && list[i].weight > 0f)
                {
                    totalWeight += list[i].weight;
                }
            }

            if (totalWeight <= 0f) return null;

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                ChunkFurnitureOption item = list[i];
                if (item == null || item.prefab == null || item.weight <= 0f) continue;

                cumulative += item.weight;
                if (roll <= cumulative) return item;
            }

            return null;
        }

        public void AlignEntryToSocket(Transform targetExitSocket)
        {
            if (targetExitSocket == null) return;

            if (entrySocket == null)
            {
                transform.position = targetExitSocket.position;
                transform.rotation = targetExitSocket.rotation;
                return;
            }

            Vector3 desiredEntryForward = -targetExitSocket.forward;
            Quaternion targetEntryRotation = Quaternion.LookRotation(desiredEntryForward, Vector3.up);

            Quaternion deltaRot = targetEntryRotation * Quaternion.Inverse(entrySocket.rotation);
            transform.rotation = deltaRot * transform.rotation;

            Vector3 offset = targetExitSocket.position - entrySocket.position;
            transform.position += offset;
        }

        public void GetWorldOverlapBox(out Vector3 center, out Vector3 halfExtents, out Quaternion orientation)
        {
            center = transform.TransformPoint(boundsLocalCenter);
            Vector3 scaledSize = Vector3.Scale(boundsSize, transform.lossyScale);
            halfExtents = Vector3.Max(scaledSize * 0.5f, Vector3.one * 0.05f);
            orientation = transform.rotation;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.25f);
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(boundsLocalCenter, boundsSize);
            Gizmos.matrix = oldMatrix;

            if (entrySocket != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(entrySocket.position, 0.2f);
                Gizmos.DrawRay(entrySocket.position, entrySocket.forward * 0.8f);
            }

            if (exitSockets != null)
            {
                Gizmos.color = Color.yellow;
                foreach (var exit in exitSockets)
                {
                    if (exit == null) continue;
                    Gizmos.DrawSphere(exit.position, 0.18f);
                    Gizmos.DrawRay(exit.position, exit.forward * 0.8f);
                }
            }

            if (patrolCenter != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(patrolCenter.position, 0.35f);
            }
        }
    }
}