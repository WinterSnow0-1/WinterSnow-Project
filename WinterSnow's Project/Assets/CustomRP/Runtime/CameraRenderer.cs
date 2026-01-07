using UnityEngine;
using UnityEngine.Rendering;
public partial class CameraRenderer
{
    ScriptableRenderContext context;

    Camera camera;

    const string bufferName = "Render Camera";
    readonly CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    CullingResults cullingResults;

    static readonly ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
    static readonly ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");

    Lighting lighting = new Lighting();
    
    public void Render(ScriptableRenderContext context, Camera camera,bool useDynamicBatching,bool useGPUInstancing, ShadowSettings shadowSettings)
    {
        this.context = context;
        this.camera = camera;
        
        PrepareBuffer();
        
        //此方法增添了几何体，因此需要在Cull前增加，来进行剔除处理
        PrepareForSceneWindow();

        if (!Cull(shadowSettings.maxDistance))
            return;

        buffer.BeginSample(SampleName);
        ExecuteBuffer();
        lighting.Setup(context,cullingResults,shadowSettings);
        buffer.EndSample(SampleName);
        setup();
        DrawVisbleGeometry(useDynamicBatching, useGPUInstancing);

        /// 对于SRP中不支持的着色器，我们会单独处理。
        /// 不支持主要包括
        /// 1. shaderTagId 不在 SRP处理中
        /// 2. 依赖build in 关系的光照
        ///     a. 依赖 Built in 的 lightMode / pass
        ///     b. 依赖 Built in 的 内置宏（灯光/阴影）UNITY_LIGHT_ATTENUATION （URP 作为一个具体的 SRP，实现了一层“兼容桥”，让一部分旧宏/旧习惯还能继续活下去）
        ///     c. 依赖Built - in 的标准， #pragma surface surf Standard fullforwardshadows
        ///     e. 使用GrabPass
        DrawUnsupportedShaders();
        
        DrawGizmos();

        lighting.Cleanup();
        Submit();
    }


    /// <summary>
    ///     具体的渲染显示
    ///     简单说下具体的关键点，之后转录到笔记中
    ///     1. CullingResults 决定有哪些物体可以画，DrawingSettings决定怎么画，FilteringSettings决定画谁
    ///     2. DrawingSettings 关键点： sortingsettings 设置渲染顺序， ShaderTagId设置渲染物体的材质，
    ///     当物体材质不在List[ShaderTagId]中时，使用fallbackMaterial作为失败时调用
    ///     <![CDATA[API: https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Rendering.DrawingSettings.html]]>
    ///     3. FilteringSettings: 我们可以自定义渲染区间，进行特殊效果渲染。也可以控制LayerMask来单独绘制某个layer的特殊pass
    ///     <![CDATA[API:https://docs.unity3d.com/ScriptReference/Rendering.FilteringSettings.html]]>
    ///     4. PerObjectData 同时unity在setup阶段额外为每个物体准备何种数据，此时开启LIGHTMAP_ON关键字
    /// </summary>
    void DrawVisbleGeometry(bool useDynamicBatching,bool useGPUInstancing)
    {
        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };

        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings)
        {
            enableInstancing = useGPUInstancing,
            enableDynamicBatching = useDynamicBatching,
            perObjectData = PerObjectData.Lightmaps  |  PerObjectData.ShadowMask |  PerObjectData.OcclusionProbe | PerObjectData.LightProbe| PerObjectData.LightProbeProxyVolume |
                            PerObjectData.OcclusionProbeProxyVolume 
        };
        drawingSettings.SetShaderPassName(1,litShaderTagId);
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

        context.DrawSkybox(camera);

        //属性 'sortingSettings' 访问返回临时值。访问的结构未被分类为变量时不能修改结构成员
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

    }

    

    void setup()
    {
        buffer.BeginSample(SampleName);
        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;
        buffer.ClearRenderTarget(flags<= CameraClearFlags.Depth, flags == CameraClearFlags.Color,
            flags == CameraClearFlags.Color? camera.backgroundColor.linear : Color.black);
        ExecuteBuffer();
        context.SetupCameraProperties(camera);
    }

    void Submit()
    {
        buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();
    }

    /// 执行buffer 并不会清除buffer中的命令行
    /// 因此我们需要手动清除一次
    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }


    /// 裁剪函数
    /// 说明：
    /// 1. ScriptableCullingParameters 结构体包含 视锥体平面，shadow distance ，culling mask/layer，Lod相关数据，是否进行额外剔除检测；
    /// 2. 在内置渲染管线下（CG），unity只会返回一个副本，实际运行时仍是使用内置的数据
    /// 3. 在SRP中，我们可以获取对应数据，进行相关修改。
    /// 4. ScriptableCullingParameters是剔除规则，cullingResults是剔除后的结果
    bool Cull(float shadowMaxDistance)
    {
        ScriptableCullingParameters cullingParameters;
        if (camera.TryGetCullingParameters(out cullingParameters))
        {
            cullingParameters.shadowDistance = Mathf.Min(shadowMaxDistance,camera.farClipPlane);
            cullingResults = context.Cull(ref cullingParameters);
            return true;
        }
        return false;
    }

}
