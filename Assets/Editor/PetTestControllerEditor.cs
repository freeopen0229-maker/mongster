using UnityEditor;
using UnityEngine;
using Mongle.Slice.Testing;

[CustomEditor(typeof(PetTestController))]
public sealed class PetTestControllerEditor:Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Local preview only. Adult aura preview does not upgrade the actual pet. Enter Play Mode first.",MessageType.Info);
        var controller=(PetTestController)target;
        using(new EditorGUI.DisabledScope(!Application.isPlaying||!controller.isActiveAndEnabled))
        {
            if(GUILayout.Button("Equip Baby Neulgui"))controller.EquipBabyNeulgui();
            if(GUILayout.Button("Equip Adult Neulgui (Aura Preview)"))controller.EquipAdultNeulgui();
            if(GUILayout.Button("Equip Adult Neulgui (Common)"))controller.EquipCommonAdultNeulgui();
            if(GUILayout.Button("Reset 8h Egg"))controller.ResetEgg();
            if(GUILayout.Button("Trigger Hatch Scroll"))controller.TriggerHatchScroll();
        }
        EditorGUILayout.LabelField("Remaining seconds",controller.EggRemainingSeconds.ToString());
        EditorGUILayout.LabelField("Status",controller.Status);
        if(Application.isPlaying)Repaint();
    }
}
