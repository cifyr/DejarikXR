using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Dejarik;

namespace Dejarik.View
{
    // Head-locked info HUD: world-space text (status, dice totals, selected-piece stats) that floats a fixed
    // distance in front of the camera and follows the head, so it's always in view and renders correctly
    // per-eye in the glasses (IMGUI can't — it draws once across the whole stereo backbuffer). Action buttons
    // live on the phone touchscreen instead (see DejarikGame.OnGUI).
    public class WorldHud : MonoBehaviour
    {
        const float Depth = 0.62f;   // meters in front of the head

        sealed class Item { public Transform Tr; public Vector2 Off; }
        readonly List<Item> _items = new List<Item>();
        TMP_Text _status, _dice, _stats;
        Camera _cam;

        public void Build()
        {
            _cam = Camera.main;
            _status = MakeText("status", new Vector2(0f, 0.205f), 0.40f, Color.white, TextAlignmentOptions.Center, 1.3f);
            _dice = MakeText("dice", new Vector2(0f, 0.135f), 0.52f, Color.white, TextAlignmentOptions.Center, 1.4f);
            _stats = MakeText("stats", new Vector2(-0.34f, 0.02f), 0.40f, Color.white, TextAlignmentOptions.Left, 0.32f);
        }

        TMP_Text MakeText(string name, Vector2 off, float size, Color color, TextAlignmentOptions align, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.rectTransform.sizeDelta = new Vector2(width, 0.2f);
            tmp.text = "";
            _items.Add(new Item { Tr = go.transform, Off = off });
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
            string who = p.Owner == Player.P0 ? "YOU" : "OPPONENT";
            _stats.text = $"{st.Name}\n<size=72%>({who})  ATK {st.Attack}  DEF {st.Defense}  MOV {st.Movement}</size>";
        }

        void LateUpdate()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            var ct = _cam.transform;
            foreach (var it in _items)
            {
                Vector3 pos = ct.position + ct.right * it.Off.x + ct.up * it.Off.y + ct.forward * Depth;
                it.Tr.position = pos;
                it.Tr.rotation = Quaternion.LookRotation(pos - ct.position, ct.up); // face the user
            }
        }
    }
}
