using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Dejarik;
using XrealAR;

namespace Dejarik.View
{
    // Orchestrates AR Dejarik: builds the board, instantiates piece views, runs the turn loop, plays the
    // engine's animation cues with the web game's timing, drives the bot, and handles gaze+tap input.
    // The pure rules live in Dejarik.Engine; this class only renders/animates state transitions.
    public class DejarikGame : MonoBehaviour
    {
        [SerializeField] float tableRadius = 0.45f;       // board play-radius in meters
        [SerializeField] Vector3 startOffset = new Vector3(0f, 0.6f, 0.8f); // board center vs tracking origin
        const Player Human = Player.P0;

        // Combat timing (ms), mirroring src/game/timing.ts.
        const float COMBAT_LEAD = 2250f, STRIKE_AT = 2630f, REACT_AT = 2950f, DEATH_REMOVE = 4050f;
        const float BOT_PONDER = 900f;

        BoardView _board;
        DiceView _dice;
        GameAudio _audio;
        GazeSelector _input;
        HandSelector _hand;
        AnchorPlacementController _anchors;

        GameState _state;
        Rng _rng;
        readonly Dictionary<string, PieceView> _views = new Dictionary<string, PieceView>();

        string _selectedId;
        string _statsPieceId;   // piece whose stats the HUD shows (yours or the opponent's)
        string _diceHud;        // combat roll totals shown during a fight
        bool _setupDone;
        string _hud = "";

        async void Start()
        {
            _input = gameObject.AddComponent<GazeSelector>();
            _hand = gameObject.AddComponent<HandSelector>();
            _dice = gameObject.AddComponent<DiceView>();
            _audio = gameObject.AddComponent<GameAudio>();
            _anchors = FindFirstObjectByType<AnchorPlacementController>();

            var rootGO = new GameObject("BoardRoot");
            rootGO.transform.SetParent(null);
            rootGO.transform.localScale = Vector3.one * (tableRadius / BoardLayout.Rim);
            _board = rootGO.AddComponent<BoardView>();
            _board.Build();
            Recenter();

            await NewGame();
        }

        async Task NewGame()
        {
            foreach (var v in _views.Values) if (v) Destroy(v.gameObject);
            _views.Clear();
            _selectedId = null;

            _rng = RngFactory.Make((uint)System.DateTime.Now.Ticks);
            _state = Engine.CreateInitialState(_rng, Human);

            var tasks = new List<Task>();
            foreach (var p in _state.Pieces)
            {
                var go = new GameObject($"piece_{p.Id}");
                go.transform.SetParent(_board.Root, false);
                var pv = go.AddComponent<PieceView>();
                _views[p.Id] = pv;
                tasks.Add(pv.Init(p));
            }
            await Task.WhenAll(tasks);
            Debug.Log($"[Dejarik] {_views.Count} pieces loaded; board pos={_board.Root.position} scale={_board.Root.lossyScale.x:F3}");

            _setupDone = true;
            if (DebugSampleDiceHud) { _diceHud = "Molator  24    vs    9  Ghhhk"; DebugHudReport(1080, 2400); DebugHudReport(2400, 1080); }
            StartCoroutine(RunGame());
        }

        IEnumerator RunGame()
        {
            yield return null;
            while (_state.Phase != Phase.GameOver)
            {
                switch (_state.Phase)
                {
                    case Phase.Play:
                        if (_state.Turn == Human) yield return HumanTurn();
                        else yield return BotTurn();
                        break;
                    case Phase.AwaitPush:
                        if (_state.Pending.Chooser == Human) yield return HumanPush();
                        else yield return BotPush();
                        break;
                    case Phase.ToTheDeath:
                        yield return DoDuel();
                        break;
                }
            }
            yield return DoVictory();
        }

        // ---- human input ----

        public static bool DebugAutoSelect;    // editor capture hook: select a piece to preview highlights
        public static bool DebugSampleDiceHud; // editor capture hook: show a sample dice-total line

