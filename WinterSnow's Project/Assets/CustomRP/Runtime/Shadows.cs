using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    const string bufferName = "Shadows";
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    ScriptableRenderContext context;

    CullingResults cullingResults;

    ShadowSettings shadowSettings;

    const int maxShadowedDirectionalLightCount = 4;
    
    static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
        dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");
    
    static Matrix4x4[]
        dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount];
    
    int ShadowedDirectionalLightCount;

    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
    }
    
    ShadowedDirectionalLight[] shadowedDirectionalLights = new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];

    public Vector2 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        /// GetShadowCasterBounds bool 如果光源影响了场景中至少一个阴影投射对象，则为 true。https://docs.unity.cn/cn/2019.4/ScriptReference/Rendering.CullingResults.GetShadowCasterBounds.html
        /// 函数所做：1. 找到当前灯光所照到的物体上的，cast shadow 不等于 off 的
        ///         2. 挑出可能把阴影投影到当前摄像机可见区域的物体
        ///         3. 求出这些物体的 世界空间下的  轴对齐包围盒  如果没有，则返回false
        if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount
            && light.shadows != LightShadows.None && light.shadowStrength > 0f
            && cullingResults.GetShadowCasterBounds(visibleLightIndex,out Bounds outBounds))
        {
            shadowedDirectionalLights[ShadowedDirectionalLightCount] = new ShadowedDirectionalLight
            {
                visibleLightIndex = visibleLightIndex
            };
            return new Vector2(light.shadowStrength, ShadowedDirectionalLightCount++);
        }
        return Vector2.zero;
    }

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings)
    {
        ShadowedDirectionalLightCount = 0;
        this.cullingResults = cullingResults;
        this.shadowSettings = shadowSettings;
        this.context = context;
    }

    public void Render()
    {
        if (ShadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else //就算不使用，也要生成对应格式的临时纹理，避免格式不对报错。我们当然也可以声明关键字来避免该情况。这里只是简单的生成一个1x1的临时纹理。
        {
            buffer.GetTemporaryRT(dirShadowAtlasId,1,1,32,FilterMode.Bilinear,RenderTextureFormat.Shadowmap);
        }
        
    }

    /// <summary>
    /// RenderTextureFormat.Shadowmap GPU自动进行阴影贴图比较的一种格式。 https://docs.unity.cn/cn/2019.4/ScriptReference/RenderTextureFormat.Shadowmap.html
    /// 我们生成后，需要将摄像机的输出存储到纹理上，同时我们并不关心初始状态，因为我们会立即清除。但是我们需要存储。
    /// </summary>
    void RenderDirectionalShadows()
    {
        int atlasSize = (int)shadowSettings.directional.atlasSize;
        buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize,
            32, FilterMode.Bilinear,RenderTextureFormat.Shadowmap);
        buffer.SetRenderTarget(
            dirShadowAtlasId, RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true,true,Color.clear);
        
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int split = ShadowedDirectionalLightCount <= 1 ? 1 : 2;
        int tileSize = atlasSize / split;
        for(int i=0;i<ShadowedDirectionalLightCount;i++)
            RenderDirectionalShadows(i,split,tileSize);
        
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }
    
    /// <summary>
    /// SetViewport 默认情况下，在渲染目标更改后，视口将设置为包含整个渲染目标。 此命令可用于将未来的渲染限制为指定的像素矩形。
    /// </summary>
    Vector2 SetTileViewport (int index, int split,float tileSize) {
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(
            offset.x * tileSize, offset.y * tileSize , tileSize , tileSize));
        return offset;
    }

    /// <summary>
    /// shadowDrawSettings 此结构描述使用哪种拆分设置 (splitData) 渲染哪个阴影光源 (lightIndex)。 https://docs.unity.cn/cn/2019.4/ScriptReference/Rendering.ShadowDrawingSettings.html
    /// ComputeDirectionalShadowMatricesAndCullingPrimitives可以帮助我们实现无限远的光照矩阵计算。（当然也可以自己去算）
    /// DrawShadows为unity 内部底层代码实现。
    /// </summary>
    void RenderDirectionalShadows(int index,int split, int tileSize)
    {
        ShadowedDirectionalLight light = shadowedDirectionalLights[index];
        var shadowSettings = new ShadowDrawingSettings(cullingResults,light.visibleLightIndex,BatchCullingProjectionType.Orthographic);
        cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
            light.visibleLightIndex, 0, 1, Vector3.zero, tileSize, 0f,
            out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
            out ShadowSplitData splitData
        );
        shadowSettings.splitData = splitData;
        dirShadowMatrices[index] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, SetTileViewport(index, split, tileSize), split);
        buffer.SetGlobalMatrixArray(dirShadowMatricesId,dirShadowMatrices);
        buffer.SetViewProjectionMatrices(viewMatrix,projectionMatrix);
        ExecuteBuffer();
        context.DrawShadows(ref shadowSettings);
    }
    
    
    /// <summary>
    /// 下面的变换需要详细说下。
    /// 1. 逆转z值，这个好说，平台问题
    /// 2. 从第一行到第三行， 进行了 * 0.5 + 0.5 * 第四行，ndc从（-1，1）到（0，1）
    /// 3. 乘以scale ，因为进行了分块。同时之影响 xy 值，不影响深度 
    /// </summary>
    /// <param name="m"></param>
    /// <param name="offset"></param>
    /// <param name="split"></param>
    /// <returns></returns>
    Matrix4x4 ConvertToAtlasMatrix (Matrix4x4 m, Vector2 offset, int split) {
        
        if (SystemInfo.usesReversedZBuffer) {
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