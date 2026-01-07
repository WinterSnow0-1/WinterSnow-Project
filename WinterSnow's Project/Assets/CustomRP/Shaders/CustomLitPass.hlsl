#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED


struct a2v //顶点着色器
{
    float4 vertex: POSITION;
    float4 normal: NORMAL;
    float3 tangent: TANGENT;
    half4 vertexColor: COLOR;
    float2 uv : TEXCOORD0;
    #if LIGHTMAP_ON
    float2 lightMapUV : TEXCOORD1;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f //片元着色器
{
    float4 positionCS: SV_POSITION;
    float2 uv: TEXCOORD0;
    float3 normal : TEXCOORD1;
    float3 tangent : TEXCOORD2;
    float3 bitangent : TEXCOORD3;
    float4 screenUV : TEXCOORD4;
    float3 posWS : TEXCOORD5;
    half4 vertexColor: COLOR;
    #if LIGHTMAP_ON
    float2 lightMapUV : VAR_LIGHT_MAP_UV;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID

};

v2f LitPassVertex(a2v v)
{
    v2f o;
    //instance branch
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    #if LIGHTMAP_ON
    o.lightMapUV = v.lightMapUV * unity_LightmapST.xy + unity_LightmapST.zw;
    #endif
    o.positionCS = TransformObjectToHClip(v.vertex);
    float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
    o.uv = baseST.xy * v.uv + baseST.zw;
    o.vertexColor = v.vertexColor;
    o.normal = TransformObjectToWorldNormal(v.normal);
    o.tangent = TransformObjectToWorldDir(v.tangent);
    o.bitangent = cross(o.normal, o.tangent);
    o.posWS = TransformObjectToWorld(v.vertex);
    return o;
}


CustomSurfaceData GenSurfaceData(v2f input, float4 col)
{
    CustomSurfaceData surface = (CustomSurfaceData)0;
    surface.normal = input.normal;
    surface.color = col.rgb;
    surface.alpha = col.a;
    surface.smoothness = GetSmoothness(input.uv);
    surface.metallic = GetMetallic(input.uv);
    surface.position = input.posWS;
    surface.depth = -TransformWorldToView(input.posWS).z;
    surface.dither = InterleavedGradientNoise(input.positionCS.xy, 0);
    surface.vDir = normalize(_WorldSpaceCameraPos - input.posWS);
    return surface;
}


float4 LitPassFragment(v2f i) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(i);

    ClipLOD(i.positionCS.xy, unity_LODFade.x);
    float4 col = GetBase(i.uv);
    CustomSurfaceData surface = GenSurfaceData(i, col);

    #if defined(_PREMULTIPLY_ALPHA)
    BRDFData brdf = GetBRDF(surface, true);
    #else
    BRDFData brdf = GetBRDF(surface);
    #endif

    GI gi;
    #if LIGHTMAP_ON
    gi = GetGI(i.lightMapUV,surface);
    #else
    gi = GetGI(0, surface);
    #endif
    float3 result = GetLighting(surface, brdf, gi);
    result += GetEmission(i.uv);
    return float4(result, surface.alpha);
}

#endif
