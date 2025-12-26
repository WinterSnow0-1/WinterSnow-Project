using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
[DisallowMultipleComponent]
public class StandardMatChecker : MonoBehaviour
{
    //之后可以扩展不同类型的展示材质：标准，皮肤，玉石
    public enum ShowMaterialType
    {
        PBR,
        ToonLit,
       // Skin,
       // Custom
    }

    public enum ShowContrastType
    {
        Default
        //Metal,
        //Rough,
    }
    
    [Serializable]
    public class SphereSample
    {
        [HorizontalGroup("Row", Width = 110)]
        [LabelText("Label")]
        public string label;

        [HorizontalGroup("Row", Width = 90)]
        [LabelText("M")]
        [Range(0, 1)] public float metallic;

        [HorizontalGroup("Row", Width = 90)]
        [LabelText("S")]
        [Range(0, 1)] public float smoothness;

        [HorizontalGroup("Row")]
        [LabelText("BaseColor")]
        public Color baseColor = new Color(0.5f, 0.5f, 0.5f, 1);
    }
    
    // 创建单独渲染层，避免其余相机观察
    const string layerName = "StandardMatChecker";
    int generatedLayer = -1;
    
    
    [TitleGroup("基础配置")]    
    [LabelText("显示相机（默认为主相机）")]
    [InfoBox("小窗吃后处理的实现：检查相机离屏渲染到 RT，并开启 renderPostProcessing + Volume。")]
    public Camera baseCamera;

    [LabelText("小窗位置 (Rect)")]
    public Rect overlayViewport = new Rect(0f, 0f, 0.20f, 1f);

    [LabelText("RT Height (px)")]
    [Range(512, 4096)]
    public int rtHeight = 1024;
    
    [LabelText("小窗背景")]
    public Color checkerBackground = new Color(0.18f, 0.18f, 0.18f, 1f);
    
    
    [TitleGroup("模式设置")]
    [LabelText("材质类型")]
    [EnumToggleButtons] 
    public ShowMaterialType materialPreset = ShowMaterialType.PBR;
    
    [LabelText("对比参数类型")]
    [EnumToggleButtons] 
    public ShowContrastType parameterPreset = ShowContrastType.Default;
    
    
    /*[ShowIf(nameof(materialPreset), ShowMaterialType.Custom)]
    [ListDrawerSettings(Expanded = true, DraggableItems = true, ShowItemCount = true)]
    public List<SphereSample> customSamples = new();*/
  
    
    
    [TitleGroup("展示物体设置")]
    [LabelText("球体大小")]
    [Min(0.01f)] public float sphereRadius = 0.12f;
    
    [LabelText("色卡贴图（默认24色色卡）)")]
    public Texture2D externalChartTexture;
    
    [LabelText("色卡尺寸")]
    public Vector2 chartWorldSize = new Vector2(0.55f, 0.38f);
    // 默认512 ， 3 ：2 宽高比
    private int generatedChartSize =  1024;
    
    
    // =========================================================
    // Generated refs
    // =========================================================
    [TitleGroup("Debug")]
    [FoldoutGroup("Debug/Generated References", expanded: false)]
    [ReadOnly] public Transform generatedRoot;

    [FoldoutGroup("Debug/Generated References")]
    [ReadOnly] public Transform posRoot;

    [FoldoutGroup("Debug/Generated References")]
    [ReadOnly] public GameObject chartQuad;

    [FoldoutGroup("Debug/Generated References")]
    [ReadOnly] public Volume autoVolume;

    [FoldoutGroup("Debug/Generated References")]
    [ReadOnly] public Camera checkerCamera;
    
    [FoldoutGroup("Debug/Generated References")]
    [ReadOnly] public Camera maskCamera;

    [FoldoutGroup("Debug/Generated References")]
    [ReadOnly] public RenderTexture checkerRT;
    
    [FoldoutGroup("Debug/Generated References")]
    [ReadOnly] public RenderTexture maskRT;
    
    [FoldoutGroup("Debug/Generated References")]
    [ReadOnly] public Canvas overlayCanvas;

    [FoldoutGroup("Debug/Generated References")]
    [ReadOnly] public RawImage overlayImage;

    [FoldoutGroup("Debug/Generated References")]
    [ReadOnly] public Material compositeMat;
    
    private readonly List<GameObject> _spheres = new();
    private readonly List<Material> _runtimeMats = new();
    
    private Texture2D _generatedChartTex;
    private Material _chartMat;

