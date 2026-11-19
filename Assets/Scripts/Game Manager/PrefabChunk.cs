using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjetoGnomoide.Procedural
{
    public enum FurnitureCategory
    {
        Centro,
        Canto,
        Especial
    }

    [Serializable]
    public class CategorySpawnGroup
    {
        public FurnitureCategory category;
        public List<Transform> spawnPoints = new List<Transform>();
        [Min(0)] public int maxItemsInCategory = 3;
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

        [Header("Pontos de Móveis (Por Categoria)")]
        public List<CategorySpawnGroup> furnitureSpawnGroups = new List<CategorySpawnGroup>();

        [Header("Pontos de Moedas")]
        public List<Transform> coinSpawnPoints = new List<Transform>();

        [Header("Volume para Evitar Sobreposição")]
        public Vector3 boundsLocalCenter = new Vector3(0f, 1.25f, 0f);
        public Vector3 boundsSize = new Vector3(6f, 2.5f, 6f);

        public Vector3 PatrolWorldPosition => patrolCenter != null ? patrolCenter.position : transform.position;

        public List<Transform> GetPointsForCategory(FurnitureCategory category)
        {
            for (int i = 0; i < furnitureSpawnGroups.Count; i++)
            {
                if (furnitureSpawnGroups[i].category == category)
                    return furnitureSpawnGroups[i].spawnPoints;
            }
            return null;
        }

        public int GetMaxItemsForCategory(FurnitureCategory category)
        {
            for (int i = 0; i < furnitureSpawnGroups.Count; i++)
            {
                if (furnitureSpawnGroups[i].category == category)
                    return furnitureSpawnGroups[i].maxItemsInCategory;
            }
            return 0;
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