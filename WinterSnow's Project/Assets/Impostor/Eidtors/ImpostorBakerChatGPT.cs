//#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

public class ImpostorBakerOdinWindowGPT : OdinEditorWindow
{
    [MenuItem("Tools/Impostor/Odin Baker GPT (Color\\Normal\\Depth)")]
    private static void Open()
    {
        var win = GetWindow<ImpostorBakerOdinWindowGPT>();
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

    // =========================
    // Sampling
    // =========================
    [TitleGroup("Sampling")]
    [MinValue(1)] public int yawSteps = 8;

    [TitleGroup("Sampling")]
    [MinValue(1)] public int pitchSteps = 4;

    [TitleGroup("Sampling")]
    [ValueDropdown(nameof(TileSizeOptions))]
    public int tileSize = 256;

    [TitleGroup("Sampling")]
    [MinValue(1.0f)]
    public float padding = 1.05f;

    [TitleGroup("Sampling")]
    [MinValue(0.5f)]
    public float distanceMul = 3.0f;

    [TitleGroup("Sampling")]
    [Range(-89f, 89f)]
    public float pitchMin = -20f;

    [TitleGroup("Sampling")]
    [Range(-89f, 89f)]
    public float pitchMax = 60f;

    [TitleGroup("Sampling")]
    [Range(0, 31)]
    public int bakeLayer = 31;

    // =========================
    // Outputs
    // =========================
    [TitleGroup("Outputs")]
    public bool bakeColor = true;

    [TitleGroup("Outputs")]
    public bool bakeNormal = true;

    [TitleGroup("Outputs")]
    public bool bakeDepth = true;

    [TitleGroup("Outputs")]
    [FolderPath(AbsolutePath = false, RequireExistingPath = false)]
    public string outputFolder = "Assets/Impostors";

    [TitleGroup("Outputs")]
    public string filePrefixOverride = ""; // 空则用对象名

    [TitleGroup("Outputs")]
    public bool overwrite = true;

    [TitleGroup("Outputs")]
    [InfoBox("Normal/Depth 默认保存 EXR（精度高）。如果你强制要 PNG，也可以关掉 useEXR，但深度会丢精度。", InfoMessageType.Warning)]
    public bool useEXRForNormalDepth = true;

    // =========================
    // Alpha Clip (for Normal/Depth replacement draw)
    // =========================
    [TitleGroup("Alpha Clip")]
    public bool enableAlphaClip = true;

    [TitleGroup("Alpha Clip")]
    [Range(0f, 1f)]
    public float fallbackCutoff = 0.5f;

    // =========================
    // Advanced
    // =========================
    [TitleGroup("Advanced")]
    [InfoBox("为了让 CopyTexture 稳定，Atlas/Tile 使用 non-MSAA。想更平滑建议提高 tileSize。", InfoMessageType.Info)]
    public bool forceNoMSAA = true;

    // =========================
    // Action
    // =========================
    [TitleGroup("Action")]
    [Button(ButtonSizes.Large)]
    private void BakeAll()
    {
        if (target == null)
        {
            Debug.LogError("Target 为空。");
            return;
        }

        EnsureFolders();
        EnsureBakeShadersExist();

        if (!bakeColor && !bakeNormal && !bakeDepth)
        {
            Debug.LogWarning("你把 Color/Normal/Depth 都关了，没东西烘焙。");
            return;
        }

        if (PrefabUtility.IsPartOfPrefabAsset(target))
        {
            string prefabPath = AssetDatabase.GetAssetPath(target);
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try { BakeInternal(root, isPrefabContents: true); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        else
        {
            BakeInternal(target, isPrefabContents: false);
        }

        AssetDatabase.Refresh();
    }

    // =========================
    // Internal
    // =========================
    private static readonly int[] TileSizeOptions = { 64, 128, 256, 512, 1024 };

    private Camera _cam;
    private Material _matNormal;
    private Material _matDepth;

    private RenderTexture _tileColor, _tileColorResolved;
    private RenderTexture _tileNormal;
    private RenderTexture _tileDepth;

    private RenderTexture _atlasColor;
    private RenderTexture _atlasNormal;
    private RenderTexture _atlasDepth;

    private readonly List<(Transform t, int layer)> _layerBackup = new();

    private const string ShaderDir = "Assets/Editor/ImpostorBaker/Shaders";
    private const string NormalShaderPath = ShaderDir + "/Hidden_ImpostorBakeNormal.shader";
    private const string DepthShaderPath  = ShaderDir + "/Hidden_ImpostorBakeDepth.shader";

    private void OnDisable()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        if (_cam != null)
        {
            DestroyImmediate(_cam.gameObject);
            _cam = null;
        }

        SafeReleaseRT(ref _tileColor);
        SafeReleaseRT(ref _tileColorResolved);
        SafeReleaseRT(ref _tileNormal);
        SafeReleaseRT(ref _tileDepth);

        SafeReleaseRT(ref _atlasColor);
        SafeReleaseRT(ref _atlasNormal);
        SafeReleaseRT(ref _atlasDepth);

        if (_matNormal != null) DestroyImmediate(_matNormal);
        if (_matDepth != null) DestroyImmediate(_matDepth);
        _matNormal = null;
        _matDepth = null;

        _layerBackup.Clear();
    }

    private void SafeReleaseRT(ref RenderTexture rt)
    {
        if (rt == null) return;
        rt.Release();
        DestroyImmediate(rt);
        rt = null;
    }

    private void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Editor"))
            AssetDatabase.CreateFolder("Assets", "Editor");

        if (!AssetDatabase.IsValidFolder("Assets/Editor/ImpostorBaker"))
            AssetDatabase.CreateFolder("Assets/Editor", "ImpostorBaker");

        if (!AssetDatabase.IsValidFolder(ShaderDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Editor/ImpostorBaker"))
                AssetDatabase.CreateFolder("Assets/Editor", "ImpostorBaker");
            AssetDatabase.CreateFolder("Assets/Editor/ImpostorBaker", "Shaders");
        }

        // 输出目录
        if (!AssetDatabase.IsValidFolder(outputFolder))
        {
            // 仅支持 Assets/... 形式
            string parent = "Assets";
            string[] parts = outputFolder.Replace("\\", "/").Split('/');
            for (int i = 1; i < parts.Length; i++)
            {
                string cur = $"{parent}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(cur))
                    AssetDatabase.CreateFolder(parent, parts[i]);
                parent = cur;
            }
        }
    }

    private void EnsureBakeShadersExist()
    {
        if (!File.Exists(NormalShaderPath))
            File.WriteAllText(NormalShaderPath, BakeNormalShaderText);

        if (!File.Exists(DepthShaderPath))
            File.WriteAllText(DepthShaderPath, BakeDepthShaderText);

        AssetDatabase.ImportAsset(NormalShaderPath);
        AssetDatabase.ImportAsset(DepthShaderPath);
        AssetDatabase.Refresh();
    }

    private void SetupCamera()
    {
        if (_cam != null) return;

        var go = new GameObject("__ImpostorBakeCam__");
        go.hideFlags = HideFlags.HideAndDontSave;
        _cam = go.AddComponent<Camera>();
        _cam.enabled = false;
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0, 0, 0, 0);
        _cam.orthographic = true;
    }

    private void SetupMaterials()
    {
        if (_matNormal == null)
        {
            var s = Shader.Find("Hidden/ImpostorBake/Normal");
            if (s == null) throw new Exception("找不到 Shader: Hidden/ImpostorBake/Normal");
            _matNormal = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
        }

        if (_matDepth == null)
        {
            var s = Shader.Find("Hidden/ImpostorBake/Depth");
            if (s == null) throw new Exception("找不到 Shader: Hidden/ImpostorBake/Depth");
            _matDepth = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
        }
    }

    private void SetupRTs(int atlasW, int atlasH)
    {
        int aa = 1; // 为了 CopyTexture 稳定，默认 non-MSAA
        if (!forceNoMSAA)
            aa = 1;

        // tile
        if (bakeColor)
        {
            _tileColor = NewRT(tileSize, tileSize, RenderTextureFormat.ARGB32, aa, "_TileColor");

            // 如果未来你想做 MSAA + resolve，这里可以扩展。当前直接用 non-MSAA。
            _tileColorResolved = null;
        }

        if (bakeNormal)
            _tileNormal = NewRT(tileSize, tileSize, RenderTextureFormat.ARGBHalf, 1, "_TileNormal");

        if (bakeDepth)
            _tileDepth = NewRT(tileSize, tileSize, RenderTextureFormat.ARGBHalf, 1, "_TileDepth");

        // atlas
        if (bakeColor)
            _atlasColor = NewRT(atlasW, atlasH, RenderTextureFormat.ARGB32, 1, "_AtlasColor");

        if (bakeNormal)
            _atlasNormal = NewRT(atlasW, atlasH, RenderTextureFormat.ARGBHalf, 1, "_AtlasNormal");

        if (bakeDepth)
            _atlasDepth = NewRT(atlasW, atlasH, RenderTextureFormat.ARGBHalf, 1, "_AtlasDepth");
    }

    private RenderTexture NewRT(int w, int h, RenderTextureFormat fmt, int aa, string name)
    {
        var rt = new RenderTexture(w, h, 24, fmt)
        {
            antiAliasing = Mathf.Max(1, aa),
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        rt.Create();
        return rt;
    }

    private void BakeInternal(GameObject root, bool isPrefabContents)
    {
        SetupCamera();
        SetupMaterials();

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogError("目标没有 Renderer，无法烘焙。");
            return;
        }

        Bounds bounds = CalculateBounds(renderers);
        Vector3 center = bounds.center;
        float radius = bounds.extents.magnitude;

        int atlasW = yawSteps * tileSize;
        int atlasH = pitchSteps * tileSize;

        SetupRTs(atlasW, atlasH);

        // 相机基础参数
        _cam.cullingMask = 1 << bakeLayer;
        _cam.nearClipPlane = 0.01f;
        _cam.farClipPlane = Mathf.Max(1f, radius * 10f);
        _cam.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) * padding;

        // replacement shader 参数（alpha clip）
        _matNormal.SetFloat("_UseAlphaClip", enableAlphaClip ? 1f : 0f);
        _matNormal.SetFloat("_FallbackCutoff", fallbackCutoff);

        _matDepth.SetFloat("_UseAlphaClip", enableAlphaClip ? 1f : 0f);
        _matDepth.SetFloat("_FallbackCutoff", fallbackCutoff);

        // 临时切层，避免拍到其他物体
        BackupAndSetLayerRecursively(root.transform, bakeLayer);

        try
        {
            // 清空 atlas
            ClearRT(_atlasColor, new Color(0, 0, 0, 0));
            ClearRT(_atlasNormal, new Color(0.5f, 0.5f, 1f, 1f)); // 默认法线指向 +Z
            ClearRT(_atlasDepth, new Color(1, 1, 1, 1));           // 默认最远

            // 逐视角烘焙
            for (int py = 0; py < pitchSteps; py++)
            {
                float pitch01 = pitchSteps == 1 ? 0.5f : (py / (pitchSteps - 1f));
                float pitchDeg = Mathf.Lerp(pitchMin, pitchMax, pitch01);

                for (int y = 0; y < yawSteps; y++)
                {
                    float yawDeg = 360f * (y / (float)yawSteps);

                    Quaternion rot = Quaternion.Euler(pitchDeg, yawDeg, 0f);
                    Vector3 forward = rot * Vector3.forward;

                    _cam.transform.position = center - forward * (radius * distanceMul);
                    _cam.transform.LookAt(center, Vector3.up);

                    int dstX = y * tileSize;
                    int dstY = (pitchSteps - 1 - py) * tileSize;

                    // 1) Color：直接相机渲染（保留原材质的 cutout/透明逻辑）
                    if (bakeColor)
                    {
                        _cam.targetTexture = _tileColor;
                        _cam.Render();

                        Graphics.CopyTexture(_tileColor, 0, 0, 0, 0, tileSize, tileSize, _atlasColor, 0, 0, dstX, dstY);
                    }

                    // 2) Normal：用 DrawRenderer replacement（兼容 SRP）
                    if (bakeNormal)
                    {
                        DrawReplacementToTile(renderers, _tileNormal, _matNormal, bounds);
                        Graphics.CopyTexture(_tileNormal, 0, 0, 0, 0, tileSize, tileSize, _atlasNormal, 0, 0, dstX, dstY);
                    }

                    // 3) Depth：线性深度 0..1
                    if (bakeDepth)
                    {
                        _matDepth.SetFloat("_ImpostorNear", _cam.nearClipPlane);
                        _matDepth.SetFloat("_ImpostorFar", _cam.farClipPlane);

                        DrawReplacementToTile(renderers, _tileDepth, _matDepth, bounds);
                        Graphics.CopyTexture(_tileDepth, 0, 0, 0, 0, tileSize, tileSize, _atlasDepth, 0, 0, dstX, dstY);
                    }
                }
            }

            // 保存
            string prefix = string.IsNullOrEmpty(filePrefixOverride) ? root.name : filePrefixOverride;

            if (bakeColor)
                SaveAtlas(_atlasColor, $"{outputFolder}/{prefix}_Impostor_Color.png", TextureFormat.RGBA32, isEXR: false);

            if (bakeNormal)
            {
                if (useEXRForNormalDepth)
                    SaveAtlas(_atlasNormal, $"{outputFolder}/{prefix}_Impostor_Normal.exr", TextureFormat.RGBAHalf, isEXR: true);
                else
                    SaveAtlas(_atlasNormal, $"{outputFolder}/{prefix}_Impostor_Normal.png", TextureFormat.RGBA32, isEXR: false);
            }

            if (bakeDepth)
            {
                if (useEXRForNormalDepth)
                    SaveAtlas(_atlasDepth, $"{outputFolder}/{prefix}_Impostor_Depth.exr", TextureFormat.RGBAHalf, isEXR: true);
                else
                    SaveAtlas(_atlasDepth, $"{outputFolder}/{prefix}_Impostor_Depth.png", TextureFormat.RGBA32, isEXR: false);
            }

            Debug.Log($"Impostor 烘焙完成：{outputFolder}  (prefix={prefix})");
        }
        finally
        {
            RestoreLayers();
            _cam.targetTexture = null;

            // 释放 RT（防止多次烘焙占用）
            SafeReleaseRT(ref _tileColor);
            SafeReleaseRT(ref _tileColorResolved);
            SafeReleaseRT(ref _tileNormal);
            SafeReleaseRT(ref _tileDepth);

            SafeReleaseRT(ref _atlasColor);
            SafeReleaseRT(ref _atlasNormal);
            SafeReleaseRT(ref _atlasDepth);
        }
    }

