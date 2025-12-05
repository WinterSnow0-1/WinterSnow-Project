using UnityEngine;
using UnityEngine.Rendering;
public class CustomRenderPipeline : RenderPipeline
{
    readonly CameraRenderer renderer = new CameraRenderer();
    public CustomRenderPipeline()
    {
        GraphicsSettings.useScriptableRenderPipelineBatching = true;
    }
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach (Camera camera in cameras)
        {
            //context.SetupCameraProperties(camera);
            /// 注意，避免直接调用camera.Render();
            /// unity官方希望所有渲染都只在Render 入口走一次。
            /// 因此在SPR管线中，其回调当前RenderPipeline的render，形成死循环。该代码写入C++底层
            /// SRP 下的正确模式是：
            /// 1.只用 ScriptableRenderContext + CommandBuffer 渲染
            /// 2.Camera 只是“数据”（视角、投影矩阵等），
            /// 3.不再用 camera.Render() 这种旧式“一键帮你画完”的 API。
            renderer.Render(context, camera);
        }
    }

}