        IEnumerator HumanTurn()
        {
            _selectedId = null;
            RefreshHighlights();
            _hud = "Your move — point at a piece and pinch (or tap the Beam).";
            if (DebugAutoSelect)
            {
                var mine = Engine.PiecesOf(_state, Human);
                if (mine.Count > 0) Select(mine[0].Id);
            }
            while (true)
            {
                yield return null;
                UpdateGaze(out int sp, out bool hit, out bool confirm);
                if (!confirm || !hit) continue;

                var before = _state;
                if (TryHumanClick(sp))
                {
                    if (!ReferenceEquals(before, _state))
                    {
                        yield return PlayFx(_state);
                        SyncViews();
                        yield break;
                    }
                }
            }
        }

        // Returns true if the click changed selection or applied an action.
        bool TryHumanClick(int sp)
        {
            var piece = Engine.PieceAt(_state, sp);
            if (piece != null) _statsPieceId = piece.Id;   // any piece you pinch shows its stats on the HUD

            if (_selectedId == null)
            {
                if (piece != null && piece.Owner == Human) { Select(piece.Id); }
                return false;
            }

            var moves = Engine.LegalMoves(_state, _selectedId);
            var atkIds = Engine.AttackTargets(_state, _selectedId);
            var atkSpaceToId = atkIds.ToDictionary(id => Engine.GetPiece(_state, id).Space, id => id);

            if (moves.Contains(sp)) { Debug.Log($"[Dejarik] move {_selectedId} -> {sp}"); _state = Engine.ApplyMove(_state, _selectedId, sp); Deselect(); return true; }
            if (atkSpaceToId.TryGetValue(sp, out var defId)) { Debug.Log($"[Dejarik] attack {_selectedId} -> {defId}"); _state = Engine.ApplyAttack(_state, _selectedId, defId, _rng); Deselect(); return true; }
            if (piece != null && piece.Owner == Human) { Select(piece.Id); return false; }
            Deselect();
            return false;
        }

        IEnumerator HumanPush()
        {
            var opts = _state.Pending.Options;
            _board.SetHighlights(null, null, opts, -1);
            _hud = "Choose where to push.";
            while (true)
            {
                yield return null;
                UpdateGaze(out int sp, out bool hit, out bool confirm);
                if (confirm && hit && opts.Contains(sp))
                {
                    _state = Engine.ResolvePush(_state, sp);
                    _board.ClearHighlights();
                    yield return PlayFx(_state);
                    SyncViews();
                    yield break;
                }
            }
        }

        void Select(string id)
        {
            _selectedId = id;
            foreach (var kv in _views) kv.Value.SetSelected(kv.Key == id);
            RefreshHighlights();
        }

        void Deselect()
        {
            _selectedId = null;
            foreach (var v in _views.Values) v.SetSelected(false);
            _board.ClearHighlights();
        }

        void RefreshHighlights()
        {
            if (_selectedId == null) { _board.ClearHighlights(); return; }
            var moves = Engine.LegalMoves(_state, _selectedId);
            var atkSpaces = Engine.AttackTargets(_state, _selectedId).Select(id => Engine.GetPiece(_state, id).Space).ToList();
            int selSpace = Engine.GetPiece(_state, _selectedId)?.Space ?? -1;
            _board.SetHighlights(moves, atkSpaces, null, selSpace);
        }

        // Primary: pinch the cell/piece nearest your fingertip. Fallback (when the hand isn't tracked, since
        // hand tracking is flaky on this hardware): head-gaze pointer + Beam tap, so you're never stuck.
        string _inputDbg = "";
        void UpdateGaze(out int space, out bool hit, out bool confirm)
        {
            const float maxDist = 0.10f; // fingertip within ~10cm of a cell center points at it
            space = -1; hit = false; confirm = false;

            bool handPointing = false;
            if (_hand != null && _hand.TryGetTip(out var tip, out bool pinch))
            {
                if (_board.NearestSpace(tip, maxDist, out space)) { hit = true; handPointing = true; _input.SetReticle(_board.WorldPos(space)); }
                if (pinch && hit) { confirm = true; Debug.Log($"[Dejarik] pinch -> space={space}"); }
            }

            if (!handPointing) // gaze fallback
            {
                hit = _board.Raycast(_input.CurrentRay, out space);
                _input.SetReticle(hit ? _board.WorldPos(space) : (Vector3?)null);
                if (_input.ConfirmDown && hit) { confirm = true; Debug.Log($"[Dejarik] gaze tap -> space={space}"); }
            }

            _inputDbg = _hand != null ? _hand.Status : "no hand selector";
        }

