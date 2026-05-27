using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Dejarik.View
{
    // Hand-tracking pointer: reports the index-fingertip world position and a pinch "tap" (index tip
    // touching thumb tip). Lets the player poke/pinch pieces and cells directly. Falls back gracefully
    // (TryGetTip returns false) when no hand is tracked, so the gaze pointer can take over.
    public class HandSelector : MonoBehaviour
    {
        // Tuned to on-device data: a full pinch only closes index-thumb tips to ~0.04 m on this tracker.
        const float PinchOn = 0.05f;    // tips closer than this = pinching
        const float PinchOff = 0.065f;  // hysteresis to release

        XRHandSubsystem _subsys;
        Transform _trackingSpace;   // XR Origin's CameraFloorOffsetObject — the space hand joints live in
        bool _pinching;
        static readonly List<XRHandSubsystem> s_subsystems = new List<XRHandSubsystem>();

        // Diagnostics surfaced to the HUD / log so we can see why pinch may not register.
        public string Status { get; private set; } = "init";
        float _nextLog;

        void EnsureSubsystem()
        {
            if (_subsys != null && _subsys.running) return;
            SubsystemManager.GetSubsystems(s_subsystems);
            _subsys = s_subsystems.Count > 0 ? s_subsystems[0] : null;
        }

        // worldTip = index fingertip in world space; pinchDown = true on the frame a pinch begins.
        public bool TryGetTip(out Vector3 worldTip, out bool pinchDown)
        {
            worldTip = default;
            pinchDown = false;
            EnsureSubsystem();
            if (_subsys == null) { Status = "no hand subsystem"; Log(); return false; }

            foreach (var hand in new[] { _subsys.rightHand, _subsys.leftHand })
            {
                if (!hand.isTracked) continue;
                var indexJoint = hand.GetJoint(XRHandJointID.IndexTip);
                var thumbJoint = hand.GetJoint(XRHandJointID.ThumbTip);
                if (!indexJoint.TryGetPose(out var iPose) || !thumbJoint.TryGetPose(out var tPose))
                { Status = "tracked, no joint pose"; Log(); continue; }

                Vector3 iTip = ToWorld(iPose.position);
                Vector3 tTip = ToWorld(tPose.position);
                worldTip = iTip;

                float d = Vector3.Distance(iTip, tTip);
                if (!_pinching && d < PinchOn) { _pinching = true; pinchDown = true; }
                else if (_pinching && d > PinchOff) { _pinching = false; }
                Status = $"tracked tip={iTip:F2} pinchDist={d:F3}{(pinchDown ? " PINCH" : "")}";
                Log();
                return true;
            }
            Status = "subsystem up, no hand tracked";
            Log();
            return false;
        }

        void Log()
        {
            if (Time.time < _nextLog) return;
            _nextLog = Time.time + 1f;
            Debug.Log($"[Hand] {Status}");
        }

        // Joint poses are in the XR Origin's tracking space (the CameraFloorOffsetObject), which is where
        // the hand-visual prefabs are parented — so convert through that transform to match exactly where
        // the rendered hand appears. (Using the moving camera double-applies the head pose; using the XR
        // Origin root drops the camera Y offset.)
        Vector3 ToWorld(Vector3 trackingSpacePos)
        {
            if (_trackingSpace == null)
            {
                var o = FindFirstObjectByType<XROrigin>();
                if (o != null) _trackingSpace = o.CameraFloorOffsetObject != null ? o.CameraFloorOffsetObject.transform : o.transform;
            }
            return _trackingSpace != null ? _trackingSpace.TransformPoint(trackingSpacePos) : trackingSpacePos;
        }
    }
}
