using UnityEngine;
using UnityEngine.Rendering;
public class Lighting
{
    const string bufferName = "Lighting";

    readonly CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    const int maxDirLightCount = 4;

    static readonly int dirLightCountId = Shader.PropertyToID("_DirectionalLightCount");
    static readonly int dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors");
    static readonly int dirLightDrectionsId = Shader.PropertyToID("_DirectionalLightDirections");
    static readonly int dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");

    CullingResults cullingResults;

    static readonly Vector4[] dirLightColors = new Vector4[maxDirLightCount];
    static readonly Vector4[] dirLightDirections = new Vector4[maxDirLightCount];
    static readonly Vector4[] dirLightShadowData = new Vector4[maxDirLightCount];

    readonly Shadows shadows = new Shadows();

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings)
    {
        this.cullingResults = cullingResults;
        buffer.BeginSample(bufferName);
        shadows.Setup(context, cullingResults, shadowSettings);
        SetupLights();
        shadows.Render();
        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public void SetupDirectionalLitght(int index, ref VisibleLight visibleLight)
    {
        dirLightColors[index] = visibleLight.finalColor;
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        dirLightShadowData[index] = shadows.ReserveDirectionalShadows(visibleLight.light, index);
    }

    void SetupLights()
    {
        /// NativeArray https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Unity.Collections.NativeArray_1.html 不懂
        /// 可以看https://www.cnblogs.com/KillerAery/p/10586659.html 
        var visibleLights = cullingResults.visibleLights;

        int dirLightCount = 0;
        for (int i = 0; i < visibleLights.Length; i++)
        {
            VisibleLight visibleLight = visibleLights[i];
            if (visibleLight.lightType == LightType.Directional)
            {
                //这里传递引用，因为visibleLight结构很大
                SetupDirectionalLitght(dirLightCount++, ref visibleLight);
                if (dirLightCount >= maxDirLightCount)
                    break;
            }
        }

        buffer.SetGlobalInt(dirLightCountId, dirLightCount);
        buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
        buffer.SetGlobalVectorArray(dirLightDrectionsId, dirLightDirections);
        buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
        buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);

    }

    public void Cleanup()
    {
        shadows.Cleanup();

    }

}
