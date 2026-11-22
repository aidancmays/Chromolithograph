// Writes into the chromolithograph style buffer (R = style id, G = subject, B = corruption). Drawn by
// ChromolithographFeature after the frame is rendered, so it tests against the camera's depth texture by hand: a
// fragment hidden behind something else is dropped.
// Pass 0: a ChromoStyled renderer's style id (R only, so subject and corruption marks underneath stay).
// Pass 1: a ChromoSubject renderer's mark (G only, so the style underneath stays).
// Pass 2: a ChromoCorrupted renderer's amount (B only, the most of what's there and this, so overlapping sources add up
// to the strongest).
// Lives in a Resources folder so builds include it.
Shader "Hidden/Chromolithograph/ChromoHints"
{
    HLSLINCLUDE
    #pragma target 3.5
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

    float _ChromoStyleId; // set globally per draw, so not a material property
    float _ChromoCorruptAmount; // likewise
    float4x4 _ChromoView;
    float4x4 _ChromoViewProj;

    struct Attributes { float4 positionOS : POSITION; };
    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float eyeDepth : TEXCOORD0;
    };

    Varyings Vert(Attributes input)
    {
        Varyings o;
        float4 world = mul(UNITY_MATRIX_M, float4(input.positionOS.xyz, 1.0));
        o.positionCS = mul(_ChromoViewProj, world);
        o.eyeDepth = -mul(_ChromoView, world).z;
        return o;
    }

    void DepthTest(Varyings input)
    {
        float scene = LinearEyeDepth(LoadSceneDepth(uint2(input.positionCS.xy)), _ZBufferParams);
        if (input.eyeDepth > scene * 1.01 + 0.03) discard;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "ChromoHints"
            ZWrite Off ZTest Always Cull Off Blend Off ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings input) : SV_Target
            {
                DepthTest(input);
                return float4(_ChromoStyleId / 255.0, 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Subject"
            ZWrite Off ZTest Always Cull Off Blend Off ColorMask G

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings input) : SV_Target
            {
                DepthTest(input);
                return float4(0.0, 1.0, 0.0, 0.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Corruption"
            ZWrite Off ZTest Always Cull Off Blend One One BlendOp Max ColorMask B

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings input) : SV_Target
            {
                DepthTest(input);
                return float4(0.0, 0.0, saturate(_ChromoCorruptAmount), 0.0);
            }
            ENDHLSL
        }
    }
}
