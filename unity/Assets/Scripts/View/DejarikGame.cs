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
        [SerializeField] float tableRadius = 0.34f;    // board play-radius in meters (2/3 of prior)
        [SerializeField] float reachDistance = 1.01f;  // board distance along your gaze (+3in, then +13in)
        [SerializeField] float downBias = 0.2f;        // board drop below gaze (+3in)
        bool _pokePrev;
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

        // Centralized per-frame pointer (computed in Update, consumed by the turn coroutines).
        int _ptrSpace = -1;
        bool _ptrConfirm;

        // Action buttons on the phone touchscreen (always reachable on the physical Beam).
        readonly Rect[] _btnRects = new Rect[3];
        Action[] _btnActions;
        GUIStyle _btnStyle, _titleStyle, _statusStyle, _diceStyle, _turnStyle, _panelStyle, _subStyle;

        bool _celebrating;

        // Hold-to-move: while the MOVE button is held, tilt the phone like a wand to push the board in x/y/z
        // (mirrors XrealARApp's SceneManipulator). Sweep top right/left, tilt up/down, roll for closer/farther.
        const float MoveSpeed = 0.4f;   // m/s at full deflection (slower than the room-scale original)
        const float MoveDeadzone = 0.08f;
        bool _moveHeldPrev;
        Quaternion _moveRef;


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

            var rootGO = new GameObject("BoardRoot");
            rootGO.transform.SetParent(null);
            rootGO.transform.localScale = Vector3.one * (tableRadius / BoardLayout.Rim);
            _board = rootGO.AddComponent<BoardView>();
            _board.Build();
            _world = gameObject.AddComponent<WorldHud>();
            _world.Build(_board.Root);
            _btnActions = new Action[] { Recenter, null, () => { _ = NewGame(); } }; // [1] MOVE is a hold, not a tap
            if (AttitudeSensor.current != null) InputSystem.EnableDevice(AttitudeSensor.current);
            _audio.StartAmbient(_board.Root); // hologram-projector hum from the board
            Recenter();

            await NewGame();
        }

        async Task NewGame()
        {
            _celebrating = false;
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
            _hud = "Your move — select a piece";
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
            // Pinched a non-target cell (not a legal move/attack): keep the current selection so a mis-aim
            // isn't destructive — only its stats update. Pick another of your pieces to switch.
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
            if (_state.Turn == Human) _hud = "Select a glowing square to move or attack";
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
            const float hoverRadius = 0.25f; // nearest cell within this is "hovered" (white square shown)
            const float pokeDist = 0.085f;   // push your fingertip this close to the hovered cell to select

            // Phone buttons: RECENTER/NEW GAME are taps; MOVE (index 1) is a hold (handled below).
            bool moveHeld = false;
            var ts = Touchscreen.current;
            if (ts != null)
                foreach (var t in ts.touches)
                {
                    if (!t.press.isPressed) continue;
                    var p = t.position.ReadValue();
                    var gui = new Vector2(p.x, Screen.height - p.y);
                    if (_btnRects[1].Contains(gui)) moveHeld = true;
                    if (t.press.wasPressedThisFrame)
                        for (int i = 0; i < 3; i++)
                            if (i != 1 && _btnRects[i].Contains(gui)) { Debug.Log($"[Dejarik] phone button {i}"); _btnActions[i]?.Invoke(); break; }
                }
            MoveBoard(moveHeld);

            // Board interaction: the cell nearest your fingertip is "hovered" and marked with a white square
            // so you can see (and correct) the target despite hand-tracking offset. Push your finger into it
            // (or pinch) to select it.
            bool pinch = false; Vector3 tip = default;
            bool handTracked = _hand != null && _hand.TryGetTip(out tip, out pinch);
            bool poke = false;
            if (handTracked && _board.NearestSpace(tip, hoverRadius, out var sp))
            {
                _ptrSpace = sp;
                _input.SetReticle(_board.WorldPos(sp));
                poke = Vector3.Distance(tip, _board.WorldPos(sp)) < pokeDist; // finger pushed into the cell
            }
            else
            {
                _input.SetReticle(null);
            }
            bool selectEdge = (!_pokePrev && poke) || pinch; // poke is edge-triggered; pinch also confirms
            _pokePrev = poke;
            if (selectEdge && _ptrSpace >= 0) { _ptrConfirm = true; _audio.Click(_board.WorldPos(_ptrSpace)); Debug.Log($"[Dejarik] select space={_ptrSpace} tip={tip:F2} poke={poke} pinch={pinch}"); }

            _board.SetRimColor(HoloMaterials.HoloFor(_state.Turn)); // rim shows whose turn it is
        }

        // GUI-space rect that avoids the device safe area + the Beam's bottom navigation bar (its buttons get
        // hidden behind the nav bar otherwise). Origin top-left to match IMGUI.
        Rect SafeRectGui()
        {
            var sa = Screen.safeArea; // pixels, origin bottom-left
            float top = Mathf.Max(Screen.height - (sa.y + sa.height), Screen.height * 0.025f);
            float bottom = Mathf.Max(sa.y, Screen.height * 0.07f); // reserve the nav bar
            return new Rect(0f, top, Screen.width, Screen.height - top - bottom);
        }

        // A row of three large buttons across the bottom of the safe area.
        void ComputeButtonRects()
        {
            var sr = SafeRectGui();
            float margin = sr.width * 0.05f, gap = sr.width * 0.03f;
            float bw = (sr.width - 2 * margin - 2 * gap) / 3f, bh = sr.height * 0.15f;
            float by = sr.yMax - bh - sr.height * 0.02f;
            _btnRects[0] = new Rect(sr.x + margin, by, bw, bh);
            _btnRects[1] = new Rect(sr.x + margin + bw + gap, by, bw, bh);
            _btnRects[2] = new Rect(sr.x + margin + 2 * (bw + gap), by, bw, bh);
        }

        // Full-screen holographic control deck on the phone: deep-space background (covers the Beam trackpad
        // surface), title, turn indicator, status/dice, and the three glowing action buttons. Taps are
        // hit-tested in Update. Matches the web app's vibe.
        void OnGUI()
        {
            if (!_setupDone) return;
            float w = Screen.width, h = Screen.height;
            GUI.DrawTexture(new Rect(0, 0, w, h), HoloGui.BgTex, ScaleMode.StretchToFill);

            int title = Mathf.RoundToInt(h * 0.052f), big = Mathf.RoundToInt(h * 0.030f);
            int mid = Mathf.RoundToInt(h * 0.026f), small = Mathf.RoundToInt(h * 0.021f);

            _btnStyle ??= HoloGui.Button(mid);
            _titleStyle ??= HoloGui.Label(title, HoloGui.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
            _turnStyle ??= HoloGui.Label(big, HoloGui.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
            _statusStyle ??= HoloGui.Label(mid, HoloGui.Foreground, TextAnchor.MiddleCenter);
            _diceStyle ??= HoloGui.Label(small, HoloGui.Amber, TextAnchor.MiddleCenter, FontStyle.Bold);
            _subStyle ??= HoloGui.Label(small, new Color(HoloGui.Cyan.r, HoloGui.Cyan.g, HoloGui.Cyan.b, 0.5f), TextAnchor.MiddleCenter);
            _panelStyle ??= HoloGui.Panel(0);

            var sr = SafeRectGui();
            GUI.Box(sr, GUIContent.none, _panelStyle);

            float rowH = sr.height * 0.10f, y = sr.y + sr.height * 0.035f;
            HoloGui.GlowLabel(new Rect(sr.x, y, sr.width, rowH), "DEJARIK", _titleStyle, HoloGui.Cyan, 0.45f);
            y += rowH * 0.95f;
            GUI.Label(new Rect(sr.x, y, sr.width, rowH * 0.6f), "H O L O C H E S S", _subStyle);
            y += rowH * 1.1f;

            bool yours = _state != null && _state.Turn == Human;
            _turnStyle.normal.textColor = yours ? HoloGui.Cyan : HoloGui.Amber;
            GUI.Label(new Rect(sr.x, y, sr.width, rowH), yours ? "● YOUR TURN" : "● OPPONENT'S TURN", _turnStyle);
            y += rowH * 1.25f;

            GUI.Label(new Rect(sr.x + sr.width * 0.06f, y, sr.width * 0.88f, rowH), _hud ?? "", _statusStyle);
            y += rowH;
            if (!string.IsNullOrEmpty(_diceHud))
                GUI.Label(new Rect(sr.x + sr.width * 0.06f, y, sr.width * 0.88f, rowH), _diceHud, _diceStyle);

            ComputeButtonRects();
            GUI.Button(_btnRects[0], "RECENTER", _btnStyle);
            GUI.Button(_btnRects[1], _moveHeldPrev ? "MOVING…\ntilt phone" : "MOVE\nhold + tilt", _btnStyle);
            GUI.Button(_btnRects[2], "NEW GAME", _btnStyle);
        }

        // Hold-to-move: deflect the phone from where it pointed when you grabbed MOVE; that deflection drives
        // the board in camera-relative x/y/z. Sweep top right/left -> right/left; tilt up/down -> up/down;
        // roll screen-right/left -> closer/farther. (Ports XrealARApp's SceneManipulator MOVE gesture.)
        void MoveBoard(bool held)
        {
            var cam = Camera.main;
            if (held && cam != null && AttitudeSensor.current != null)
            {
                Quaternion att = AttitudeSensor.current.attitude.ReadValue();
                if (!_moveHeldPrev) _moveRef = att;
                Defl(_moveRef, att, out float r, out float u, out float roll);
                Vector3 dir = Flat(cam.transform.right) * r + Vector3.up * u + Flat(cam.transform.forward) * (-roll);
                _board.Root.position += dir * (MoveSpeed * Time.deltaTime);
            }
            _moveHeldPrev = held;
        }

        // Deflection of the phone from a reference orientation, expressed in the reference frame.
        void Defl(Quaternion refAtt, Quaternion att, out float right, out float up, out float roll)
        {
            Quaternion d = Quaternion.Inverse(refAtt) * att;
            Vector3 top = d * Vector3.up;        // device +Y (top edge)
            Vector3 nrm = d * Vector3.forward;   // device +Z (screen normal)
            right = Dz(top.x);
            up = Dz(top.z);
            roll = Dz(nrm.x);
        }

        float Dz(float v) => Mathf.Abs(v) < MoveDeadzone ? 0f : v;
        static Vector3 Flat(Vector3 v) { v = Vector3.ProjectOnPlane(v, Vector3.up); return v.sqrMagnitude < 1e-4f ? Vector3.forward : v.normalized; }

        // ---- bot ----

        // The bot's turn, telegraphed step-by-step so it's easy to follow: (a) light every square the chosen
        // piece could act on, (b) jitter out the squares it did NOT pick, leaving only the chosen one pulsing,
        // (c) hold a beat on that square, then (d) commit and animate the move/attack.
        IEnumerator BotTurn()
        {
            _hud = "Opponent is thinking...";
            var action = Bot.Action(_state, _rng);
            if (action == null)
            {
                yield return new WaitForSeconds(BOT_PONDER / 1000f);
                _state = Engine.PassAction(_state);
                yield return PlayFx(_state);
                SyncViews();
                yield break;
            }

            string pid = action.Type == BotAction.Kind.Move ? action.PieceId : action.AttackerId;
            int pieceSpace = Engine.GetPiece(_state, pid).Space;
            var moves = Engine.LegalMoves(_state, pid);
            var atkSpaces = Engine.AttackTargets(_state, pid).Select(id => Engine.GetPiece(_state, id).Space).ToList();
            bool chosenIsAttack = action.Type == BotAction.Kind.Attack;
            int chosenSpace = chosenIsAttack ? Engine.GetPiece(_state, action.DefenderId).Space : action.Dest;

            // (a) light up every square the piece could act on (board illumination, no overlay markers).
            _hud = "Opponent is choosing...";
            _board.SetHighlights(moves, atkSpaces, null, pieceSpace);
            yield return new WaitForSeconds(0.95f);

            // (b) glitch out the squares it did NOT pick; the chosen square stays lit.
            var rejected = moves.Concat(atkSpaces).Where(sp => sp != chosenSpace).Distinct().ToList();
            _board.GlitchOutCells(rejected);
            yield return new WaitForSeconds(0.7f);

            // (c) hold a beat on the lone chosen square, then (d) commit + animate the move/attack.
            yield return new WaitForSeconds(0.45f);
            _board.ClearHighlights();

            if (chosenIsAttack) _state = Engine.ApplyAttack(_state, action.AttackerId, action.DefenderId, _rng);
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
            if (!_state.Winner.HasValue) { _hud = "Draw."; yield break; }
            _hud = $"Player {_state.Winner.Value.Num()} wins!";
            StartCoroutine(Celebrate(_state.Winner.Value));   // cheer forever until NEW GAME
            yield break;
        }

        // The winning team cheers continuously until the game is reset. Re-asserting PlayVictory is cheap
        // (PlayLoop no-ops if already looping) and re-arms any clip that played once; the whoop repeats.
        IEnumerator Celebrate(Player winner)
        {
            _celebrating = true;
            float nextWhoop = 0f;
            while (_celebrating)
            {
                foreach (var v in _views.Values)
                    if (v && v.Owner == winner) v.PlayVictory();
                if (Time.time >= nextWhoop)
                {
                    _audio.Victory(_board.WorldPos(Board.Center));
                    nextWhoop = Time.time + 5f;
                }
                yield return new WaitForSeconds(1.5f);
            }
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
                    _audio.Move(v.transform.position);
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
            _audio.Dice(center);

            yield return new WaitForSeconds(COMBAT_LEAD / 1000f);
            // Square off: both combatants turn to face each other before the strike ("look before fighting").
            if (atkView != null && atkFx.Facing.HasValue)
            {
                atkView.FaceSpace(atkFx.Facing.Value);
                var defView = FindViewAtSpace(atkFx.Facing.Value);
                if (defView != null) defView.FaceSpace(atkView.Space);
            }

            yield return new WaitForSeconds((STRIKE_AT - COMBAT_LEAD) / 1000f);
            if (atkView != null)
            {
                atkView.PlayAttack(combat.Outcome == Outcome.Kill);
                _audio.Attack(combat.AttackerType.Value, atkView.transform.position, combat.Outcome == Outcome.Kill); // roar/strike
            }

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
                        {
                            defv.PlayAttack(true);
                            _audio.Attack(combat.DefenderType.Value, defv.transform.position, true);
                        }
                    }
                    dv.PlayDeathAndDissolve(d.ByType, DEATH_REMOVE - REACT_AT);
                    _audio.Death(d.PieceTypeVal.Value, dv.transform.position);
                    _views.Remove(d.PieceId);
                }
            }
            if (deaths.Count == 0 && hit != null && _views.TryGetValue(hit.PieceId, out var hv2))
            {
                hv2.PlayHit();
                _audio.Hit(combat.DefenderType.Value, hv2.transform.position);
            }

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

        // Place the board where you're looking (centered on your gaze, with a slight downward bias so the
        // flat board's face is visible). Rotate it so your (P0) side faces you: P0 sits on local +Z, so
        // local +Z points back toward the camera. Tap RECENTER while looking where you want the board.
        void Recenter()
        {
            var cam = Camera.main;
            if (cam == null) { _board.Root.SetPositionAndRotation(new Vector3(0f, -0.25f, 0.6f), Quaternion.Euler(0f, 180f, 0f)); return; }
            Vector3 fwd = cam.transform.forward;
            _board.Root.position = cam.transform.position + fwd * reachDistance + Vector3.down * downBias;
            Vector3 flat = fwd; flat.y = 0f; flat = flat.sqrMagnitude < 1e-4f ? Vector3.forward : flat.normalized;
            _board.Root.rotation = Quaternion.LookRotation(-flat, Vector3.up);
        }
    }
}
