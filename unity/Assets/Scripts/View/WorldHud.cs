using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Dejarik;

namespace Dejarik.View
{
    // World-space HUD: IMGUI/OnGUI renders once across the full stereo backbuffer and is unusable in the
    // glasses, so the status line, dice totals, piece stats, and action buttons are all real 3D objects
    // anchored above/around the board and billboarded to the camera. Buttons are selected like cells.
    public class WorldHud : MonoBehaviour
    {
        public sealed class Button { public Transform Tr; public Collider Col; public Action OnClick; }

        TMP_Text _status, _dice, _stats;
        readonly List<Button> _buttons = new List<Button>();
        Camera _cam;
        Transform _root;

        public IReadOnlyList<Button> Buttons => _buttons;

        public void Build(Transform boardRoot, Action recenter, Action pin, Action newGame)
        {
            _root = boardRoot;
            _cam = Camera.main;

            // Info HUD floats by the board (visible while looking at it). Action buttons live on the phone
            // touchscreen (see DejarikGame.OnGUI) so they're always reachable even if the board drifts.
            _status = MakeText("status", new Vector3(0f, 5.6f, 0f), 5.5f, Color.white, TextAlignmentOptions.Center, 18f);
            _dice = MakeText("dice", new Vector3(0f, 4.3f, 0f), 6.5f, Color.white, TextAlignmentOptions.Center, 18f);
            _stats = MakeText("stats", new Vector3(-6.2f, 2.2f, -1f), 4.5f, Color.white, TextAlignmentOptions.Left, 7f);
        }

        TMP_Text MakeText(string name, Vector3 localPos, float size, Color color, TextAlignmentOptions align, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = localPos;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.rectTransform.sizeDelta = new Vector2(width, 6f);
            tmp.text = "";
            return tmp;
        }

        void MakeButton(string label, Vector3 localPos, Color color, Action onClick)
        {
            var go = new GameObject($"btn_{label}");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(2.6f, 1.0f, 0.2f);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            quad.transform.SetParent(go.transform, false);
            var mr = quad.GetComponent<MeshRenderer>();
            mr.material = new Material(Shader.Find("Unlit/Color")) { color = color };
            Destroy(quad.GetComponent<Collider>());

            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(1f, 1f, 1f);

            var txtGo = new GameObject("label");
            txtGo.transform.SetParent(go.transform, false);
            txtGo.transform.localPosition = new Vector3(0f, 0f, -0.6f);
            txtGo.transform.localScale = new Vector3(1f / 2.6f, 1f, 1f / 0.2f); // undo parent stretch
            var tmp = txtGo.AddComponent<TextMeshPro>();
            tmp.text = label;
            tmp.fontSize = 3f;
            tmp.color = Color.black;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(8f, 2f);

            _buttons.Add(new Button { Tr = go.transform, Col = col, OnClick = onClick });
        }

        public void SetStatus(string s) { if (_status) _status.text = s; }
        public void SetDice(string s) { if (_dice) _dice.text = s ?? ""; }

        public void SetStats(GamePiece p)
        {
            if (!_stats) return;
            if (p == null) { _stats.text = ""; return; }
            var st = Pieces.Stats[p.Type];
            _stats.color = HoloMaterials.HoloFor(p.Owner);
            string who = p.Owner == Player.P0 ? "YOU" : "OPPONENT";
            _stats.text = $"{st.Name}\n<size=70%>({who})\nATK {st.Attack}\nDEF {st.Defense}\nMOV {st.Movement}</size>";
        }

        // Nearest button to a fingertip (within maxDist world meters).
        public bool NearestButton(Vector3 world, float maxDist, out Button btn)
        {
            btn = null; float best = maxDist;
            foreach (var b in _buttons)
            {
                float d = Vector3.Distance(world, b.Tr.position);
                if (d < best) { best = d; btn = b; }
            }
            return btn != null;
        }

        public bool RaycastButton(Ray ray, out Button btn)
        {
            btn = null;
            if (Physics.Raycast(ray, out var hit, 50f))
                foreach (var b in _buttons)
                    if (b.Col == hit.collider) { btn = b; return true; }
            return false;
        }

        void LateUpdate()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            // Billboard the text to the camera (buttons stay flat on the board).
            foreach (var t in new[] { _status, _dice, _stats })
                if (t) t.transform.rotation = Quaternion.LookRotation(t.transform.position - _cam.transform.position);
        }
    }
}
