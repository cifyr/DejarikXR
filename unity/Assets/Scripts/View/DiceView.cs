using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Dejarik;

namespace Dejarik.View
{
    // Physical holographic dice: cubes drop onto the board and clatter/bounce to a rest, colored by side
    // (attacker vs defender). The numeric totals are shown on the HUD by DejarikGame. Lives at world scale
    // (not under the tiny board root) so the rigidbody physics stays stable.
    public class DiceView : MonoBehaviour
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<GameObject> _rig = new List<GameObject>();
        Coroutine _running;
        PhysicsMaterial _bounce;
        static Texture2D _atlas;
        static Mesh _diceMesh;

        // Standard dice pip layouts (fractional positions within a face cell), indexed by value-1.
        static readonly Vector2[][] PipLayout =
        {
            new[] { new Vector2(.5f, .5f) },                                                                 // 1
            new[] { new Vector2(.3f, .7f), new Vector2(.7f, .3f) },                                           // 2
            new[] { new Vector2(.3f, .7f), new Vector2(.5f, .5f), new Vector2(.7f, .3f) },                    // 3
            new[] { new Vector2(.3f, .7f), new Vector2(.7f, .7f), new Vector2(.3f, .3f), new Vector2(.7f, .3f) }, // 4
            new[] { new Vector2(.3f, .7f), new Vector2(.7f, .7f), new Vector2(.5f, .5f), new Vector2(.3f, .3f), new Vector2(.7f, .3f) }, // 5
            new[] { new Vector2(.3f, .74f), new Vector2(.7f, .74f), new Vector2(.3f, .5f), new Vector2(.7f, .5f), new Vector2(.3f, .26f), new Vector2(.7f, .26f) }, // 6
        };