    private void ClearRT(RenderTexture rt, Color clearColor)
    {
        if (rt == null) return;
        var old = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, clearColor);
        RenderTexture.active = old;
    }

    private Bounds CalculateBounds(Renderer[] renderers)
    {
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private void DrawReplacementToTile(Renderer[] renderers, RenderTexture tileRT, Material replacementMat, Bounds bounds)
    {
        // 这里用 Graphics.DrawMesh + 设置相机矩阵，避免 SRP 下 ReplacementShader 不生效
        var cmd = new UnityEngine.Rendering.CommandBuffer { name = "ImpostorBakeReplacement" };

        cmd.SetRenderTarget(tileRT);
        cmd.ClearRenderTarget(true, true, new Color(0, 0, 0, 0));

        // 用真实 Camera 的矩阵，避免手搓矩阵的坐标系/深度方向坑
        Matrix4x4 view = _cam.worldToCameraMatrix;
        Matrix4x4 proj = GL.GetGPUProjectionMatrix(_cam.projectionMatrix, true);
        cmd.SetViewProjectionMatrices(view, proj);

        var mpb = new MaterialPropertyBlock();

        foreach (var r in renderers)
        {
            if (r == null || !r.enabled) continue;

            int subMeshCount = 1;
            Mesh mesh = null;
            Matrix4x4 localToWorld = r.localToWorldMatrix;

            if (r is MeshRenderer mr)
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                mesh = mf.sharedMesh;
                subMeshCount = mesh.subMeshCount;
            }
            else if (r is SkinnedMeshRenderer smr)
            {
                // BakeMesh：会生成当前姿态的 Mesh（Editor 下也可用）
                if (smr.sharedMesh == null) continue;
                var baked = new Mesh();
                smr.BakeMesh(baked);
                mesh = baked;
                subMeshCount = mesh.subMeshCount;
            }
            else
            {
                continue;
            }

            // 尝试从原材质拿 alpha clip 贴图与 cutoff
            var mat = r.sharedMaterial;
            FillAlphaClipProps(mpb, mat);

            for (int si = 0; si < subMeshCount; si++)
                cmd.DrawMesh(mesh, localToWorld, replacementMat, si, 0, mpb);

            if (r is SkinnedMeshRenderer)
                DestroyImmediate(mesh);
        }

        UnityEngine.Graphics.ExecuteCommandBuffer(cmd);
        cmd.Release();
    }

    private void FillAlphaClipProps(MaterialPropertyBlock mpb, Material srcMat)
    {
        mpb.Clear();

        float useAlpha = enableAlphaClip ? 1f : 0f;
        float useBase = 0f;

        Texture texBase = null;
        Vector4 stBase = new Vector4(1, 1, 0, 0);

        if (srcMat != null)
        {
            if (srcMat.HasProperty("_BaseMap"))
            {
                texBase = srcMat.GetTexture("_BaseMap");
                if (srcMat.HasProperty("_BaseMap_ST")) stBase = srcMat.GetVector("_BaseMap_ST");
                useBase = 1f;
            }
            else if (srcMat.HasProperty("_MainTex"))
            {
                texBase = srcMat.GetTexture("_MainTex");
                if (srcMat.HasProperty("_MainTex_ST")) stBase = srcMat.GetVector("_MainTex_ST");
                useBase = 0f;
            }
        }

        float cutoff = fallbackCutoff;
        if (srcMat != null)
        {
            if (srcMat.HasProperty("_Cutoff")) cutoff = srcMat.GetFloat("_Cutoff");
            else if (srcMat.HasProperty("_AlphaClipThreshold")) cutoff = srcMat.GetFloat("_AlphaClipThreshold");
        }

        // 统一塞给 replacement shader
        mpb.SetFloat("_UseAlphaClip", useAlpha);
        mpb.SetFloat("_UseBaseMap", useBase);
        mpb.SetFloat("_Cutoff", cutoff);

        // 两个都赋值，shader 用 _UseBaseMap 决定采样哪个
        mpb.SetTexture("_BaseMap", texBase);
        mpb.SetVector("_BaseMap_ST", stBase);

        mpb.SetTexture("_MainTex", texBase);
        mpb.SetVector("_MainTex_ST", stBase);
    }

    private void SaveAtlas(RenderTexture atlasRT, string assetPath, TextureFormat format, bool isEXR)
    {
        if (atlasRT == null) return;

        if (!overwrite && File.Exists(assetPath))
            assetPath = Path.ChangeExtension(assetPath, null) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + Path.GetExtension(assetPath);

        var old = RenderTexture.active;
        RenderTexture.active = atlasRT;

        Texture2D tex = new Texture2D(atlasRT.width, atlasRT.height, format, false, true);
        tex.ReadPixels(new Rect(0, 0, atlasRT.width, atlasRT.height), 0, 0);
        tex.Apply(false);

        RenderTexture.active = old;

        byte[] bytes;
        if (isEXR)
        {
            bytes = tex.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat | Texture2D.EXRFlags.CompressZIP);
        }
        else
        {
            bytes = tex.EncodeToPNG();
        }

        File.WriteAllBytes(assetPath, bytes);
        AssetDatabase.ImportAsset(assetPath);

        DestroyImmediate(tex);
    }

    private void BackupAndSetLayerRecursively(Transform root, int layer)
    {
        _layerBackup.Clear();
        var stack = new Stack<Transform>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var t = stack.Pop();
            _layerBackup.Add((t, t.gameObject.layer));
            t.gameObject.layer = layer;

            for (int i = 0; i < t.childCount; i++)
                stack.Push(t.GetChild(i));
        }
    }

    private void RestoreLayers()
    {
        foreach (var (t, layer) in _layerBackup)
            if (t != null) t.gameObject.layer = layer;
        _layerBackup.Clear();
    }

    // =========================
    // Embedded Shader Text
    // =========================
    private const string BakeNormalShaderText = @"
