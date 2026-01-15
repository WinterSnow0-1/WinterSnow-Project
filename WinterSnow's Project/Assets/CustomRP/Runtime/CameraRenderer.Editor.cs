using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
public partial class CameraRenderer
{

    //在所有的模式下都有定义，避免报错
    partial void DrawUnsupportedShaders();
    
    //只有在编辑器模式下使用，这样方便我们去查看哪些老的材质尚未更新
#if UNITY_EDITOR
    static readonly ShaderTagId[] unsupportedShaderTagIds ={
        new ShaderTagId("Always"),        
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };
    
    static Material errorMaterial;
    
    partial void DrawUnsupportedShaders()
    {
        if (errorMaterial == null)
        {
            errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }
        var drawingSettings = new DrawingSettings(unsupportedShaderTagIds[0], new SortingSettings(camera))
        {
            overrideMaterial = errorMaterial
        };
        var filteringSettings = new FilteringSettings(RenderQueueRange.all);
        for(int i = 1; i < unsupportedShaderTagIds.Length;i++)
        {
            drawingSettings.SetShaderPassName(i,unsupportedShaderTagIds[i]);
        }
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }
#endif
    
    
    partial void DrawGizmos();
    
    partial void DrawGizmosBeforeFX ();

    partial void DrawGizmosAfterFX ();
    
#if UNITY_EDITOR
    /// <summary>
    /// 同时绘制后处理前后的 gizmos ;
    /// 某一个具体 Gizmo 属于哪个子集，是 Unity 自己分类的，脚本层没有 API 改。
    /// </summary>
    partial void DrawGizmos()
    {
        if (Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera,GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera,GizmoSubset.PostImageEffects);
        }
    }
    
    partial void DrawGizmosBeforeFX () {
        if (Handles.ShouldRenderGizmos()) {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            //context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
    }

    partial void DrawGizmosAfterFX () {
        if (Handles.ShouldRenderGizmos()) {
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
    }
    
#endif


    partial void PrepareForSceneWindow();
    
#if UNITY_EDITOR
    partial void PrepareForSceneWindow()
    {
        if (camera.cameraType == CameraType.SceneView)
        {
            //将 UI 几何形状发射到 Scene 视图以进行渲染。
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }
    }
#endif

    partial void PrepareBuffer();
    
#if UNITY_EDITOR    
    string SampleName { get; set; }

    partial void PrepareBuffer()
    {
        Profiler.BeginSample("Editor Only");
        buffer.name = SampleName = camera.name;
        Profiler.EndSample();
    }
#else
    const string SampleName = bufferName;
#endif
    
}
