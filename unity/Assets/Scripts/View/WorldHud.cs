using TMPro;
using UnityEngine;
using Dejarik;

namespace Dejarik.View
{
    // Info HUD that floats above the board (world-anchored) and turns to face the player. Shows the status
    // line, dice totals, and selected-piece stats. (Action buttons are on the phone — DejarikGame.OnGUI.)
    public class WorldHud : MonoBehaviour
    {
        Transform _board;
        Camera _cam;
        TMP_Text _status, _dice, _stats;
        float _yStatus, _yDice, _yStats;

        public void Build(Transform board)
        {
            _board = board;
            _cam = Camera.main;
            _status = MakeText("status", 0.16f, Color.white, TextAlignmentOptions.Center, FontStyles.Normal);
            _dice = MakeText("dice", 0.22f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            _stats = MakeText("stats", 0.18f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            _yStatus = 0.46f; _yDice = 0.34f; _yStats = 0.27f; // heights above the board
        }

        TMP_Text MakeText(string name, float size, Color color, TextAlignmentOptions align, FontStyles style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = style;
            tmp.rectTransform.sizeDelta = new Vector2(1.6f, 0.2f);
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

        void LateUpdate()
        {
            if (_board == null) return;
            if (_cam == null) _cam = Camera.main;
            Vector3 baseP = _board.position;
            Place(_status, baseP + Vector3.up * _yStatus);
            Place(_dice, baseP + Vector3.up * _yDice);
            Place(_stats, baseP + Vector3.up * _yStats);
        }

        void Place(TMP_Text t, Vector3 pos)
        {
            if (t == null) return;
            t.transform.position = pos;
            if (_cam != null)
                t.transform.rotation = Quaternion.LookRotation(pos - _cam.transform.position, Vector3.up); // face the player
        }
    }
}
