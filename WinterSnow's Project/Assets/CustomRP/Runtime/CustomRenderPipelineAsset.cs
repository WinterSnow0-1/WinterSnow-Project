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
    PostFXSettings postFXSettings = default;
    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(useDynamicBatching,useGPUInstancing,useSRPBatching,useLightsPerObject, shadows, postFXSettings);
    }
}
