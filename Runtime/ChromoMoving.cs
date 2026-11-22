using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chromolithograph
{
    /// <summary>
    /// Makes the print's grain, paper and hatching travel with this object (and, by default, its children) instead of
    /// staying fixed in the world, so a moving object doesn't look like a texture scrolling across its faces.
    /// Rigid meshes carry the texture they had where they started, so a moving platform at its start matches the
    /// world around it exactly. Skinned meshes (characters) carry it on their rest pose, so it stays on each limb as
    /// they animate. Objects without this component are unchanged and share one texture across the world.
    /// The player, enemies and moving platforms get one automatically (Integration/ArtStyle/ChromoMovingAuto).
    /// </summary>
    [DisallowMultipleComponent]
    public class ChromoMoving : MonoBehaviour
    {
        [Tooltip("Carry the texture on the renderers of child objects too.")]
        public bool includeChildren = true;

        /// <summary>One renderer and how its texture is mapped.</summary>
        public class Part
        {
            public Renderer renderer;
            public int submeshes;
            /// <summary>Mesh space to texture space (the renderer's transform when it was captured).</summary>
            public Matrix4x4 reference;
            /// <summary>The same for normals (inverse transpose).</summary>
            public Matrix4x4 referenceNormal;
            /// <summary>Skinned: the mesh's unskinned vertices on the GPU, read by vertex index.</summary>
            public GraphicsBuffer rest;
            public Mesh mesh;
            public int stride, positionOffset, positionFormat, normalOffset, normalFormat;
        }

        static readonly List<ChromoMoving> all = new List<ChromoMoving>();
        public static IReadOnlyList<ChromoMoving> All => all;

        readonly List<Part> parts = new List<Part>();
        public IReadOnlyList<Part> Parts => parts;

        static readonly List<Renderer> found = new List<Renderer>();
        static bool warnedFormat;

        void OnEnable()
        {
            Reseat();
            if (!all.Contains(this)) all.Add(this);
        }

        void OnDisable()
        {
            all.Remove(this);
            Release();
        }

        void OnTransformChildrenChanged()
        {
            if (isActiveAndEnabled) Refresh();
        }

        float nextCheck;

        // Weapons and gear equipped deep in the hierarchy don't raise OnTransformChildrenChanged, so look again now
        // and then. Renderers already known keep their texture.
        void LateUpdate()
        {
            if (!includeChildren || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 0.5f;
            Refresh();
        }

        /// <summary>
        /// Gathers the renderers again. New ones carry the texture they have where they are now; ones already known
        /// keep theirs. Reseat() starts every renderer over from where it is now.
        /// </summary>
        public void Refresh()
        {
            found.Clear();
            if (includeChildren) GetComponentsInChildren(false, found);
            else if (TryGetComponent(out Renderer own)) found.Add(own);
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                Part old = parts[i];
                if (old.renderer != null && found.Contains(old.renderer) && SameMesh(old)) continue;
                old.rest?.Dispose();
                parts.RemoveAt(i);
            }
            foreach (Renderer renderer in found)
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                if (Known(renderer)) continue;
                Matrix4x4 reference = renderer.transform.localToWorldMatrix;
                var part = new Part
                {
                    renderer = renderer,
                    submeshes = Mathf.Max(1, renderer.sharedMaterials.Length),
                    reference = reference,
                    referenceNormal = reference.inverse.transpose,
                };
                if (renderer is SkinnedMeshRenderer skinned && !CaptureRest(skinned.sharedMesh, part)) continue;
                parts.Add(part);
            }
            found.Clear();
        }

        /// <summary>Forgets every renderer's texture and captures them all again from where they are now.</summary>
        public void Reseat()
        {
            Release();
            Refresh();
        }

        bool Known(Renderer renderer)
        {
            for (int i = 0; i < parts.Count; i++) if (parts[i].renderer == renderer) return true;
            return false;
        }

        // A skinned renderer whose mesh was swapped needs its vertex buffer read again.
        static bool SameMesh(Part part) =>
            !(part.renderer is SkinnedMeshRenderer skinned) || (part.rest != null && part.mesh == skinned.sharedMesh);

        /// <summary>Finds the unskinned positions (and normals) in the mesh's GPU vertex buffer.</summary>
        static bool CaptureRest(Mesh mesh, Part part)
        {
            if (mesh == null || !mesh.HasVertexAttribute(VertexAttribute.Position)) return false;
            int stream = mesh.GetVertexAttributeStream(VertexAttribute.Position);
            part.positionFormat = FormatCode(mesh.GetVertexAttributeFormat(VertexAttribute.Position));
            part.normalFormat = -1;
            part.normalOffset = 0;
            if (mesh.HasVertexAttribute(VertexAttribute.Normal) && mesh.GetVertexAttributeStream(VertexAttribute.Normal) == stream)
            {
                part.normalFormat = FormatCode(mesh.GetVertexAttributeFormat(VertexAttribute.Normal));
                part.normalOffset = mesh.GetVertexAttributeOffset(VertexAttribute.Normal);
            }
            if (part.positionFormat < 0)
            {
                if (!warnedFormat) Debug.LogWarning($"ChromoMoving: '{mesh.name}' stores positions in a compressed format, so its texture stays in the world (turn off vertex compression for it).");
                warnedFormat = true;
                return false;
            }
            mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
            part.rest = mesh.GetVertexBuffer(stream);
            part.mesh = mesh;
            part.stride = mesh.GetVertexBufferStride(stream);
            part.positionOffset = mesh.GetVertexAttributeOffset(VertexAttribute.Position);
            return part.rest != null;
        }

        // 0 = 32-bit floats, 1 = 16-bit floats, -1 = not readable here.
        static int FormatCode(VertexAttributeFormat format) =>
            format == VertexAttributeFormat.Float32 ? 0 : format == VertexAttributeFormat.Float16 ? 1 : -1;

        void Release()
        {
            foreach (Part part in parts) part.rest?.Dispose();
            parts.Clear();
        }
    }
}
