using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;
using Dejarik;

namespace Dejarik.View
{
    // One loaded, normalized, holo-shaded creature. The model is scaled to unit height with feet at the
    // local origin (mirroring useHoloModel in the web game), so PieceView can scale it by PieceScale and
    // place it on a cell. Clips play through the legacy Animation component.
    public sealed class CreatureInstance
    {
        public GameObject Holder;            // unit-height, feet at origin; PieceView scales/places this
        public Animation Anim;               // legacy clips (idle/walk/attack/hit/death/victory/finishing)
        public List<string> ClipNames = new List<string>();
        public List<Material> Materials = new List<Material>();
        public Transform RootBone;           // animated root bone to lock (name ends ROOTSHJnt), may be null
        public Vector3 RootBaseLocalPos;
        public Quaternion RootBaseLocalRot;

        static readonly Regex IdleRe = new Regex("idle", RegexOptions.IgnoreCase);
        static readonly Regex WalkRe = new Regex("walk", RegexOptions.IgnoreCase);
        static readonly Regex AttackRe = new Regex("attack", RegexOptions.IgnoreCase);
        static readonly Regex FinishRe = new Regex("finishingmove", RegexOptions.IgnoreCase);
        static readonly Regex VictoryRe = new Regex("victory", RegexOptions.IgnoreCase);
        static readonly Regex HitRe = new Regex("hit", RegexOptions.IgnoreCase);
        static readonly Regex DeathRe = new Regex("death", RegexOptions.IgnoreCase);

        public string Idle => Find(IdleRe);
        public string Walk => Find(WalkRe);
        public string Attack => Find(AttackRe);
        public string Finish => Find(FinishRe);
        public string Victory => Find(VictoryRe);
        public string Hit => Find(HitRe);
        public string DeathBy(PieceType? by)
        {
            if (by.HasValue)
            {
                var s = ClipNames.FirstOrDefault(n => Regex.IsMatch(n, $"death.?{by.Value}", RegexOptions.IgnoreCase));
                if (s != null) return s;
            }
            // Prefer the generic "_death|" clip over a death-vs-specific-type variant.
            return ClipNames.FirstOrDefault(n => Regex.IsMatch(n, "_death\\|", RegexOptions.IgnoreCase)) ?? Find(DeathRe);
        }
        string Find(Regex re) => ClipNames.FirstOrDefault(n => re.IsMatch(n));
    }

    public static class CreatureFactory
    {
        public static async Task<CreatureInstance> Load(PieceType type, Player owner, Transform parent)
        {
            string rel = $"Models/Creatures/{Pieces.IdOf(type)}.glb";
            byte[] data = await GlbStreaming.ReadBytes(rel);

            var gltf = new GltfImport();
            bool parsed = await gltf.LoadGltfBinary(data, new Uri("file://creature/" + Pieces.IdOf(type)));
            if (!parsed) throw new InvalidOperationException($"glTFast failed to parse {rel}");

            var holder = new GameObject($"creature_{Pieces.IdOf(type)}");
            holder.transform.SetParent(parent, false);
            var instantiator = new GameObjectInstantiator(gltf, holder.transform);
            bool ok = await gltf.InstantiateMainSceneAsync(instantiator);
            if (!ok) { UnityEngine.Object.Destroy(holder); throw new InvalidOperationException($"glTFast failed to instantiate {rel}"); }

            var inst = new CreatureInstance { Holder = holder, Anim = instantiator.SceneInstance?.LegacyAnimation };

            // Normalize to unit height with feet at the local origin.
            var b = WorldBounds(holder.transform);
            if (b.HasValue && b.Value.size.y > 1e-4f)
            {
                float norm = 1f / b.Value.size.y;
                holder.transform.localScale = Vector3.one * norm;
            }
            var b2 = WorldBounds(holder.transform);
            if (b2.HasValue)
            {
                // Offset children so the combined bounds sit centered in x/z with min.y at 0 (world == local
                // here since holder is at the origin under its parent).
                Vector3 c = b2.Value.center;
                float minY = b2.Value.min.y;
                foreach (Transform child in holder.transform)
                    child.localPosition -= new Vector3(c.x, minY, c.z) / Mathf.Max(1e-4f, holder.transform.localScale.x);
            }

            // Holo material + skinning-safe renderers.
            foreach (var r in holder.GetComponentsInChildren<Renderer>())
            {
                var src = r.sharedMaterial;
                Texture main = src != null && src.HasProperty("_MainTex") ? src.GetTexture("_MainTex") ?? src.mainTexture : src?.mainTexture;
                Texture norm = src != null && src.HasProperty("_BumpMap") ? src.GetTexture("_BumpMap") : null;
                var mat = HoloMaterials.Creature(main, norm, owner);
                r.material = mat;
                inst.Materials.Add(mat);
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }

            // Clip names from the legacy Animation component.
            if (inst.Anim != null)
                foreach (AnimationState st in inst.Anim) inst.ClipNames.Add(st.name);

            // Animated root bone to lock so authored root drift doesn't move the creature off its cell.
            foreach (var t in holder.GetComponentsInChildren<Transform>())
                if (Regex.IsMatch(t.name, "ROOTSHJnt$")) { inst.RootBone = t; break; }
            if (inst.RootBone != null)
            {
                inst.RootBaseLocalPos = inst.RootBone.localPosition;
                inst.RootBaseLocalRot = inst.RootBone.localRotation;
            }

            return inst;
        }

        static Bounds? WorldBounds(Transform root)
        {
            var rs = root.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return null;
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
