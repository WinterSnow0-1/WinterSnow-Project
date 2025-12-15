using UnityEngine;
using UnityEngine.Rendering;
public class Shadows
{
    const string bufferName = "Shadows";
    readonly CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    ScriptableRenderContext context;

    CullingResults cullingResults;

    ShadowSettings shadowSettings;

    const int maxShadowedDirectionalLightCount = 4, maxCascades = 4;

    static readonly int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
    static readonly int dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");
    static readonly int cascadeCountId = Shader.PropertyToID("_CascadeCount");
    static readonly int cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres");
    static readonly int cascadeDataId = Shader.PropertyToID("_CascadeData");
    static readonly int shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");
    static readonly int shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");

    static readonly string[] directionalFilterKeywords =
    {
        "_DIRECTIONAL_PCF3", "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7"
    };

    static readonly string[] cascadeBlendKeywords =
    {
        "_CASCADE_BLEND_SOFT", "_CASCADE_BLEND_DITHER"
    };

    static string[] shadowMaskKeywords =
    {
        "_SHADOW_MASK_ALWAYS",
        "_SHADOW_MASK_DISTANCE"
    };

    static readonly Vector4[] cascadeCullingSpheres = new Vector4[maxCascades];
    static readonly Vector4[] cascadeData = new Vector4[maxCascades];