        // ---- bot ----

        IEnumerator BotTurn()
        {
            _hud = "Opponent is thinking...";
            var action = Bot.Action(_state, _rng);
            // Telegraph: light the chosen piece's options briefly.
            if (action != null)
            {
                string pid = action.Type == BotAction.Kind.Move ? action.PieceId : action.AttackerId;
                var moves = Engine.LegalMoves(_state, pid);
                var atk = Engine.AttackTargets(_state, pid).Select(id => Engine.GetPiece(_state, id).Space).ToList();
                _board.SetHighlights(moves, atk, null, Engine.GetPiece(_state, pid).Space);
            }
            yield return new WaitForSeconds(BOT_PONDER / 1000f);
            _board.ClearHighlights();

            if (action == null) _state = Engine.PassAction(_state);
            else if (action.Type == BotAction.Kind.Attack) _state = Engine.ApplyAttack(_state, action.AttackerId, action.DefenderId, _rng);
            else _state = Engine.ApplyMove(_state, action.PieceId, action.Dest);

            yield return PlayFx(_state);
            SyncViews();
        }

        IEnumerator BotPush()
        {
            int dest = Bot.PushTarget(_state);
            _board.SetHighlights(null, null, new List<int> { dest }, -1);
            yield return new WaitForSeconds(BOT_PONDER / 1000f);
            _board.ClearHighlights();
            _state = Engine.ResolvePush(_state, dest);
            yield return PlayFx(_state);
            SyncViews();
        }

        IEnumerator DoDuel()
        {
            _hud = "To the death!";
            _state = Engine.ResolveToTheDeath(_state, _rng);
            yield return PlayFx(_state);
            SyncViews();
        }

        IEnumerator DoVictory()
        {
            _board.ClearHighlights();
            _hud = _state.Winner.HasValue ? $"Player {_state.Winner.Value.Num()} wins!" : "Draw.";
            _audio.PlayVictory();
            foreach (var v in _views.Values)
                if (_state.Winner.HasValue && v.Owner == _state.Winner.Value) v.PlayVictory();
            yield break;
        }

        // ---- animation of an applied transition ----

