#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/SurfaceData.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

#pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
#pragma multi_compile_instancing

v2f LitPassVertex(a2v v)
{
    v2f o;
    //instance branch
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    o.positionCS = TransformObjectToHClip(v.vertex);
    float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
    o.uv = baseST.xy * v.uv + baseST.zw;
    o.vertexColor = v.vertexColor;
    o.normal = TransformObjectToWorldNormal(v.normal);
    o.tangent = TransformObjectToWorldDir(v.tangent);
    o.bitangent = cross(o.normal, o.tangent);
    o.posWS = mul(unity_ObjectToWorld, v.vertex);
    o.screenUV = ComputeScreenPos(o.positionCS);
    return o;
}

float4 LitPassFragment(v2f i) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(i);
    float4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseCol);
    float smoothness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);
    float metallic = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic);
    CustomSurfaceData surface = GenSurfaceData(i, col, smoothness, metallic);

    BRDFData brdf = GetBRDF(surface);
    float3 result = GetLighting(surface, brdf);

    return float4(result, surface.alpha);
}

#endif