    private static readonly int ID_BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ID_Metallic = Shader.PropertyToID("_Metallic");
    private static readonly int ID_Smoothness = Shader.PropertyToID("_Smoothness");
    private static readonly int ID_BaseMap = Shader.PropertyToID("_BaseMap");
    private static readonly int ID_MainTex = Shader.PropertyToID("_MainTex");
    
#if UNITY_EDITOR
    private double _nextEditorRender;
#endif
    
    [Button("生成对应的所有物体", ButtonSizes.Large)]
    public void RebuildSidebar()
    {
        EnsureRoot();
        EnsureLayer();
        EnsureBaseCamera();
        
        ClearGeneratedInternal();
        
        EnsurePosRoot();
        BuildChartInRig();
        BuildSamplesInRig();
        
        EnsureGlobalVolume();
        
        ApplyIsolationToBaseCamera();
        
        EnsureCheckerCamera();
        EnsureMaskCamera();
        EnsureRT();
        
        EnsureUIOverlay();
        MarkDirtyRender();
        
#if UNITY_EDITOR
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
#endif
    }
    
    [Button("删除生成的所有物体", ButtonSizes.Large)]
    public void DeleteAll()
    {
        // RT/UI/Camera
        DestroyRT();
        DestroyUIOverlay();
        DestroyCheckerCamera();
        DestroyMaskCamera();
        
        // Volume (only generated)
        if (autoVolume != null && autoVolume.transform.parent == generatedRoot)
            SafeDestroy(autoVolume.gameObject);
        autoVolume = null;

        // Rig / objects / materials / textures
        ClearGeneratedInternal();

        if (generatedRoot != null)
            SafeDestroy(generatedRoot.gameObject);
        generatedRoot = null;

#if UNITY_EDITOR
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
#endif
    }
    
    
    
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            EnsureLayer();
            EnsureBaseCamera();

            ApplyIsolationToBaseCamera();

            EnsureCheckerCamera();
            EnsureMaskCamera();
            EnsureRT();
            EnsureUIOverlay();
            MarkDirtyRender();
        }
    }
#endif
    
    private void EnsureRoot()
    {
        if (generatedRoot != null) return;

        var t = transform.Find("__StandardMatChecker_Generated");
        if (t == null)
        {
            var go = new GameObject("__StandardMatChecker_Generated");
            go.transform.SetParent(transform, false);
            generatedRoot = go.transform;
        }
        else generatedRoot = t;
    }
    
    // 生成单独层，避免其余相机观察到生成物体
    private void EnsureLayer()
    {
#if UNITY_EDITOR
        if (generatedLayer >= 0) return;

        int idx = LayerMask.NameToLayer(layerName);
        if (idx >= 0)
        {
            generatedLayer = idx;
            return;
        }

        //内部文件路径，存储Tag和层Layer的配置
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layersProp = tagManager.FindProperty("layers");

        int found = -1;
        for (int i = 8; i <= 31; i++)
        {
            var sp = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(sp.stringValue))
            {
                sp.stringValue = layerName;
                found = i;
                break;
            }
        }

        if (found >= 0)
        {
            tagManager.ApplyModifiedProperties();
            generatedLayer = LayerMask.NameToLayer(layerName);
        }
        else
        {
            Debug.LogWarning("没有空的 Layer Slot（8~31 都被占用了），将无法隔离渲染层。");
            generatedLayer = -1;
        }
#else
        generatedLayer = LayerMask.NameToLayer(layerName);
#endif
    }
    
    private void EnsureBaseCamera()
    {
        if (baseCamera != null) return;
        baseCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
    }
    
    private static void SafeDestroy(UnityEngine.Object obj)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) DestroyImmediate(obj);
        else Destroy(obj);
#else
        Destroy(obj);
