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
        Texture2D _pips;

        // A die face (5 pips) — light face with dark pips, like the web game's dice.
        static Texture2D PipTexture()
        {
            int s = 96;
            var tex = new Texture2D(s, s);
            Color light = HoloMaterials.Hex("#dff6ff"), dark = HoloMaterials.Hex("#06121a");
            var px = new Color[s * s];
            for (int i = 0; i < px.Length; i++) px[i] = light;
            float r = s * 0.10f;
            Vector2[] pips = { new Vector2(0.27f, 0.27f), new Vector2(0.73f, 0.27f), new Vector2(0.5f, 0.5f),
                               new Vector2(0.27f, 0.73f), new Vector2(0.73f, 0.73f) };
            foreach (var p in pips)
            {
                int cx = (int)(p.x * s), cy = (int)(p.y * s);
                for (int y = -((int)r); y <= r; y++)
                    for (int x = -((int)r); x <= r; x++)
                        if (x * x + y * y <= r * r)
                        {
                            int px2 = cx + x, py2 = cy + y;
                            if (px2 >= 0 && px2 < s && py2 >= 0 && py2 < s) px[py2 * s + px2] = dark;
                        }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
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
            if (_pips == null) _pips = PipTexture();
            float die = 0.034f;
            void Spawn(int n, Color col)
            {
                for (int i = 0; i < n && i < 10; i++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.SetParent(transform, true);
                    go.transform.localScale = Vector3.one * die;
                    Vector2 off = Random.insideUnitCircle * 0.05f;
                    go.transform.position = center + new Vector3(off.x, 0.28f + i * 0.012f, off.y);
                    go.transform.rotation = Random.rotation;
                    var mr = go.GetComponent<MeshRenderer>();
                    var m = new Material(Shader.Find("Dejarik/Hologram"));
                    m.SetTexture("_MainTex", _pips);
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
