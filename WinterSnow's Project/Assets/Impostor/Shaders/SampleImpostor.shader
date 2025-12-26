Shader "Unlit/SampleImpostor"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ColorAtlas ("Color Atlas", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _ALPHATEST_ON
            #pragma multi_compile_fragment _ _USE_NORMAL

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"


            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 posWS : TEXCOORD1;
            };

            sampler2D _MainTex;
            TEXTURE2D(_ColorAtlas);
            SAMPLER(sampler_ColorAtlas);
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                float4 objPos = float4(0,0,0,1);
                float3 viewPos = mul(UNITY_MATRIX_MV,objPos);
                viewPos.xy += v.uv - 0.5f;
                o.vertex = TransformWViewToHClip(viewPos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.posWS = mul(unity_ObjectToWorld,objPos);
                return o;
            }

            float2 FrameUV(float2 uv01, int ix, int iy, int yawSteps, int pitchSteps)
            {
                // uv01: 0..1 inside a tile
                float2 tileCount = float2(yawSteps, pitchSteps);
                return (uv01 + float2(ix, iy)) / tileCount;
            }
            
            float2 posToUV(float3 vDir)
            {
                // r = alen + blen 或者 2 - alen - blen
                float r = sqrt(1 - pow(vDir.y,2));
                
                bool up = vDir.y > 0;
                // r = alen + blen
                r = up? r : 2-r;
                // rPhi.y = angle
                float angle = atan2(vDir.z,vDir.x);
                if (angle<0)
                    angle += PI * 2;

                //计算使用的phi
                float tmpPhi = angle * 4 / PI;
                
                //判定落在哪个象限
                //area = angle / (1/2 * PI) = phi /2;
                int area = floor(tmpPhi / 2);
                float tmp =0;
                float2 ab = (1,1);
                switch (area)
                {
                    // 注意由于知道落在哪个象限，因此知道具体，ab的符号，也就可以根据 |a| + |b| 和原公式求出对应的 ab，再反推具体的uv。
                    case(0):
                        tmp = r * (tmpPhi - 1);
                        ab = float2(r-tmp,tmp+r);
                        break;
                    case(1):
                        tmp = r * (tmpPhi - 3);
                        ab = float2(- r - tmp, - tmp + r);
                        break;
                    case(2):
                        tmp = r * (tmpPhi - 5);
                        ab = float2(tmp - r, - (tmp + r));
                        break;
                    default:
                        tmp = r * (tmpPhi - 7);
                        ab = float2(r + tmp,tmp -r);
                        break;
                }
                ab /= 2;
                ab = (ab + 1)/2 + 0.01f;
                //return ab;
                return floor(ab*16)*0.0625f;
                return ab;
            }
            

            float4 frag (v2f i) : SV_Target
            {
                float3 vDirWS = normalize(i.posWS - _WorldSpaceCameraPos.xyz);
                float3 vDirOS = TransformWorldToObjectDir(vDirWS);
                vDirOS = normalize(vDirOS);

                float2 tmpUV = posToUV(vDirOS);
                tmpUV = clamp(tmpUV,0,1);
                float2 uv = i.uv * 0.0625f + tmpUV.xy;
                uv = clamp(uv,0,1);
                //return float4(pow(uv,2.2),0,1);
                float4 c00 = SAMPLE_TEXTURE2D(_ColorAtlas, sampler_ColorAtlas,  uv);
                return c00;
            }
            ENDHLSL
        }
    }
}