Shader ""Hidden/ImpostorBake/Normal""
{
    Properties
    {
        _BaseMap(""BaseMap"", 2D) = ""white"" {}
        _MainTex(""MainTex"", 2D) = ""white"" {}
        _UseBaseMap(""UseBaseMap"", Float) = 1
        _UseAlphaClip(""UseAlphaClip"", Float) = 1
        _Cutoff(""Cutoff"", Float) = 0.5
        _FallbackCutoff(""FallbackCutoff"", Float) = 0.5
    }
    SubShader
    {
        Tags { ""RenderType""=""Opaque"" ""Queue""=""Geometry"" }
        Pass
        {
            ZWrite On
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""

            sampler2D _BaseMap;
            sampler2D _MainTex;
            float4 _BaseMap_ST;
            float4 _MainTex_ST;
            float _UseBaseMap;
            float _UseAlphaClip;
            float _Cutoff;
            float _FallbackCutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos  : SV_POSITION;
                float3 nWS  : TEXCOORD0;
                float2 uvB  : TEXCOORD1;
                float2 uvM  : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.nWS = UnityObjectToWorldNormal(v.normal);
                o.uvB = TRANSFORM_TEX(v.uv, _BaseMap);
                o.uvM = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (_UseAlphaClip > 0.5)
                {
                    float cutoff = (_Cutoff > 0.0001) ? _Cutoff : _FallbackCutoff;
                    float2 uv = lerp(i.uvM, i.uvB, saturate(_UseBaseMap));
                    fixed a = lerp(tex2D(_MainTex, uv).a, tex2D(_BaseMap, uv).a, saturate(_UseBaseMap));
                    clip(a - cutoff);
                }

                float3 n = normalize(i.nWS);
                return float4(n * 0.5 + 0.5, 1);
            }
            ENDCG
        }
    }
}";
    private const string BakeDepthShaderText = @"
