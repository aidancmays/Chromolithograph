// Chromolithograph style hints for hand-written shaders.
//
// The Chromolithograph feature draws every renderer whose shader has a pass tagged "LightMode" = "ChromoHints" into its
// style buffer (one style id per pixel, 0 = the palette's plain Look), after the frame is rendered. Use it when a
// shader should choose a style per pixel itself, for example the terrain picking the style of its strongest material.
// For a whole object with one style, a ChromoStyled component is simpler and needs no shader change. When an object
// has both, the ChromoStyled wins and the shader's pass is switched off for it (ChromoStyleOwners).
//
// Style ids come from Chromolithograph.ChromoStyles.Acquire(style) and stay the same while held, so C# can keep
// them in material properties (for example a float array indexed by material slot).
//
// The pass draws after post-processing, so it tests against the camera's depth texture by hand and must place
// vertices with ChromoHintsClipPos, not the usual view-projection matrix. A pass looks like this:
//
//     Pass
//     {
//         Name "ChromoHints"
//         Tags { "LightMode" = "ChromoHints" }
//         ZWrite Off ZTest Always Cull Back Blend Off
//
//         HLSLPROGRAM
//         #pragma vertex HintsVert
//         #pragma fragment HintsFrag
//         #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
//         #include "Packages/com.aidancmays.chromolithograph/ShaderLibrary/ChromoHints.hlsl"
//
//         struct HintsVaryings { float4 positionCS : SV_POSITION; float eyeDepth : TEXCOORD0; /* your own data */ };
//
//         HintsVaryings HintsVert(Attributes input)
//         {
//             HintsVaryings o;
//             float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
//             o.positionCS = ChromoHintsClipPos(positionWS);
//             o.eyeDepth = ChromoHintsEyeDepth(positionWS);
//             return o;
//         }
//
//         float4 HintsFrag(HintsVaryings input) : SV_Target
//         {
//             float styleId = /* your choice, 0-15 */ 0;
//             return ChromoHintsOutput(input.positionCS, input.eyeDepth, styleId);
//         }
//         ENDHLSL
//     }
//
// Shaders that move their own vertices (wind sway, vertex animation) also want a "ChromoMotion" pass, or the print's
// grain, paper and hatching, which are mapped from world position, slide across them as they move. It is drawn the
// same way (ChromoHintsClipPos, ChromoHintsEyeDepth), at the moved position, and returns
// ChromoMotionOutput(positionCS, eyeDepth, texturePos, textureNormal) with the position and normal the texture should
// come from: the unmoved world position and normal (for foliage, before the wind is added). Use
//     Tags { "LightMode" = "ChromoMotion" }  ZWrite Off ZTest Always Cull Off Blend Off
// and declare the fragment as returning ChromoMotionTargets (no SV_Target on the function; the struct carries them).
// A ChromoMoving component on the same object takes precedence.
//
// Keep the pass's material properties in the same UnityPerMaterial buffer as the shader's other passes (SRP batcher).
// Instanced draws (Graphics.RenderMeshIndirect) work the same way: read the instance in HintsVert as the other passes do.

#ifndef CHROMOLITHOGRAPH_HINTS_INCLUDED
#define CHROMOLITHOGRAPH_HINTS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

float4x4 _ChromoView;     // set by the feature for the style pass
float4x4 _ChromoViewProj;

float4 ChromoHintsClipPos(float3 positionWS)
{
    return mul(_ChromoViewProj, float4(positionWS, 1.0));
}

float ChromoHintsEyeDepth(float3 positionWS)
{
    return -mul(_ChromoView, float4(positionWS, 1.0)).z;
}

// The value to write for this style id, or discards the fragment when something nearer hides it.
float4 ChromoHintsOutput(float4 positionCS, float eyeDepth, float styleId)
{
    float scene = LinearEyeDepth(LoadSceneDepth(uint2(positionCS.xy)), _ZBufferParams);
    if (eyeDepth > scene * 1.01 + 0.03) discard;
    return float4(round(styleId) / 255.0, 0.0, 0.0, 0.0); // G (subject) is drawn after, by the feature
}

struct ChromoMotionTargets
{
    float4 position : SV_Target0; // texture-space position, a = 1
    float4 normal : SV_Target1;   // texture-space normal
};

// What a ChromoMotion pass writes, or discards the fragment when something nearer hides it.
ChromoMotionTargets ChromoMotionOutput(float4 positionCS, float eyeDepth, float3 texturePos, float3 textureNormal)
{
    float scene = LinearEyeDepth(LoadSceneDepth(uint2(positionCS.xy)), _ZBufferParams);
    if (eyeDepth > scene * 1.01 + 0.03) discard;
    ChromoMotionTargets o;
    o.position = float4(texturePos, 1.0);
    o.normal = float4(normalize(textureNormal + 1e-9), 0.0);
    return o;
}

#endif