        IEnumerator PlayFx(GameState after)
        {
            var fx = after.Fx;
            if (fx == null || fx.Count == 0) yield break;

            bool hasCombat = fx.Any(f => f.Kind == FxKind.Combat);
            if (!hasCombat)
            {
                var mv = fx.FirstOrDefault(f => f.Kind == FxKind.Move);
                if (mv != null && _views.TryGetValue(mv.PieceId, out var v))
                {
                    int finalSpace = Engine.GetPiece(after, mv.PieceId)?.Space ?? v.Space;
                    _audio.PlayMove();
                    v.WalkAlong(mv.Path, finalSpace);
                    float t = 0f;
                    while (v != null && v.IsWalking && t < 6f) { t += Time.deltaTime; yield return null; }
                }
                yield break;
            }

            var combat = fx.First(f => f.Kind == FxKind.Combat);
            var attackFxs = fx.Where(f => f.Kind == FxKind.Attack).ToList();
            var deaths = fx.Where(f => f.Kind == FxKind.Death).ToList();
            var hit = fx.FirstOrDefault(f => f.Kind == FxKind.Hit);
            var atkFx = attackFxs.FirstOrDefault();
            string attackerId = atkFx?.PieceId;
            PieceView atkView = attackerId != null && _views.TryGetValue(attackerId, out var av) ? av : null;

            int atkTotal = combat.AttackDice.Sum(), defTotal = combat.DefenseDice.Sum();
            _diceHud = $"{Pieces.Stats[combat.AttackerType.Value].Name}  {atkTotal}    vs    {defTotal}  {Pieces.Stats[combat.DefenderType.Value].Name}";
            Vector3 center = _board.WorldPos(Board.Center);
            _dice.ShowRoll(atkTotal, defTotal, combat.AttackDice.Length, combat.DefenseDice.Length, combat.AttackerOwner.Value, center);
            _audio.PlayDice();

            yield return new WaitForSeconds(COMBAT_LEAD / 1000f);
            // Square off: both combatants turn to face each other before the strike ("look before fighting").
            if (atkView != null && atkFx.Facing.HasValue)
            {
                atkView.FaceSpace(atkFx.Facing.Value);
                var defView = FindViewAtSpace(atkFx.Facing.Value);
                if (defView != null) defView.FaceSpace(atkView.Space);
            }

            yield return new WaitForSeconds((STRIKE_AT - COMBAT_LEAD) / 1000f);
            if (atkView != null) atkView.PlayAttack(combat.Outcome == Outcome.Kill);
            _audio.PlayStrike();

            yield return new WaitForSeconds((REACT_AT - STRIKE_AT) / 1000f);
            foreach (var d in deaths)
            {
                if (_views.TryGetValue(d.PieceId, out var dv))
                {
                    // counter-kill: the defender lands the finishing counter as the attacker falls.
                    if (d.WasAttacker == true)
                    {
                        var defAtk = attackFxs.Skip(1).FirstOrDefault();
                        if (defAtk != null && _views.TryGetValue(defAtk.PieceId, out var defv))
                            defv.PlayAttack(true);
                    }
                    dv.PlayDeathAndDissolve(d.ByType, DEATH_REMOVE - REACT_AT);
                    _views.Remove(d.PieceId);
                    _audio.PlayDeath();
                }
            }
            if (deaths.Count == 0 && hit != null && _views.TryGetValue(hit.PieceId, out var hv2))
                hv2.PlayHit();

            yield return new WaitForSeconds((DEATH_REMOVE - REACT_AT) / 1000f + 0.2f);
            _diceHud = null;
        }

        // Verifies the HUD content + that every element fits on-screen at a given resolution (we can't
        // pixel-capture IMGUI headlessly). Mirrors the rects computed in OnGUI.
        void DebugHudReport(float w, float h)
        {
            float margin = w * 0.04f;
            float gap = w * 0.025f;
            float bw = (w - 2 * margin - 2 * gap) / 3f;
            float bh = h * 0.10f;
            float by = h - bh - h * 0.06f;
            float btnRight = margin + 2 * (bw + gap) + bw;
            var statsBox = new Rect(margin, h * 0.24f, w * 0.46f, h * 0.20f);
            var sp0 = _statsPieceId != null ? Engine.GetPiece(_state, _statsPieceId) : null;
            string stats = sp0 != null ? $"{Pieces.Stats[sp0.Type].Name} A{Pieces.Stats[sp0.Type].Attack}/D{Pieces.Stats[sp0.Type].Defense}/M{Pieces.Stats[sp0.Type].Movement}" : "none";
            bool buttonsFit = btnRight <= w + 0.5f && by + bh <= h;
            bool statsFit = statsBox.xMax <= w && statsBox.yMax <= h;
            Debug.Log($"[HUD] {w}x{h}: hud='{_hud}' dice='{_diceHud}' stats=({stats}) " +
                      $"buttonsRight={btnRight:F0}/{w} fit={buttonsFit} statsBox={statsBox} fit={statsFit}");
        }

        PieceView FindViewAtSpace(int space)
        {
            foreach (var v in _views.Values) if (v != null && v.Space == space) return v;
            return null;
        }

        // Reconcile views with state: snap spaces and remove any pieces that left without a death anim.
        void SyncViews()
        {
            foreach (var p in _state.Pieces)
                if (_views.TryGetValue(p.Id, out var v) && !v.IsWalking && v.Space != p.Space)
                    v.SnapTo(p.Space);

            var alive = new HashSet<string>(_state.Pieces.Select(p => p.Id));
            foreach (var id in _views.Keys.ToList())
                if (!alive.Contains(id))
                {
                    if (_views[id]) Destroy(_views[id].gameObject);
                    _views.Remove(id);
                }
        }

