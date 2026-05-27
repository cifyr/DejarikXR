using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace XrealAR.EditorTools
{
    // Offline visual iteration: opens Main.unity, enters play mode headlessly (domain reload disabled so
    // this static state survives), waits for the async glb loads, renders a framed camera to a PNG, and
    // quits. Lets us see the AR board/pieces on the Mac without the glasses.
    //   Unity -batchmode -projectPath unity -executeMethod XrealAR.EditorTools.SceneCapture.Run -logFile ...
    // (no -nographics: rendering is required; no -quit: this exits itself)
    public static class SceneCapture
    {
        const string OutPath = "/tmp/dejarik_capture.png";
        const double WaitSeconds = 7.0;   // allow async creature/board loads to finish
        const double TimeoutSeconds = 60.0;
        static double _enteredAt = -1.0;
        static double _startedAt;

        public static void Run()
        {
            _startedAt = EditorApplication.timeSinceStartup;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            Dejarik.View.DejarikGame.DebugAutoSelect = true; // preview selection highlights in the capture
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - _startedAt > TimeoutSeconds) { Finish(false); return; }
            if (!EditorApplication.isPlaying) return;
            if (_enteredAt < 0) _enteredAt = now;
            if (now - _enteredAt < WaitSeconds) return;
            Capture();
            Finish(true);
        }

        static void Capture()
        {
            // Frame the board (DejarikGame places it ~ (0, 0.6, 0.8) relative to the origin camera).
            Vector3 target = new Vector3(0f, 0.6f, 0.8f);
            var go = new GameObject("CaptureCam");
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.10f, 0.13f, 1f); // dark gray, not pure black, to spot a black plane
            cam.nearClipPlane = 0.01f;
            cam.fieldOfView = 45f;
            go.transform.position = target + new Vector3(0.0f, 0.62f, -0.62f); // 3/4 view framing whole board + pieces
            go.transform.LookAt(target);

            int w = 1280, h = 960;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(OutPath, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;

            int pieces = Object.FindObjectsByType<Dejarik.View.PieceView>(FindObjectsSortMode.None).Length;
            Debug.Log($"[Capture] wrote {OutPath}; PieceViews in scene = {pieces}");
        }

        static void Finish(bool ok)
        {
            EditorApplication.update -= Tick;
            Debug.Log($"[Capture] finishing ok={ok}");
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 2);
        }
    }
}
