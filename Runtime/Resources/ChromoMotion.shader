// Writes where a moving object's texture is mapped from (ChromoMoving): texture-space position (a = 1) and normal,
// into two targets the print reads instead of the world position and normal. Drawn by ChromolithographFeature after
// the frame is rendered, so it tests against the camera's depth texture by hand.
// Pass 0: rigid meshes (mesh-space position and normal, through the captured reference matrix).
// Pass 1: skinned meshes (the unskinned vertex read from the mesh's own vertex buffer by vertex index).
// Lives in a Resources folder so builds include it.
Shader "Hidden/Chromolithograph/ChromoMotion"
{
    HLSLINCLUDE
    #pragma target 4.5
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

    float4x4 _ChromoView;
    float4x4 _ChromoViewProj;
    float4x4 _ChromoMotionRef;        // mesh space -> texture space
    float4x4 _ChromoMotionRefNormal;  // the same for normals
    ByteAddressBuffer _ChromoRest;
    float4 _ChromoRestLayout;         // stride, position offset, position format, normal offset
    float _ChromoRestNormalFormat;    // -1 = no normals in the buffer

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float eyeDepth : TEXCOORD0;
        float3 texturePos : TEXCOORD1;
        float3 textureNormal : TEXCOORD2;
    };

    struct Output
    {
        float4 position : SV_Target0;
        float4 normal : SV_Target1;
    };

    Varyings Place(float3 positionOS, float3 restPos, float3 restNormal)
    {
        Varyings o;
        float4 world = mul(UNITY_MATRIX_M, float4(positionOS, 1.0));
        o.positionCS = mul(_ChromoViewProj, world);
        o.eyeDepth = -mul(_ChromoView, world).z;
        o.texturePos = mul(_ChromoMotionRef, float4(restPos, 1.0)).xyz;
        o.textureNormal = mul((float3x3)_ChromoMotionRefNormal, restNormal);
        return o;
    }

    Output Frag(Varyings input)
    {
        float scene = LinearEyeDepth(LoadSceneDepth(uint2(input.positionCS.xy)), _ZBufferParams);
        if (input.eyeDepth > scene * 1.01 + 0.03) discard;
        float3 n = input.textureNormal;
        // No normals to read: the face's own, from how the texture position changes across the screen.
        if (dot(n, n) < 1e-8) n = cross(ddy(input.texturePos), ddx(input.texturePos));
        Output o;
        o.position = float4(input.texturePos, 1.0);
        o.normal = float4(normalize(n + 1e-9), 0.0);
        return o;
    }

    float3 Read3(uint address, int format)
    {
        if (format == 0) return asfloat(_ChromoRest.Load3(address));
        uint2 h = _ChromoRest.Load2(address & ~3u);
        // Halves packed two to a word; an attribute can start halfway through one.
        if ((address & 3u) != 0) { h = uint2((h.x >> 16) | (h.y << 16), h.y >> 16); }
        return float3(f16tof32(h.x), f16tof32(h.x >> 16), f16tof32(h.y));
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Rigid"
            ZWrite Off ZTest Always Cull Back Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            Varyings Vert(Attributes input) { return Place(input.positionOS.xyz, input.positionOS.xyz, input.normalOS); }
            ENDHLSL
        }

        Pass
        {
            Name "Skinned"
            ZWrite Off ZTest Always Cull Back Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            struct Attributes { float4 positionOS : POSITION; uint vertexID : SV_VertexID; };
            Varyings Vert(Attributes input)
            {
                uint at = input.vertexID * (uint)_ChromoRestLayout.x;
                float3 restPos = Read3(at + (uint)_ChromoRestLayout.y, (int)_ChromoRestLayout.z);
                float3 restNormal = _ChromoRestNormalFormat >= 0.0 ? Read3(at + (uint)_ChromoRestLayout.w, (int)_ChromoRestNormalFormat) : 0.0;
                return Place(input.positionOS.xyz, restPos, restNormal);
            }
            ENDHLSL
        }
    }
}
