using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Dejarik;

namespace Dejarik.View
{
    // Lightweight holographic dice: emissive cubes tumble above the board while the roll "settles", with
    // floating roll totals in each side's color, then fade. Cosmetic; the outcome is read from the engine.
    public class DiceView : MonoBehaviour
    {
        const float RollMs = 1300f, HoldMs = 950f;

        readonly List<GameObject> _spawned = new List<GameObject>();
        Coroutine _running;

        public void ShowRoll(int atkTotal, int defTotal, int atkCount, int defCount, Player attacker, Vector3 worldCenter)
        {
            if (_running != null) StopCoroutine(_running);
            Clear();
            _running = StartCoroutine(Run(atkTotal, defTotal, atkCount, defCount, attacker, worldCenter));
        }

        IEnumerator Run(int atkTotal, int defTotal, int atkCount, int defCount, Player attacker, Vector3 center)
        {
            var atkColor = HoloMaterials.HoloFor(attacker);
            var defColor = HoloMaterials.HoloFor(attacker.Other());
            float up = 0.18f;

            var cubes = new List<Transform>();
            void SpawnCubes(int n, Color c, float side)
            {
                for (int i = 0; i < n; i++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.SetParent(transform, false);
                    go.transform.localScale = Vector3.one * 0.02f;
                    go.transform.position = center + new Vector3(side * 0.06f + (i - n * 0.5f) * 0.012f, up, 0f);
                    Destroy(go.GetComponent<Collider>());
                    var mr = go.GetComponent<MeshRenderer>();
                    var m = new Material(Shader.Find("Standard"));
                    m.color = Color.Lerp(c, Color.white, 0.4f);
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    m.SetColor("_EmissionColor", c * 1.2f);
                    mr.material = m;
                    cubes.Add(go.transform);
                    _spawned.Add(go);
                }
            }
            SpawnCubes(atkCount, atkColor, -1f);
            SpawnCubes(defCount, defColor, 1f);

            var atkLabel = MakeLabel(atkTotal.ToString(), atkColor, center + new Vector3(-0.06f, up + 0.05f, 0f));
            var defLabel = MakeLabel(defTotal.ToString(), defColor, center + new Vector3(0.06f, up + 0.05f, 0f));

            float t0 = Time.time;
            while ((Time.time - t0) * 1000f < RollMs)
            {
                foreach (var c in cubes)
                    if (c) c.Rotate(new Vector3(360f, 540f, 270f) * Time.deltaTime, Space.Self);
                yield return null;
            }
            yield return new WaitForSeconds(HoldMs / 1000f);

            float f0 = Time.time;
            while ((Time.time - f0) < 0.3f)
            {
                float a = 1f - (Time.time - f0) / 0.3f;
                foreach (var go in _spawned)
                    if (go) { var mr = go.GetComponent<MeshRenderer>(); if (mr) { var col = mr.material.color; col.a = a; } }
                yield return null;
            }
            Clear();
        }

        GameObject MakeLabel(string text, Color color, Vector3 pos)
        {
            try
            {
                var go = new GameObject("dieTotal");
                go.transform.SetParent(transform, false);
                go.transform.position = pos;
                var tmp = go.AddComponent<TMPro.TextMeshPro>();
                tmp.text = text;
                tmp.fontSize = 2.2f;
                tmp.color = color;
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                go.transform.localScale = Vector3.one * 0.05f;
                _spawned.Add(go);
                return go;
            }
            catch { return null; }
        }

        void Update()
        {
            // Billboard labels toward the camera.
            var cam = Camera.main;
            if (cam == null) return;
            foreach (var go in _spawned)
                if (go != null && go.GetComponent<TMPro.TextMeshPro>() != null)
                    go.transform.rotation = Quaternion.LookRotation(go.transform.position - cam.transform.position);
        }

        void Clear()
        {
            foreach (var go in _spawned) if (go) Destroy(go);
            _spawned.Clear();
        }
    }
}
