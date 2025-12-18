
Shader "Hidden/ImpostorBake/Normal"
{
    Properties
    {
        _BaseMap("BaseMap", 2D) = "white" {}
        _MainTex("MainTex", 2D) = "white" {}
        _UseBaseMap("UseBaseMap", Float) = 1
        _UseAlphaClip("UseAlphaClip", Float) = 1
        _Cutoff("Cutoff", Float) = 0.5
        _FallbackCutoff("FallbackCutoff", Float) = 0.5
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

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos  : SV_POSITION;
                float3 nWS  : TEXCOORD0;
                float2 uvB  : TEXCOORD1;
                float2 uvM  : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.nWS = UnityObjectToWorldNormal(v.normal);
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

                float3 n = normalize(i.nWS);
                return float4(n * 0.5 + 0.5, 1);
            }
            ENDCG
        }
    }
}