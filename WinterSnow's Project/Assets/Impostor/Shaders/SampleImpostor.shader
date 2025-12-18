Shader "Impostor/SampleImpostor"
{
    Properties
    {
        _ColorAtlas ("Color Atlas", 2D) = "white" {}
        _NormalAtlas("Normal Atlas", 2D) = "bump" {}
        _UseNormal ("Use Normal(0/1)", Float) = 1

        _ySteps   ("Yaw Steps", Float) = 8
        _xSteps ("Pitch Steps", Float) = 4
        _PitchMin   ("Pitch Min", Float) = -20
        _PitchMax   ("Pitch Max", Float) = 60

        _Size ("Billboard Size (XY)", Vector) = (2,2,0,0)
        _Pivot("Pivot (UV space)", Vector) = (0.5,0.0,0,0)

        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.33
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _ALPHATEST_ON
            #pragma multi_compile_fragment _ _USE_NORMAL

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_ColorAtlas); SAMPLER(sampler_ColorAtlas);
            TEXTURE2D(_NormalAtlas); SAMPLER(sampler_NormalAtlas);

            float _UseNormal;
            float _ySteps, _xSteps;
            float _PitchMin, _PitchMax;
            float4 _Size;
            float4 _Pivot;
            float _Cutoff;


            #define PI 3.1415926f
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 posWS      : TEXCOORD1;
                float3 viewDirOS  : TEXCOORD2; // anchor/object-space view dir (for frame picking)
            };

            float2 FrameUV(float2 uv01, int ix, int iy, int ySteps, int xSteps)
            {
                // uv01: 0..1 inside a tile
                float2 tileCount = float2(ySteps, xSteps);
                return (uv01 + float2(ix, iy)) / tileCount;
            }

            //正常的等距拆分
            /*float4 SampleAtlasBilinear(float2 uv01, float3 viewDirOS)
            {
                int ySteps = (int)max(1, round(_ySteps));
                int xSteps = (int)max(1, round(_xSteps));

                float3 d = normalize(viewDirOS);

                // yaw: [-pi, pi] -> [0,1)
                float yaw = atan2(d.x, d.z); // right-handed
                float yaw01 = frac(yaw * (1.0 / (2.0 * PI)) + 1.0);

                // pitch in degrees: asin(y)
                float pitchDeg = degrees(asin(clamp(d.y, -1, 1)));
                float pitch01 = saturate((pitchDeg - _PitchMin) / max(1e-5, (_PitchMax - _PitchMin)));

                float fy = yaw01 * ySteps;
                int y0 = (int)floor(fy);
                float ty = frac(fy);
                int y1 = (y0 + 1) % ySteps;

                float fp = pitch01 * (xSteps - 1);
                int p0 = (int)floor(fp);
                float tp = frac(fp);
                int p1 = min(p0 + 1, xSteps - 1);

                // 你的烘焙代码：dstY = (xSteps - 1 - py) * tileSize
                // 所以 pitch index 需要翻转到 atlas 的行号
                int iy0 = (xSteps - 1 - p0);
                int iy1 = (xSteps - 1 - p1);

                float4 c00 = SAMPLE_TEXTURE2D(_ColorAtlas, sampler_ColorAtlas, FrameUV(uv01, y0, iy0, ySteps, xSteps));
                float4 c10 = SAMPLE_TEXTURE2D(_ColorAtlas, sampler_ColorAtlas, FrameUV(uv01, y1, iy0, ySteps, xSteps));
                float4 c01 = SAMPLE_TEXTURE2D(_ColorAtlas, sampler_ColorAtlas, FrameUV(uv01, y0, iy1, ySteps, xSteps));
                float4 c11 = SAMPLE_TEXTURE2D(_ColorAtlas, sampler_ColorAtlas, FrameUV(uv01, y1, iy1, ySteps, xSteps));

                float4 cx0 = lerp(c00, c10, ty);
                float4 cx1 = lerp(c01, c11, ty);
                return lerp(cx0, cx1, tp);
            }*/

            /// 八面体映射
            float4 SampleAtlasBilinear(float2 uv01, float3 viewDirOS)
            {
                int ySteps = (int)max(1, round(_ySteps));
                int xSteps = (int)max(1, round(_xSteps));

                float3 d = -normalize(viewDirOS);

                bool up = d.z >= 0;

                // 由 z 反推 r：z = ±(1 - r^2)  =>  r^2 = 1 - |z|
                float r = sqrt(clamp(1 - abs(d.z),0,1));
                
                // s = |u| + |v|
                float s = 0;
                if (up)
                    s = r;
                else
                    s = 2 - r;

                float theta = atan2(d.y,d.x);
                if (theta < 0)
                    theta += 2 * PI;
                //象限
                int q = (int)floor((theta * 2) / PI);
                
                float a = 0, b =0,t =0;

                switch (q)
                {
                    // a> 0 ,b>0
                    case(0):
                        t = (4 * theta - PI ) * r / PI;
                        a = (s - t)/2;
                        b = (s + t)/2;
                        break;
                    // a< 0 ,b>0
                    case(1):
                        t = (4 * theta - 3 * PI ) * r / PI;
                        a = (- s - t)/2;
                        b = (- s + t)/2;
                        break;
                    // a< 0 ,b<0
                    case(2):
                        t = (4 * theta - 5 * PI ) * r / PI;
                        a = (- s + t)/2;
                        b = (- s - t)/2;
                        break;
                    // a> 0 ,b<0
                    case(3):
                        t = (4 * theta - 7 * PI ) * r / PI;
                        a = (s + t)/2;
                        b = (s - t)/2;
                        break;
                }
                
                // 映射回 uv：[ -1..1 ] -> [ 0..1 ]
                float2 ab = float2(a, b);
                ab.x = clamp(ab.x, -1.0f, 1.0f);
                ab.y = clamp(ab.y, -1.0f, 1.0f);
                float2 uv = ab * 0.5f + 0.5f;

                int x0 = (int) floor(uv.x * (xSteps-1));
                int y0 = (int) floor(uv.y * (ySteps-1));
            
                /*
                float fy = uv.y * ySteps;
                int y0 = (int)floor(fy);
                float ty = frac(fy);
                int y1 = (y0 + 1) % ySteps;

                float fp = uv.x * (xSteps - 1);
                int p0 = (int)floor(fp);
                float tp = frac(fp);
                int p1 = min(p0 + 1, xSteps - 1);

                // 你的烘焙代码：dstY = (xSteps - 1 - py) * tileSize
                // 所以 pitch index 需要翻转到 atlas 的行号
                int iy0 = (xSteps - 1 - p0);
                int iy1 = (xSteps - 1 - p1);
                */

               // return float4(uv,0,1);

                float4 c00 = SAMPLE_TEXTURE2D(_ColorAtlas, sampler_ColorAtlas, FrameUV(uv01, x0, y0, ySteps, xSteps));
                return c00;
                    /*float4 c10 = SAMPLE_TEXTURE2D(_ColorAtlas, sampler_ColorAtlas, FrameUV(uv01, y1, iy0, ySteps, xSteps));
                    float4 c01 = SAMPLE_TEXTURE2D(_ColorAtlas, sampler_ColorAtlas, FrameUV(uv01, y0, iy1, ySteps, xSteps));
                    float4 c11 = SAMPLE_TEXTURE2D(_ColorAtlas, sampler_ColorAtlas, FrameUV(uv01, y1, iy1, ySteps, xSteps));

                    float4 cx0 = lerp(c00, c10, ty);
                    float4 cx1 = lerp(c01, c11, ty);
                    return lerp(cx0, cx1, tp);*/
            }


            float3 SampleNormalWSBilinear(float2 uv01, float3 viewDirOS)
            {
                int ySteps = (int)max(1, round(_ySteps));
                int xSteps = (int)max(1, round(_xSteps));

                float3 d = normalize(viewDirOS);

                float yaw = atan2(d.x, d.z);
                float yaw01 = frac(yaw * (1.0 / (2.0 * PI)) + 1.0);

                float pitchDeg = degrees(asin(clamp(d.y, -1, 1)));
                float pitch01 = saturate((pitchDeg - _PitchMin) / max(1e-5, (_PitchMax - _PitchMin)));

                float fy = yaw01 * ySteps;
                int y0 = (int)floor(fy);
                float ty = frac(fy);
                int y1 = (y0 + 1) % ySteps;

                float fp = pitch01 * (xSteps - 1);
                int p0 = (int)floor(fp);
                float tp = frac(fp);
                int p1 = min(p0 + 1, xSteps - 1);

                int iy0 = (xSteps - 1 - p0);
                int iy1 = (xSteps - 1 - p1);

                float3 n00 = SAMPLE_TEXTURE2D(_NormalAtlas, sampler_NormalAtlas, FrameUV(uv01, y0, iy0, ySteps, xSteps)).xyz * 2 - 1;
                float3 n10 = SAMPLE_TEXTURE2D(_NormalAtlas, sampler_NormalAtlas, FrameUV(uv01, y1, iy0, ySteps, xSteps)).xyz * 2 - 1;
                float3 n01 = SAMPLE_TEXTURE2D(_NormalAtlas, sampler_NormalAtlas, FrameUV(uv01, y0, iy1, ySteps, xSteps)).xyz * 2 - 1;
                float3 n11 = SAMPLE_TEXTURE2D(_NormalAtlas, sampler_NormalAtlas, FrameUV(uv01, y1, iy1, ySteps, xSteps)).xyz * 2 - 1;

                float3 nx0 = normalize(lerp(n00, n10, ty));
                float3 nx1 = normalize(lerp(n01, n11, ty));
                return normalize(lerp(nx0, nx1, tp));
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // billboard: 用 UV + pivot 控制尺寸与锚点（不依赖网格顶点位置）
                float2 offset = (IN.uv - _Pivot.xy) * _Size.xy;

                float3 centerWS = TransformObjectToWorld(float3(0,0,0));
                float3 camRightWS = normalize(float3(UNITY_MATRIX_I_V._m00, UNITY_MATRIX_I_V._m10, UNITY_MATRIX_I_V._m20));
                float3 camUpWS    = normalize(float3(UNITY_MATRIX_I_V._m01, UNITY_MATRIX_I_V._m11, UNITY_MATRIX_I_V._m21));
                float3 posWS = centerWS + camRightWS * offset.x + camUpWS * offset.y;

                OUT.posWS = posWS;
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.uv = IN.uv;

                // frame pick 用“物体坐标系”的相机方向（注意：不要被 billboard 旋转影响）
                float3 viewDirWS = GetCameraPositionWS() - centerWS;
                OUT.viewDirOS = TransformWorldToObjectDir(viewDirWS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float4 col = SampleAtlasBilinear(IN.uv, IN.viewDirOS);

                #if defined(_ALPHATEST_ON)
                    clip(col.a - _Cutoff);
                #endif

                // 简单 URP 主光 + 球谐环境（如果你有 normal atlas）
                /*if (_UseNormal > 0.5)
                {
                    float3 nWS = SampleNormalWSBilinear(IN.uv, IN.viewDirOS);

                    Light mainLight = GetMainLight();
                    float ndl = saturate(dot(nWS, mainLight.direction));
                    float3 ambient = SampleSH(nWS);

                    col.rgb = col.rgb * (ambient + mainLight.color * ndl);
                }*/

                return col;
            }
            ENDHLSL
        }
    }
}
