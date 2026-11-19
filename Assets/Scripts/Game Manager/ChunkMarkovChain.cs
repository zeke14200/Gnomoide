using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjetoGnomoide.Procedural
{
    public enum ChunkNodeKind
    {
        SafeRoom,
        Corridor,
        ExplorationRoom,
        DeadEnd
    }

    [Serializable]
    public struct MarkovTransitionRule
    {
        public ChunkNodeKind fromState;
        public ChunkNodeKind toState;
        [Min(0f)] public float baseWeight;

        [Tooltip("Ajuste de peso por round acima do Round 1 (ex: +0.15f por round para novos corredores).")]
        public float weightBonusPerRound;

        [Tooltip("Limite máximo de contribuição após multiplicação por round.")]
        public float maxWeight;
    }

    [CreateAssetMenu(fileName = "MarkovChunkChain", menuName = "Projeto Gnomoide/Procedural/Markov Chunk Chain")]
    public class ChunkMarkovChain : ScriptableObject
    {
        [Header("Regras de Transição")]
        [SerializeField] private List<MarkovTransitionRule> transitions = new List<MarkovTransitionRule>();

        public ChunkNodeKind GetNextKind(
            ChunkNodeKind currentState,
            int currentRound,
            int remainingCorridorsBudget,
            int currentDepth,
            int maxDepth,
            System.Random rng = null)
        {
            // Travas estruturais para evitar explosão infinita e garantir fechamento de rotas
            if (currentDepth >= maxDepth || remainingCorridorsBudget <= 0)
            {
                if (currentState == ChunkNodeKind.Corridor)
                {
                    // Se o orçamento de corredores acabou, prioriza salas fechadas/exploração sem gerar mais corredores
                    return RollFromFiltered(currentState, currentRound, allowCorridors: false, rng, fallback: ChunkNodeKind.DeadEnd);
                }
                return ChunkNodeKind.DeadEnd;
            }

            return RollFromFiltered(currentState, currentRound, allowCorridors: true, rng, fallback: ChunkNodeKind.ExplorationRoom);
        }

        private ChunkNodeKind RollFromFiltered(
            ChunkNodeKind currentState,
            int currentRound,
            bool allowCorridors,
            System.Random rng,
            ChunkNodeKind fallback)
        {
            int roundOffset = Mathf.Max(0, currentRound - 1);
            float total = 0f;

            var candidates = new List<(ChunkNodeKind kind, float weight)>();

            for (int i = 0; i < transitions.Count; i++)
            {
                var rule = transitions[i];
                if (rule.fromState != currentState) continue;
                if (!allowCorridors && rule.toState == ChunkNodeKind.Corridor) continue;

                float w = rule.baseWeight + (rule.weightBonusPerRound * roundOffset);
                if (rule.maxWeight > 0f) w = Mathf.Min(w, rule.maxWeight);
                w = Mathf.Max(0f, w);

                if (w > 0f)
                {
                    candidates.Add((rule.toState, w));
                    total += w;
                }
            }

            if (candidates.Count == 0 || total <= 0f)
                return fallback;

            float roll = rng != null
                ? (float)(rng.NextDouble() * total)
                : UnityEngine.Random.Range(0f, total);

            float acc = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                acc += candidates[i].weight;
                if (roll <= acc)
                    return candidates[i].kind;
            }

            return candidates[candidates.Count - 1].kind;
        }
    }
}