        // 3x2 atlas of the six faces (1..6), light face with dark pips, like the web game's dice.
        static Texture2D PipAtlas()
        {
            const int cs = 96, cols = 3, rows = 2;
            int w = cs * cols, h = cs * rows;
            var tex = new Texture2D(w, h);
            Color light = HoloMaterials.Hex("#dff6ff"), dark = HoloMaterials.Hex("#06121a");
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = light;
            float r = cs * 0.085f;
            for (int v = 1; v <= 6; v++)
            {
                int col = (v - 1) % cols, rowc = (v - 1) / cols;
                int ox = col * cs, oy = rowc * cs;
                foreach (var p in PipLayout[v - 1])
                {
                    int cx = ox + (int)(p.x * cs), cy = oy + (int)(p.y * cs);
                    for (int y = -((int)r); y <= r; y++)
                        for (int x = -((int)r); x <= r; x++)
                            if (x * x + y * y <= r * r)
                            {
                                int px2 = cx + x, py2 = cy + y;
                                if (px2 >= 0 && px2 < w && py2 >= 0 && py2 < h) px[py2 * w + px2] = dark;
                            }
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // Unit cube whose six faces UV-map to atlas cells 1..6, with opposite faces summing to 7 like a die.
        static Mesh DiceMesh()
        {
            var verts = new List<Vector3>(); var uvs = new List<Vector2>();
            var norms = new List<Vector3>(); var tris = new List<int>();
            void Face(Vector3 normal, int value)
            {
                Vector3 right = Mathf.Abs(normal.y) > 0.9f ? Vector3.right : Vector3.Normalize(Vector3.Cross(normal, Vector3.up));
                Vector3 up = Vector3.Cross(normal, right);          // cross(right,up) == normal -> outward winding
                Vector3 c = normal * 0.5f;
                int b = verts.Count;
                int col = (value - 1) % 3, rowc = (value - 1) / 3;
                float u0 = col / 3f, u1 = (col + 1) / 3f, v0 = rowc / 2f, v1 = (rowc + 1) / 2f;
                verts.Add(c - right * 0.5f - up * 0.5f); uvs.Add(new Vector2(u0, v0));
                verts.Add(c + right * 0.5f - up * 0.5f); uvs.Add(new Vector2(u1, v0));
                verts.Add(c + right * 0.5f + up * 0.5f); uvs.Add(new Vector2(u1, v1));
                verts.Add(c - right * 0.5f + up * 0.5f); uvs.Add(new Vector2(u0, v1));
                for (int i = 0; i < 4; i++) norms.Add(normal);
                tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            Face(Vector3.right, 1); Face(Vector3.left, 6);
            Face(Vector3.up, 2); Face(Vector3.down, 5);
            Face(Vector3.forward, 3); Face(Vector3.back, 4);
            var m = new Mesh();
            m.SetVertices(verts); m.SetUVs(0, uvs); m.SetNormals(norms); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        public void ShowRoll(int atkTotal, int defTotal, int atkCount, int defCount, Player attacker, Vector3 boardCenter)
        {
            if (_running != null) StopCoroutine(_running);
            ClearDice();
            _running = StartCoroutine(Run(atkCount, defCount, attacker, boardCenter));
        }

        IEnumerator Run(int atkCount, int defCount, Player attacker, Vector3 center)
        {
            EnsureRig(center);
            if (_atlas == null) _atlas = PipAtlas();
            if (_diceMesh == null) _diceMesh = DiceMesh();
            float die = 0.034f;
            void Spawn(int n, Color col)
            {
                for (int i = 0; i < n && i < 10; i++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.GetComponent<MeshFilter>().sharedMesh = _diceMesh; // 6 distinct faces (collider stays a unit cube)
                    go.transform.SetParent(transform, true);
                    go.transform.localScale = Vector3.one * die;
                    Vector2 off = Random.insideUnitCircle * 0.05f;
                    go.transform.position = center + new Vector3(off.x, 0.28f + i * 0.012f, off.y);
                    go.transform.rotation = Random.rotation;
                    var mr = go.GetComponent<MeshRenderer>();
                    var m = new Material(Shader.Find("Dejarik/Hologram"));
                    m.SetTexture("_MainTex", _atlas);
                    m.SetColor("_HoloColor", Color.Lerp(col, Color.white, 0.3f));
                    m.SetFloat("_RimPower", 1.8f);
                    m.SetFloat("_Glow", 1.1f);
                    m.SetFloat("_Alpha", 0.97f);
                    mr.material = m;
                    var bc = go.GetComponent<BoxCollider>();
                    bc.material = _bounce;
                    var rb = go.AddComponent<Rigidbody>();
                    rb.mass = 0.03f;
                    rb.linearVelocity = new Vector3(Random.Range(-0.15f, 0.15f), -0.1f, Random.Range(-0.15f, 0.15f));
                    rb.angularVelocity = Random.insideUnitSphere * 16f;
                    _spawned.Add(go);
                }
            }
            Spawn(atkCount, HoloMaterials.HoloFor(attacker));
            Spawn(defCount, HoloMaterials.HoloFor(attacker.Other()));

            yield return new WaitForSeconds(2.3f);      // tumble + settle
            yield return new WaitForSeconds(0.95f);      // hold settled

            // shrink out
            float t0 = Time.time;
            while (Time.time - t0 < 0.3f)
            {
                float k = 1f - (Time.time - t0) / 0.3f;
                foreach (var go in _spawned) if (go) go.transform.localScale = Vector3.one * (die * k);
                yield return null;
            }
            ClearDice();
        }

        // Floor + low containing walls at the board plane so dice land and clatter without rolling off.
        void EnsureRig(Vector3 center)
        {
            if (_bounce == null)
                _bounce = new PhysicsMaterial { bounciness = 0.4f, dynamicFriction = 0.4f, staticFriction = 0.4f,
                    bounceCombine = PhysicsMaterialCombine.Maximum };
            foreach (var g in _rig) if (g) Destroy(g);
            _rig.Clear();

            var floor = new GameObject("diceFloor");
            floor.transform.SetParent(transform, true);
            floor.transform.position = center - new Vector3(0f, 0.02f, 0f);
            var fb = floor.AddComponent<BoxCollider>();
            fb.size = new Vector3(0.6f, 0.04f, 0.6f);
            fb.material = _bounce;
            _rig.Add(floor);

            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                var wall = new GameObject($"diceWall{i}");
                wall.transform.SetParent(transform, true);
                wall.transform.position = center + new Vector3(Mathf.Cos(a) * 0.26f, 0.05f, Mathf.Sin(a) * 0.26f);
                wall.transform.rotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                var wb = wall.AddComponent<BoxCollider>();
                wb.size = new Vector3(0.22f, 0.12f, 0.02f);
                wb.material = _bounce;
                _rig.Add(wall);
            }
        }

        void ClearDice()
        {
            foreach (var go in _spawned) if (go) Destroy(go);
            _spawned.Clear();
        }
    }
}