#endif
    }

    
    private void ClearSamplesOnly()
    {
        foreach (var s in _spheres)
            if (s) SafeDestroy(s);
        _spheres.Clear();
    }
    
    // 删除自动生成的物体
    private void ClearGeneratedInternal()
    {
        ClearSamplesOnly();

        if (chartQuad) SafeDestroy(chartQuad);
        chartQuad = null;

        if (posRoot != null)
        {
            SafeDestroy(posRoot.gameObject);
            posRoot = null;
        }

        foreach (var m in _runtimeMats)
            if (m) SafeDestroy(m);
        _runtimeMats.Clear();

        if (_generatedChartTex) SafeDestroy(_generatedChartTex);
        _generatedChartTex = null;

        if (generatedRoot != null)
        {
            for (int i = generatedRoot.childCount - 1; i >= 0; i--)
            {
                var child = generatedRoot.GetChild(i);
                SafeDestroy(child.gameObject);
            }
        }
    }
    
    // 材质和色卡的位置根节点
    private void EnsurePosRoot()
    {
        if (posRoot != null) return;
        if (generatedRoot == null) EnsureRoot();

        var t = generatedRoot.Find("__PosRoot");
        if (t == null)
        {
            var go = new GameObject("__PosRoot");
            go.transform.SetParent(generatedRoot, false);
            posRoot = go.transform;
        }
        else posRoot = t;

        posRoot.localPosition = Vector3.one;
    }
    
    //生成色卡
    private void BuildChartInRig()
    {
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null) unlit = Shader.Find("Unlit/Texture");
        if (unlit == null)
        {
            Debug.LogError("找不到可用的 Unlit Shader");
            return;
        }

        chartQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        chartQuad.name = "ColorChart_24";
        chartQuad.transform.SetParent(posRoot, false);
        chartQuad.transform.localPosition = new Vector3(0,2.5f,0);
        chartQuad.transform.localRotation = Quaternion.identity;
        chartQuad.transform.localScale = new Vector3(chartWorldSize.x , chartWorldSize.y * 1.33f, 1);
        chartQuad.layer = generatedLayer;
        
        
        var col = chartQuad.GetComponent<Collider>();
        if (col) SafeDestroy(col);

        _chartMat = new Material(unlit)
        {
            name = "(Runtime) ChartMat",
            hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
        };

        if (externalChartTexture == null)
            externalChartTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/StandardMaterialChecker/Macbeth_Color_Chart_4K.png");
        
        if (externalChartTexture == null)
        {
            Debug.LogError("缺少色卡");
            return;
        }

        Texture2D chartTex = externalChartTexture;
        if (_chartMat.HasProperty(ID_BaseMap)) _chartMat.SetTexture(ID_BaseMap, chartTex);
        if (_chartMat.HasProperty(ID_MainTex)) _chartMat.SetTexture(ID_MainTex, chartTex);
        if (_chartMat.HasProperty(ID_BaseColor)) _chartMat.SetColor(ID_BaseColor, Color.white);

        chartQuad.GetComponent<Renderer>().sharedMaterial = _chartMat;
        _runtimeMats.Add(_chartMat);
    }
    
    private List<SphereSample> GetSamplesForPreset(ShowContrastType p)
    {
        var list = new List<SphereSample>();
        switch (p)
        {
            case ShowContrastType.Default:
                // 重点看 IBL / 高光 / 粗糙度
                list.Add(new SphereSample { label = "custom_S0.5_M1.0", metallic = 1f, smoothness = 0.5f, baseColor = new Color(0.73f, 0.73f, 0.73f, 1) });
                list.Add(new SphereSample { label = "custom_S1.0_M0.0", metallic = 0f, smoothness = 1f, baseColor = new Color(0.18f, 0.18f, 0.18f, 1) });
                list.Add(new SphereSample { label = "custom_S1.0_M1.0", metallic = 1f, smoothness = 1f, baseColor = new Color(1,1,1, 1) });
                list.Add(new SphereSample { label = "custom_S0.15_M0.0", metallic = 0f, smoothness = 0.150f, baseColor = new Color(0.5f, 0.5f, 0.5f, 1) });
                break;
        }
        return list;
    }
    
    private void BuildSamplesInRig()
    {
        // 使用默认材质
        
        Shader lit = null;
        if(materialPreset == ShowMaterialType.PBR)
            lit = Shader.Find("Universal Render Pipeline/Lit");
        else if (materialPreset == ShowMaterialType.ToonLit)
            lit = Shader.Find("Universal Render Pipeline/Lit");
        
        
        if (lit == null)
        {
            Debug.LogError("找不到 Shader: Universal Render Pipeline/Lit。确认项目使用 URP。");
            return;
        }

        List<SphereSample> samples = GetSamplesForPreset(parameterPreset);

        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i];

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Sphere_{i:D2}_{s.label}";
            go.transform.SetParent(posRoot, false);
            go.transform.localScale = Vector3.one * (sphereRadius * 2f);

            // chart 在上，球从 chart 下方往下排
            float y = 1.4f - (chartWorldSize.y * 0.5f) - 0.18f - i * sphereRadius * 2.3f;
            go.transform.localPosition = new Vector3(0f, y, 0f);

            var col = go.GetComponent<Collider>();
            if (col) SafeDestroy(col);

            var r = go.GetComponent<Renderer>();
            var mat = new Material(lit)
            {
                name = $"(Runtime) Lit_{s.label}",
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };

            mat.SetColor(ID_BaseColor, s.baseColor);
            mat.SetFloat(ID_Metallic, s.metallic);
            mat.SetFloat(ID_Smoothness, s.smoothness);
            go.layer = generatedLayer;

            r.sharedMaterial = mat;

            _spheres.Add(go);
            _runtimeMats.Add(mat);
        }
    }
    
    private void EnsureGlobalVolume()
    {
        EnsureRoot();

        autoVolume = FindObjectOfType<Volume>();
        if (autoVolume == null)
        {
            Debug.Log("当前场景中没有设置后处理");
        }
        

    }
    
    private void ApplyIsolationToBaseCamera()
    {
        if (generatedLayer < 0) return;

        EnsureBaseCamera();
        if (baseCamera == null) return;

        baseCamera.cullingMask &= ~(1 << generatedLayer);
    }
    
    private void EnsureCheckerCamera()
    {
        if (checkerCamera != null) return;

        var go = new GameObject("StandardMatChecker_OffscreenCamera");
        go.transform.SetParent(transform, false);

        checkerCamera = go.AddComponent<Camera>();
        checkerCamera.enabled = true; 
        checkerCamera.clearFlags = CameraClearFlags.SolidColor;
        checkerCamera.backgroundColor = checkerBackground;
        checkerCamera.nearClipPlane = 0.01f;
        checkerCamera.farClipPlane = 2f;
        checkerCamera.allowHDR = true;
        checkerCamera.allowMSAA = false;
        checkerCamera.orthographic = true;
        checkerCamera.orthographicSize = 5;
        checkerCamera.sensorSize = Vector2.one;
        
        // 位置朝向
        checkerCamera.transform.localPosition = new Vector3(1,0,0);
        checkerCamera.transform.localRotation = Quaternion.Euler( new Vector3(0f, 0f, 0f));

        // URP 附加：开启后处理 + Volume 影响
        var data = checkerCamera.GetComponent<UniversalAdditionalCameraData>();
        if (data == null) data = checkerCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        data.renderType = CameraRenderType.Base;
        data.renderPostProcessing = true;
        data.requiresColorOption = CameraOverrideOption.On;
        data.requiresDepthOption = CameraOverrideOption.Off;
        data.volumeLayerMask = ~0;            // 让全局 Volume 生效
        data.volumeTrigger = checkerCamera.transform;

        // culling mask：只渲染生成层
        checkerCamera.cullingMask = (generatedLayer >= 0) ? (1 << generatedLayer) : ~0;
    }
    
    private void EnsureMaskCamera()
    {
        if (maskCamera != null) return;

        var go = new GameObject("StandardMatAlphaMask_OffscreenCamera");
        go.transform.SetParent(transform, false);

        maskCamera = go.AddComponent<Camera>();
        maskCamera.enabled = true; 
        maskCamera.clearFlags = CameraClearFlags.SolidColor;
        maskCamera.backgroundColor = new Color(0,0,0,0);
        maskCamera.nearClipPlane = checkerCamera.nearClipPlane;
        maskCamera.farClipPlane = checkerCamera.farClipPlane;
        maskCamera.orthographic = checkerCamera.orthographic;
        maskCamera.orthographicSize =checkerCamera.orthographicSize;;
        maskCamera.sensorSize = Vector2.one;
        
        // 位置朝向
        maskCamera.transform.localPosition = checkerCamera.transform.localPosition;
        maskCamera.transform.localRotation = checkerCamera.transform.localRotation;
        
        // culling mask：只渲染生成层
        maskCamera.cullingMask = checkerCamera.cullingMask;
    }
    
    
    private void DestroyRT()
    {
        if (checkerCamera != null) checkerCamera.targetTexture = null;

        if (checkerRT != null)
        {
            checkerRT.Release();
            SafeDestroy(checkerRT);
            checkerRT = null;
        }
    }
    
    private void DestroyUIOverlay()
    {
        if (overlayImage != null)
        {
            SafeDestroy(overlayImage.gameObject);
            overlayImage = null;
        }
        if (overlayCanvas != null)
        {
            SafeDestroy(overlayCanvas.gameObject);
            overlayCanvas = null;
        }
    }
    
    private void DestroyCheckerCamera()
    {
        if (checkerCamera != null)
        {
            SafeDestroy(checkerCamera.gameObject);
            checkerCamera = null;
        }
    }
    
    private void DestroyMaskCamera()
    {
        if (maskCamera != null)
        {
            SafeDestroy(maskCamera.gameObject);
            maskCamera = null;
        }
    }

    
    private void EnsureRT()
    {
        if (checkerCamera == null) return;

        // 计算 RT 宽高：依据屏幕 viewport 的像素宽高比例
        float screenAspect = (Screen.height > 0) ? (Screen.width / (float)Screen.height) : (16f / 9f);
        float viewportAspect = (overlayViewport.height > 0) ? ((overlayViewport.width ) / overlayViewport.height) : screenAspect;
        viewportAspect = overlayViewport.width / overlayViewport.height;

        int h = Mathf.Clamp(rtHeight, 128, 4096);
        int w = Mathf.Clamp(Mathf.RoundToInt(h * viewportAspect * 1.75f), 128, 4096);

        bool needRecreate = checkerRT == null || checkerRT.width != w || checkerRT.height != h;

        if (needRecreate)
        {
            DestroyRT();

            var fmt = RenderTextureFormat.ARGBHalf;
            checkerRT = new RenderTexture(w, h, 0, fmt)
            {
                name = $"StandardMatChecker_RT_{w}x{h}",
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
            checkerRT.Create();
            
            maskRT = new RenderTexture(w , h , 0,fmt)
            {
                name = $"StandardMatMask_RT_{w}x{h}",
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
            maskRT.Create();
        }

        checkerCamera.targetTexture = checkerRT;

        // 更新 UI
        if (overlayImage != null)
        {
            compositeMat.SetTexture("_MainTex", checkerRT);
            compositeMat.SetTexture("_MaskTex", maskRT);
            overlayImage.material = compositeMat;
            overlayImage.texture = checkerRT;
        }
    }
    
    private void RenderCheckerIfReady()
    {
        if (checkerCamera == null || checkerRT == null) return;
        
        checkerCamera.clearFlags = CameraClearFlags.SolidColor;
        checkerCamera.backgroundColor = new Color(checkerBackground.r, checkerBackground.g, checkerBackground.b, 0f);
        checkerCamera.allowHDR = true;
        checkerCamera.cullingMask = (generatedLayer >= 0) ? (1 << generatedLayer) : ~0;
        checkerCamera.sensorSize = Vector2.one;
        // 相机跟随参数
        checkerCamera.transform.localPosition = new Vector3(1,0,0);
        checkerCamera.transform.localRotation = Quaternion.Euler( new Vector3(0f, 0f, 0f));

        // 确保 RT 尺寸跟得上
        EnsureRT();

        maskCamera.Render();
        checkerCamera.Render();
    }
    
    //由于camera要吃后处理，因此不能透明，需要二次渲染
    private void RenderMaskIfReady()
    {
        if (maskCamera == null)
        {
            var go = new GameObject("StandardMatChecker_MaskCamera");
            go.transform.SetParent(transform, false);
            maskCamera = go.AddComponent<Camera>();

            // 复制 checker 参数（或你手动设置）
            maskCamera.CopyFrom(checkerCamera);

            var data = maskCamera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = maskCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;     // ✅ 关键：mask 不要后处理
            data.renderType = CameraRenderType.Base;
        }

        maskCamera.transform.position = checkerCamera.transform.position;
        maskCamera.transform.rotation = checkerCamera.transform.rotation;
        maskCamera.clearFlags = CameraClearFlags.SolidColor;
        maskCamera.backgroundColor = new Color(0,0,0,0);

        maskCamera.cullingMask = checkerCamera.cullingMask;

        maskCamera.targetTexture = maskRT;

    }

    
    private void MarkDirtyRender()
    {
        EnsureRT();
        RenderCheckerIfReady();
        RenderMaskIfReady();
    }
    
    private void EnsureUIOverlay()
    {
        if (overlayCanvas == null)
        {
            var canvasGO = new GameObject("StandardMatChecker_UI");
            canvasGO.transform.SetParent(transform, false);

            overlayCanvas = canvasGO.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 5000;

            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        if (overlayImage == null)
        {
            var imgGO = new GameObject("CheckerWindow");
            imgGO.transform.SetParent(overlayCanvas.transform, false);

            overlayImage = imgGO.AddComponent<RawImage>();
            overlayImage.raycastTarget = false;
        }

        // 把 RawImage 锚到 overlayViewport
        var rt = overlayImage.rectTransform;
        rt.anchorMin = new Vector2(overlayViewport.xMin, overlayViewport.yMin);
        rt.anchorMax = new Vector2(overlayViewport.xMax, overlayViewport.yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        overlayImage.texture = checkerRT;
        
        if (compositeMat == null)
            compositeMat = new Material(Shader.Find("UI/MatCheckerComposite"));
        // 让 UI 不受场景缩放影响
        overlayCanvas.gameObject.layer = 0;
    }

    
}
