#if DEJARIK_ANDROID_XR
using System;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Dejarik.View
{
    // Galaxy XR (Android XR) replacement for the Beam Pro phone control deck. There is no companion
    // phone on a standalone headset, so the deck moves into world space, attached to the player's left
    // wrist. Turn your left palm up and a two-button glowing panel appears on the back of the wrist;
    // poke a button with your right index fingertip to fire it. The two phone-deck actions map cleanly:
    //   RECENTER  -> button press
    //   NEW GAME  -> button press
    //   MOVE      -> right-hand pinch on the board, drag (replaces phone hold-and-tilt)
    //
    // VERIFY: untested on Galaxy XR hardware. Wrist joint pose conventions (axis orientation, whether
    // palm-up detection via wrist.up or wrist.forward) may differ between OpenXR runtimes — adjust
    // PalmDot / panel offsets on-device. The board-drag uses the same HandSelector pinch as cell
    // selection; a small grace window keeps a poke-then-drag from misfiring on the same frame.
    public class WorldDeck : MonoBehaviour
    {
        public event Action OnRecenter;
        public event Action OnNewGame;

        // Panel only renders when the left palm faces the head (i.e., you're looking at the inside of
        // your wrist). Threshold is dot(palmNormal, headForward) — palmNormal pointing back at the head
        // means you're looking at it.
        const float PalmFaceDotThreshold = 0.35f;
        const float ButtonPokeDist = 0.04f;       // fingertip needs to push this deep into a button
        const float ButtonCooldownSec = 0.6f;     // debounce so a single poke doesn't fire twice

        XRHandSubsystem _subsys;
        Transform _trackingSpace;
        Transform _root;          // worldspace deck root, parented under the offset, repositioned each frame
        Transform _panel;         // visible panel quad
        ButtonView _recenterBtn, _newGameBtn;

        // Board drag state. Captured at pinch-down on the board collider; nulled at pinch-up.
        Transform _boardRoot;
        Bounds _boardBounds;
        bool _dragging;
        Vector3 _dragOffset;      // boardRoot.position - tip at pinch start
        bool _pinchPrev;
        float _btnCooldownUntil;

        static readonly List<XRHandSubsystem> s_subsystems = new List<XRHandSubsystem>();

        public void AttachToBoard(Transform boardRoot, float boardRadius)
        {
            _boardRoot = boardRoot;
            // Bounds are recomputed each frame from boardRoot.position so dragging follows correctly.
            _boardBounds = new Bounds(boardRoot.position, Vector3.one * boardRadius * 2f);
        }

        void Awake()
        {
            BuildPanel();
        }

        void BuildPanel()
        {
            _root = new GameObject("WorldDeck").transform;
            _root.SetParent(transform, false);

            // Panel quad: a translucent backing rectangle ~10cm x 6cm sitting just above the wrist.
            _panel = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
            _panel.name = "Panel";
            _panel.SetParent(_root, false);
            _panel.localScale = new Vector3(0.10f, 0.06f, 1f);
            _panel.localPosition = new Vector3(0f, 0.02f, 0f);
            _panel.localRotation = Quaternion.Euler(90f, 0f, 0f);   // flat on the wrist, label-up
            var pr = _panel.GetComponent<Renderer>();
            pr.sharedMaterial = MakeUnlit(new Color(HoloGui.Cyan.r, HoloGui.Cyan.g, HoloGui.Cyan.b, 0.20f));
            DestroyImmediate(_panel.GetComponent<Collider>());

            _recenterBtn = ButtonView.Make("RECENTER", new Vector3(-0.025f, 0.025f, 0f), _root);
            _newGameBtn = ButtonView.Make("NEW GAME", new Vector3(+0.025f, 0.025f, 0f), _root);

            _root.gameObject.SetActive(false);
        }

        void Update()
        {
            EnsureSubsystem();
            if (_subsys == null) { _root.gameObject.SetActive(false); return; }

            // Position the deck on the left wrist; show only when palm faces the head.
            var left = _subsys.leftHand;
            bool show = false;
            if (left.isTracked && left.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wristPose))
            {
                Vector3 wristWorldPos = ToWorld(wristPose.position);
                Quaternion wristWorldRot = ToWorldRot(wristPose.rotation);
                _root.SetPositionAndRotation(wristWorldPos, wristWorldRot);

                Vector3 palmNormal = wristWorldRot * Vector3.up;     // VERIFY axis: wrist.up == back of hand on most OpenXR runtimes
                Vector3 headFwd = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
                float dot = Vector3.Dot(palmNormal, -headFwd);
                show = dot > PalmFaceDotThreshold;
            }
            _root.gameObject.SetActive(show);
            if (!show) { _pinchPrev = false; _dragging = false; return; }

            // Right-hand fingertip drives button hits + board drag.
            Vector3 tip = default;
            bool tipOk = false, pinchNow = false;
            var right = _subsys.rightHand;
            if (right.isTracked
                && right.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var iPose)
                && right.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var tPose))
            {
                tip = ToWorld(iPose.position);
                Vector3 thumb = ToWorld(tPose.position);
                pinchNow = Vector3.Distance(tip, thumb) < 0.05f;
                tipOk = true;
            }

            // Buttons: hit only when fingertip pushes through the panel face. Cooldown prevents bounce.
            if (tipOk && Time.time >= _btnCooldownUntil)
            {
                if (HitButton(_recenterBtn, tip)) { Debug.Log("[WorldDeck] RECENTER"); OnRecenter?.Invoke(); _btnCooldownUntil = Time.time + ButtonCooldownSec; }
                else if (HitButton(_newGameBtn, tip)) { Debug.Log("[WorldDeck] NEW GAME"); OnNewGame?.Invoke(); _btnCooldownUntil = Time.time + ButtonCooldownSec; }
            }

            // Board drag: pinch the air over the board (right hand) and move your hand to translate it.
            if (_boardRoot != null && tipOk)
            {
                bool pinchEdge = pinchNow && !_pinchPrev;
                _boardBounds.center = _boardRoot.position;
                if (pinchEdge && _boardBounds.Contains(tip))
                {
                    _dragging = true;
                    _dragOffset = _boardRoot.position - tip;
                    Debug.Log($"[WorldDeck] drag start, offset={_dragOffset:F3}");
                }
                if (_dragging && pinchNow)
                {
                    _boardRoot.position = tip + _dragOffset;
                }
                if (_dragging && !pinchNow)
                {
                    _dragging = false;
                    Debug.Log("[WorldDeck] drag end");
                }
            }
            _pinchPrev = pinchNow;
        }

        bool HitButton(ButtonView b, Vector3 tip)
        {
            if (b == null) return false;
            Vector3 localTip = b.Root.InverseTransformPoint(tip);
            // Quad is +X/+Y in local; press depth along +Z.
            return Mathf.Abs(localTip.x) <= b.HalfWidth
                && Mathf.Abs(localTip.y) <= b.HalfHeight
                && localTip.z < 0f && localTip.z > -ButtonPokeDist;
        }

        void EnsureSubsystem()
        {
            if (_subsys != null && _subsys.running) return;
            SubsystemManager.GetSubsystems(s_subsystems);
            _subsys = s_subsystems.Count > 0 ? s_subsystems[0] : null;
        }

        Vector3 ToWorld(Vector3 p)
        {
            EnsureTrackingSpace();
            return _trackingSpace != null ? _trackingSpace.TransformPoint(p) : p;
        }

        Quaternion ToWorldRot(Quaternion r)
        {
            EnsureTrackingSpace();
            return _trackingSpace != null ? _trackingSpace.rotation * r : r;
        }

        void EnsureTrackingSpace()
        {
            if (_trackingSpace != null) return;
            var o = FindFirstObjectByType<XROrigin>();
            if (o != null) _trackingSpace = o.CameraFloorOffsetObject != null ? o.CameraFloorOffsetObject.transform : o.transform;
        }

        // Translucent unlit material. Unlit/Color is in the always-included shader list (XrealXRSetup).
        static Material MakeUnlit(Color c)
        {
            var shader = Shader.Find("Unlit/Color");
            var m = new Material(shader);
            m.color = c;
            m.renderQueue = 3000; // transparent
            return m;
        }

        // Worldspace button: a glowing colored quad with an optional label. Hit-tested by WorldDeck.
        class ButtonView
        {
            public Transform Root;
            public float HalfWidth;
            public float HalfHeight;

            public static ButtonView Make(string label, Vector3 localPos, Transform parent)
            {
                var bv = new ButtonView();
                bv.Root = new GameObject($"Btn_{label}").transform;
                bv.Root.SetParent(parent, false);
                bv.Root.localPosition = localPos;
                bv.Root.localRotation = Quaternion.Euler(90f, 0f, 0f);   // face up off the wrist

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.transform.SetParent(bv.Root, false);
                quad.transform.localScale = new Vector3(0.04f, 0.018f, 1f);
                bv.HalfWidth = 0.02f;
                bv.HalfHeight = 0.009f;
                var qr = quad.GetComponent<Renderer>();
                qr.sharedMaterial = MakeUnlit(new Color(HoloGui.Cyan.r, HoloGui.Cyan.g, HoloGui.Cyan.b, 0.55f));
                UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());

                // Label: a TextMesh child. TMP would be nicer but TextMesh has no extra dependencies and
                // renders fine at this size for the small label set we need.
                var lblGO = new GameObject("Label");
                lblGO.transform.SetParent(bv.Root, false);
                lblGO.transform.localPosition = new Vector3(0f, 0f, -0.0005f);
                var tm = lblGO.AddComponent<TextMesh>();
                tm.text = label;
                tm.characterSize = 0.0024f;
                tm.fontSize = 64;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = HoloGui.Cyan;

                return bv;
            }
        }
    }
}
#endif
