using System.Collections.Generic;
using HoleOut.Core;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>Bộ cấp độ khó dùng khi thiết kế màn, xếp từ dễ tới khó. Chỉnh ngưỡng trong Inspector.</summary>
    [CreateAssetMenu(menuName = "HoleOut/Difficulty Profile", fileName = "HoleOutDifficultyProfile")]
    public class DifficultyProfileSO : ScriptableObject
    {
        public List<DifficultyTier> tiers = DifficultyTier.Defaults();

        public int IndexOf(string tierName) => tiers.FindIndex(t => t.name == tierName);

        void Reset() => tiers = DifficultyTier.Defaults();
    }
}
