using System;
using System.Collections.Generic;
using UnityEngine;
using Mongle.Slice;

namespace Mongle.VisualRenewal
{
    [CreateAssetMenu(menuName = "Mongle/Visual Renewal/Catalog")]
    public sealed class PetVisualCatalog : ScriptableObject
    {
        public PetVisualDefinition[] entries = Array.Empty<PetVisualDefinition>();

        public void ValidateCatalog()
        {
            if (entries == null || entries.Length == 0)
                throw new InvalidOperationException("Visual catalog is empty.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in entries)
            {
                if (!d || !d.rigPrefab || d.speciesId < 0 ||
                    !Enum.IsDefined(typeof(PetGrade), d.grade) ||
                    !Enum.IsDefined(typeof(GrowthStage), d.stage))
                    throw new InvalidOperationException("Invalid visual definition/prefab.");
                string key = d.speciesId + "/" + d.stage + "/" + d.grade;
                if (!keys.Add(key)) throw new InvalidOperationException("Duplicate visual: " + key);
            }
        }

        public PetVisualDefinition Resolve(int speciesId, GrowthStage stage, PetGrade grade)
        {
            // 조회 때도 중복을 거부한다. 검증 메뉴를 거치지 않은 데이터도 안전하게 실패한다.
            if (entries == null) throw new InvalidOperationException("Visual catalog entries missing.");
            PetVisualDefinition found = null;
            foreach (var d in entries)
            {
                if (!d || d.speciesId != speciesId || d.stage != stage || d.grade != grade) continue;
                if (found) throw new InvalidOperationException("Duplicate matching visual definition.");
                found = d;
            }
            if (!found || !found.rigPrefab)
                throw new InvalidOperationException($"Missing visual: {speciesId}/{stage}/{grade}");
            return found;
        }
    }
}
