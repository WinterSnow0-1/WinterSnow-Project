using System;
using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

public class ImpostorBakerOdinWindow : OdinEditorWindow
{
    [MenuItem("Tools/Impostor/Impostor烘培 (Color\\Normal\\Depth)")]
    private static void Open()
    {
        var win = GetWindow<ImpostorBakerOdinWindow>();
        win.titleContent = new GUIContent("Impostor Baker");
        win.Show();
    }
    
    // =========================
    // Target
    // =========================
    [TitleGroup("Target")]
    [InfoBox("Target 可选 Scene 对象或 Project Prefab 资源。建议给 BakeLayer 设置一个空层，避免拍到其它物体。")]
    [SerializeField] private GameObject target;
    
    [TitleGroup("Target")]
    [Button(ButtonSizes.Medium)]
    private void UseSelection()
    {
        if (Selection.activeGameObject != null)
            target = Selection.activeGameObject;
    }
}