using System;
using Mongle.VisualRenewal;
using UnityEditor;
using UnityEngine;
using Mongle.Slice;

/// <summary>정상 Unity 에디터에서 실행하는 참조/셰이더 검사. 이 환경에서 실행한 검사는 아니다.</summary>
public static class PetVisualRenewalValidation
{
    [MenuItem("Mongle/Visual Renewal/Validate Selected Catalog")]
    public static void ValidateSelected()
    {
        var catalog = Selection.activeObject as PetVisualCatalog;
        if (!catalog) throw new InvalidOperationException("Select a PetVisualCatalog asset first.");
        catalog.ValidateCatalog();
        var shader = Shader.Find("Mongle/VisualRenewal/PetLocalGlow");
        if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("LocalGlow shader missing/has errors.");
        // 통합 본체/코스튬/오라 참조는 Play Mode Equip에서도 검증한다.
        foreach (var d in catalog.entries)
        {
            if (!d.rigPrefab.GetComponent<PetVisualBindings>()) throw new InvalidOperationException("Rig binding missing: " + d.name);
            var row = d.bodyMaterials;
            foreach (VisualQuality tier in Enum.GetValues(typeof(VisualQuality)))
            {
                var material = row == null ? null : row.Resolve(tier);
                if (!material || !material.HasProperty("_MaskTex") || !material.HasProperty("_GlowStrength"))
                    throw new InvalidOperationException("Material/mask properties missing: " + d.name);
            }
        }
        Debug.Log("VISUAL RENEWAL ASSET REFERENCES PASS. Play Mode, SpriteSkin timing and device visuals still require review.");
    }
}
