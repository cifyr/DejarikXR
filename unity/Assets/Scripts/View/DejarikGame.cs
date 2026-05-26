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
        AnchorPlacementController _anchors;

        GameState _state;
        Rng _rng;
        readonly Dictionary<string, PieceView> _views = new Dictionary<string, PieceView>();

        string _selectedId;
        bool _setupDone;
        string _hud = "";

        async void Start()
        {
            _input = gameObject.AddComponent<GazeSelector>();
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

            _setupDone = true;
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

        IEnumerator HumanTurn()
        {
            _selectedId = null;
            RefreshHighlights();
            _hud = "Your move — look at a piece and tap.";
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
            if (_selectedId == null)
            {
                if (piece != null && piece.Owner == Human) { Select(piece.Id); }
                return false;
            }

            var moves = Engine.LegalMoves(_state, _selectedId);
            var atkIds = Engine.AttackTargets(_state, _selectedId);
            var atkSpaceToId = atkIds.ToDictionary(id => Engine.GetPiece(_state, id).Space, id => id);

            if (moves.Contains(sp)) { _state = Engine.ApplyMove(_state, _selectedId, sp); Deselect(); return true; }
            if (atkSpaceToId.TryGetValue(sp, out var defId)) { _state = Engine.ApplyAttack(_state, _selectedId, defId, _rng); Deselect(); return true; }
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

        void UpdateGaze(out int space, out bool hit, out bool confirm)
        {
            hit = _board.Raycast(_input.CurrentRay, out space);
            _input.SetReticle(hit ? _board.WorldPos(space) : (Vector3?)null);
            confirm = _input.ConfirmDown;
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

            Vector3 center = _board.WorldPos(Board.Center) + Vector3.up * 0.12f;
            _dice.ShowRoll(combat.AttackDice.Sum(), combat.DefenseDice.Sum(),
                combat.AttackDice.Length, combat.DefenseDice.Length, combat.AttackerOwner.Value, center);
            _audio.PlayDice();

            yield return new WaitForSeconds(COMBAT_LEAD / 1000f);
            if (atkView != null && atkFx.Facing.HasValue) atkView.FaceSpace(atkFx.Facing.Value);
            if (hit != null && _views.TryGetValue(hit.PieceId, out var hv) && hit.Facing.HasValue) hv.FaceSpace(hit.Facing.Value);

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

        void OnGUI()
        {
            if (!_setupDone) return;
            float w = Screen.width, h = Screen.height;
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(h * 0.03f), alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, 12, w, h * 0.06f), _hud, style);

            var bstyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(h * 0.028f) };
            float bw = w * 0.26f, bh = h * 0.09f, y = h - bh - 20f;
            if (GUI.Button(new Rect(20, y, bw, bh), "RECENTER", bstyle)) Recenter();
            if (GUI.Button(new Rect(30 + bw, y, bw, bh), "PIN BOARD", bstyle) && _anchors != null)
                _ = _anchors.PinAsync(_board.Root, "dejarik-board");
            if (GUI.Button(new Rect(40 + 2 * bw, y, bw, bh), "NEW GAME", bstyle)) _ = NewGame();
        }
    }
}
