using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjetoGnomoide.Procedural
{
    [Serializable]
    public struct WeightedEntry<T>
    {
        public T item;
        [Min(0f)] public float weight;
        public string label;
    }

    [Serializable]
    public class WeightedRandomTable<T>
    {
        [SerializeField] private List<WeightedEntry<T>> entries = new List<WeightedEntry<T>>();

        public IReadOnlyList<WeightedEntry<T>> Entries => entries;

        public bool HasValidEntries()
        {
            return GetTotalWeight() > 0f;
        }

        public float GetTotalWeight()
        {
            float total = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].item != null && entries[i].weight > 0f)
                {
                    total += entries[i].weight;
                }
            }
            return total;
        }

        public T Roll(System.Random rng = null)
        {
            float totalWeight = GetTotalWeight();
            if (totalWeight <= 0f) return default;

            float rollValue = rng != null
                ? (float)(rng.NextDouble() * totalWeight)
                : UnityEngine.Random.Range(0f, totalWeight);

            float cumulative = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.item == null || entry.weight <= 0f) continue;

                cumulative += entry.weight;
                if (rollValue <= cumulative)
                {
                    return entry.item;
                }
            }

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].item != null && entries[i].weight > 0f)
                    return entries[i].item;
            }

            return default;
        }
    }
}