Shader ""Hidden/ImpostorBake/Depth""
{
    Properties
    {
        _BaseMap(""BaseMap"", 2D) = ""white"" {}
        _MainTex(""MainTex"", 2D) = ""white"" {}
        _UseBaseMap(""UseBaseMap"", Float) = 1
        _UseAlphaClip(""UseAlphaClip"", Float) = 1
        _Cutoff(""Cutoff"", Float) = 0.5
        _FallbackCutoff(""FallbackCutoff"", Float) = 0.5

        _ImpostorNear(""Near"", Float) = 0.01
        _ImpostorFar(""Far"", Float) = 100
    }
    SubShader
    {
        Tags { ""RenderType""=""Opaque"" ""Queue""=""Geometry"" }
        Pass
        {
            ZWrite On
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""

            sampler2D _BaseMap;
            sampler2D _MainTex;
            float4 _BaseMap_ST;
            float4 _MainTex_ST;

            float _UseBaseMap;
            float _UseAlphaClip;
            float _Cutoff;
            float _FallbackCutoff;

            float _ImpostorNear;
            float _ImpostorFar;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float3 vPos  : TEXCOORD0; // view space
                float2 uvB   : TEXCOORD1;
                float2 uvM   : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos  = UnityObjectToClipPos(v.vertex);
                o.vPos = UnityObjectToViewPos(v.vertex);
                o.uvB = TRANSFORM_TEX(v.uv, _BaseMap);
                o.uvM = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (_UseAlphaClip > 0.5)
                {
                    float cutoff = (_Cutoff > 0.0001) ? _Cutoff : _FallbackCutoff;
                    float2 uv = lerp(i.uvM, i.uvB, saturate(_UseBaseMap));
                    fixed a = lerp(tex2D(_MainTex, uv).a, tex2D(_BaseMap, uv).a, saturate(_UseBaseMap));
                    clip(a - cutoff);
                }

                // Unity view space：前方通常是 -Z，所以用 -i.vPos.z
                float d = (-i.vPos.z - _ImpostorNear) / max(1e-5, (_ImpostorFar - _ImpostorNear));
                d = saturate(d);

                return float4(d, d, d, 1);
            }
            ENDCG
        }
    }
}";
}
//#endif
