using UnityEngine;
using UnityEngine.InputSystem;

namespace Dejarik.View
{
    // Head-gaze pointer + confirm, the most reliable XREAL input (hand tracking is flaky on this hardware).
    // The ray is the camera's forward; confirm is a tap on the Beam Pro touchscreen (any tap) or a keyboard
    // Space in the editor. A small holo reticle marks the current aim point.
    public class GazeSelector : MonoBehaviour
    {
        Camera _cam;
        Transform _reticle;
        Material _reticleMat;

        public Ray CurrentRay => new Ray(_cam.transform.position, _cam.transform.forward);

        public bool ConfirmDown
        {
            get
            {
                var ts = Touchscreen.current;
                if (ts != null)
                    foreach (var t in ts.touches)
                        if (t.press.wasPressedThisFrame) return true;
                var kb = Keyboard.current;
                return kb != null && kb.spaceKey.wasPressedThisFrame;
            }
        }

        void Awake()
        {
            _cam = Camera.main;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "GazeReticle";
            Destroy(go.GetComponent<Collider>());
            _reticleMat = new Material(Shader.Find("Standard"));
            _reticleMat.color = HoloMaterials.P0;
            _reticleMat.EnableKeyword("_EMISSION");
            _reticleMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            _reticleMat.SetColor("_EmissionColor", HoloMaterials.P0 * 1.4f);
            go.GetComponent<MeshRenderer>().material = _reticleMat;
            go.transform.localScale = Vector3.one * 0.02f;
            _reticle = go.transform;
        }

        // Orchestrator calls this with the board hit point (or null to park the reticle at a default depth).
        public void SetReticle(Vector3? worldPoint)
        {
            if (_cam == null) return;
            Vector3 p = worldPoint ?? (_cam.transform.position + _cam.transform.forward * 0.8f);
            _reticle.position = p;
            _reticle.rotation = Quaternion.LookRotation(p - _cam.transform.position);
            _reticleMat.color = worldPoint.HasValue ? HoloMaterials.P0 : new Color(1, 1, 1, 0.5f);
        }
    }
}
