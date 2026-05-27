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
        const float PinchOn = 0.025f;   // tips closer than this = pinching
        const float PinchOff = 0.04f;   // hysteresis to release

        XRHandSubsystem _subsys;
        Transform _origin;
        bool _pinching;
        static readonly List<XRHandSubsystem> s_subsystems = new List<XRHandSubsystem>();

        void Awake()
        {
            var xrOrigin = FindFirstObjectByType<XROrigin>();
            _origin = xrOrigin != null ? xrOrigin.transform : null;
        }

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
            if (_subsys == null) return false;

            foreach (var hand in new[] { _subsys.rightHand, _subsys.leftHand })
            {
                if (!hand.isTracked) continue;
                var indexJoint = hand.GetJoint(XRHandJointID.IndexTip);
                var thumbJoint = hand.GetJoint(XRHandJointID.ThumbTip);
                if (!indexJoint.TryGetPose(out var iPose) || !thumbJoint.TryGetPose(out var tPose)) continue;

                Vector3 iTip = ToWorld(iPose.position);
                Vector3 tTip = ToWorld(tPose.position);
                worldTip = iTip;

                float d = Vector3.Distance(iTip, tTip);
                if (!_pinching && d < PinchOn) { _pinching = true; pinchDown = true; }
                else if (_pinching && d > PinchOff) { _pinching = false; }
                return true;
            }
            return false;
        }

        Vector3 ToWorld(Vector3 trackingSpacePos) =>
            _origin != null ? _origin.TransformPoint(trackingSpacePos) : trackingSpacePos;
    }
}
