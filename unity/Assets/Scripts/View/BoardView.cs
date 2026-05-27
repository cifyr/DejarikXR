using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;
using Dejarik;

namespace Dejarik.View
{
    // Renders the board: 25 flat glowing cells (with colliders for gaze selection), a glowing rim, and the
    // extracted Jedi Challenges table glb underneath. All in board-local units under this transform (the
    // "board root"), which the game places/scales/anchors in AR space.
    public class BoardView : MonoBehaviour
    {
        readonly Dictionary<int, MeshRenderer> _rend = new Dictionary<int, MeshRenderer>();
        readonly Dictionary<int, CellRole> _baseRole = new Dictionary<int, CellRole>();
        readonly Dictionary<Collider, int> _spaceByCol = new Dictionary<Collider, int>();

        public Transform Root => transform;
        public Vector3 WorldPos(int space) => transform.TransformPoint(BoardLayout.Pos3D(space));

        public void Build()
        {
            MakeCell(Board.Center, BoardLayout.CircleMesh(BoardLayout.RCenter), CellRole.Center);
            for (int i = 0; i < Board.Rays; i++)
            {
                MakeCell(i + 1, BoardLayout.SectorMesh(BoardLayout.Inner[0], BoardLayout.Inner[1], i),
                    i % 2 == 0 ? CellRole.Light : CellRole.Dark);
                MakeCell(i + 13, BoardLayout.SectorMesh(BoardLayout.Outer[0], BoardLayout.Outer[1], i),
                    i % 2 == 1 ? CellRole.Light : CellRole.Dark);
            }

            // Glowing rim.
            var rim = new GameObject("rim");
            rim.transform.SetParent(transform, false);
            rim.transform.localPosition = new Vector3(0f, BoardLayout.BaseTop, 0f);
            rim.AddComponent<MeshFilter>().sharedMesh = BoardLayout.RingMesh(BoardLayout.Rim - 0.08f, BoardLayout.Rim);
            rim.AddComponent<MeshRenderer>().sharedMaterial = HoloMaterials.RimGlow();

            _ = LoadTable();
        }

        void MakeCell(int space, Mesh mesh, CellRole baseRole)
        {
            var go = new GameObject($"cell_{space}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, BoardLayout.BaseTop + 0.002f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.material = HoloMaterials.Cell(baseRole);
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            _rend[space] = mr;
            _baseRole[space] = baseRole;
            _spaceByCol[col] = space;
        }

        // Recolor cells: highlight move/attack/push/selected, everything else to its base role.
        public void SetHighlights(IList<int> moves, IList<int> attacks, IList<int> pushes, int selectedSpace)
        {
            foreach (var kv in _rend)
            {
                int sp = kv.Key;
                CellRole role;
                if (pushes != null && pushes.Contains(sp)) role = CellRole.Push;
                else if (moves != null && moves.Contains(sp)) role = CellRole.Move;
                else if (attacks != null && attacks.Contains(sp)) role = CellRole.Attack;
                else if (sp == selectedSpace) role = CellRole.Selected;
                else role = _baseRole[sp];
                kv.Value.material = HoloMaterials.Cell(role);
            }
        }

        public void ClearHighlights() => SetHighlights(null, null, null, -1);

        // Gaze/controller ray -> board space, or -1 if the ray misses the board.
        public bool Raycast(Ray ray, out int space)
        {
            space = -1;
            if (Physics.Raycast(ray, out var hit, 50f) && _spaceByCol.TryGetValue(hit.collider, out var sp))
            {
                space = sp;
                return true;
            }
            return false;
        }

        // Nearest board space to a world point (for hand/fingertip selection), within maxDist meters.
        public bool NearestSpace(Vector3 world, float maxDist, out int space)
        {
            space = -1;
            float best = maxDist;
            foreach (var sp in _rend.Keys)
            {
                float d = Vector3.Distance(world, WorldPos(sp));
                if (d < best) { best = d; space = sp; }
            }
            return space >= 0;
        }

        // The extracted holochess table glb: scale so its top rim matches the play radius, drop it so the
        // open top sits just below the cells (mirrors BoardTable in the web game).
        const float BoardTopRadius = 0.41f;
        const float BoardTopLocalY = 0.92f;

        async Task LoadTable()
        {
            try
            {
                byte[] data = await GlbStreaming.ReadBytes("Models/board.glb");
                var gltf = new GltfImport();
                if (!await gltf.LoadGltfBinary(data, new Uri("file://board"))) return;
                var holder = new GameObject("table");
                holder.transform.SetParent(transform, false);
                if (!await gltf.InstantiateMainSceneAsync(new GameObjectInstantiator(gltf, holder.transform)))
                { Destroy(holder); return; }

                var mat = HoloMaterials.BoardTable();
                foreach (var r in holder.GetComponentsInChildren<Renderer>()) r.material = mat;

                float s = BoardLayout.Rim / BoardTopRadius;
                holder.transform.localScale = Vector3.one * s;
                holder.transform.localPosition = new Vector3(0f, BoardLayout.BaseTop - 0.02f - BoardTopLocalY * s, 0f);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoardView] table glb load failed (cells still shown): {e.Message}");
            }
        }
    }
}
