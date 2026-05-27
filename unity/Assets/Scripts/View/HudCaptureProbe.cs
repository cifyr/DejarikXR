using System.Collections;
using System.IO;
using UnityEngine;

namespace Dejarik.View
{
    // Editor-only helper: waits for the game to set up, then screenshots the full game view (which includes
    // the IMGUI HUD that a Camera.Render-to-texture capture misses) so we can verify the HUD offline.
    public class HudCaptureProbe : MonoBehaviour
    {
        public string OutPath = "/tmp/dejarik_hud.png";
        public static bool Done;

        IEnumerator Start()
        {
            Done = false;
            yield return new WaitForSeconds(7.5f); // allow async creature loads + a selection to populate the HUD
            // File-based capture (schedules its own end-of-frame write; WaitForEndOfFrame doesn't fire in batch).
            ScreenCapture.CaptureScreenshot(OutPath);
            Debug.Log($"[HudCapture] requested {OutPath} ({Screen.width}x{Screen.height})");
            float t0 = Time.realtimeSinceStartup;
            while (!File.Exists(OutPath) && Time.realtimeSinceStartup - t0 < 8f) yield return null;
            yield return new WaitForSeconds(0.5f); // let the file finish writing
            Debug.Log($"[HudCapture] done exists={File.Exists(OutPath)}");
            Done = true;
        }
    }
}
