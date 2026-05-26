using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace XrealAR
{
    // Enumerates .glb scenes dropped onto the device. Default location is the app's external files dir,
    // which adb can write to without root: /sdcard/Android/data/<package>/files/scenes/
    public class SceneCatalog
    {
        public string ScenesDir { get; }

        public SceneCatalog(string scenesDir = null)
        {
            ScenesDir = scenesDir ?? Path.Combine(Application.persistentDataPath, "scenes");
            Debug.Log($"[SceneCatalog] scenesDir={ScenesDir}");
        }

        public IReadOnlyList<string> List()
        {
            try
            {
                if (!Directory.Exists(ScenesDir))
                {
                    Debug.LogWarning($"[SceneCatalog] scenesDir does not exist yet, creating: {ScenesDir}");
                    Directory.CreateDirectory(ScenesDir);
                    return Array.Empty<string>();
                }

                var files = Directory.GetFiles(ScenesDir, "*.glb", SearchOption.TopDirectoryOnly);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                Debug.Log($"[SceneCatalog] found {files.Length} glb file(s) in {ScenesDir}");
                return files;
            }
            catch (Exception e)
            {
                throw new IOException($"failed listing scenes in {ScenesDir}", e);
            }
        }
    }
}
