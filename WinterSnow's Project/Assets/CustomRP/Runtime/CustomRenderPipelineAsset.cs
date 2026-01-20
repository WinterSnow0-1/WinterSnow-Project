using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;
[CreateAssetMenu(menuName = "Rendering/CreateCustomRenderPipelineAsset")]
public class CustomRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField]
    ShadowSettings shadows = default;
    
    [SerializeField]
    bool useDynamicBatching = true, useGPUInstancing = true, useSRPBatching = true, useLightsPerObject = true;
    
    [SerializeField]
    bool allowHDR = true;
    
    [DrawWithUnity]
    public enum ColorLUTResolution { _16 = 16, _32 = 32, _64 = 64 }

    [SerializeField]
    ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;
    
    [SerializeField]
    PostFXSettings postFXSettings = default;
    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(allowHDR,useDynamicBatching,useGPUInstancing,useSRPBatching,useLightsPerObject, shadows, postFXSettings, (int)colorLUTResolution);
    }
}
