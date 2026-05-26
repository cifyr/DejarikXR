using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace XrealAR
{
    // Persists which .glb was placed at which saved anchor guid, so a relaunch can reload the same
    // scene at the same physical spot. The anchor pose itself is restored by AR Foundation; this only
    // records the guid -> glb-filename association alongside it.
    [Serializable]
    public class ScenePlacement
    {
        public string anchorGuid;
        public string glbFileName;
    }

    [Serializable]
    class PlacementList
    {
        public List<ScenePlacement> items = new();
    }

    public class ScenePlacementStore
    {
        readonly string _path;

        public ScenePlacementStore(string path = null)
        {
            _path = path ?? Path.Combine(Application.persistentDataPath, "placements.json");
            Debug.Log($"[PlacementStore] path={_path}");
        }

        public List<ScenePlacement> Load()
        {
            try
            {
                if (!File.Exists(_path))
                {
                    Debug.Log("[PlacementStore] no placements file yet, returning empty");
                    return new List<ScenePlacement>();
                }
                var json = File.ReadAllText(_path);
                var parsed = JsonUtility.FromJson<PlacementList>(json);
                Debug.Log($"[PlacementStore] loaded {parsed?.items?.Count ?? 0} placement(s)");
                return parsed?.items ?? new List<ScenePlacement>();
            }
            catch (Exception e)
            {
                throw new IOException($"failed loading placements from {_path}", e);
            }
        }

        public void Save(List<ScenePlacement> items)
        {
            try
            {
                var json = JsonUtility.ToJson(new PlacementList { items = items }, prettyPrint: true);
                File.WriteAllText(_path, json);
                Debug.Log($"[PlacementStore] saved {items.Count} placement(s) to {_path}");
            }
            catch (Exception e)
            {
                throw new IOException($"failed saving placements to {_path}", e);
            }
        }
    }
}
