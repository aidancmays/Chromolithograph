using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Chromolithograph
{
    /// <summary>
    /// Prints every frame as a chromolithograph (after URP's post-processing, before UI): the frame is separated into
    /// the palette's inks, each ink breaks into its own grain and sits slightly out of register, the key stone draws
    /// outlines and shadow hatching, inks drop out with distance until only paper is left, and the whole lot is
    /// multiplied onto paper. Inks and settings (the Look) live on the palette; volume overrides change the Look per area.
    ///
    /// Add it to the renderer (PC_Renderer > Add Renderer Feature > Chromolithograph) and give it a palette.
    ///
    /// Grain, paper and hatching are mapped onto the surfaces in world space, so they stay on things as the camera moves.
    ///
    /// Objects with a ChromoStyled component are first drawn into a style buffer (one id per pixel), and the print uses
    /// that style's settings there instead of the Look.
    ///
    /// Portal views (disabled cameras rendered on request, post-processing off) only get the key lines, drawn with their own depth, so things
    /// seen through a portal are outlined too; the main camera then prints the whole frame, portal screens included,
    /// in one consistent print.
    /// </summary>
    [DisallowMultipleRendererFeature("Chromolithograph")]
    public class ChromolithographFeature : ScriptableRendererFeature
    {
        [Tooltip("Inks, paper and key stone. Bake it in its inspector after changing it.")]
        public ChromoPalette palette;

        [Tooltip("Print the Scene view too.")]
        public bool showInSceneView = true;

        [Tooltip("Draw key lines inside portal views (cameras with post-processing off).")]
        public bool linesInPortalViews = true;

        [Tooltip("Print the sky: pixels with nothing behind them get the Look's Sky settings, and ChromoSky's time-of-day styles, glow and horizon tint apply. Off = the sky prints as bare paper and ChromoSky is ignored (for projects with no visible sky).")]
        public bool sky = true;

        [Tooltip("Print corruption: ChromoCorrupted objects and the corruption map. Off = they are not gathered or drawn and the print has no corruption at all.")]
        public bool corruption = true;

        [SerializeField, HideInInspector] Shader shader;

        [SerializeField, HideInInspector] Shader hintsShader;
        [SerializeField, HideInInspector] Shader motionShader;

        Material material;
        Material hintsMaterial;
        Material motionMaterial;
        PrintPass pass;
        readonly ChromoLook look = new ChromoLook();
        bool warnedUnbaked;

        const string ShaderName = "Hidden/Chromolithograph/Chromolithograph";
        const string HintsShaderName = "Hidden/Chromolithograph/ChromoHints";
        const string MotionShaderName = "Hidden/Chromolithograph/ChromoMotion";

        public override void Create()
        {
            pass = new PrintPass { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing };
        }

        protected override void Dispose(bool disposing)
        {
            ChromoStyleOwners.RestoreAll();
            CoreUtils.Destroy(material);
            CoreUtils.Destroy(hintsMaterial);
            CoreUtils.Destroy(motionMaterial);
            material = null;
            hintsMaterial = null;
            motionMaterial = null;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            CameraType type = renderingData.cameraData.cameraType;
            if (type == CameraType.Preview || type == CameraType.Reflection) return;
            if (type == CameraType.SceneView && !showInSceneView) return;
            if (ChromoDebug.Mode == ChromoDebug.View.Off) return;

            if (palette == null || !palette.IsBaked)
            {
                if (!warnedUnbaked)
                {
                    Debug.LogWarning(palette == null
                        ? "Chromolithograph: no palette on the renderer feature, so nothing is printed."
                        : $"Chromolithograph: palette '{palette.name}' isn't baked yet. Select it and press Bake.");
                    warnedUnbaked = true;
                }
                return;
            }
            warnedUnbaked = false;

            // The palette's Look, with anything a Chromolithograph volume override ticks laid over it.
            palette.look.Resolve(VolumeManager.instance.stack.GetComponent<ChromoVolume>(), look);
            if (look.intensity <= 0f) return;

            // Portal views come from disabled cameras rendered on request with post-processing off. Only those get
            // lines alone; a normal camera prints even if its own post-processing is off.
            Camera camera = renderingData.cameraData.camera;
            bool linesOnly = type == CameraType.Game && !renderingData.cameraData.postProcessEnabled && camera != null && !camera.enabled;
            if (linesOnly && !linesInPortalViews) return;

            if (!EnsureMaterial()) return;
            if (!linesOnly && type == CameraType.Game) ChromoStrip.PrintedFrame = Time.frameCount;

            pass.Setup(material, hintsMaterial, motionMaterial, palette, look, linesOnly, sky, corruption);
            pass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            pass.requiresIntermediateTexture = true;
            renderer.EnqueuePass(pass);
        }

        bool EnsureMaterial()
        {
            if (material != null) return true;
            if (shader == null) shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"Chromolithograph: shader '{ShaderName}' not found.");
                return false;
            }
            material = CoreUtils.CreateEngineMaterial(shader);
            if (hintsShader == null) hintsShader = Shader.Find(HintsShaderName);
            if (hintsShader != null) hintsMaterial = CoreUtils.CreateEngineMaterial(hintsShader);
            else Debug.LogError($"Chromolithograph: shader '{HintsShaderName}' not found, so object styles are off.");
            if (motionShader == null) motionShader = Shader.Find(MotionShaderName);
            if (motionShader != null) motionMaterial = CoreUtils.CreateEngineMaterial(motionShader);
            else Debug.LogError($"Chromolithograph: shader '{MotionShaderName}' not found, so moving objects' texture stays in the world.");
            return true;
        }

        class PrintPass : ScriptableRenderPass
        {
            static readonly Dictionary<Camera, MaterialPropertyBlock> blocks = new Dictionary<Camera, MaterialPropertyBlock>();

            Material material;
            Material hintsMaterial;
            Material motionMaterial;
            ChromoPalette palette;
            ChromoLook look;
            bool linesOnly;
            bool skyOn = true;
            bool corruptionOn = true;

            readonly Vector4[] inkColors = new Vector4[ChromoPalette.MaxInks];
            readonly Vector4[] inkParams = new Vector4[ChromoPalette.MaxInks];
            readonly Vector4[] inkParams2 = new Vector4[ChromoPalette.MaxInks];

            // Style 0 is the Look; 1 to MaxStyles - 1 are the ChromoStyles holding ids (ChromoStyles).
            const int MaxStyles = ChromoStyles.MaxStyles;
            readonly ChromoLook styleLook = new ChromoLook();

            struct StyledDraw
            {
                public Renderer renderer;
                public int submeshes;
                public float id; // pass 0: the style id; pass 2: the corruption amount
                public int pass; // ChromoHints.shader: 0 = style id, 1 = subject mark, 2 = corruption
            }

            class HintsData
            {
                public Material material;
                public StyledDraw[] draws;
                public int count;
                public Matrix4x4 view, viewProj;
                public RendererListHandle list;
                public bool skipList;
                // In play: one culled renderer list per style id in use (renderers marked by ChromoStyled.LayerBit).
                public RendererListHandle[] styleLists;
                public float[] styleListIds;
                public int styleListCount;
            }

            // Lit and unlit opaque and transparent shaders: the style lists draw any renderer whose shader has one of these.
            static readonly List<ShaderTagId> StyleListTags = new List<ShaderTagId>
            {
                new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"), new ShaderTagId("SRPDefaultUnlit"),
            };
            readonly List<int> styleIds = new List<int>();
            static int sweepFrame = -1;

            // Shaders that write their own style ids (per material, like the terrain's) add a pass with this LightMode.
            static readonly ShaderTagId HintsTag = new ShaderTagId("ChromoHints");

            StyledDraw[] draws = new StyledDraw[64];
            readonly List<Material> sharedMaterials = new List<Material>();

            class MotionData
            {
                public Material material;
                public ChromoMoving.Part[] parts;
                public int count;
                public Matrix4x4 view, viewProj;
                public RendererListHandle list;
            }

            // Shaders that move their own vertices (wind sway) add a pass with this LightMode that writes where each
            // pixel's texture comes from (the unmoved position), so the texture sways with them.
            static readonly ShaderTagId MotionTag = new ShaderTagId("ChromoMotion");

            ChromoMoving.Part[] movingParts = new ChromoMoving.Part[32];

            class PassData
            {
                public Material material;
                public int shaderPass;
                public TextureHandle source;
                public MaterialPropertyBlock block;
                public TextureHandle hints;
                public TextureHandle motionPos, motionNormal;
                public bool motionOn;
            }

            // Performance plan P0: where the print's main-thread time goes, and how many renderers it draws one by one.
            static readonly Unity.Profiling.ProfilerMarker GatherStylesMarker = new Unity.Profiling.ProfilerMarker("Chromo.GatherStyles");
            static readonly Unity.Profiling.ProfilerMarker GatherSubjectsMarker = new Unity.Profiling.ProfilerMarker("Chromo.GatherSubjects");
            static readonly Unity.Profiling.ProfilerMarker GatherCorruptedMarker = new Unity.Profiling.ProfilerMarker("Chromo.GatherCorrupted");
            static readonly Unity.Profiling.ProfilerMarker GatherMovingMarker = new Unity.Profiling.ProfilerMarker("Chromo.GatherMoving");
            static readonly Unity.Profiling.ProfilerMarker FillMarker = new Unity.Profiling.ProfilerMarker("Chromo.FillProperties");
            static readonly Unity.Profiling.ProfilerMarker DrawStyledMarker = new Unity.Profiling.ProfilerMarker("Chromo.DrawStyled");
            static readonly Unity.Profiling.ProfilerMarker DrawMovingMarker = new Unity.Profiling.ProfilerMarker("Chromo.DrawMoving");

            public PrintPass()
            {
                profilingSampler = new ProfilingSampler("Chromolithograph");
            }

            public void Setup(Material material, Material hintsMaterial, Material motionMaterial, ChromoPalette palette, ChromoLook look, bool linesOnly, bool sky, bool corruption)
            {
                skyOn = sky;
                corruptionOn = corruption;
                this.motionMaterial = motionMaterial;
                this.material = material;
                this.hintsMaterial = hintsMaterial;
                this.palette = palette;
                this.look = look;
                this.linesOnly = linesOnly;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (resources.isActiveTargetBackBuffer) return;

                Camera camera = cameraData.camera;
                // One block per camera, since portal cameras record their passes before the frame's draws run.
                if (!blocks.TryGetValue(camera, out MaterialPropertyBlock block))
                {
                    if (blocks.Count > 32) blocks.Clear(); // forget destroyed cameras
                    block = new MaterialPropertyBlock();
                    blocks[camera] = block;
                }
                // This camera's view now, not renderer.isVisible (last frame's, any camera): a chunk or character that
                // has just come into view gets its style and texture mapping on its first frame.
                GeometryUtility.CalculateFrustumPlanes(cameraData.GetProjectionMatrix() * cameraData.GetViewMatrix(), frustum);
                int drawCount;
                styleIds.Clear();
                using (GatherStylesMarker.Auto()) drawCount = ChromoPrintStats.SkipStyles ? 0 : GatherStyles();
                using (GatherSubjectsMarker.Auto()) drawCount = GatherSubjects(drawCount);
                if (corruptionOn) using (GatherCorruptedMarker.Auto()) drawCount = GatherCorrupted(drawCount);
                using (FillMarker.Auto()) FillProperties(block, cameraData);
                if (!linesOnly)
                {
                    ChromoPrintStats.StyledRenderers = CountStyledRenderers();
                    ChromoPrintStats.StyledDraws = drawCount;
                }

                TextureHandle target = resources.activeColorTexture;

                // The style buffer: R = style id per pixel (0 = the plain Look), drawn from shaders' ChromoHints passes
                // and ChromoStyled renderers; G = 1 on subjects (ChromoSubject); B = corruption (ChromoCorrupted), the
                // most of any source. Subjects and corruption are drawn last, each only into its own channel.
                RenderTextureDescriptor hintsDesc = cameraData.cameraTargetDescriptor;
                hintsDesc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
                hintsDesc.depthStencilFormat = GraphicsFormat.None;
                hintsDesc.msaaSamples = 1;
                TextureHandle hints = UniversalRenderer.CreateRenderGraphTexture(renderGraph, hintsDesc, "_ChromoHints", true);
                using (var builder = renderGraph.AddRasterRenderPass<HintsData>("Chromolithograph Styles", out HintsData hd, profilingSampler))
                {
                    hd.material = hintsMaterial;
                    hd.draws = draws;
                    hd.count = hintsMaterial != null ? drawCount : 0;
                    hd.skipList = ChromoPrintStats.SkipStyles;
                    if (hd.styleLists == null)
                    {
                        hd.styleLists = new RendererListHandle[MaxStyles];
                        hd.styleListIds = new float[MaxStyles];
                    }
                    hd.styleListCount = 0;
                    hd.view = cameraData.GetViewMatrix();
                    hd.viewProj = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), true) * hd.view;
                    UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                    UniversalLightData lightData = frameData.Get<UniversalLightData>();
                    DrawingSettings drawing = RenderingUtils.CreateDrawingSettings(HintsTag, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                    var filtering = new FilteringSettings(RenderQueueRange.all);
                    hd.list = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing, filtering));
                    builder.UseRendererList(hd.list);
                    if (hintsMaterial != null && !ChromoPrintStats.SkipStyles)
                    {
                        for (int i = 0; i < styleIds.Count && hd.styleListCount < MaxStyles; i++)
                        {
                            DrawingSettings styleDrawing = RenderingUtils.CreateDrawingSettings(StyleListTags, renderingData, cameraData, lightData, SortingCriteria.None);
                            styleDrawing.overrideMaterial = hintsMaterial;
                            styleDrawing.overrideMaterialPassIndex = 0;
                            styleDrawing.perObjectData = PerObjectData.None;
                            var styleFiltering = new FilteringSettings(RenderQueueRange.all, -1, ChromoStyled.LayerBit(styleIds[i]));
                            RendererListHandle styleList = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, styleDrawing, styleFiltering));
                            builder.UseRendererList(styleList);
                            hd.styleLists[hd.styleListCount] = styleList;
                            hd.styleListIds[hd.styleListCount++] = styleIds[i];
                        }
                    }
                    builder.SetRenderAttachment(hints, 0, AccessFlags.Write);
                    if (resources.cameraDepthTexture.IsValid()) builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.AllowPassCulling(false);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((HintsData d, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalMatrix(Ids.HintsView, d.view);
                        context.cmd.SetGlobalMatrix(Ids.HintsViewProj, d.viewProj);
                        // Shaders' own ChromoHints passes first (materials a ChromoStyled owns have theirs off: ChromoStyleOwners).
                        if (!d.skipList) context.cmd.DrawRendererList(d.list);
                        using var drawMarker = DrawStyledMarker.Auto();
                        // ChromoStyled renderers, one list per style (in play; edit mode draws them one by one below).
                        for (int i = 0; i < d.styleListCount; i++)
                        {
                            context.cmd.SetGlobalFloat(Ids.StyleId, d.styleListIds[i]);
                            context.cmd.DrawRendererList(d.styleLists[i]);
                        }
                        for (int i = 0; i < d.count; i++)
                        {
                            StyledDraw draw = d.draws[i];
                            if (draw.renderer == null) continue;
                            if (draw.pass == 0) context.cmd.SetGlobalFloat(Ids.StyleId, draw.id);
                            else if (draw.pass == 2) context.cmd.SetGlobalFloat(Ids.CorruptAmount, draw.id);
                            for (int sub = 0; sub < draw.submeshes; sub++)
                                context.cmd.DrawRenderer(draw.renderer, d.material, sub, draw.pass);
                        }
                    });
                }
                // Moving objects: where each pixel's texture is mapped from (ChromoMoving), so it travels with them.
                bool motionOn = motionMaterial != null && !linesOnly && !ChromoPrintStats.SkipMotion;
                int movingCount = 0;
                if (motionOn)
                {
                    using (GatherMovingMarker.Auto()) movingCount = GatherMoving();
                    ChromoPrintStats.MovingDraws = movingCount;
                }
                TextureHandle motionPos = TextureHandle.nullHandle, motionNormal = TextureHandle.nullHandle;
                if (motionOn)
                {
                    RenderTextureDescriptor posDesc = hintsDesc;
                    posDesc.graphicsFormat = GraphicsFormat.R32G32B32A32_SFloat;
                    motionPos = UniversalRenderer.CreateRenderGraphTexture(renderGraph, posDesc, "_ChromoMotionPos", true);
                    RenderTextureDescriptor normalDesc = hintsDesc;
                    normalDesc.graphicsFormat = GraphicsFormat.R8G8B8A8_SNorm;
                    motionNormal = UniversalRenderer.CreateRenderGraphTexture(renderGraph, normalDesc, "_ChromoMotionNormal", true);
                    using (var builder = renderGraph.AddRasterRenderPass<MotionData>("Chromolithograph Moving", out MotionData md, profilingSampler))
                    {
                        md.material = motionMaterial;
                        md.parts = movingParts;
                        md.count = movingCount;
                        md.view = cameraData.GetViewMatrix();
                        md.viewProj = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), true) * md.view;
                        UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                        UniversalLightData lightData = frameData.Get<UniversalLightData>();
                        DrawingSettings drawing = RenderingUtils.CreateDrawingSettings(MotionTag, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                        md.list = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing, new FilteringSettings(RenderQueueRange.all)));
                        builder.UseRendererList(md.list);
                        builder.SetRenderAttachment(motionPos, 0, AccessFlags.Write);
                        builder.SetRenderAttachment(motionNormal, 1, AccessFlags.Write);
                        if (resources.cameraDepthTexture.IsValid()) builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                        builder.AllowPassCulling(false);
                        builder.AllowGlobalStateModification(true);
                        builder.SetRenderFunc((MotionData d, RasterGraphContext context) =>
                        {
                            RasterCommandBuffer cmd = context.cmd;
                            cmd.SetGlobalMatrix(Ids.HintsView, d.view);
                            cmd.SetGlobalMatrix(Ids.HintsViewProj, d.viewProj);
                            // Shaders' own ChromoMotion passes first, so a ChromoMoving component on top still wins.
                            cmd.DrawRendererList(d.list);
                            using var drawMarker = DrawMovingMarker.Auto();
                            for (int i = 0; i < d.count; i++)
                            {
                                ChromoMoving.Part part = d.parts[i];
                                if (part.renderer == null) continue;
                                cmd.SetGlobalMatrix(Ids.MotionRef, part.reference);
                                cmd.SetGlobalMatrix(Ids.MotionRefNormal, part.referenceNormal);
                                int shaderPass = 0;
                                if (part.rest != null)
                                {
                                    shaderPass = 1;
                                    cmd.SetGlobalBuffer(Ids.Rest, part.rest);
                                    cmd.SetGlobalVector(Ids.RestLayout, new Vector4(part.stride, part.positionOffset, part.positionFormat, part.normalOffset));
                                    cmd.SetGlobalFloat(Ids.RestNormalFormat, part.normalFormat);
                                }
                                for (int sub = 0; sub < part.submeshes; sub++)
                                    cmd.DrawRenderer(part.renderer, d.material, sub, shaderPass);
                            }
                        });
                    }
                }

                TextureDesc desc = renderGraph.GetTextureDesc(target);
                desc.name = "_ChromolithographSource";
                desc.clearBuffer = false;
                TextureHandle copy = renderGraph.CreateTexture(desc);
                renderGraph.AddBlitPass(target, copy, Vector2.one, Vector2.zero, passName: "Chromolithograph Copy");

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Chromolithograph", out PassData data, profilingSampler))
                {
                    data.material = material;
                    data.shaderPass = linesOnly ? 1 : 0;
                    data.source = copy;
                    data.block = block;
                    builder.UseTexture(copy, AccessFlags.Read);
                    builder.UseTexture(hints, AccessFlags.Read);
                    data.hints = hints;
                    data.motionOn = motionOn;
                    data.motionPos = motionPos;
                    data.motionNormal = motionNormal;
                    if (data.motionOn)
                    {
                        builder.UseTexture(motionPos, AccessFlags.Read);
                        builder.UseTexture(motionNormal, AccessFlags.Read);
                    }
                    if (resources.cameraDepthTexture.IsValid()) builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    if (resources.cameraNormalsTexture.IsValid()) builder.UseTexture(resources.cameraNormalsTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(target, 0, AccessFlags.Write);
                    builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
                    {
                        RTHandle source = d.source;
                        d.block.SetTexture(Ids.BlitTexture, source);
                        d.block.SetTexture(Ids.Hints, (RTHandle)d.hints);
                        d.block.SetFloat(Ids.MotionOn, d.motionOn ? 1f : 0f);
                        if (d.motionOn)
                        {
                            d.block.SetTexture(Ids.MotionPos, (RTHandle)d.motionPos);
                            d.block.SetTexture(Ids.MotionNormal, (RTHandle)d.motionNormal);
                        }
                        d.block.SetVector(Ids.BlitScaleBias, new Vector4(1f, 1f, 0f, 0f));
                        context.cmd.DrawProcedural(Matrix4x4.identity, d.material, d.shaderPass, MeshTopology.Triangles, 3, 1, d.block);
                    });
                }
            }

            readonly Plane[] frustum = new Plane[6];

            bool InView(Renderer renderer) =>
                renderer.enabled && renderer.gameObject.activeInHierarchy && GeometryUtility.TestPlanesAABB(frustum, renderer.bounds);

            /// <summary>InView with the renderer's object already known (ChromoStyled keeps it), saving a native call per renderer.</summary>
            bool InView(Renderer renderer, GameObject owner) =>
                owner.activeInHierarchy && renderer.enabled && GeometryUtility.TestPlanesAABB(frustum, renderer.bounds);

            static int CountStyledRenderers()
            {
                int total = 0;
                IReadOnlyList<ChromoStyled> all = ChromoStyled.All;
                for (int s = 0; s < all.Count; s++)
                {
                    if (all[s] != null) total += all[s].Renderers.Count;
                }
                return total;
            }

            /// <summary>
            /// Gets the ChromoStyled renderers ready. In play, lists the style ids in use (each drawn later as one culled
            /// renderer list) and returns 0. In edit mode, lists the visible renderers one by one with their ids.
            /// </summary>
            int GatherStyles()
            {
                ChromoStyled.FlushPending(); // children added this frame (streamed terrain) are gathered once, here
                ChromoStyleOwners.BeginFrame();
                styleIds.Clear();
                if (!Application.isPlaying) return GatherStylesOneByOne();
                bool sweep = sweepFrame != Time.frameCount; // once a frame, however many cameras
                sweepFrame = Time.frameCount;
                IReadOnlyList<ChromoStyled> all = ChromoStyled.All;
                for (int s = 0; s < all.Count; s++)
                {
                    ChromoStyled styled = all[s];
                    if (styled == null) continue;
                    int id = styled.StyleId; // also keeps its renderers' layer bit up to date
                    if (id == 0) continue;
                    if (!styleIds.Contains(id)) styleIds.Add(id);
                    if (sweep) SweepOwners(styled);
                }
                return 0;
            }

            /// <summary>
            /// Tells ChromoStyleOwners which materials this ChromoStyled owns, a slice of its renderers a frame (every
            /// renderer about once a second): asking thousands of renderers for their materials every frame cost ms.
            /// </summary>
            void SweepOwners(ChromoStyled styled)
            {
                IReadOnlyList<Renderer> renderers = styled.Renderers;
                IReadOnlyList<GameObject> owners = styled.RendererObjects;
                int n = renderers.Count;
                if (n == 0) return;
                int step = Mathf.Min(n, Mathf.Max(64, n / 60 + 1));
                int r = styled.sweepCursor;
                for (int k = 0; k < step; k++, r++)
                {
                    if (r >= n) r = 0;
                    Renderer renderer = renderers[r];
                    if (renderer == null || !owners[r].activeInHierarchy || !renderer.enabled) continue;
                    renderer.GetSharedMaterials(sharedMaterials);
                    for (int m = 0; m < sharedMaterials.Count; m++) ChromoStyleOwners.OwnedBy(sharedMaterials[m]);
                }
                styled.sweepCursor = r >= n ? 0 : r;
            }

            /// <summary>Lists the visible ChromoStyled renderers with the ids their styles hold (edit mode).</summary>
            int GatherStylesOneByOne()
            {
                int count = 0;
                IReadOnlyList<ChromoStyled> all = ChromoStyled.All;
                for (int s = 0; s < all.Count; s++)
                {
                    ChromoStyled styled = all[s];
                    if (styled == null) continue;
                    int id = styled.StyleId;
                    if (id == 0) continue;
                    IReadOnlyList<Renderer> renderers = styled.Renderers;
                    IReadOnlyList<GameObject> owners = styled.RendererObjects;
                    for (int r = 0; r < renderers.Count; r++)
                    {
                        Renderer renderer = renderers[r];
                        // A renderer's object outlives it, so checking the renderer is enough.
                        if (renderer == null || !InView(renderer, owners[r])) continue;
                        // The ChromoStyled owns this renderer's style, so its shader's own style pass needn't draw it.
                        renderer.GetSharedMaterials(sharedMaterials);
                        for (int m = 0; m < sharedMaterials.Count; m++) ChromoStyleOwners.OwnedBy(sharedMaterials[m]);
                        if (count == draws.Length) System.Array.Resize(ref draws, count * 2);
                        draws[count++] = new StyledDraw { renderer = renderer, submeshes = Mathf.Max(1, sharedMaterials.Count), id = id };
                    }
                }
                return count;
            }

            /// <summary>Adds the visible ChromoSubject renderers after the style draws, which end at count.</summary>
            int GatherSubjects(int count)
            {
                IReadOnlyList<ChromoSubject> all = ChromoSubject.All;
                for (int s = 0; s < all.Count; s++)
                {
                    ChromoSubject subject = all[s];
                    if (subject == null) continue;
                    IReadOnlyList<Renderer> renderers = subject.Renderers;
                    for (int r = 0; r < renderers.Count; r++)
                    {
                        Renderer renderer = renderers[r];
                        if (renderer == null || !InView(renderer)) continue;
                        renderer.GetSharedMaterials(sharedMaterials);
                        if (count == draws.Length) System.Array.Resize(ref draws, count * 2);
                        draws[count++] = new StyledDraw { renderer = renderer, submeshes = Mathf.Max(1, sharedMaterials.Count), pass = 1 };
                    }
                }
                return count;
            }

            /// <summary>Adds the visible renderers of ChromoCorrupted objects with any corruption, after count.</summary>
            int GatherCorrupted(int count)
            {
                IReadOnlyList<ChromoCorrupted> all = ChromoCorrupted.All;
                for (int s = 0; s < all.Count; s++)
                {
                    ChromoCorrupted corrupted = all[s];
                    if (corrupted == null || corrupted.amount <= 0f) continue;
                    IReadOnlyList<Renderer> renderers = corrupted.Renderers;
                    for (int r = 0; r < renderers.Count; r++)
                    {
                        Renderer renderer = renderers[r];
                        if (renderer == null || !InView(renderer)) continue;
                        renderer.GetSharedMaterials(sharedMaterials);
                        if (count == draws.Length) System.Array.Resize(ref draws, count * 2);
                        draws[count++] = new StyledDraw
                        {
                            renderer = renderer, submeshes = Mathf.Max(1, sharedMaterials.Count), id = Mathf.Clamp01(corrupted.amount), pass = 2,
                        };
                    }
                }
                return count;
            }

            /// <summary>Lists the visible renderers of ChromoMoving objects.</summary>
            int GatherMoving()
            {
                int count = 0;
                IReadOnlyList<ChromoMoving> all = ChromoMoving.All;
                for (int m = 0; m < all.Count; m++)
                {
                    ChromoMoving moving = all[m];
                    if (moving == null) continue;
                    IReadOnlyList<ChromoMoving.Part> parts = moving.Parts;
                    for (int p = 0; p < parts.Count; p++)
                    {
                        Renderer renderer = parts[p].renderer;
                        if (renderer == null || !InView(renderer)) continue;
                        if (count == movingParts.Length) System.Array.Resize(ref movingParts, count * 2);
                        movingParts[count++] = parts[p];
                    }
                }
                return count;
            }

            readonly Vector4[][] styleVectors = NewStyleVectors();

            static Vector4[][] NewStyleVectors()
            {
                var vectors = new Vector4[StyleVectorCount][];
                for (int i = 0; i < vectors.Length; i++) vectors[i] = new Vector4[StyleSlots];
                return vectors;
            }

            // Two more slots than there are style ids: the sky's (ChromoSky's time-of-day styles) and the sunrise and
            // sunset glow's, which the shader blends over the sky's by a mask around the sun.
            const int SkySlot = MaxStyles;
            const int GlowSlot = MaxStyles + 1;
            const int StyleSlots = MaxStyles + 2;
            const int StyleVectorCount = 12;
            readonly Vector4[][] skyFrom = NewStyleVectors(), skyTo = NewStyleVectors();

            /// <summary>One look's settings, packed the way the shader reads them.</summary>
            static void Pack(ChromoLook v, Vector4[][] into, int style)
            {
                into[0][style] = new Vector4(v.intensity, v.grainSize, v.grainSoftness, v.registration);
                into[1][style] = new Vector4(v.inkVariation, v.paperFibre, v.foxing, v.skyInk);
                into[2][style] = new Vector4(v.grainStrength, v.washBlur, v.saturation, v.contrast);
                into[3][style] = new Vector4(v.handColouring, v.evenTexture, v.textureDepth, v.fogSkyTint);
                into[4][style] = new Vector4(v.wetness, v.bloomSize, v.edgePooling, v.granulation);
                into[5][style] = new Vector4(v.skySaturation, v.skyGrainStrength, v.skyGrainSize, v.exposure);
                into[6][style] = new Vector4(v.lineWidth, v.lineStrength, v.depthSensitivity, v.creaseSensitivity);
                into[7][style] = new Vector4(v.lineWobble, v.lineFarDistance, v.lineHighlightBreak, v.shadowBelow);
                into[8][style] = new Vector4(v.colourEdges, v.colourSensitivity, v.depthGap, v.styleOutlines);
                into[9][style] = new Vector4(v.hatching, v.hatchSpacing, v.hatchBelow, v.shadowFill);
                into[10][style] = new Vector4(v.fadeStart, Mathf.Max(v.fadeEnd, v.fadeStart + 1f), v.distanceFade, v.shadowSoftness);
                into[11][style] = new Vector4(v.skyNightFill, v.skyNightBelow, v.skyNightSoftness, 0f);
            }

            void FillStyles(MaterialPropertyBlock block)
            {
                for (int i = 0; i < MaxStyles; i++)
                {
                    ChromoLook l = look;
                    ChromoStyle style = ChromoStyles.StyleOf(i);
                    if (style != null)
                    {
                        style.Apply(look, styleLook);
                        l = styleLook;
                    }
                    Pack(l, styleVectors, i);
                }
                FillSkySlot();
                for (int k = 0; k < StyleVectorCount; k++) block.SetVectorArray(Ids.StyleVectors[k], styleVectors[k]);
                FillGlow(block);
            }

            // The sky prints with the Look, or with the procedural sky's time-of-day styles blended (each applied over
            // the Look, then every setting blended).
            void FillSkySlot()
            {
                bool live = skyOn && ChromoSky.IsLive;
                if (!live || (ChromoSky.Style == null && ChromoSky.StyleTo == null))
                {
                    for (int k = 0; k < StyleVectorCount; k++) styleVectors[k][SkySlot] = styleVectors[k][0];
                }
                else
                {
                    PackStyle(ChromoSky.Style, skyFrom);
                    PackStyle(ChromoSky.StyleTo, skyTo);
                    float t = Mathf.Clamp01(ChromoSky.StyleBlend);
                    for (int k = 0; k < StyleVectorCount; k++)
                        styleVectors[k][SkySlot] = Vector4.Lerp(skyFrom[k][0], skyTo[k][0], t);
                }
                // Sky off: the sky slot keeps the Look but lays no ink (Sky Ink 0 = bare paper).
                if (!skyOn) styleVectors[1][SkySlot].w = 0f;
                glowAmount = live && ChromoSky.GlowStyle != null ? Mathf.Clamp01(ChromoSky.GlowAmount) : 0f;
                if (glowAmount > 0f)
                {
                    PackStyle(ChromoSky.GlowStyle, skyFrom);
                    for (int k = 0; k < StyleVectorCount; k++) styleVectors[k][GlowSlot] = skyFrom[k][0];
                }
                else
                {
                    for (int k = 0; k < StyleVectorCount; k++) styleVectors[k][GlowSlot] = styleVectors[k][SkySlot];
                }
            }

            float glowAmount;

            // The glow's mask: towards the sun (x y z, w = amount), local up (x y z), and its width and height in
            // degrees.
            void FillGlow(MaterialPropertyBlock block)
            {
                Vector3 sun = ChromoSky.SunDirection.sqrMagnitude > 1e-8f ? ChromoSky.SunDirection.normalized : Vector3.up;
                Vector3 up = ChromoSky.Up.sqrMagnitude > 1e-8f ? ChromoSky.Up.normalized : Vector3.up;
                block.SetVector(Ids.GlowSun, new Vector4(sun.x, sun.y, sun.z, glowAmount));
                block.SetVector(Ids.GlowUp, new Vector4(up.x, up.y, up.z, 0f));
                block.SetVector(Ids.GlowShape, new Vector4(Mathf.Max(ChromoSky.GlowWidth, 0.1f), Mathf.Max(ChromoSky.GlowHeight, 0.1f), 0f, 0f));
            }

            void PackStyle(ChromoStyle style, Vector4[][] into)
            {
                if (style == null)
                {
                    Pack(look, into, 0);
                    return;
                }
                style.Apply(look, styleLook);
                Pack(styleLook, into, 0);
            }

            // Corruption's look (the Look's Corruption settings, not per style) and its inks. The crumple's size goes in
            // as 1080p pixels per texel (a facet is about 36.6 texels across); the blooms' as 1 / metres.
            void FillCorruption(MaterialPropertyBlock block)
            {
                // Corruption off: override the corruption map's global "on" flag for this print, so it is skipped too.
                if (!corruptionOn) block.SetVector(Ids.CorruptionMap1, Vector4.zero);
                block.SetTexture(Ids.CrumpleTex, ChromoCrumple.Texture);
                block.SetVector(Ids.Corrupt1, new Vector4(look.corruptDrain, look.corruptCrumple, Mathf.Max(look.corruptCrumplePixels, 1f) / 36.6f, look.corruptCreaseDark));
                block.SetVector(Ids.Corrupt2, new Vector4(look.corruptCreaseLines, look.corruptFacets, Mathf.Max(look.corruptEdge, 0.01f), look.corruptBranching));
                block.SetVector(Ids.Corrupt3, new Vector4(look.corruptWetFront, look.corruptBlooms, 1f / Mathf.Max(look.corruptBloomSize, 0.01f), look.corruptSalt));
                block.SetVector(Ids.Corrupt4, new Vector4(look.corruptBloomDrift, look.corruptBloomMorph, look.corruptBloomsLarge, look.corruptBloomsSmall));
                block.SetVector(Ids.CorruptDark, palette.corruptDark.linear);
                block.SetVector(Ids.CorruptMid, palette.corruptMid.linear);
                block.SetVector(Ids.CorruptLight, palette.corruptLight.linear);
                block.SetVector(Ids.CorruptBloom, palette.corruptBloom.linear);
                block.SetVector(Ids.CorruptBloomRim, palette.corruptBloomRim.linear);
            }

            void FillProperties(MaterialPropertyBlock block, UniversalCameraData cameraData)
            {
                block.Clear();

                int count = palette.PrintedInkCount;
                for (int i = 0; i < ChromoPalette.MaxInks; i++)
                {
                    if (i < count)
                    {
                        ChromoPalette.Ink ink = palette.inks[i];
                        Color c = ink.color.linear;
                        inkColors[i] = new Vector4(c.r, c.g, c.b, ink.density);
                        inkParams[i] = new Vector4(ink.grainScale, ink.fadeOrder, ink.registration.x, ink.registration.y);
                        inkParams2[i] = new Vector4(ink.grainBoost, 0f, 0f, 0f);
                    }
                    else
                    {
                        inkColors[i] = new Vector4(1f, 1f, 1f, 0f);
                        inkParams[i] = new Vector4(1f, 1f, 0f, 0f);
                        inkParams2[i] = Vector4.zero;
                    }
                }
                block.SetVectorArray(Ids.InkColor, inkColors);
                block.SetVectorArray(Ids.InkParams, inkParams);
                block.SetVectorArray(Ids.InkParams2, inkParams2);
                block.SetFloat(Ids.InkCount, count);
                block.SetTexture(Ids.SeparationA, palette.SeparationA);
                block.SetTexture(Ids.SeparationB, palette.SeparationB);
                block.SetTexture(Ids.GrainA, palette.GrainA);
                block.SetTexture(Ids.GrainB, palette.GrainB);
                block.SetTexture(Ids.PaperTex, palette.PaperTexture);
                block.SetVector(Ids.PaperColor, palette.paper.linear);
                block.SetVector(Ids.FoxColor, palette.foxing.linear);
                block.SetVector(Ids.KeyColor, palette.key.linear);
                block.SetVector(Ids.ShadowColor, palette.shadow.linear);
                block.SetVector(Ids.Subject, new Vector4(look.subjectHold, look.backgroundFade, 0f, 0f));
                block.SetVector(Ids.NightSky, palette.nightSky.linear);
                FillCorruption(block);
                // The sky's horizon colour for Fog Sky Tint (already display-linear); w = 0 while no sky is running.
                Color horizon = ChromoSky.Horizon;
                block.SetVector(Ids.Horizon, new Vector4(horizon.r, horizon.g, horizon.b, skyOn && ChromoSky.IsLive ? 1f : 0f));
                block.SetVector(Ids.Sizes, new Vector4(ChromoPalette.SeparationSize, ChromoPalette.GrainSize, ChromoPalette.PaperSize, 0f));

                float height = Mathf.Max(1f, cameraData.cameraTargetDescriptor.height);
                // World size of one 1080p pixel: at 1 m depth for perspective (the shader scales by depth), flat for
                // orthographic (x = 1 tells the shader to ignore depth).
                Camera camera = cameraData.camera;
                bool ortho = camera != null && camera.orthographic;
                float pixelWorld = camera == null ? 0.001f
                    : ortho ? 2f * camera.orthographicSize / 1080f
                    : 2f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) / 1080f;
                block.SetVector(Ids.PaperOffset, new Vector4(ortho ? 1f : 0f, 0f, 1080f / height, pixelWorld));

                FillStyles(block);
                block.SetVector(Ids.Strip1, new Vector4(ChromoStrip.Centre.x, ChromoStrip.Centre.y, ChromoStrip.ToCircle.x, ChromoStrip.ToCircle.y));
                float stripAmount = cameraData.cameraType == CameraType.Game ? ChromoStrip.Amount : 0f;
                block.SetVector(Ids.Strip2, new Vector4(ChromoStrip.Clear, ChromoStrip.Width, stripAmount, ChromoStrip.Ragged));
                Color inner = ChromoStrip.PaintInner, outer = ChromoStrip.PaintOuter;
                float paint = cameraData.cameraType == CameraType.Game ? ChromoStrip.PaintOpacity : 0f;
                block.SetVector(Ids.Paint1, new Vector4(inner.r, inner.g, inner.b, inner.a * paint));
                block.SetVector(Ids.Paint2, new Vector4(outer.r, outer.g, outer.b, outer.a * paint));
                block.SetVector(Ids.Paint3, new Vector4(ChromoStrip.PaintClear, ChromoStrip.PaintWidth, ChromoStrip.PaintOuterFrom, ChromoStrip.PaintOuterAmount));
                block.SetVector(Ids.Paint4, new Vector4(ChromoStrip.PaintRagged, ChromoStrip.PaintOuterRagged, ChromoStrip.PaintOuterHardness, ChromoStrip.PaintGrain));
                block.SetVector(Ids.Paint5, new Vector4(ChromoStrip.PaintPooling, ChromoStrip.PaintBlotchSize, ChromoStrip.PaintDrift.x, ChromoStrip.PaintDrift.y));
                block.SetVector(Ids.Proof, new Vector4((float)ChromoDebug.Mode, ChromoDebug.Ink, 0f, 0f));
            }
        }

        static class Ids
        {
            public static readonly int BlitTexture = Shader.PropertyToID("_BlitTexture");
            public static readonly int BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
            public static readonly int InkColor = Shader.PropertyToID("_InkColor");
            public static readonly int InkParams = Shader.PropertyToID("_InkParams");
            public static readonly int InkParams2 = Shader.PropertyToID("_InkParams2");
            public static readonly int InkCount = Shader.PropertyToID("_InkCount");
            public static readonly int SeparationA = Shader.PropertyToID("_SeparationA");
            public static readonly int SeparationB = Shader.PropertyToID("_SeparationB");
            public static readonly int GrainA = Shader.PropertyToID("_GrainA");
            public static readonly int GrainB = Shader.PropertyToID("_GrainB");
            public static readonly int PaperTex = Shader.PropertyToID("_PaperTex");
            public static readonly int PaperColor = Shader.PropertyToID("_PaperColor");
            public static readonly int FoxColor = Shader.PropertyToID("_FoxColor");
            public static readonly int KeyColor = Shader.PropertyToID("_KeyColor");
            public static readonly int ShadowColor = Shader.PropertyToID("_ShadowColor");
            public static readonly int Subject = Shader.PropertyToID("_ChromoSubject");
            public static readonly int NightSky = Shader.PropertyToID("_NightSkyColor");
            public static readonly int GlowSun = Shader.PropertyToID("_ChromoGlowSun");
            public static readonly int Horizon = Shader.PropertyToID("_ChromoHorizon");
            public static readonly int GlowUp = Shader.PropertyToID("_ChromoGlowUp");
            public static readonly int GlowShape = Shader.PropertyToID("_ChromoGlowShape");
            public static readonly int Sizes = Shader.PropertyToID("_ChromoSizes");
            public static readonly int PaperOffset = Shader.PropertyToID("_PaperOffset");
            public static readonly int Hints = Shader.PropertyToID("_ChromoHints");
            public static readonly int Strip1 = Shader.PropertyToID("_Strip1");
            public static readonly int Strip2 = Shader.PropertyToID("_Strip2");
            public static readonly int Paint1 = Shader.PropertyToID("_Paint1");
            public static readonly int Paint2 = Shader.PropertyToID("_Paint2");
            public static readonly int Paint3 = Shader.PropertyToID("_Paint3");
            public static readonly int Paint4 = Shader.PropertyToID("_Paint4");
            public static readonly int Paint5 = Shader.PropertyToID("_Paint5");
            public static readonly int[] StyleVectors =
            {
                Shader.PropertyToID("_SPrint1"), Shader.PropertyToID("_SPrint2"), Shader.PropertyToID("_SPrint3"),
                Shader.PropertyToID("_SHand"), Shader.PropertyToID("_SWet"), Shader.PropertyToID("_SSky"),
                Shader.PropertyToID("_SLine1"), Shader.PropertyToID("_SLine2"), Shader.PropertyToID("_SLine3"),
                Shader.PropertyToID("_SHatch"), Shader.PropertyToID("_SFade"), Shader.PropertyToID("_SNight"),
            };
            public static readonly int HintsView = Shader.PropertyToID("_ChromoView");
            public static readonly int HintsViewProj = Shader.PropertyToID("_ChromoViewProj");
            public static readonly int StyleId = Shader.PropertyToID("_ChromoStyleId");
            public static readonly int CorruptAmount = Shader.PropertyToID("_ChromoCorruptAmount");
            public static readonly int CrumpleTex = Shader.PropertyToID("_CrumpleTex");
            public static readonly int Corrupt1 = Shader.PropertyToID("_Corrupt1");
            public static readonly int CorruptionMap1 = Shader.PropertyToID("_ChromoCorruptionMap1");
            public static readonly int Corrupt2 = Shader.PropertyToID("_Corrupt2");
            public static readonly int Corrupt3 = Shader.PropertyToID("_Corrupt3");
            public static readonly int Corrupt4 = Shader.PropertyToID("_Corrupt4");
            public static readonly int CorruptDark = Shader.PropertyToID("_CorruptDark");
            public static readonly int CorruptMid = Shader.PropertyToID("_CorruptMid");
            public static readonly int CorruptLight = Shader.PropertyToID("_CorruptLight");
            public static readonly int CorruptBloom = Shader.PropertyToID("_CorruptBloom");
            public static readonly int CorruptBloomRim = Shader.PropertyToID("_CorruptBloomRim");
            public static readonly int MotionRef = Shader.PropertyToID("_ChromoMotionRef");
            public static readonly int MotionRefNormal = Shader.PropertyToID("_ChromoMotionRefNormal");
            public static readonly int Rest = Shader.PropertyToID("_ChromoRest");
            public static readonly int RestLayout = Shader.PropertyToID("_ChromoRestLayout");
            public static readonly int RestNormalFormat = Shader.PropertyToID("_ChromoRestNormalFormat");
            public static readonly int MotionOn = Shader.PropertyToID("_ChromoMotionOn");
            public static readonly int MotionPos = Shader.PropertyToID("_ChromoMotionPos");
            public static readonly int MotionNormal = Shader.PropertyToID("_ChromoMotionNormal");
            public static readonly int Proof = Shader.PropertyToID("_Proof");
        }
    }

    /// <summary>
    /// What the print did on the last main-camera frame (performance plan P0, shown by the Performance debug panel):
    /// renderers the ChromoStyled objects hold, those drawn one by one into the style buffer, and moving parts drawn.
    /// </summary>
    public static class ChromoPrintStats
    {
        public static int StyledRenderers, StyledDraws, MovingDraws;

        /// <summary>Debug switches (Performance panel): skip the style buffer's draws, or the moving objects' pass, to measure them.</summary>
        public static bool SkipStyles, SkipMotion;
    }
}
