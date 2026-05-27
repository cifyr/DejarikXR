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

        public void ShowRoll(int atkTotal, int defTotal, int atkCount, int defCount, Player attacker, Vector3 boardCenter)
        {
            if (_running != null) StopCoroutine(_running);
            ClearDice();
            _running = StartCoroutine(Run(atkCount, defCount, attacker, boardCenter));
        }

        IEnumerator Run(int atkCount, int defCount, Player attacker, Vector3 center)
        {
            EnsureRig(center);
            float die = 0.022f;
            void Spawn(int n, Color col)
            {
                for (int i = 0; i < n && i < 10; i++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.SetParent(transform, true);
                    go.transform.localScale = Vector3.one * die;
                    Vector2 off = Random.insideUnitCircle * 0.04f;
                    go.transform.position = center + new Vector3(off.x, 0.22f + i * 0.01f, off.y);
                    go.transform.rotation = Random.rotation;
                    var mr = go.GetComponent<MeshRenderer>();
                    var m = new Material(Shader.Find("Unlit/Color")) { color = Color.Lerp(col, Color.white, 0.25f) };
                    mr.material = m;
                    var bc = go.GetComponent<BoxCollider>();
                    bc.material = _bounce;
                    var rb = go.AddComponent<Rigidbody>();
                    rb.mass = 0.02f;
                    rb.linearVelocity = new Vector3(Random.Range(-0.1f, 0.1f), -0.2f, Random.Range(-0.1f, 0.1f));
                    rb.angularVelocity = Random.insideUnitSphere * 12f;
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
