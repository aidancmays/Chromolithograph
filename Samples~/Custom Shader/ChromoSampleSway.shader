// A minimal shader that works with the Chromolithograph print:
//   - a plain lit-looking colour pass (the print redraws the frame, so the colour is what gets separated into inks),
//   - DepthOnly and DepthNormals passes (the print reads the camera's depth and normals textures),
//   - a ChromoHints pass that picks a print style per pixel (here: one style below a height, another above it),
//   - a ChromoMotion pass, because the vertices sway, so the print's grain stays stuck to the surface.
// Put ChromoSampleStyles on the object to fill in the style ids.
Shader "Chromolithograph Samples/Sway"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (0.6, 0.55, 0.4, 1)
        _SwayAmount ("Sway (m)", Float) = 0.15
        _SwaySpeed ("Sway speed", Float) = 1.5
        _SplitHeight ("Style split height (m, world)", Float) = 1
        [HideInInspector] _StyleLow ("Style id below", Float) = 0
        [HideInInspector] _StyleHigh ("Style id above", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _SwayAmount;
            float _SwaySpeed;
            float _SplitHeight;
            float _StyleLow;
            float _StyleHigh;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
        };

        // Bends the object sideways, more the higher above its pivot.
        float3 Sway(float3 positionWS)
        {
            float3 pivot = TransformObjectToWorld(float3(0, 0, 0));
            float h = max(0.0, positionWS.y - pivot.y);
            float s = sin(_Time.y * _SwaySpeed + pivot.x * 0.7 + pivot.z * 0.3);
            return positionWS + float3(s, 0, s * 0.5) * _SwayAmount * h * h;
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 positionWS = Sway(TransformObjectToWorld(input.positionOS.xyz));
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                Light light = GetMainLight();
                float ndl = saturate(dot(normalize(input.normalWS), light.direction));
                float3 colour = _BaseColor.rgb * (light.color * ndl + SampleSH(normalize(input.normalWS)));
                return float4(colour, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Vert(Attributes input) : SV_POSITION
            {
                return TransformWorldToHClip(Sway(TransformObjectToWorld(input.positionOS.xyz)));
            }

            float Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformWorldToHClip(Sway(TransformObjectToWorld(input.positionOS.xyz)));
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return float4(NormalizeNormalPerPixel(input.normalWS), 0);
            }
            ENDHLSL
        }

        // The print style per pixel (see ShaderLibrary/ChromoHints.hlsl).
        Pass
        {
            Name "ChromoHints"
            Tags { "LightMode" = "ChromoHints" }
            ZWrite Off ZTest Always Cull Back Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.aidancmays.chromolithograph/ShaderLibrary/ChromoHints.hlsl"

            struct Varyings { float4 positionCS : SV_POSITION; float eyeDepth : TEXCOORD0; float heightWS : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 rest = TransformObjectToWorld(input.positionOS.xyz);
                float3 positionWS = Sway(rest);
                o.positionCS = ChromoHintsClipPos(positionWS);
                o.eyeDepth = ChromoHintsEyeDepth(positionWS);
                o.heightWS = rest.y;
                return o;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float styleId = input.heightWS < _SplitHeight ? _StyleLow : _StyleHigh;
                return ChromoHintsOutput(input.positionCS, input.eyeDepth, styleId);
            }
            ENDHLSL
        }

        // Where the print's grain, paper and hatching come from: the unswayed surface.
        Pass
        {
            Name "ChromoMotion"
            Tags { "LightMode" = "ChromoMotion" }
            ZWrite Off ZTest Always Cull Off Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.aidancmays.chromolithograph/ShaderLibrary/ChromoHints.hlsl"

            struct Varyings { float4 positionCS : SV_POSITION; float eyeDepth : TEXCOORD0; float3 restWS : TEXCOORD1; float3 normalWS : TEXCOORD2; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 rest = TransformObjectToWorld(input.positionOS.xyz);
                float3 positionWS = Sway(rest);
                o.positionCS = ChromoHintsClipPos(positionWS);
                o.eyeDepth = ChromoHintsEyeDepth(positionWS);
                o.restWS = rest;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            ChromoMotionTargets Frag(Varyings input)
            {
                return ChromoMotionOutput(input.positionCS, input.eyeDepth, input.restWS, input.normalWS);
            }
            ENDHLSL
        }
    }
}
