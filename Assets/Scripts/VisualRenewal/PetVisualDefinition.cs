using System;
using UnityEngine;
using Mongle.Slice;

namespace Mongle.VisualRenewal
{
    public enum VisualQuality { Low, Standard, Showcase }

    [Serializable]
    public sealed class PetCostumeAttachment
    {
        public string costumeId;
        public string socketId;
        public GameObject prefab;
    }

    [Serializable]
    public sealed class PetBodyMaterials
    {
        // 개체별 Material 복제를 하지 않는다. 세 품질 단계 모두 명시한다.
        public Material low, standard, showcase;
        public Material Resolve(VisualQuality quality)
        {
            return quality == VisualQuality.Low ? low :
                   quality == VisualQuality.Standard ? standard : showcase;
        }
    }

    [Serializable]
    public sealed class PetSocketEffect
    {
        public string socketId;
        public ParticleSystem prefab;
        public VisualQuality minimumQuality = VisualQuality.Standard;
        // 몸에 붙은 빛은 Local, 떨어져 남는 입자는 World.
        public ParticleSystemSimulationSpace simulationSpace = ParticleSystemSimulationSpace.Local;
        public int sortingOrder;
    }

    /// <summary>
    /// 종+성장단계+현재 등급에 대응하는 시각 정의. 성장/각성의 확률 판정은 하지 않는다.
    /// rigPrefab은 하나의 몸체 SpriteRenderer+통합 골격/Animator다.
    /// 본체를 쪼개서 조립하지 않는다. 추가물은 코스튬과 오라뿐이다.
    /// </summary>
    [CreateAssetMenu(menuName = "Mongle/Visual Renewal/Definition")]
    public sealed class PetVisualDefinition : ScriptableObject
    {
        [Min(0)] public int speciesId;
        public GrowthStage stage;
        public PetGrade grade;
        public GameObject rigPrefab;
        public PetBodyMaterials bodyMaterials = new PetBodyMaterials();
        public PetCostumeAttachment[] costumes = Array.Empty<PetCostumeAttachment>();
        public PetSocketEffect[] effects = Array.Empty<PetSocketEffect>();
    }
}