    static readonly Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades];

    int ShadowedDirectionalLightCount;

    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }

    readonly ShadowedDirectionalLight[] shadowedDirectionalLights = new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];

    public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        /// GetShadowCasterBounds bool 如果光源影响了场景中至少一个阴影投射对象，则为 true。https://docs.unity.cn/cn/2019.4/ScriptReference/Rendering.CullingResults.GetShadowCasterBounds.html
        /// 函数所做：1. 找到当前灯光所照到的物体上的，cast shadow 不等于 off 的
        ///         2. 挑出可能把阴影投影到当前摄像机可见区域的物体
        ///         3. 求出这些物体的 世界空间下的  轴对齐包围盒  如果没有，则返回false
        if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount && light.shadows != LightShadows.None && light.shadowStrength > 0f)
        {
            float maskChannel = -1;
            LightBakingOutput lightBaking = light.bakingOutput;
            if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed && lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
            {
                useShadowMask = true;
                maskChannel = lightBaking.occlusionMaskChannel;
            }
            
            if (!cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)) 
                return new Vector4(-light.shadowStrength, 0f, 0f,maskChannel);
            
            
            shadowedDirectionalLights[ShadowedDirectionalLightCount] = new ShadowedDirectionalLight
            {
                visibleLightIndex = visibleLightIndex,
                slopeScaleBias = light.shadowBias,
                nearPlaneOffset = light.shadowNearPlane
            };
            return new Vector4(light.shadowStrength, shadowSettings.directional.cascadeCount * ShadowedDirectionalLightCount++, light.shadowNormalBias,maskChannel);
        }
        return new Vector4(0,0,0,-1);
    }

    bool useShadowMask;

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings)
    {
        ShadowedDirectionalLightCount = 0;
        this.cullingResults = cullingResults;
        this.shadowSettings = shadowSettings;
        this.context = context;
        useShadowMask = false;
    }

    public void Render()
    {
        if (ShadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else //就算不使用，也要生成对应格式的临时纹理，避免格式不对报错。我们当然也可以声明关键字来避免该情况。这里只是简单的生成一个1x1的临时纹理。
        {
            buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }

        buffer.BeginSample(bufferName);
        SetKeywords(shadowMaskKeywords, useShadowMask ? QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask? 0 : 1 : -1);
        buffer.EndSample(bufferName);
        ExecuteBuffer();

    }

    /// <summary>
    ///     RenderTextureFormat.Shadowmap GPU自动进行阴影贴图比较的一种格式。
    ///     https://docs.unity.cn/cn/2019.4/ScriptReference/RenderTextureFormat.Shadowmap.html
    ///     我们生成后，需要将摄像机的输出存储到纹理上，同时我们并不关心初始状态，因为我们会立即清除。但是我们需要存储。
    /// </summary>
    void RenderDirectionalShadows()
    {
        int atlasSize = (int)shadowSettings.directional.atlasSize;
        buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, true, Color.clear);

        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int tiles = ShadowedDirectionalLightCount * shadowSettings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;
        for (int i = 0; i < ShadowedDirectionalLightCount; i++)
            RenderDirectionalShadows(i, split, tileSize);
        float f = 1 - shadowSettings.directional.cascadeFade;
        buffer.SetGlobalVector(shadowDistanceFadeId, new Vector4(1f / shadowSettings.maxDistance, 1f / shadowSettings.distanceFade, 1 / (1 - f * f)));
        
        SetKeywords( /// -1 是因为unity中并没有提供 PCF 2x2 的方式，而是从3x3开始，因此需要我们手动是实现
            directionalFilterKeywords, (int)shadowSettings.directional.filterMode - 1);
        
        SetKeywords( /// -1 因为 hard不需要设置关键字
            cascadeBlendKeywords, (int)shadowSettings.directional.cascadeBlend - 1);
        
        buffer.SetGlobalVector(shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize));
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }


    void SetKeywords(string[] keywords, int enabledIndex)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            if (i == enabledIndex)
            {
                buffer.EnableShaderKeyword(keywords[i]);
            }
            else
            {
                buffer.DisableShaderKeyword(keywords[i]);
            }
        }
    }

    /// <summary>
    ///     SetViewport 默认情况下，在渲染目标更改后，视口将设置为包含整个渲染目标。 此命令可用于将未来的渲染限制为指定的像素矩形。
    /// </summary>
    Vector2 SetTileViewport(int index, int split, float tileSize)
    {
        var offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
        return offset;
    }


    void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
    {
        float texelSize = 2f * cullingSphere.w / tileSize;
        float filterSize = texelSize * ((float)shadowSettings.directional.filterMode + 1f);
        cullingSphere.w -= filterSize;
        cullingSphere.w *= cullingSphere.w;
        cascadeCullingSpheres[index] = cullingSphere;
        cascadeData[index] = new Vector4(1 / cullingSphere.w, filterSize * 1.4142136f);
    }

    /// <summary>
    ///     shadowDrawSettings 此结构描述使用哪种拆分设置 (splitData) 渲染哪个阴影光源 (lightIndex)。
    ///     https://docs.unity.cn/cn/2019.4/ScriptReference/Rendering.ShadowDrawingSettings.html
    ///     ComputeDirectionalShadowMatricesAndCullingPrimitives可以帮助我们实现无限远的光照矩阵计算。（当然也可以自己去算）
    ///     DrawShadows为unity 内部底层代码实现。
    ///     SetGlobalDepthBias 中，前者是正常的深度偏移，后者则是斜度偏移， 原因：三角形越斜，深度沿屏幕变化越快，量化误差 / 采样误差越容易把“本来在表面”的点打成“被自己挡住”，
    ///     shadowCascadeBlendCullingFactor : 控制两个级联阴影中的重叠区域 每个区域扩张 shadowCascadeBlendCullingFactor * 当前 cascade
    ///     深度长度。因此越大过渡区域越大，此时消耗越大。
    /// </summary>
    void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        ShadowedDirectionalLight light = shadowedDirectionalLights[index];
        var shadowDrawSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex, BatchCullingProjectionType.Orthographic);

        int cascadeCount = shadowSettings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = shadowSettings.directional.CascadeRatios;

        float cullingFactor = Mathf.Max(0f, 0.8f - shadowSettings.directional.cascadeFade);

        for (int i = 0; i < cascadeCount; i++)
        {
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.visibleLightIndex, i, cascadeCount, ratios, tileSize, light.nearPlaneOffset,
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix, out ShadowSplitData splitData);
            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            shadowDrawSettings.splitData = splitData;

            if (index == 0)
                SetCascadeData(i, splitData.cullingSphere, tileSize);

            int tileIndex = tileOffset + i;
            dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, SetTileViewport(tileIndex, split, tileSize), split);
            buffer.SetGlobalInt(cascadeCountId, shadowSettings.directional.cascadeCount);
            buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
            buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowDrawSettings);
            buffer.SetGlobalDepthBias(0f, 0f);
        }
    }


    /// <summary>
    ///     下面的变换需要详细说下。
    ///     1. 逆转z值，这个好说，平台问题
    ///     2. 从第一行到第三行， 进行了 * 0.5 + 0.5 * 第四行，ndc从（-1，1）到（0，1）
    ///     3. 乘以scale ，因为进行了分块。同时之影响 xy 值，不影响深度
    /// </summary>
    /// <param name="m"></param>
    /// <param name="offset"></param>
    /// <param name="split"></param>
    /// <returns></returns>
    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
    {

        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
        float scale = 1f / split;
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);
        return m;
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        ExecuteBuffer();
    }
}
