using UnityEngine;
using Unity.Collections;
using UnityEngine.Experimental.GlobalIllumination;
using LightType = UnityEngine.LightType;


// 注意unity中会将 Editor文件夹中的文件 编辑在  Assembly-CSharp-Editor 程序集
// 而主脚本会放在  Assembly-CShar 程序集，这会导致同一类不会读取到
public partial class CustomRenderPipeline 
{

    partial void InitializeForEditor ();
    
#if UNITY_EDITOR

    // 让Unity在编辑模式下调用我们覆写的函数
    partial void InitializeForEditor () {
        Lightmapping.SetDelegate(lightsDelegate);
    }
    
    
    /*protected override void Dispose (bool disposing) {
        base.Dispose(disposing);
        Lightmapping.ResetDelegate();
    }*/
    
    // 我们通过 RequestLightsDelegate函数 来获取光源的GI信息，对于哪些被跳过的灯光会调用 InitNoBake 来记录，而不会直接跳过。
    // 因为unity默认烘焙灯光时对于 spot 和 point 类型的灯光的强度处理有些问题。
    // 所以我们从这里覆写函数，修改NativeArray<LightDataGI> 对应的信息
    // 同时我们也相当于去重写了一遍如何去控制当前各种光源如何获取GI信息的。
    static Lightmapping.RequestLightsDelegate lightsDelegate =
        (Light[] lights, NativeArray<LightDataGI> output) =>
        {
            var lightData = new LightDataGI();
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                switch (light.type)
                {
                    case LightType.Directional:
                        var directionalLight = new DirectionalLight();
                        LightmapperUtils.Extract(light, ref directionalLight);
                        lightData.Init(ref directionalLight);
                        break;
                    case LightType.Spot:
                        var spotLight = new SpotLight();
                        LightmapperUtils.Extract(light, ref spotLight);
                        spotLight.innerConeAngle = light.innerSpotAngle * Mathf.Deg2Rad;
                        spotLight.angularFalloff = AngularFalloffType.AnalyticAndInnerAngle;
                        lightData.Init(ref spotLight);
                        break;
                    case LightType.Point:
                        var pointLight = new PointLight();
                        LightmapperUtils.Extract(light, ref pointLight);
                        lightData.Init(ref pointLight);
                        break;
                    case LightType.Area:
                        var rectangleLight = new RectangleLight();
                        LightmapperUtils.Extract(light, ref rectangleLight);
                        rectangleLight.mode = LightMode.Baked;
                        lightData.Init(ref rectangleLight);
                        break;
                    default:
                        lightData.InitNoBake(light.GetInstanceID());
                        break;
                }
                lightData.falloff = FalloffType.InverseSquared;
                output[i] = lightData;
            }
        };

#endif

}
