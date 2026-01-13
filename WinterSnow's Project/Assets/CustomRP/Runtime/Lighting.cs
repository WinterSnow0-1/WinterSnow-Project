using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
public class Lighting
{
    const string bufferName = "Lighting";

    readonly CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    const int maxDirLightCount = 4, maxOtherLightCount = 64;

    static readonly int dirLightCountId = Shader.PropertyToID("_DirectionalLightCount");
    static readonly int dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors");
    static readonly int dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections");
    static readonly int dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");
    static readonly int otherLightCountId = Shader.PropertyToID("_OtherLightCount");
    static readonly int otherLightColorsId = Shader.PropertyToID("_OtherLightColors");
    static readonly int otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions");
    static readonly int otherLightDirectionsId = Shader.PropertyToID("_OtherLightDirections");
    static readonly int otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles");
    static readonly int otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");

    static string lightsPerObjectKeyword = "_LIGHTS_PER_OBJECT";

    CullingResults cullingResults;

    static readonly Vector4[] dirLightColors = new Vector4[maxDirLightCount];
    static readonly Vector4[] dirLightDirections = new Vector4[maxDirLightCount];
    static readonly Vector4[] dirLightShadowData = new Vector4[maxDirLightCount];

    static readonly Vector4[] otherLightColors = new Vector4[maxOtherLightCount];
    static readonly Vector4[] otherLightPositions = new Vector4[maxOtherLightCount];
    static readonly Vector4[] otherLightDirections = new Vector4[maxOtherLightCount];
    static readonly Vector4[] otherLightSpotAngles = new Vector4[maxOtherLightCount];
    static readonly Vector4[] otherLightShadowData = new Vector4[maxOtherLightCount];
    readonly Shadows shadows = new Shadows();

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings,bool useLightsPerObject)
    {
        this.cullingResults = cullingResults;
        buffer.BeginSample(bufferName);
        shadows.Setup(context, cullingResults, shadowSettings);
        SetupLights(useLightsPerObject);
        shadows.Render();
        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public void SetupDirectionalLight(int index, int visibleIndex, ref VisibleLight visibleLight)
    {
        dirLightColors[index] = visibleLight.finalColor;
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        dirLightShadowData[index] = shadows.ReserveDirectionalShadows(visibleLight.light, visibleIndex);
    }

    void SetupPointLight(int index, int visibleIndex, ref VisibleLight visibleLight)
    {
        otherLightColors[index] = visibleLight.finalColor;
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;
        otherLightSpotAngles[index] = new Vector4(0f, 1f);
        Light light = visibleLight.light;
        otherLightShadowData[index] = shadows.ReserveOtherShadows(light, visibleIndex);
    }

    void SetupSpotLight(int index, int visibleIndex, ref VisibleLight visibleLight)
    {
        otherLightColors[index] = visibleLight.finalColor;
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;
        otherLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        

        Light light = visibleLight.light;
        float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
        float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
        float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
        otherLightSpotAngles[index] = new Vector4(angleRangeInv, -outerCos * angleRangeInv);
        otherLightShadowData[index] = shadows.ReserveOtherShadows(light, visibleIndex);
    }

    void SetupLights(bool useLightsPerObject)
    {
        
        //获取 所有可见光的索引
        NativeArray<int> indexMap = useLightsPerObject ? cullingResults.GetLightIndexMap(Allocator.Temp) : default;
        
        /// NativeArray https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Unity.Collections.NativeArray_1.html 不懂
        /// 可以看https://www.cnblogs.com/KillerAery/p/10586659.html 
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;

        int dirLightCount = 0, otherLightCount = 0;
        
        //为了消除不可见光的指数，我们需要做第二次循环
        int i;
        for (i = 0; i < visibleLights.Length; i++)
        {
            int newIndex = -1;
            VisibleLight visibleLight = visibleLights[i];
            switch (visibleLight.lightType)
            {
                case LightType.Directional:
                    if (dirLightCount < maxDirLightCount)
                        SetupDirectionalLight(dirLightCount++, i, ref visibleLight);
                    break;
                case LightType.Point:
                    if (otherLightCount < maxOtherLightCount)
                    {
                        newIndex = otherLightCount;
                        SetupPointLight(otherLightCount++, i, ref visibleLight);
                    }
                    break;
                case LightType.Spot:
                    if (otherLightCount < maxOtherLightCount)
                    {
                        newIndex = otherLightCount;
                        SetupSpotLight(otherLightCount++, i, ref visibleLight);
                    }
                    break;
            }
            
            
            if (useLightsPerObject)
                indexMap[i] = newIndex;
        }
        
        if (useLightsPerObject)
        {
            for (; i < indexMap.Length; i++)
                indexMap[i] = -1;
            cullingResults.SetLightIndexMap(indexMap);
            indexMap.Dispose();
            Shader.EnableKeyword(lightsPerObjectKeyword);
        }
        else
        {
            Shader.DisableKeyword(lightsPerObjectKeyword);
        }

        buffer.SetGlobalInt(dirLightCountId, dirLightCount);
        if (dirLightCount > 0)
        {
            buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
            buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
            buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
        }

        buffer.SetGlobalInt(otherLightCountId, otherLightCount);
        if (otherLightCount > 0)
        {
            buffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
            buffer.SetGlobalVectorArray(otherLightPositionsId, otherLightPositions);
            buffer.SetGlobalVectorArray(otherLightDirectionsId, otherLightDirections);
            buffer.SetGlobalVectorArray(otherLightSpotAnglesId, otherLightSpotAngles);
            buffer.SetGlobalVectorArray(otherLightShadowDataId, otherLightShadowData);
        }

    }

    public void Cleanup()
    {
        shadows.Cleanup();

    }

}
