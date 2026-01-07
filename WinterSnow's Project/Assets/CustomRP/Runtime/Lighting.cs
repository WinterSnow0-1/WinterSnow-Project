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
    static readonly int otherLightDirectionsId = Shader.PropertyToID("_OtherLightDrections");
    static readonly int otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles");


    CullingResults cullingResults;

    static readonly Vector4[] dirLightColors = new Vector4[maxDirLightCount];
    static readonly Vector4[] dirLightDirections = new Vector4[maxDirLightCount];
    static readonly Vector4[] dirLightShadowData = new Vector4[maxDirLightCount];

    static readonly Vector4[] otherLightColors = new Vector4[maxOtherLightCount];
    static readonly Vector4[] otherLightPositions = new Vector4[maxOtherLightCount];
    static readonly Vector4[] otherLightDirections = new Vector4[maxOtherLightCount];
    static readonly Vector4[] otherLightSpotAngles = new Vector4[maxOtherLightCount];
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

    void SetupPointLight(int index, ref VisibleLight visibleLight)
    {
        otherLightColors[index] = visibleLight.finalColor;
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;
        otherLightSpotAngles[index] = new Vector4(0f, 1f);
    }

    void SetupSpotLight(int index, ref VisibleLight visibleLight)
    {
        otherLightColors[index] = visibleLight.finalColor;
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;
        otherLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);

        float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.light.innerSpotAngle);
        float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
        float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
        otherLightSpotAngles[index] = new Vector4(angleRangeInv, -outerCos * angleRangeInv);
    }

    void SetupLights()
    {
        /// NativeArray https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Unity.Collections.NativeArray_1.html 不懂
        /// 可以看https://www.cnblogs.com/KillerAery/p/10586659.html 
        var visibleLights = cullingResults.visibleLights;

        int dirLightCount = 0, otherLightCount = 0;
        ;
        for (int i = 0; i < visibleLights.Length; i++)
        {
            VisibleLight visibleLight = visibleLights[i];
            switch (visibleLight.lightType)
            {
                case LightType.Directional:
                    if (dirLightCount < maxDirLightCount)
                        SetupDirectionalLitght(dirLightCount++, ref visibleLight);
                    break;
                case LightType.Point:
                    if (otherLightCount < maxOtherLightCount)
                        SetupPointLight(otherLightCount++, ref visibleLight);
                    break;
                case LightType.Spot:
                    if (otherLightCount < maxOtherLightCount)
                        SetupSpotLight(otherLightCount++, ref visibleLight);
                    break;
            }

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
        }

    }

    public void Cleanup()
    {
        shadows.Cleanup();

    }

}
