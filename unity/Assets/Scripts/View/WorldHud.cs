using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Dejarik;

namespace Dejarik.View
{
    // Head-locked info HUD: world-space text (status, dice totals, piece stats) RIGIDLY parented to the
    // camera so it's pinned exactly to the view with no lag (re-positioning each frame in LateUpdate trails
    // the head). Kept within the glasses' narrow FOV. Action buttons live on the phone (DejarikGame.OnGUI).
    public class WorldHud : MonoBehaviour
    {
        const float Depth = 0.5f;   // meters in front of the eyes

        Transform _root;
        TMP_Text _status, _dice, _stats;

        public void Build()
        {
            _root = new GameObject("HudRoot").transform;
            Attach();
            _status = MakeText("status", new Vector3(0f, 0.090f, Depth), 0.17f, Color.white, TextAlignmentOptions.Center, 0.9f);
            _dice = MakeText("dice", new Vector3(0f, 0.038f, Depth), 0.26f, Color.white, TextAlignmentOptions.Center, 0.9f);
            _stats = MakeText("stats", new Vector3(0f, -0.02f, Depth), 0.16f, Color.white, TextAlignmentOptions.Center, 0.9f);
        }

        void Attach()
        {
            var cam = Camera.main;
            if (cam == null) return;
            _root.SetParent(cam.transform, false);
            _root.localPosition = Vector3.zero;
            _root.localRotation = Quaternion.identity;
        }

        TMP_Text MakeText(string name, Vector3 localPos, float size, Color color, TextAlignmentOptions align, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity; // faces +Z (forward) — readable, parented to the eye
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.rectTransform.sizeDelta = new Vector2(width, 0.16f);
            tmp.text = "";
            return tmp;
        }

        public void SetStatus(string s) { if (_status) _status.text = s; }
        public void SetDice(string s) { if (_dice) _dice.text = s ?? ""; }

        public void SetStats(GamePiece p)
        {
            if (!_stats) return;
            if (p == null) { _stats.text = ""; return; }
            var st = Pieces.Stats[p.Type];
            _stats.color = HoloMaterials.HoloFor(p.Owner);
            string who = p.Owner == Player.P0 ? "YOU" : "OPP";
            _stats.text = $"{st.Name} ({who})   ATK {st.Attack}  DEF {st.Defense}  MOV {st.Movement}";
        }

        // Re-attach if the camera wasn't ready at Build (rigid parenting otherwise needs no per-frame work).
        void LateUpdate()
        {
            if (_root != null && _root.parent == null) Attach();
        }
    }
}