        // ---- placement + HUD ----

        void Recenter()
        {
            var cam = Camera.main;
            if (cam == null) { _board.Root.position = startOffset; return; }
            Vector3 fwd = cam.transform.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude < 1e-4f ? Vector3.forward : fwd.normalized;
            _board.Root.position = cam.transform.position + fwd * startOffset.z + Vector3.up * (startOffset.y - cam.transform.position.y);
            _board.Root.rotation = Quaternion.identity;
        }

        static bool _hudLogged;
        void OnGUI()
        {
            if (!_setupDone) return;
            float w = Screen.width, h = Screen.height;
            float margin = w * 0.04f;

            if (!_hudLogged && Event.current.type == EventType.Repaint)
            {
                _hudLogged = true;
                var sp0 = _statsPieceId != null ? Engine.GetPiece(_state, _statsPieceId) : null;
                Debug.Log($"[HUD] screen={w}x{h} hud='{_hud}' dice='{_diceHud}' stats={(sp0 != null ? Pieces.Stats[sp0.Type].Name : "none")} input='{_inputDbg}'");
                Debug.Log($"[HUD] statsBox=({margin},{h * 0.24f},{w * 0.46f},{h * 0.20f}) buttonsY={h - h * 0.10f - h * 0.06f} (screen h={h})");
            }

            // Status banner, kept inside the top safe area.
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(h * 0.026f),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
            };
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(margin, h * 0.05f, w - 2 * margin, h * 0.08f), _hud, style);

            // Dice roll totals during combat.
            if (!string.IsNullOrEmpty(_diceHud))
            {
                var dstyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(h * 0.034f), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
                };
                dstyle.normal.textColor = Color.white;
                GUI.Label(new Rect(margin, h * 0.14f, w - 2 * margin, h * 0.07f), _diceHud, dstyle);
            }

            // Selected-piece stats panel (yours or the opponent's).
            var sp = _statsPieceId != null ? Engine.GetPiece(_state, _statsPieceId) : null;
            if (sp != null)
            {
                var st = Pieces.Stats[sp.Type];
                var c = HoloMaterials.HoloFor(sp.Owner);
                var box = new Rect(margin, h * 0.24f, w * 0.46f, h * 0.20f);
                var bg = new GUIStyle(GUI.skin.box);
                GUI.color = new Color(c.r, c.g, c.b, 0.85f);
                GUI.Box(box, GUIContent.none);
                GUI.color = Color.white;
                var ps = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(h * 0.024f) };
                ps.normal.textColor = Color.white;
                string who = sp.Owner == Human ? "YOU" : "OPPONENT";
                GUI.Label(new Rect(box.x + 14, box.y + 8, box.width - 20, box.height - 12),
                    $"{st.Name}  ({who})\nAttack    {st.Attack}\nDefense  {st.Defense}\nMove      {st.Movement}", ps);
            }

            // Input/hand-tracking status (diagnostic), just above the buttons.
            var dbg = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(h * 0.018f), alignment = TextAnchor.MiddleCenter };
            dbg.normal.textColor = new Color(0.6f, 0.85f, 1f);
            GUI.Label(new Rect(margin, h - h * 0.06f - h * 0.10f - h * 0.05f, w - 2 * margin, h * 0.045f), $"input: {_inputDbg}", dbg);

            // Three buttons in a row that always fits the width, lifted off the bottom safe area.
            var bstyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(h * 0.022f), wordWrap = true };
            float gap = w * 0.025f;
            float bw = (w - 2 * margin - 2 * gap) / 3f;
            float bh = h * 0.10f;
            float y = h - bh - h * 0.06f;
            if (GUI.Button(new Rect(margin, y, bw, bh), "RECENTER", bstyle)) Recenter();
            if (GUI.Button(new Rect(margin + bw + gap, y, bw, bh), "PIN", bstyle) && _anchors != null)
                _ = _anchors.PinAsync(_board.Root, "dejarik-board");
            if (GUI.Button(new Rect(margin + 2 * (bw + gap), y, bw, bh), "NEW GAME", bstyle)) _ = NewGame();
        }
    }
}
