#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED

#define MIN_REFLECTIVITY 0.04

struct BRDFData
{
    float3 diffuse;
    float3 specular;
    float preRoughness;
    float roughness;
    float oneMinusReflectivity;
};

float OneMinusReflectivity(float metallic)
{
    float range = 1 - MIN_REFLECTIVITY;
    return range - metallic * range;
}

float SpecularStrength(CustomSurfaceData surface,BRDFData brdf,CustomLight light)
{
    float3 h = SafeNormalize(light.direction + surface.vDir);
    float nh2 = Square(saturate(dot(surface.normal,h)));
    float lh2 = Square(saturate(dot(light.direction,h)));
    float r2 = Square(brdf.roughness);
    float d2 = Square(nh2 * (r2 - 1) + 1.0001);
    float normalization = brdf.roughness * 4 + 2;
    return r2 / (d2 * max(0.1,lh2) * normalization);
}

float3 CustomDirectBDRF(CustomSurfaceData surface,BRDFData brdf,CustomLight light)
{
    return SpecularStrength(surface,brdf,light) * brdf.specular + brdf.diffuse;
}

BRDFData GetBRDF(CustomSurfaceData surfaceData, bool applyAlphaToDiffuse = false)
{
    BRDFData brdf;
    brdf.oneMinusReflectivity =OneMinusReflectivity(surfaceData.metallic);
    brdf.diffuse = surfaceData.color * brdf.oneMinusReflectivity;
    if (applyAlphaToDiffuse)
	    brdf.diffuse *= surfaceData.alpha;
    brdf.specular = lerp(MIN_REFLECTIVITY,surfaceData.color,surfaceData.metallic);
    brdf.preRoughness = PerceptualSmoothnessToPerceptualRoughness(surfaceData.smoothness);
    brdf.roughness = PerceptualRoughnessToRoughness(brdf.preRoughness);
    return brdf;
}
#endif