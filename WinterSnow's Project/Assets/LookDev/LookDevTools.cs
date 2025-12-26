using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine.SceneManagement;
#endif

[ExecuteInEditMode]
[RequireComponent(typeof(Light))]
[DisallowMultipleComponent]
public class LookDevTools : MonoBehaviour
{
#if UNITY_EDITOR
    Light m_Light;

    // 折叠收起的窗口？
    bool m_isFoldedWin = false;
    // 正在编辑光源方向？
    bool m_isEditLightDir = false;
    // 用于光源旋转方向复位的方向值
    Quaternion m_lastLightDir = Quaternion.identity;
    // 自动旋转光源
    bool m_isAutoRoateLight = false;
    Material skyBoxMaterial;
    // 不同天空材质，切换不同后处理效果
    Volume sceneVolume;
    VolumeProfile m_volumeProfile;
    
    [SerializeField]
    private LookDevConfig m_LookDevConfig;

   

    [ReadOnly]
    public float m_OriginalIntensity = -1f;

    //──────────────────────────────────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        m_Light = GetComponent<Light>();
        if (m_OriginalIntensity < 0) m_OriginalIntensity = m_Light.intensity;
        skyBoxMaterial = RenderSettings.skybox;
        sceneVolume = FindObjectOfType<Volume>();
        m_volumeProfile = sceneVolume?.profile;
    }

    //──────────────────────────────────────────────────────────────────────────────────────────────────────
    //https://zhuanlan.zhihu.com/p/124269658 （Scene View扩展显示常驻GUI）
    private void OnEnable() { SceneView.duringSceneGui += OnSceneGUI; }
    private void OnDisable() { SceneView.duringSceneGui -= OnSceneGUI; }

    //──────────────────────────────────────────────────────────────────────────────────────────────────────
    private Rect _WindowsRect = new Rect();
    
    static int StableHash(string s)
    {
        unchecked
        {
            int hash = (int)2166136261;
            for (int i = 0; i < s.Length; i++)
                hash = (hash ^ s[i]) * 16777619;
            return hash;
        }
    }
    
    private static readonly int WindowId = StableHash("LookDevTools");
    private void OnSceneGUI(SceneView sceneview)
    {
        if (m_Light == null) return;
        
        //固定左下角显示
        _WindowsRect = GUILayout.Window(WindowId, _WindowsRect, DrawWindows, "光源工具");
        if (m_isFoldedWin)
        {
            _WindowsRect.height = 45;
            m_isEditLightDir = false;
        }

        // _WindowsRect.position = new Vector2(10f, sceneview.camera.scaledPixelHeight - _WindowsRect.height - 10);
        _WindowsRect.position = new Vector2(10f, 10);
        
        if (m_isEditLightDir)
        {
            Vector3 lightGizmoPos = sceneview.camera.ViewportToWorldPoint(new Vector3(0.2f, 0.35f, 1));
            Quaternion lightGizmoRoate = m_Light.transform.rotation;
            Handles.ConeHandleCap(0, lightGizmoPos, lightGizmoRoate, 0.05f, EventType.Repaint);
            Quaternion newLightRoate = Handles.RotationHandle(lightGizmoRoate, lightGizmoPos);
            if (m_isAutoRoateLight)
            {
                newLightRoate = newLightRoate * Quaternion.AngleAxis(Time.deltaTime * 20, Quaternion.Inverse(newLightRoate) * Vector3.up);
            }
            if (newLightRoate != lightGizmoRoate)
            {
                Undo.RecordObject(m_Light.transform, "光源工具旋转");
                m_Light.transform.rotation = newLightRoate;
            }
        }
    }

    //──────────────────────────────────────────────────────────────────────────────────────────────────────
    private void DrawWindows(int windowsID)
    {
        //监听输入
        EditorGUI.BeginChangeCheck();
        //为了撤销 m_Light.intensity
        Undo.RecordObject(m_Light, "lightTools");

        //绘制界面
        this.OnDrawGUI();

        //监听输入
        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(this);
        }
        //可以防止穿透过去
        GUI.DragWindow();
        //GUI.BringWindowToFront(windowsID);
    }

    //──────────────────────────────────────────────────────────────────────────────────────────────────────
    // 光源方向复位
    private void ResetLightDir()
    {
        m_Light.transform.rotation = m_lastLightDir;
    }

    //──────────────────────────────────────────────────────────────────────────────────────────────────────
    private void OnDrawGUI()
    {
        GUILayout.BeginHorizontal();
        if (m_isFoldedWin)
            m_isFoldedWin = GUILayout.Toggle(m_isFoldedWin, "展开", "button");
        else
            m_isFoldedWin = GUILayout.Toggle(m_isFoldedWin, "收起", "button");
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        if (m_isFoldedWin)
            return;
        SirenixEditorGUI.Title("预设", "", TextAlignment.Left, true);
        if (m_LookDevConfig != null && m_LookDevConfig.datas != null)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < m_LookDevConfig.datas.Count; i++)
            {
                var preset = m_LookDevConfig.datas[i];
                if (GUILayout.Button(preset.desc)) //Editor LocalizationHelper 忽略
                {
                    m_Light.intensity = preset.lightIntensity;
                    m_Light.transform.rotation = Quaternion.Euler(preset.lightRotation);
                    m_Light.color = preset.lightColor;

                    skyBoxMaterial = preset.skyBoxMaterial;
                    RenderSettings.skybox = skyBoxMaterial;
                    RenderSettings.ambientMode = AmbientMode.Skybox;
                    RenderSettings.ambientIntensity = 1;
                    RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
                    RenderSettings.reflectionIntensity = 1;

                    m_volumeProfile = preset.volumeProfile;
                    FindObjectOfType<Volume>().profile = m_volumeProfile;
                }
            }

            GUILayout.EndHorizontal();
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        SirenixEditorGUI.Title("光源强度", "", TextAlignment.Left, true);
        SirenixEditorGUI.BeginBox();

        m_Light.intensity = EditorGUILayout.Slider("", m_Light.intensity, 0f, 5f, GUILayout.Width(255));

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("复位")) { m_Light.intensity = m_OriginalIntensity; }
        if (GUILayout.Button("定位")) { Selection.activeGameObject = gameObject; }
        EditorGUILayout.EndHorizontal();
        

        SirenixEditorGUI.EndBox();

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        SirenixEditorGUI.Title("光源方向", "", TextAlignment.Left, true);
        SirenixEditorGUI.BeginBox();
        if (m_isEditLightDir)
        {
            m_isEditLightDir = GUILayout.Toggle(m_isEditLightDir, "关闭编辑", "button");
            if (m_isEditLightDir == false)
            {
                ResetLightDir();
            }
        }
        else
        {
            if (GUILayout.Toggle(m_isEditLightDir, "开启编辑", "button"))
            {
                m_isEditLightDir = true;
                m_lastLightDir = m_Light.transform.rotation;
            }
        }

        if (m_isEditLightDir)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("复位") && m_isEditLightDir) { ResetLightDir(); }
            m_isAutoRoateLight = GUILayout.Toggle(m_isAutoRoateLight, "自动", "button");
            EditorGUILayout.EndHorizontal();
        }

        SirenixEditorGUI.EndBox();
        
        
        //-----------------------------------------
        // 环境光
        SirenixEditorGUI.Title("环境光", "", TextAlignment.Left, true);
        SirenixEditorGUI.BeginBox();
        EditorGUI.BeginChangeCheck();
        skyBoxMaterial = EditorGUILayout.ObjectField("天空球", skyBoxMaterial, typeof(Material), false) as Material;
        if (EditorGUI.EndChangeCheck())
        {
            RenderSettings.skybox = skyBoxMaterial;
        }

        SirenixEditorGUI.EndBox();
        
        //-----------------------------------------
        // 环境光
        SirenixEditorGUI.Title("后处理", "", TextAlignment.Left, true);
        SirenixEditorGUI.BeginBox();
        EditorGUI.BeginChangeCheck();
        m_volumeProfile = EditorGUILayout.ObjectField("后处理", m_volumeProfile, typeof(VolumeProfile), false) as VolumeProfile;
        if (EditorGUI.EndChangeCheck())
        {
            FindObjectOfType<Volume>().profile = m_volumeProfile;
        }
        SirenixEditorGUI.EndBox();
    }
#endif
}