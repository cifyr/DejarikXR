using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace XrealAR
{
    // Persists each scene's placement (position + rotation + scale) to a JSON file, one entry per glb name,
    // so cycling away and back — or relaunching — restores every scene independently. Coordinates are
    // world-space relative to the session-start tracking origin (no spatial anchor yet — see TODO).
    [Serializable]
    class PlacementData
    {
        public string glb;
        public Vector3 pos;
        public Quaternion rot;
        public float scale = 1f;
        public string anchorGuid;   // XREAL spatial-anchor persistent guid, if pinned to the room
    }

    [Serializable]
    class PlacementBook
    {
        public List<PlacementData> entries = new();
    }

    static class PlacementStore
    {
        static string FilePath => Path.Combine(Application.persistentDataPath, "placement.json");

        static PlacementBook Read()
        {
            try
            {
                if (!File.Exists(FilePath)) return new PlacementBook();
                string json = File.ReadAllText(FilePath);
                // Migrate the old single-entry format ({glb,pos,rot,scale}) to the keyed book.
                if (json.Contains("\"entries\""))
                    return JsonUtility.FromJson<PlacementBook>(json) ?? new PlacementBook();
                var legacy = JsonUtility.FromJson<PlacementData>(json);
                var book = new PlacementBook();
                if (legacy != null && !string.IsNullOrEmpty(legacy.glb)) book.entries.Add(legacy);
                return book;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Placement] read failed: {e}");
                return new PlacementBook();
            }
        }

        public static void Save(string glb, Vector3 pos, Quaternion rot, float scale)
        {
            try
            {
                var book = Read();
                var data = book.entries.Find(e => e.glb == glb);
                if (data == null) { data = new PlacementData { glb = glb }; book.entries.Add(data); }
                data.pos = pos; data.rot = rot; data.scale = scale;
                File.WriteAllText(FilePath, JsonUtility.ToJson(book, true));
                Debug.Log($"[Placement] saved {glb} pos={pos.ToString("F2")} rot={rot.eulerAngles.ToString("F0")} scale={scale:F2}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Placement] save failed for {glb}: {e}");
            }
        }

        public static string GetAnchorGuid(string glb)
        {
            var data = Read().entries.Find(e => e.glb == glb);
            return string.IsNullOrEmpty(data?.anchorGuid) ? null : data.anchorGuid;
        }

        public static void SetAnchorGuid(string glb, string anchorGuid)
        {
            try
            {
                var book = Read();
                var data = book.entries.Find(e => e.glb == glb);
                if (data == null) { data = new PlacementData { glb = glb }; book.entries.Add(data); }
                data.anchorGuid = anchorGuid;
                File.WriteAllText(FilePath, JsonUtility.ToJson(book, true));
                Debug.Log($"[Placement] {(string.IsNullOrEmpty(anchorGuid) ? "cleared" : "set")} anchor for {glb} guid={anchorGuid}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Placement] set anchor failed for {glb}: {e}");
            }
        }

        public static bool TryLoad(string glb, out Vector3 pos, out Quaternion rot, out float scale)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            scale = 1f;
            try
            {
                var data = Read().entries.Find(e => e.glb == glb);
                if (data == null) return false;
                pos = data.pos;
                rot = data.rot;
                scale = data.scale <= 0f ? 1f : data.scale;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Placement] load failed for {glb}: {e}");
                return false;
            }
        }
    }
}
