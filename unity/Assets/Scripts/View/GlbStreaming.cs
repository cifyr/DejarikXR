using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Dejarik.View
{
    // Reads a file bundled under StreamingAssets, cross-platform. On Android StreamingAssets lives inside
    // the APK (a "jar:file://...!/assets/..." URL) and must be read via UnityWebRequest; on the editor/
    // desktop it's a plain file path.
    public static class GlbStreaming
    {
        public static async Task<byte[]> ReadBytes(string relativePath)
        {
            string full = Path.Combine(Application.streamingAssetsPath, relativePath);
            if (full.Contains("://"))
            {
                using var req = UnityWebRequest.Get(full);
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();
                if (req.result != UnityWebRequest.Result.Success)
                    throw new IOException($"failed reading StreamingAssets '{relativePath}': {req.error}");
                return req.downloadHandler.data;
            }
            if (!File.Exists(full))
                throw new FileNotFoundException($"StreamingAssets file missing: {full}", full);
            return await File.ReadAllBytesAsync(full);
        }
    }
}
