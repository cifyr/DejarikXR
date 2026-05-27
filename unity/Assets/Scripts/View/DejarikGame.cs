using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Dejarik;
using XrealAR;

namespace Dejarik.View
{
    // Orchestrates AR Dejarik: builds the board, instantiates piece views, runs the turn loop, plays the
    // engine's animation cues with the web game's timing, drives the bot, and handles gaze+tap input.
    // The pure rules live in Dejarik.Engine; this class only renders/animates state transitions.
    public class DejarikGame : MonoBehaviour
    {
        [SerializeField] float tableRadius = 0.6f;     // board play-radius in meters
        [SerializeField] float reachForward = 0.5f;    // board distance in front of the head
        [SerializeField] float reachDown = 0.35f;      // board drop below eye level (to hand reach)
        const Player Human = Player.P0;

        // Combat timing (ms), mirroring src/game/timing.ts.
        const float COMBAT_LEAD = 2250f, STRIKE_AT = 2630f, REACT_AT = 2950f, DEATH_REMOVE = 4050f;
        const float BOT_PONDER = 900f;

        BoardView _board;
        DiceView _dice;
        GameAudio _audio;
        GazeSelector _input;
        HandSelector _hand;
        WorldHud _world;
        AnchorPlacementController _anchors;

        // Centralized per-frame pointer (computed in Update, consumed by the turn coroutines).
        int _ptrSpace = -1;
        bool _ptrConfirm;

        // Action buttons on the phone touchscreen (always reachable on the physical Beam).
        readonly Rect[] _btnRects = new Rect[3];
        Action[] _btnActions;
        GUIStyle _btnStyle;


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
            _world = gameObject.AddComponent<WorldHud>();
            _world.Build();
            _btnActions = new Action[] { Recenter, PinBoard, () => { _ = NewGame(); } };
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
            if (DebugSampleDiceHud) _diceHud = "Molator  24    vs    9  Ghhhk";
            StartCoroutine(RunGame());
        }

        IEnumerator RunGame()
        {
            yield return null;
            // Head tracking isn't ready at Start, so the first placement can be off; re-center once it settles.
            yield return new WaitForSeconds(1.2f);
            Recenter();
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
            _hud = "Your move — pinch a piece";
            if (DebugAutoSelect)
            {
                var mine = Engine.PiecesOf(_state, Human);
                if (mine.Count > 0) Select(mine[0].Id);
            }
            while (true)
            {
                yield return null;
                if (!_ptrConfirm || _ptrSpace < 0) continue;
                _ptrConfirm = false;

                var before = _state;
                if (TryHumanClick(_ptrSpace) && !ReferenceEquals(before, _state))
                {
                    yield return PlayFx(_state);
                    SyncViews();
                    yield break;
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
                if (_ptrConfirm && _ptrSpace >= 0 && opts.Contains(_ptrSpace))
                {
                    _ptrConfirm = false;
                    _state = Engine.ResolvePush(_state, _ptrSpace);
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

        // Centralized input, once per frame. Primary: pinch the cell/piece (or button) nearest your
        // fingertip. Fallback only when NO hand is tracked at all: head-gaze + Beam tap (so you're never
        // fully stuck). Buttons are handled here so they work in any phase.
        void Update()
        {
            if (!_setupDone || _world == null) return;

            _world.SetStatus(_hud);
            _world.SetDice(_diceHud);
            _world.SetStats(_statsPieceId != null ? Engine.GetPiece(_state, _statsPieceId) : null);

            ComputeButtonRects();
            _ptrSpace = -1; _ptrConfirm = false;
            const float maxDist = 0.13f; // fingertip within ~13cm of a cell selects it

            // A tap on the Beam touchscreen: first try the phone buttons, else it's a board-confirm.
            bool tap = false; Vector2 tapGui = default;
            var ts = Touchscreen.current;
            if (ts != null)
                foreach (var t in ts.touches)
                    if (t.press.wasPressedThisFrame) { tap = true; var p = t.position.ReadValue(); tapGui = new Vector2(p.x, Screen.height - p.y); }
            if (tap)
                for (int i = 0; i < 3; i++)
                    if (_btnRects[i].Contains(tapGui)) { Debug.Log($"[Dejarik] phone button {i}"); _btnActions[i]?.Invoke(); tap = false; break; }

            // Board selection by pointing: cast a ray from the eye THROUGH the fingertip onto the board, so
            // whatever cell your finger visually overlaps is picked (robust to hand-tracking depth error).
            // Pinch confirms. Falls back to head-gaze + tap when no hand is tracked.
            bool pinch = false; Vector3 tip = default;
            bool handTracked = _hand != null && _hand.TryGetTip(out tip, out pinch);
            bool confirm = pinch || tap;
            var cam = Camera.main;
            if (handTracked && cam != null)
            {
                var ray = new Ray(cam.transform.position, tip - cam.transform.position);
                if (_board.Raycast(ray, out var sp)) { _ptrSpace = sp; _input.SetReticle(_board.WorldPos(sp)); }
                else { _board.NearestSpace(tip, maxDist, out _ptrSpace); _input.SetReticle(_ptrSpace >= 0 ? _board.WorldPos(_ptrSpace) : tip); }
            }
            else
            {
                if (_board.Raycast(_input.CurrentRay, out var sp)) { _ptrSpace = sp; _input.SetReticle(_board.WorldPos(sp)); }
                else _input.SetReticle(null);
            }
            if (confirm && _ptrSpace >= 0) { _ptrConfirm = true; Debug.Log($"[Dejarik] confirm space={_ptrSpace}"); }
        }

        void ComputeButtonRects()
        {
            float w = Screen.width, h = Screen.height, margin = w * 0.04f, gap = w * 0.025f;
            float bw = (w - 2 * margin - 2 * gap) / 3f, bh = h * 0.11f, y = h - bh - h * 0.05f;
            _btnRects[0] = new Rect(margin, y, bw, bh);
            _btnRects[1] = new Rect(margin + bw + gap, y, bw, bh);
            _btnRects[2] = new Rect(margin + 2 * (bw + gap), y, bw, bh);
        }

        // Action buttons drawn on the phone screen; taps handled in Update via Touchscreen hit-testing.
        void OnGUI()
        {
            if (!_setupDone) return;
            _btnStyle ??= new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(Screen.height * 0.024f), wordWrap = true };
            ComputeButtonRects();
            GUI.Button(_btnRects[0], "RECENTER", _btnStyle);
            GUI.Button(_btnRects[1], "PIN BOARD", _btnStyle);
            GUI.Button(_btnRects[2], "NEW GAME", _btnStyle);
        }

        void PinBoard() { if (_anchors != null) _ = _anchors.PinAsync(_board.Root, "dejarik-board"); }

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

        // Place the board in front of and below the head, at hand-reach height so you can pinch the cells.
        void Recenter()
        {
            var cam = Camera.main;
            if (cam == null) { _board.Root.position = new Vector3(0f, -reachDown, reachForward); _board.Root.rotation = Quaternion.identity; return; }
            Vector3 fwd = cam.transform.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude < 1e-4f ? Vector3.forward : fwd.normalized;
            _board.Root.position = cam.transform.position + fwd * reachForward + Vector3.up * -reachDown;
            _board.Root.rotation = Quaternion.identity;
        }
    }
}
