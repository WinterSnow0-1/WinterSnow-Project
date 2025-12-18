Shader "Hidden/ImpostorBake/Depth"
{
    Properties
    {
        _BaseMap("BaseMap", 2D) = "white" {}
        _MainTex("MainTex", 2D) = "white" {}
        _UseBaseMap("UseBaseMap", Float) = 1
        _UseAlphaClip("UseAlphaClip", Float) = 1
        _Cutoff("Cutoff", Float) = 0.5
        _FallbackCutoff("FallbackCutoff", Float) = 0.5

        _ImpostorNear("Near", Float) = 0.01
        _ImpostorFar("Far", Float) = 100
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            ZWrite On
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _BaseMap;
            sampler2D _MainTex;
            float4 _BaseMap_ST;
            float4 _MainTex_ST;

            float _UseBaseMap;
            float _UseAlphaClip;
            float _Cutoff;
            float _FallbackCutoff;

            float _ImpostorNear;
            float _ImpostorFar;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float3 vPos  : TEXCOORD0; // view space
                float2 uvB   : TEXCOORD1;
                float2 uvM   : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos  = UnityObjectToClipPos(v.vertex);
                o.vPos = UnityObjectToViewPos(v.vertex);
                o.uvB = TRANSFORM_TEX(v.uv, _BaseMap);
                o.uvM = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (_UseAlphaClip > 0.5)
                {
                    float cutoff = (_Cutoff > 0.0001) ? _Cutoff : _FallbackCutoff;
                    float2 uv = lerp(i.uvM, i.uvB, saturate(_UseBaseMap));
                    fixed a = lerp(tex2D(_MainTex, uv).a, tex2D(_BaseMap, uv).a, saturate(_UseBaseMap));
                    clip(a - cutoff);
                }

                // Unity view space：前方通常是 -Z，所以用 -i.vPos.z
                float d = (-i.vPos.z - _ImpostorNear) / max(1e-5, (_ImpostorFar - _ImpostorNear));
                d = saturate(d);

                return float4(d, d, d, 1);
            }
            ENDCG
        }
    }
}
