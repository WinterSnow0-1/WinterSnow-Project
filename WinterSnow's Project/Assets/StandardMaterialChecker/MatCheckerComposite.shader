Shader "UI/MatCheckerComposite"
{
    Properties
    {
        _MainTex ("Color RT", 2D) = "black" {}
        _MaskTex ("Mask RT", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            ZWrite Off
            Cull Off
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            sampler2D _MainTex;
            sampler2D _MaskTex;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                fixed4 m = tex2D(_MaskTex, i.uv);
                float a = m.a;
                a = smoothstep(0.5, 0.98, a);
                c.a = a;
                c.rgb *= c.a;
                return c;
            }
            ENDHLSL
        }
    }
}
