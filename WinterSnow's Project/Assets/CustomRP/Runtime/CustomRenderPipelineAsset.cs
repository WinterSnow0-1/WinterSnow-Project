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
    
    [SerializeField]
    PostFXSettings postFXSettings = default;
    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(allowHDR,useDynamicBatching,useGPUInstancing,useSRPBatching,useLightsPerObject, shadows, postFXSettings);
    }
}
