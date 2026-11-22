// Prints the frame as a chromolithograph. Driven by ChromolithographFeature, which sets every property each frame
// through a property block; see that class and ChromoPalette for what the numbers mean.
//
// Per pixel: each ink's coverage comes from the palette's separation table (two RGBA 3D textures, inks 0-3 and 4-7),
// looked up from the colour under that ink's registration offset. The coverage is faded by distance, compared with
// the ink's grain to lay a dot, and the dot multiplies the ink onto the paper. The key stone (outlines from depth and
// normals, hatching in deep shadow) prints last.
//
// Grain, paper and hatching are mapped onto the surfaces in world space, kept at a steady size on screen (see
// MakeSpace), so they stay on things as the camera moves.
//
// Pass 0 prints. Pass 1 only draws key lines onto the frame (portal views, which the main camera prints later).
// Lives in a Resources folder so builds include it.
Shader "Hidden/Chromolithograph/Chromolithograph"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

        #define MAX_INKS 8
        #define MAX_STYLES 16

        TEXTURE3D(_SeparationA);
        TEXTURE3D(_SeparationB);
        TEXTURE2D(_GrainA);
        TEXTURE2D(_GrainB);
        TEXTURE2D(_PaperTex);
        SAMPLER(chromo_linear_clamp_sampler);
        SAMPLER(chromo_linear_repeat_sampler);

        float4 _InkColor[MAX_INKS];   // linear rgb, density
        float4 _InkParams[MAX_INKS];  // grain scale, fade order, registration x, y (px at 1080p)
        float4 _InkParams2[MAX_INKS]; // grain boost
        float _InkCount;
        float4 _PaperColor;
        float4 _FoxColor;
        float4 _KeyColor;
        float4 _ShadowColor;   // deep shadow ink
        float4 _ChromoSizes;   // separation size, grain texture size, paper texture size
        float4 _PaperOffset;   // z = 1080 / screen height, w = world size of a 1080p pixel at 1 m depth
        static float4 _Print1;        // intensity, grain size, grain softness, registration
        static float4 _Print2;        // ink variation, paper fibre, foxing, sky ink
        static float4 _Print3;        // grain strength, wash blur (px at 1080p), saturation, contrast
        static float4 _Hand;          // hand colouring, even texture, texture depth, fog sky tint
        static float4 _Wet;           // wetness, wet bloom size, edge pooling, granulation
        static float4 _Sky;           // sky saturation, sky grain strength, sky grain size, exposure (stops)
        static float4 _Line1;         // width, strength, depth sensitivity, crease sensitivity
        static float4 _Line2;         // wobble, far distance, highlight break, deep shadow below
        static float4 _Line3;         // colour edges, colour sensitivity, depth gap (m), style outlines
        static float4 _Hatch;         // strength, spacing, below, deep shadow fill
        static float4 _Fade;          // start, end, amount, deep shadow softness
        static float4 _Night;         // night sky fill, below, softness
        float4 _NightSkyColor;        // the palette's night sky ink (linear)
        float4 _ChromoGlowSun;        // glow: towards the sun (world), amount
        float4 _ChromoHorizon;        // the sky's horizon colour (display-linear), w = 1 while the sky is running
        float4 _ChromoGlowUp;         // glow: local up (world)
        float4 _ChromoGlowShape;      // glow: width from the sun, height above the horizon (degrees)
        float4 _Proof;
        // Every setting above marked static comes per style: style 0 is the palette's Look, 1 and up the
        // ChromoStyles on screen, slot MAX_STYLES the sky's (ChromoSky's time of day) and MAX_STYLES + 1 the sunrise and
        // sunset glow's. LoadStyle copies this pixel's style into them at the start of each fragment; sky pixels then
        // switch to the sky slot, blended toward the glow's around the sun (LoadSky).
        float4 _SPrint1[MAX_STYLES + 2];
        float4 _SPrint2[MAX_STYLES + 2];
        float4 _SPrint3[MAX_STYLES + 2];
        float4 _SHand[MAX_STYLES + 2];
        float4 _SWet[MAX_STYLES + 2];
        float4 _SSky[MAX_STYLES + 2];
        float4 _SLine1[MAX_STYLES + 2];
        float4 _SLine2[MAX_STYLES + 2];
        float4 _SLine3[MAX_STYLES + 2];
        float4 _SHatch[MAX_STYLES + 2];
        float4 _SFade[MAX_STYLES + 2];
        float4 _SNight[MAX_STYLES + 2];
        float4 _Strip1;        // stripped-paint vignette: centre (0-1 on screen), to circle space x, y
        float4 _Strip2;        // clear radius, fade width (half screen heights), amount, raggedness
        float4 _Paint1;        // painted vignette: inner colour (linear), its opacity (0 = off)
        float4 _Paint2;        // edge colour (linear), its opacity
        float4 _Paint3;        // clear radius, fade width (half screen heights), edge colour from, how much of it is reached (the pulse)
        float4 _Paint4;        // wash raggedness, edge colour raggedness, edge colour hardness, grain
        float4 _Paint5;        // pooling, blotch size (half screen heights), blotch drift offset xy
        TEXTURE2D(_ChromoHints);    // R: style id per pixel (id / 255), 0 = the palette's Look; G: 1 on subjects;
                                    // B: how corrupted (ChromoCorrupted)
        float4 _ChromoSubject;      // subject hold, background fade (Look, not per style)
        // Corruption (Corruption Plan 3.2; the Look's Corruption settings, not per style).
        TEXTURE2D(_CrumpleTex);     // ChromoCrumple: R creases, G facet tone, B bloom field, A dendrite field
        float4 _Corrupt1;           // drain, crumple strength, crumple 1080p pixels per texel, crease darkness
        float4 _Corrupt2;           // crease lines, facet mottle, edge width, branching
        float4 _Corrupt3;           // wet front, blooms, 1 / bloom size (m), salt
        float4 _Corrupt4;           // bloom drift (m/s), bloom morph (per second), large layer, small layer
        float4 _CorruptDark;        // corruption inks (linear)
        float4 _CorruptMid;
        float4 _CorruptLight;
        float4 _CorruptBloom;       // inside the blooms (linear)
        float4 _CorruptBloomRim;    // the blooms' dried rim (linear)
        // The spreading corruption field (Corruption/CorruptionField): a top-down window of the world around the camera,
        // wrapping, one texel per cell. Set globally by the field; x = 0 when there is none.
        TEXTURE2D(_ChromoCorruptionMap);
        float4 _ChromoCorruptionMap1;  // 1 / window size (m), window min x, min z, on
        float4 _ChromoCorruptionMap2;  // window max x, max z
        // Moving objects (ChromoMoving): where they are on screen, the position and normal their texture is mapped from,
        // which travel with them instead of staying in the world. a = 1 where a moving object is drawn.
        TEXTURE2D_FLOAT(_ChromoMotionPos);
        TEXTURE2D(_ChromoMotionNormal);
        float _ChromoMotionOn;
        static int StyleId;         // mode (ChromoDebug.View), ink
        static float Subject;       // 1 where a ChromoSubject is drawn
        static float Corrupt;       // how corrupted this pixel's object is (0..1), before the feathered edge

        int StyleAt(float2 uv)
        {
            uint2 pixel = min(uint2(uv * _ScaledScreenParams.xy), uint2(_ScaledScreenParams.xy) - 1);
            return min((int)round(LOAD_TEXTURE2D(_ChromoHints, pixel).r * 255.0), MAX_STYLES - 1);
        }

        // Copies one slot's settings into the statics (a style id, or MAX_STYLES for the sky).
        void LoadSlot(int slot)
        {
            _Print1 = _SPrint1[slot];
            _Print2 = _SPrint2[slot];
            _Print3 = _SPrint3[slot];
            _Hand = _SHand[slot];
            _Wet = _SWet[slot];
            _Sky = _SSky[slot];
            _Line1 = _SLine1[slot];
            _Line2 = _SLine2[slot];
            _Line3 = _SLine3[slot];
            _Hatch = _SHatch[slot];
            _Fade = _SFade[slot];
            _Night = _SNight[slot];
        }

        // The sky's settings: the sky slot, blended toward the glow's near the sun and the horizon.
        void LoadSky(float2 uv)
        {
            LoadSlot(MAX_STYLES);
            [branch] if (_ChromoGlowSun.w > 0.0)
            {
                float3 dir = normalize(ComputeWorldSpacePosition(uv, 0.5, UNITY_MATRIX_I_VP) - _WorldSpaceCameraPos);
                float angle = degrees(acos(clamp(dot(dir, _ChromoGlowSun.xyz), -1.0, 1.0)));
                float elevation = degrees(asin(clamp(dot(dir, _ChromoGlowUp.xyz), -1.0, 1.0)));
                float across = angle / _ChromoGlowShape.x;
                float mask = _ChromoGlowSun.w * exp(-across * across) * (1.0 - smoothstep(0.0, _ChromoGlowShape.y, max(elevation, 0.0)));
                const int g = MAX_STYLES + 1;
                _Print1 = lerp(_Print1, _SPrint1[g], mask);
                _Print2 = lerp(_Print2, _SPrint2[g], mask);
                _Print3 = lerp(_Print3, _SPrint3[g], mask);
                _Hand = lerp(_Hand, _SHand[g], mask);
                _Wet = lerp(_Wet, _SWet[g], mask);
                _Sky = lerp(_Sky, _SSky[g], mask);
                _Line1 = lerp(_Line1, _SLine1[g], mask);
                _Line2 = lerp(_Line2, _SLine2[g], mask);
                _Line3 = lerp(_Line3, _SLine3[g], mask);
                _Hatch = lerp(_Hatch, _SHatch[g], mask);
                _Fade = lerp(_Fade, _SFade[g], mask);
                _Night = lerp(_Night, _SNight[g], mask);
            }
        }

        void LoadStyle(float2 uv)
        {
            uint2 pixel = min(uint2(uv * _ScaledScreenParams.xy), uint2(_ScaledScreenParams.xy) - 1);
            float3 hint = LOAD_TEXTURE2D(_ChromoHints, pixel).rgb;
            StyleId = min((int)round(hint.r * 255.0), MAX_STYLES - 1);
            Subject = hint.g;
            Corrupt = hint.b;
            LoadSlot(StyleId);
        }

        float3 SampleColor(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, chromo_linear_clamp_sampler, uv, 0).rgb;
        }

        // Pixels at 1080p to a uv offset. The hints and motion textures are the camera target's size, which render
        // scale shrinks, so this uses the scaled screen size too (at render scale 1 the two are the same).
        float2 UvFrom1080(float2 px) { return px / (_ScaledScreenParams.xy * _PaperOffset.z); }

        float3 ToSrgb(float3 c)
        {
            c = saturate(c);
            float3 lo = c * 12.92;
            float3 hi = 1.055 * pow(c, 1.0 / 2.4) - 0.055;
            return lerp(lo, hi, step(0.0031308, c));
        }

        float SrgbLuma(float3 c) { return ToSrgb(dot(c, float3(0.2126, 0.7152, 0.0722)).xxx).x; }

        bool IsSky(float rawDepth)
        {
        #if UNITY_REVERSED_Z
            return rawDepth <= 1e-6;
        #else
            return rawDepth >= 1.0 - 1e-6;
        #endif
        }

        // ---- Texture space stuck to the world --------------------------------------------------------------------
        // Grain, paper and hatching are mapped onto the surfaces themselves (projected from the three world axes,
        // like the terrain's triplanar textures), so they stay on things as the camera moves and turns. To keep
        // their size on screen roughly constant, the mapping is scaled in powers of two with distance and the two
        // nearest scales are blended (the "infinite zoom" trick from Benard et al., Dynamic Solid Textures).
        // The sky is mapped the same way onto a far-off sphere by view direction. Moving objects (ChromoMoving) are
        // mapped from where their surface was when they started (or their rest pose, for skinned meshes), so their
        // texture travels with them instead of scrolling across them.
        struct Space
        {
            float3 world;    // where the pixel is in the world (not where its texture comes from)
            float3 pos;      // world position (or direction x 1000 for the sky)
            float3 weights;  // triplanar weights
            float2 uvX, uvY, uvZ;
            float scale0;    // world size of one 1080p pixel at the nearer power-of-two step
            float blend;     // 0..1 towards the next step (twice as big)
            float step0;     // which power-of-two step scale0 is
            float sky;       // 1 for the sky
            float4 gradX, gradY, gradZ; // screen derivatives of uvX, uvY, uvZ (ddx.xy, ddy.xy), taken once here
                                        // where every pixel runs, so samples inside branches can use them
        };

        Space MakeSpace(float2 uv, float raw, bool sky, float eyeDepth)
        {
            Space s;
            s.sky = sky ? 1.0 : 0.0;
        #if UNITY_REVERSED_Z
            float device = raw;
        #else
            float device = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, raw);
        #endif
            float3 world = ComputeWorldSpacePosition(uv, sky ? 0.5 : device, UNITY_MATRIX_I_VP);
            s.world = world;
            float3 n;
            float depth;
            if (sky)
            {
                float3 dir = normalize(world - _WorldSpaceCameraPos);
                s.pos = dir * 1000.0;
                n = dir;
                depth = 1000.0;
            }
            else
            {
                s.pos = world;
                n = SampleSceneNormals(uv);
                depth = eyeDepth;
                if (_ChromoMotionOn > 0.5)
                {
                    uint2 pixel = min(uint2(uv * _ScaledScreenParams.xy), uint2(_ScaledScreenParams.xy) - 1);
                    float4 moving = LOAD_TEXTURE2D(_ChromoMotionPos, pixel);
                    if (moving.a > 0.5)
                    {
                        s.pos = moving.xyz;
                        n = LOAD_TEXTURE2D(_ChromoMotionNormal, pixel).xyz;
                    }
                }
            }
            float3 w = pow(abs(n), 6.0);
            s.weights = w / max(dot(w, 1.0), 1e-5);
            s.uvX = s.pos.zy; s.uvY = s.pos.xz; s.uvZ = s.pos.xy;
            s.gradX = float4(ddx(s.uvX), ddy(s.uvX));
            s.gradY = float4(ddx(s.uvY), ddy(s.uvY));
            s.gradZ = float4(ddx(s.uvZ), ddy(s.uvZ));
            float level = log2(max((_PaperOffset.x > 0.5 ? 1.0 : depth) * _PaperOffset.w, 1e-6));
            s.step0 = floor(level);
            s.scale0 = exp2(s.step0);
            s.blend = frac(level);
            return s;
        }

        // One channel-set of a tiling texture on the surface at one scale (texels = pxPerTexel 1080p pixels).
        float4 Tri(TEXTURE2D_PARAM(tex, samp), Space s, float worldTexel, float2 offset)
        {
            float k = 1.0 / (worldTexel * _ChromoSizes.y);
            float4 v = 0;
            if (s.weights.x > 0.01) v += SAMPLE_TEXTURE2D_LOD(tex, samp, s.uvX * k + offset, 0) * s.weights.x;
            if (s.weights.y > 0.01) v += SAMPLE_TEXTURE2D_LOD(tex, samp, s.uvY * k + offset.yx, 0) * s.weights.y;
            if (s.weights.z > 0.01) v += SAMPLE_TEXTURE2D_LOD(tex, samp, s.uvZ * k - offset, 0) * s.weights.z;
            return v;
        }

        // A texture at a steady size on screen: the two nearest power-of-two scales, blended.
        float4 Steady(TEXTURE2D_PARAM(tex, samp), Space s, float pxPerTexel, float2 offset, out float sumSquares)
        {
            float wa = 1.0 - s.blend, wb = s.blend;
            float4 a = 0, b = 0;
            // Each step keeps its own offset wherever it is used, so when the nearer step drops out and the next one
            // takes its slot, the texture carries on unchanged instead of jumping.
            float2 offsetA = offset + frac(s.step0 * float2(0.618, 0.382));
            float2 offsetB = offset + frac((s.step0 + 1.0) * float2(0.618, 0.382));
            if (wa > 0.001) a = Tri(TEXTURE2D_ARGS(tex, samp), s, s.scale0 * pxPerTexel, offsetA);
            if (wb > 0.001) b = Tri(TEXTURE2D_ARGS(tex, samp), s, s.scale0 * pxPerTexel * 2.0, offsetB);
            sumSquares = (wa * wa + wb * wb) * dot(s.weights, s.weights);
            return a * wa + b * wb;
        }

        // ---- Grain ------------------------------------------------------------------------------------------------
        // The grain textures are equalised (every value equally common), so ink lands exactly as much as its
        // coverage says. Averaging samples (triplanar planes, the two distance steps, fine and wet grain) would
        // squeeze them toward grey and change how much ink lands as the mix shifts with distance, which shows as a
        // lighter colour pulsing in and out. So samples are blended as Gaussians instead: each is mapped to a
        // normal value, the weighted sum is rescaled to unit spread, and mapped back to equalised. The logistic
        // curve stands in for the normal one; it is close enough and cheap.
        float ToGauss(float u) { u = clamp(u, 0.001, 0.999); return log(u / (1.0 - u)) * 0.5513; }
        float FromGauss(float x) { return 1.0 / (1.0 + exp(-x * 1.8138)); }

        // A mipmapped sample, its footprint taken from how fast the surface uv changes across the screen, so grain
        // at a glancing angle or far away averages out instead of shimmering.
        float4 SampleSurface(TEXTURE2D_PARAM(tex, samp), float2 p, float4 grad, float k)
        {
            return SAMPLE_TEXTURE2D_GRAD(tex, samp, p, grad.xy * k, grad.zw * k);
        }

        // One grain channel on the surface at one scale: weighted sum of Gaussians, and the sum of squared weights.
        float TriGauss(TEXTURE2D_PARAM(tex, samp), Space s, float worldTexel, float2 offset, float4 channel, out float variance)
        {
            float k = 1.0 / (worldTexel * _ChromoSizes.y);
            float v = 0;
            variance = 0;
            float3 w = s.weights;
            if (w.x > 0.01) { v += ToGauss(dot(SampleSurface(TEXTURE2D_ARGS(tex, samp), s.uvX * k + offset, s.gradX, k), channel)) * w.x; variance += w.x * w.x; }
            if (w.y > 0.01) { v += ToGauss(dot(SampleSurface(TEXTURE2D_ARGS(tex, samp), s.uvY * k + offset.yx, s.gradY, k), channel)) * w.y; variance += w.y * w.y; }
            if (w.z > 0.01) { v += ToGauss(dot(SampleSurface(TEXTURE2D_ARGS(tex, samp), s.uvZ * k - offset, s.gradZ, k), channel)) * w.z; variance += w.z * w.z; }
            return v;
        }

        // The same at a steady size on screen: both distance steps, each with its own offset (see Steady).
        float SteadyGauss(TEXTURE2D_PARAM(tex, samp), Space s, float pxPerTexel, float2 offset, float4 channel, out float variance)
        {
            float wa = 1.0 - s.blend, wb = s.blend;
            float a = 0, b = 0, va = 0, vb = 0;
            float2 offsetA = offset + frac(s.step0 * float2(0.618, 0.382));
            float2 offsetB = offset + frac((s.step0 + 1.0) * float2(0.618, 0.382));
            if (wa > 0.001) a = TriGauss(TEXTURE2D_ARGS(tex, samp), s, s.scale0 * pxPerTexel, offsetA, channel, va);
            if (wb > 0.001) b = TriGauss(TEXTURE2D_ARGS(tex, samp), s, s.scale0 * pxPerTexel * 2.0, offsetB, channel, vb);
            variance = wa * wa * va + wb * wb * vb;
            return a * wa + b * wb;
        }

        float GrainFrom(TEXTURE2D_PARAM(tex, samp), Space s, float scale, float2 offset, float4 channel)
        {
            // Wet grain: the same texture blown up several times, where bilinear filtering turns its steps into soft
            // blooms, like pigment drifting in a wet wash. Wetness mixes it in place of the dry, fine grain.
            float wet = _Wet.x;
            float fine = 0, bloom = 0, vf = 0, vb = 0;
            if (wet < 0.999) fine = SteadyGauss(TEXTURE2D_ARGS(tex, samp), s, scale, offset, channel, vf);
            if (wet > 0.001) bloom = SteadyGauss(TEXTURE2D_ARGS(tex, samp), s, scale * _Wet.y, offset + 0.29, channel, vb);
            float dry = 1.0 - wet;
            float variance = dry * dry * vf + wet * wet * vb;
            return FromGauss((fine * dry + bloom * wet) * rsqrt(max(variance, 1e-4)));
        }

        float Grain(int ink, Space s)
        {
            float scale = _InkParams[ink].x * _Print1.y * lerp(1.0, _Sky.z, s.sky);
            float2 offset = float2(0.371, 0.917) * ink;
            int c = ink & 3;
            float4 channel = float4(c == 0, c == 1, c == 2, c == 3);
            if (ink < 4) return GrainFrom(TEXTURE2D_ARGS(_GrainA, chromo_linear_repeat_sampler), s, scale, offset, channel);
            return GrainFrom(TEXTURE2D_ARGS(_GrainB, chromo_linear_repeat_sampler), s, scale, offset, channel);
        }

        float4 PaperAt(Space s, float pxPerTexel, float2 offset)
        {
            float ss;
            return Steady(TEXTURE2D_ARGS(_PaperTex, chromo_linear_repeat_sampler), s, pxPerTexel * _ChromoSizes.y / _ChromoSizes.z, offset, ss);
        }

        // ---- Inks -------------------------------------------------------------------------------------------------
        float3 SeparationUvw(float3 linearColor)
        {
            float n = _ChromoSizes.x;
            return ToSrgb(linearColor) * ((n - 1.0) / n) + 0.5 / n;
        }

        // The colour the inks are separated from: a small blur, so neighbouring pixels don't flip between ink mixes
        // and colours run together like Audubon's washes.
        float3 WashColor(float2 uv, float eyeDepth)
        {
            // Taps across a silhouette are left out (by depth), so one object's colour doesn't bleed into what's
            // behind it and print a pale rim of mixed inks along the edge.
            // The taps lie on a spiral (golden angle, even over the disc, weighted towards the centre) turned by a
            // different angle at every pixel. Four taps at fixed corners used to stamp four faint copies of any small
            // object diagonally around it; a turned spiral spreads them into one soft halo instead.
            float2 r = UvFrom1080(_Print3.yy) * 1.4;
            float3 c = SampleColor(uv) * 0.3;
            float total = 0.3;
            float2 pixel = uv * _ScaledScreenParams.xy;
            float turn = frac(52.9829189 * frac(dot(floor(pixel), float2(0.06711056, 0.00583715)))) * 6.2831853;
            float s, co;
            sincos(turn, s, co);
            float2x2 rotation = float2x2(co, -s, s, co);
            static const float3 taps[8] =
            {
                // xy = direction x radius (0..1), z = weight (Gaussian in the radius)
                float3( 0.250,  0.000, 0.112), float3(-0.319,  0.292, 0.092), float3( 0.049, -0.557, 0.077),
                float3( 0.402,  0.525, 0.064), float3(-0.739, -0.131, 0.053), float3( 0.700, -0.445, 0.044),
                float3(-0.234,  0.870, 0.036), float3(-0.446, -0.859, 0.030),
            };
            [unroll] for (int k = 0; k < 8; k++)
            {
                float2 t = uv + mul(rotation, taps[k].xy) * r;
                float d = LinearEyeDepth(SampleSceneDepth(t), _ZBufferParams);
                float w = taps[k].z * saturate(1.0 - abs(d - eyeDepth) / (eyeDepth * 0.05 + 0.05));
                c += SampleColor(t) * w;
                total += w;
            }
            return c / total;
        }

        // The look controls: exposure, contrast (around mid grey) and saturation, with extra saturation in the sky.
        float3 AdjustColor(float3 c, float sky)
        {
            c = max(c, 0.0) * exp2(_Sky.w);
            c = 0.18 * pow(c / 0.18 + 1e-5, _Print3.w);
            float luma = dot(c, float3(0.2126, 0.7152, 0.0722));
            float saturation = _Print3.z * lerp(1.0, _Sky.x, sky);
            return max(luma + (c - luma) * saturation, 0.0);
        }

        // The colour one ink is separated from: the blurred colour, shifted by that ink's registration offset (one
        // extra sample per ink rather than a blur per ink).
        float3 InkSource(float2 inkUv, float3 wash, float3 centre, float sky)
        {
            return AdjustColor(wash + SampleColor(inkUv) - centre, sky);
        }

        float Coverage(int ink, float3 color)
        {
            float3 uvw = SeparationUvw(color);
            float4 c = ink < 4 ? SAMPLE_TEXTURE3D_LOD(_SeparationA, chromo_linear_clamp_sampler, uvw, 0)
                               : SAMPLE_TEXTURE3D_LOD(_SeparationB, chromo_linear_clamp_sampler, uvw, 0);
            int channel = ink & 3;
            float v = channel == 0 ? c.r : channel == 1 ? c.g : channel == 2 ? c.b : c.a;
            // A whisper of an ink would only scatter lone specks; leave it off.
            return saturate((v - 0.05) / 0.95);
        }

        // How much ink lands: a flat wash of the coverage, broken up by the grain as much as strength says.
        // At 0 it is smooth colour, at 1 every dot is either ink or paper. Both average to the coverage.
        // The grain moves the ink up or down around the coverage by at most the room there is on either side, so the
        // average is always exactly the coverage, however much contrast the grain has. (A threshold, as before, is
        // only right for full-contrast grain: where mipmaps or blending soften the grain it lightened pale inks and
        // darkened strong ones, and that changed with distance, which showed as a shimmer.)
        float InkAmount(float coverage, float grain, float strength)
        {
            float x = (0.5 - grain) * 2.0;
            // Softness shapes the grain symmetrically: low pushes values toward the ends (crisp specks), 1 leaves it.
            x = sign(x) * pow(abs(x), lerp(0.3, 1.0, saturate(_Print1.z / 0.6)));
            float room = min(coverage, 1.0 - coverage);
            return saturate(coverage + x * room * strength);
        }

        // ---- Key stone --------------------------------------------------------------------------------------------
        // Inverse eye depth is affine across a flat surface on screen, so its second difference is zero there and
        // only real silhouettes stand out.
        float InverseDepth(float2 uv, out bool sky)
        {
            float raw = SampleSceneDepth(uv);
            sky = IsSky(raw);
            return 1.0 / max(LinearEyeDepth(raw, _ZBufferParams), 1e-4);
        }

        // Outline strength at uv (0..1), from depth silhouettes and normal creases.
        float Edges(float2 uv, float eyeDepth)
        {
            if (_Line1.x <= 0.0) return 0.0;
            float width = _Line1.x * lerp(1.0, 0.45, saturate(eyeDepth / _Line2.y));
            // Depth is point sampled, so every tap lands on whole texels. Put the centre on its texel and step a whole
            // number of texels either side, so the two neighbours always sit symmetrically about it. Left to round on
            // their own, one neighbour could land a texel further out than the other; where depth changes fast from
            // texel to texel (a surface seen at a grazing angle) that lopsided second difference read as a step, and
            // the surface filled with pools of dotted ink that crawled as the line wobble moved.
            // The reach rounds up or down by where uv sits inside its texel, so on average it is still the line
            // width (as the free taps were) and the wobble still varies the stroke.
            float2 texel = _CameraDepthTexture_TexelSize.xy;
            float2 inTexel = frac(uv / texel);
            uv = (floor(uv / texel) + 0.5) * texel;
            float stepTexels = UvFrom1080(float2(width, width)).x / texel.x;

            bool skyC;
            float wC = InverseDepth(uv, skyC);
            float edge = 0.0;
            static const float2 dirs[4] = { float2(1, 0), float2(0, 1), float2(1, 1), float2(1, -1) };
            float3 nC = SampleSceneNormals(uv);
            float3 cC = 0.0;
            if (_Line3.x > 0.0) cC = ToSrgb(SampleColor(uv));
            [unroll] for (int k = 0; k < 4; k++)
            {
                float reach = max(floor(stepTexels * (k < 2 ? 1.0 : 0.7071) + (k == 1 ? inTexel.y : inTexel.x)), 1.0);
                float2 o = dirs[k] * texel * reach;
                bool skyA, skyB;
                float wA = InverseDepth(uv + o, skyA);
                float wB = InverseDepth(uv - o, skyB);
                // Near side of a silhouette: the neighbours are, on average, much further away than this pixel.
                float jump = (wC - 0.5 * (wA + wB)) / wC;
                // One-sided too, so a thin object against the sky still gets both edges.
                jump = max(jump, max(wC - wA, wC - wB) / wC * 0.5);
                edge = max(edge, saturate((jump - _Line1.z) / _Line1.z));
                // The same in metres, for low steps seen head on (a slab on the ground) that are small next to
                // their distance.
                // Taken from the inverse-depth second difference (zero on any flat surface, even a slope seen at a
                // glancing angle), scaled to metres: about half the step's height.
                float dC = 1.0 / wC;
                float gap = (wC - 0.5 * (wA + wB)) / wC * dC * 2.0;
                float gapLimit = _Line3.z * (1.0 + dC * 0.05);
                if (!skyA && !skyB) edge = max(edge, saturate((gap - gapLimit) / gapLimit));

                if (!skyC && k < 2)
                {
                    // Colour boundaries, as an engraver outlines one colour against another.
                    if (_Line3.x > 0.0)
                    {
                        float diff = max(length(ToSrgb(SampleColor(uv + o)) - cC), length(ToSrgb(SampleColor(uv - o)) - cC));
                        edge = max(edge, saturate((diff - _Line3.y) / _Line3.y) * _Line3.x);
                    }
                    // Where objects with different styles meet.
                    if (_Line3.w > 0.0 && (StyleAt(uv + o) != StyleId || StyleAt(uv - o) != StyleId))
                        edge = max(edge, _Line3.w);
                    float3 nA = SampleSceneNormals(uv + o);
                    float3 nB = SampleSceneNormals(uv - o);
                    float bend = max(skyA ? 0.0 : 1.0 - dot(nC, nA), skyB ? 0.0 : 1.0 - dot(nC, nB));
                    edge = max(edge, saturate((bend - _Line1.w) / _Line1.w));
                }
            }
            return skyC ? 0.0 : edge;
        }

        // One set of hatch strokes along dir, in coarse units (the further of the two distance steps), where strokes
        // sit every spacing units. The nearer step's strokes are those plus one halfway between each pair, so as the
        // distance step blends (b: 0 near, 1 far) the in-between strokes thin away while the kept ones thicken by
        // the same amount. The ink laid stays the same at every distance, so the hatching never pulses lighter.
        // How much of a pixel (aa wide) a stroke of half-width h covers when the pixel is d from its centre. Exact box
        // overlap, so a stroke thinning to nothing fades to nothing (the old soft-edge ramp left half a line at zero
        // width, which then vanished at the next distance step and showed as a seam).
        float StrokeCover(float h, float d, float aa)
        {
            return saturate((min(h, d + aa * 0.5) - max(-h, d - aa * 0.5)) / aa);
        }

        float HatchSet(float2 p, float2 dir, float width, float b, float aa)
        {
            float spacing = _Hatch.y;
            float t = dot(p, dir) / spacing;
            // Kept strokes sit on whole t and in-between ones on halves. At the next step (twice as coarse) t halves,
            // so the old kept strokes land exactly on the new kept and in-between ones and the hand-over can't jump.
            // (They sat on halves before, which put the next step's strokes a quarter spacing off and showed a seam.)
            float dKept = abs(frac(t + 0.5) - 0.5) * spacing;
            float dBetween = abs(frac(t) - 0.5) * spacing;
            float kept = StrokeCover(width * (1.0 + b), dKept, aa);
            float between = StrokeCover(width * (1.0 - b), dBetween, aa);
            return max(kept, between);
        }

        float Hatching(Space s, float luma)
        {
            float below = max(_Hatch.z, 1e-3);
            float dark = saturate((below - luma) / below);
            if (_Hatch.x <= 0.0 || dark <= 0.0) return 0.0;
            // Strokes lie on the surface's main projection plane.
            float2 p = s.weights.x > s.weights.y ? (s.weights.x > s.weights.z ? s.uvX : s.uvZ) : (s.weights.y > s.weights.z ? s.uvY : s.uvZ);
            float2 coarse = p / (s.scale0 * 2.0);
            // One 1080p pixel in coarse units, for soft stroke edges.
            float aa = exp2(s.blend) * 0.5;
            float spacing = _Hatch.y;
            // Stroke half-widths at the nearer step, halved into coarse units.
            float w1 = saturate(dark * 2.0) * spacing * 0.22 * 0.5;
            float w2 = saturate(dark * 2.0 - 1.0) * spacing * 0.18 * 0.5;
            float h1 = HatchSet(coarse, float2(0.7071, 0.7071), w1, s.blend, aa);
            float h2 = HatchSet(coarse, float2(0.7071, -0.7071), w2, s.blend, aa);
            return max(h1, h2) * _Hatch.x;
        }

        // The distance fading is measured at: the background (not a subject) fades up to four times sooner.
        float FadeDistance(float eyeDepth)
        {
            return eyeDepth * (1.0 + _ChromoSubject.y * 3.0 * (1.0 - Subject));
        }

        // How much of an ink survives at this distance (inks with a low fade order drop out first).
        float InkFade(float order, float eyeDepth, bool sky)
        {
            if (sky) return _Print2.w;
            // Each ink fades over half the whole range, staggered by its order, so the bands overlap and no single
            // distance shows a step. Subjects hold their inks (Subject Hold).
            float range = _Fade.y - _Fade.x;
            float start = _Fade.x + range * order * 0.5;
            float amount = _Fade.z * (1.0 - _ChromoSubject.x * Subject);
            return 1.0 - amount * smoothstep(start, start + range * 0.5, FadeDistance(eyeDepth));
        }

        float LineFade(float eyeDepth)
        {
            float fade = 1.0 - smoothstep(_Fade.x, lerp(_Fade.x, _Fade.y, 0.6), FadeDistance(eyeDepth));
            return lerp(fade, 1.0, _ChromoSubject.x * Subject);
        }

        // The key stone's coverage at uv: outlines plus hatching.
        float KeyStone(float2 uv, Space s, float3 color, float eyeDepth, bool sky, float keyGrain)
        {
            // Lines fade out with distance; past that (and in the sky) there is nothing to find. One return at the end,
            // since FXC warns about an uninitialised result when a branch returns early.
            float fade = LineFade(eyeDepth);
            float result = 0.0;
            [branch] if (!sky && fade > 0.0)
            {
                float4 wob = PaperAt(s, 3.0, float2(0.13, 0.71));
                float2 wobble = (wob.ag - 0.5) * 2.0 * _Line2.x;
                float2 lineUv = uv + UvFrom1080(wobble);

                float luma = SrgbLuma(color);
                float outline = Edges(lineUv, eyeDepth) * _Line1.y;
                outline *= lerp(1.0, 1.0 - _Line2.z, smoothstep(0.55, 0.95, luma));
                float hatch = Hatching(s, luma);
                float key = max(outline, hatch) * fade;
                // Crayon breaks the line a little where it's light.
                result = saturate((key * 1.15 - keyGrain * 0.35) / 0.8);
            }
            return result;
        }

        // How much paint the exhaustion vignette strips here (0 = none, 1 = bare page): its own shape, from the edge of
        // the clear area around the player out to the end of its fade, scaled by how far it has closed.
        float StripAt(float2 uv)
        {
            if (_Strip2.z <= 0.0) return 0.0;
            float2 local = (uv - _Strip1.xy) * _Strip1.zw;
            float s = saturate((length(local) - _Strip2.x) / max(_Strip2.y, 1e-3));
            return smoothstep(0.0, 1.0, s) * _Strip2.z;
        }

        // ---- Corruption -------------------------------------------------------------------------------------------
        // The crumple texture is laid in the same texture space as the grain and paper (MakeSpace): stuck to the
        // surfaces, travelling with moving objects, and stepped in powers of two with distance with the two nearest
        // steps blended, so its facets, creases and blooms keep about the same size on screen near and far.

        // One step: the crumple texture on the surface with one texel = worldTexel metres, optionally turned 37
        // degrees (so a turned layer never lines up with an unturned one).
        float4 CrumpleTri(Space s, float worldTexel, float2 offset, bool turned)
        {
            float k = 1.0 / (worldTexel * 256.0);
            const float2x2 r = float2x2(0.7986, -0.6018, 0.6018, 0.7986);
            float2 uvX = s.uvX, uvY = s.uvY, uvZ = s.uvZ;
            float4 gX = s.gradX, gY = s.gradY, gZ = s.gradZ;
            if (turned)
            {
                uvX = mul(r, uvX); uvY = mul(r, uvY); uvZ = mul(r, uvZ);
                gX = float4(mul(r, gX.xy), mul(r, gX.zw));
                gY = float4(mul(r, gY.xy), mul(r, gY.zw));
                gZ = float4(mul(r, gZ.xy), mul(r, gZ.zw));
            }
            float4 v = 0;
            if (s.weights.x > 0.01) v += SampleSurface(TEXTURE2D_ARGS(_CrumpleTex, chromo_linear_repeat_sampler), uvX * k + offset, gX, k) * s.weights.x;
            if (s.weights.y > 0.01) v += SampleSurface(TEXTURE2D_ARGS(_CrumpleTex, chromo_linear_repeat_sampler), uvY * k + offset.yx, gY, k) * s.weights.y;
            if (s.weights.z > 0.01) v += SampleSurface(TEXTURE2D_ARGS(_CrumpleTex, chromo_linear_repeat_sampler), uvZ * k - offset, gZ, k) * s.weights.z;
            return v;
        }

        // The crumple texture at a steady size on screen (texels = pxPerTexel 1080p pixels): the two nearest
        // power-of-two steps, blended, each with its own offset (as Steady does for the paper).
        float4 CrumpleAt(Space s, float pxPerTexel, float2 offset, bool turned)
        {
            float wa = 1.0 - s.blend, wb = s.blend;
            float4 a = 0, b = 0;
            float2 offsetA = offset + frac(s.step0 * float2(0.618, 0.382));
            float2 offsetB = offset + frac((s.step0 + 1.0) * float2(0.618, 0.382));
            if (wa > 0.001) a = CrumpleTri(s, s.scale0 * pxPerTexel, offsetA, turned);
            if (wb > 0.001) b = CrumpleTri(s, s.scale0 * pxPerTexel * 2.0, offsetB, turned);
            return a * wa + b * wb;
        }

        // Smooth 3D value noise (0..1), for the blooms, which live in the world rather than the texture space.
        float CorruptHash(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        float CorruptNoise(float3 p)
        {
            float3 i = floor(p);
            float3 f = p - i;
            f = f * f * (3.0 - 2.0 * f);
            float a = lerp(CorruptHash(i), CorruptHash(i + float3(1, 0, 0)), f.x);
            float b = lerp(CorruptHash(i + float3(0, 1, 0)), CorruptHash(i + float3(1, 1, 0)), f.x);
            float c = lerp(CorruptHash(i + float3(0, 0, 1)), CorruptHash(i + float3(1, 0, 1)), f.x);
            float d = lerp(CorruptHash(i + float3(0, 1, 1)), CorruptHash(i + float3(1, 1, 1)), f.x);
            return lerp(lerp(a, b, f.y), lerp(c, d, f.y), f.z);
        }

        // One layer of blooms at a point (p in this layer's bloom sizes, drift already in those units): three
        // octaves that drift with the stain and each slide along their own direction at the morph speed, so the
        // blooms slowly change shape as they move. About 0.5 +- 0.15.
        float CorruptBloomLayer(float3 p, float3 drift, float morph, float3 seed)
        {
            float n = CorruptNoise(p - drift + float3(0.0, morph, 0.0) + seed) * 0.571
                    + CorruptNoise(p * 2.03 - drift * 1.3 + float3(morph * 1.7, 0.0, 17.3) + seed) * 0.286
                    + CorruptNoise(p * 4.11 - drift * 1.7 + float3(31.1, -morph * 2.3, morph) + seed) * 0.143;
            return 0.5 + (n - 0.5) * 1.15;
        }

        // Three layers of blooms (xyz: the Bloom Size's, 2.5 times bigger, 0.4 times the size; a layer at zero
        // strength isn't computed), and in w a static ridged field for the bleed's branching. p is in metres.
        float4 CorruptBlooms(float3 p, float time)
        {
            float k = _Corrupt3.z;
            float3 drift = float3(0.8, 0.0, 0.6) * (time * _Corrupt4.x);
            float morph = time * _Corrupt4.y;
            float4 b = float4(CorruptBloomLayer(p * k, drift * k, morph, 0.0), 0.0, 0.0, 0.0);
            [branch] if (_Corrupt4.z > 0.0) b.y = CorruptBloomLayer(p * (k * 0.4), drift * (k * 0.4), morph * 0.7, float3(71.3, 13.9, 47.1));
            [branch] if (_Corrupt4.w > 0.0) b.z = CorruptBloomLayer(p * (k * 2.5), drift * (k * 2.5), morph * 1.4, float3(23.7, 89.1, 5.3));
            float3 q = p * k;
            float r = 1.0 - abs(CorruptNoise(q * 1.37 + 53.7) * 2.0 - 1.0);
            r = r * 0.65 + (1.0 - abs(CorruptNoise(q * 2.91 + 91.3) * 2.0 - 1.0)) * 0.35;
            b.w = saturate((r - 0.35) / 0.6);
            return b;
        }

        // A colour through the corruption inks by brightness: darks to Dark, middle tones to Mid, lights to Light.
        float3 CorruptInks(float luma)
        {
            float3 low = lerp(_CorruptDark.rgb, _CorruptMid.rgb, saturate(luma * 2.0));
            return lerp(low, _CorruptLight.rgb, saturate(luma * 2.0 - 1.0));
        }

        float4 FragPrint(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            LoadStyle(uv);
            float3 original = SampleColor(uv);

            float raw = SampleSceneDepth(uv);
            bool sky = IsSky(raw);
            // The sky prints with its own settings (the Look, or the procedural sky's time-of-day styles).
            if (sky) LoadSky(uv);
            float eyeDepth = sky ? 1e6 : LinearEyeDepth(raw, _ZBufferParams);
            Space space = MakeSpace(uv, raw, sky, eyeDepth);

            int mode = (int)_Proof.x;
            int proofInk = (int)_Proof.y;
            int inkCount = (int)_InkCount;
            // The corruption view prints as normal, then shows where corruption landed.
            bool corruptionView = mode == 5;
            if (corruptionView) mode = 0;

            float3 wash = _Print3.y > 0.0 ? WashColor(uv, eyeDepth) : original;

            // Corruption: where the object is corrupted, its colour drains into the corruption inks before they're
            // split into stones, so the print itself changes colour (the inks, grain and lines all follow). The amount
            // is turned into coverage through a branching field, so a partly corrupted surface is covered in feathered,
            // vein-like bleeds that creep further as the amount grows, with a wet front at their edge.
            // The spreading field covers the ground and everything on it, except the plate's subjects (characters keep
            // their own corruption, from ChromoCorrupted), so they stay readable against it.
            [branch] if (!sky && _ChromoCorruptionMap1.w > 0.0 && Subject < 0.5)
            {
                float2 xz = space.world.xz;
                if (all(xz > _ChromoCorruptionMap1.yz) && all(xz < _ChromoCorruptionMap2.xy))
                    Corrupt = max(Corrupt, SAMPLE_TEXTURE2D_LOD(_ChromoCorruptionMap, chromo_linear_repeat_sampler, xz * _ChromoCorruptionMap1.x, 0).r);
            }
            float corrupt = 0.0;   // how much of corruption's look this pixel takes
            float front = 0.0;     // 1 on the edge of the bleed
            float4 crumple = float4(0.0, 0.5, 0.5, 0.5);
            float3 bloomFields = 0.0;
            [branch] if (!sky && Corrupt > 0.0)
            {
                crumple = CrumpleAt(space, _Corrupt1.z, 0.0, false);
                // Blooms in the world (on the surface's texture position, so they stay on moving objects), drifting and
                // changing shape over time; the bleed's branching from a static field at the same size plus the crumple's.
                float4 blooms = CorruptBlooms(space.pos, _Time.y);
                bloomFields = blooms.xyz;
                float w = _Corrupt2.z;
                // (the mix mostly spans 0.1 to 0.85; stretched so the whole amount range moves the edge)
                float dendrite = blooms.w * 0.6 + crumple.a * 0.4;
                float field = lerp(0.5, saturate((dendrite - 0.1) / 0.75), _Corrupt2.w);
                float at = lerp(-w, 1.0 + w, 1.0 - Corrupt);
                corrupt = smoothstep(at - w, at + w, field);
                front = 4.0 * corrupt * (1.0 - corrupt);
                float strength = _Corrupt1.y;
                // Each facet of the film takes its own value, so the planes read apart once remapped.
                float luma = saturate(SrgbLuma(wash) + (crumple.g - 0.5) * _Corrupt2.y * strength * 0.6);
                wash = lerp(wash, CorruptInks(luma), _Corrupt1.x * corrupt);
            }

            // Coverage view: one ink's coverage, before grain.
            if (mode == 3)
            {
                float c = Coverage(clamp(proofInk, 0, MAX_INKS - 1), AdjustColor(wash, space.sky));
                return float4((1.0 - c).xxx, 1.0);
            }

            float4 sheet = PaperAt(space, 1.0, 0.0);
            float3 result = _PaperColor.rgb;
            result *= 1.0 + (sheet.r - 0.5) * 0.14 * _Print2.y;
            result = lerp(result, result * _FoxColor.rgb, saturate(sheet.b) * _Print2.z);
            float4 mottles = PaperAt(space, 6.0, float2(0.53, 0.17));
            // Fog Sky Tint: with distance the paper takes on the sky's horizon colour, so as the inks drop out far land
            // fades into the sky rather than to bare paper. Subjects hold, as they do their inks.
            [branch] if (!sky && _Hand.w > 0.0 && _ChromoHorizon.w > 0.0)
            {
                float far = smoothstep(_Fade.x, _Fade.y, FadeDistance(eyeDepth)) * _Fade.z * (1.0 - _ChromoSubject.x * Subject);
                result *= lerp(1.0, _ChromoHorizon.rgb, far * _Hand.w);
            }
            float3 sheetColor = result;
            float edge = saturate(length(original - wash) * 6.0);
            float3 smooth = result; // the same inks laid as flat washes, without grain

            // Ink 0's grain is also the key stone's and the shared tooth's, so it is sampled once.
            float keyGrain = Grain(0, space);

            [unroll] for (int i = 0; i < MAX_INKS; i++)
            {
                bool include = i < inkCount && (mode == 0 || (mode == 1 && i <= proofInk) || (mode == 2 && i == proofInk));
                float4 p = _InkParams[i];
                float fade = InkFade(p.y, eyeDepth, sky);
                // An ink faded out entirely lays nothing, so its table and grain aren't read.
                float coverage = 0.0;
                [branch] if (include && fade > 0.0)
                {
                    float2 inkUv = uv + UvFrom1080(p.zw * _Print1.w);
                    coverage = Coverage(i, InkSource(inkUv, wash, original, space.sky)) * fade;
                }
                [branch] if (coverage > 0.0)
                {
                    float mottle = (i & 1) == 0 ? mottles.g : mottles.a;
                    float density = _InkColor[i].a * (1.0 - _Print2.x * mottle * 0.6);
                    // Granulation: pigment settles into the paper's tooth.
                    density *= 1.0 + (sheet.r - 0.5) * _Wet.w;
                    float strength = saturate(lerp(_Print3.x, _Sky.y, space.sky) + _InkParams2[i].x);
                    // Grain only moves ink by the room on either side of the coverage, so at full coverage (or no
                    // grain strength) it can't change anything and isn't sampled.
                    float room = min(coverage, 1.0 - coverage) * strength;
                    // (if, not ?:, which HLSL may evaluate on both sides)
                    float grain = keyGrain;
                    if (i != 0)
                    {
                        grain = 0.5;
                        [branch] if (room > 0.0) grain = Grain(i, space);
                    }
                    float ink = InkAmount(coverage, grain, strength);
                    // Edge pooling: a wet wash dries darker at its rim, where the colour changes.
                    ink = saturate(ink * (1.0 + edge * _Wet.z));
                    result *= lerp(1.0, _InkColor[i].rgb, ink * density);
                    smooth *= lerp(1.0, _InkColor[i].rgb, coverage * density);
                }
            }

            if (mode == 0)
            {
                // Hand colouring: the inks can only mix so many colours, so pull the print toward the scene's own
                // colour (as the Bien plates were finished by hand), keeping the grain the inks laid.
                float3 target = sheetColor * lerp(1.0, saturate(AdjustColor(wash, space.sky)), InkFade(0.5, eyeDepth, sky));
                float3 safeSmooth = max(smooth, 1e-3);
                float3 correction = pow(clamp(target / safeSmooth, 0.25, 4.0), _Hand.x);
                // Even texture: swap some of the inks' own grain (which is strongest where several inks overlap at
                // half coverage) for one shared tooth, so every colour carries about the same texture.
                float3 inkTexture = result / safeSmooth;
                float3 evenTexture = (1.0 + (0.5 - keyGrain) * _Hand.z).xxx;
                result = smooth * correction * lerp(inkTexture, evenTexture, _Hand.y);
            }

            // Corruption's surface: creases where pigment pooled, facet tones, blooms with hard rims, salt in the darks,
            // and the wet front darkening at the bleed's edge.
            [branch] if (mode == 0 && corrupt > 0.0)
            {
                float strength = _Corrupt1.y * corrupt;
                result *= 1.0 + (crumple.g - 0.5) * _Corrupt2.y * strength;
                result *= lerp(1.0, _CorruptDark.rgb, saturate(crumple.r * _Corrupt1.w * strength));
                // The three sizes are summed into one field before its edge is cut, so big and small blooms run into
                // each other and share one hard rim instead of lying on top of each other. (Rescaled so the field keeps
                // the same spread, and so the same share of the surface blooms, whatever the layer strengths.)
                float3 layer = float3(1.0, _Corrupt4.z, _Corrupt4.w);
                float bloomField = 0.5 + dot(bloomFields - 0.5, layer) * rsqrt(dot(layer, layer));
                float inside = smoothstep(0.6, 0.62, bloomField);
                float bloom = inside * _Corrupt3.y * corrupt;
                float rim = inside * (1.0 - smoothstep(0.62, 0.68, bloomField)) * _Corrupt3.y * corrupt;
                result = lerp(result, lerp(result, sheetColor * _CorruptBloom.rgb, 0.45), bloom);
                result *= lerp(1.0, _CorruptBloomRim.rgb, rim * 0.6);
                float salt = smoothstep(1.0 - 0.12 * _Corrupt3.w, 1.02 - 0.12 * _Corrupt3.w, keyGrain);
                salt *= (1.0 - smoothstep(0.15, 0.45, SrgbLuma(result))) * corrupt * step(0.001, _Corrupt3.w);
                result = lerp(result, sheetColor * lerp(1.0, _CorruptLight.rgb, 0.4), salt * 0.8);
                result *= lerp(1.0, _CorruptDark.rgb, front * _Corrupt3.x * 0.6);
            }

            // Deep shadow: where the scene is all but black, its own shadow ink prints solid, so the darkest areas read as
            // one even ink (the way the plates' deepest darks were) instead of grey and black patches of mixed inks.
            float shadowLaid = 0.0;
            if (mode == 0 && !sky && _Hatch.w > 0.0)
            {
                float luma = SrgbLuma(wash);
                float fill = 1.0 - smoothstep(_Line2.w, _Line2.w + _Fade.w, luma);
                fill *= _Hatch.w * InkFade(1.0, eyeDepth, sky);
                shadowLaid = InkAmount(fill, keyGrain, 0.5);
                result = lerp(result, sheetColor * _ShadowColor.rgb, shadowLaid);
            }

            // Night sky: where the sky is dark, the palette's night ink is laid as one smooth wash (no grain), so a night
            // sky reads as an even dark instead of blotches of mixed inks.
            if (mode == 0 && sky && _Night.x > 0.0)
            {
                float luma = SrgbLuma(wash);
                float fill = (1.0 - smoothstep(_Night.y, _Night.y + _Night.z, luma)) * _Night.x;
                result = lerp(result, sheetColor * _NightSkyColor.rgb, fill);
            }

            // Stripped paint: the inks flake away in ragged patches (paper blotches and grain), leaving the bare page
            // with a faint stain of the colour that was there. The key lines stay, like the drawing under a wash.
            float strip = mode == 0 ? StripAt(uv) : 0.0;
            float flakes = saturate(mottles.g * 0.6 + keyGrain * 0.4);
            if (strip > 0.0)
            {
                flakes = lerp(0.5, flakes, _Strip2.w);
                float keep = saturate(((0.1 + 0.9 * flakes) - strip * 1.15 + 0.075) / 0.15);
                float3 stain = sheetColor * lerp(1.0, result / max(sheetColor, 1e-3), 0.12);
                result = lerp(stain, result, keep);
            }

            bool includeKey = mode == 0 || ((mode == 1 || mode == 2) && proofInk >= inkCount);
            if (includeKey)
            {
                // No lines are drawn over the solid dark, so where it's complete they aren't looked for.
                float key = 0.0;
                [branch] if (shadowLaid < 1.0) key = KeyStone(uv, space, original, eyeDepth, sky, keyGrain);
                // Corruption's creases print as the key stone's angular cracks, in place of some of the drawing.
                [branch] if (mode == 0 && corrupt > 0.0)
                {
                    float crack = saturate((crumple.r * 1.3 - keyGrain * 0.3) / 0.8) * _Corrupt2.x;
                    key = lerp(key, max(key * 0.6, crack), corrupt * _Corrupt1.y);
                }
                key *= 1.0 - shadowLaid;
                result *= lerp(1.0, _KeyColor.rgb, key);
            }

            // The exhaustion vignette, painted on as a last wash of opaque body colour over the print: its own inner
            // and edge colours (laid over, not multiplied, so the edge red stays red). Both edges wander in blotches
            // fixed to the screen (drifting slowly, like the overlay's noise), with the grain and paper fibre breaking
            // them up finely, and the wash is heavier where it pools at its rim.
            if (mode == 0 && max(_Paint1.w, _Paint2.w) > 0.0)
            {
                float2 local = (uv - _Strip1.xy) * _Strip1.zw;
                float s = saturate((length(local) - _Paint3.x) / max(_Paint3.y, 1e-3));

                // The mottle blobs are about 1/12 of the paper texture across, so this makes them Blotch Size wide.
                float2 bp = local / max(_Paint5.y * 12.0, 1e-3) + _Paint5.zw;
                float4 p1 = SAMPLE_TEXTURE2D(_PaperTex, chromo_linear_repeat_sampler, bp);
                float4 p2 = SAMPLE_TEXTURE2D(_PaperTex, chromo_linear_repeat_sampler, bp * 2.0 + float2(0.5, 0.25));
                float fine = (keyGrain - 0.5) * 0.6 + (p1.r - 0.5) * 0.4;
                float washBlot = ((p1.g - 0.5) * 0.6 + (p1.a - 0.5) * 0.4) * 2.0;
                float edgeBlot = ((p2.g - 0.5) * 0.6 + (p2.a - 0.5) * 0.4) * 2.0;

                float sWash = saturate(s + (washBlot + fine * 0.5) * _Paint4.x);
                float sEdge = s + (edgeBlot + fine) * _Paint4.y;
                float toOuter = _Paint3.z < 1.0 ? saturate((sEdge - _Paint3.z) / (1.0 - _Paint3.z)) : 0.0;
                // Hardness turns the blend into patches with crisp, jagged borders; the pulse pushes them in and out.
                float amount = saturate(_Paint3.w);
                float edgeAt = 0.1 + 0.8 * (1.0 - amount);
                float hard = saturate((toOuter - edgeAt) / lerp(0.5, 0.03, _Paint4.z) + 0.5);
                toOuter = lerp(toOuter * amount, hard, _Paint4.z);

                float4 colour = lerp(_Paint1, _Paint2, toOuter);
                float wash = smoothstep(0.0, 1.0, sWash) * colour.a;
                float laid = InkAmount(wash, keyGrain, _Paint4.w);
                laid = saturate(laid * (1.0 + 4.0 * wash * (1.0 - wash) * _Paint5.x));
                result = lerp(result, colour.rgb, laid);
            }

            float3 printed = lerp(original, result, _Print1.x);
            [branch] if (corruptionView)
            {
                float grey = SrgbLuma(printed) * 0.5 + 0.25;
                printed = lerp(grey.xxx, float3(0.9, 0.05, 0.05), corrupt);
                printed = lerp(printed, float3(1.0, 0.85, 0.1), front * 0.6);
            }
            return float4(printed, 1.0);
        }

        float4 FragLines(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            LoadStyle(uv);
            float3 original = SampleColor(uv);
            float raw = SampleSceneDepth(uv);
            bool sky = IsSky(raw);
            float eyeDepth = sky ? 1e6 : LinearEyeDepth(raw, _ZBufferParams);
            Space space = MakeSpace(uv, raw, sky, eyeDepth);
            float key = KeyStone(uv, space, original, eyeDepth, sky, 0.0);
            return float4(lerp(original, original * _KeyColor.rgb, key * _Print1.x), 1.0);
        }
        ENDHLSL

        Pass
        {
            Name "Chromolithograph Print"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPrint
            ENDHLSL
        }

        Pass
        {
            Name "Chromolithograph Lines"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragLines
            ENDHLSL
        }
    }
}
