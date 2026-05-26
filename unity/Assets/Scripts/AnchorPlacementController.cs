using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace XrealAR
{
    // World-locks a placed scene with an AR Foundation 6 spatial anchor and persists it across sessions.
    // VERIFY against the bundled XREAL "AR Features/Anchors" sample: the XREAL provider also exposes a
    // MapQualityIndicator and ARAnchorManager.TryRemap(trackableId); observe 5-15s before saving and keep
    // anchored content within ~3 m or tracking drift becomes visible.
    public class AnchorPlacementController : MonoBehaviour
    {
        [SerializeField] ARAnchorManager anchorManager;

        // The ARAnchorManager lives on the XR Origin (AF requirement), not necessarily on this object,
        // so resolve it from the scene when not wired explicitly.
        void Awake()
        {
            if (anchorManager == null)
                anchorManager = FindFirstObjectByType<ARAnchorManager>();
            if (anchorManager == null)
                Debug.LogError("[Anchor] no ARAnchorManager in scene; add one to the XR Origin");
        }

        public async Task<ARAnchor> CreateAnchorAsync(Pose pose)
        {
            Debug.Log($"[Anchor] CreateAnchorAsync pos={pose.position} rot={pose.rotation.eulerAngles}");
            var result = await anchorManager.TryAddAnchorAsync(pose);
            if (!result.status.IsSuccess())
                throw new InvalidOperationException($"TryAddAnchorAsync failed status={result.status}");
            Debug.Log($"[Anchor] created trackableId={result.value.trackableId}");
            return result.value;
        }

        public async Task<SerializableGuid> SaveAsync(ARAnchor anchor)
        {
            Debug.Log($"[Anchor] SaveAsync trackableId={anchor.trackableId}");
            var result = await anchorManager.TrySaveAnchorAsync(anchor);
            if (!result.status.IsSuccess())
                throw new InvalidOperationException($"TrySaveAnchorAsync failed status={result.status} trackableId={anchor.trackableId}");
            Debug.Log($"[Anchor] saved guid={result.value}");
            return result.value;
        }

        public async Task<ARAnchor> LoadAsync(SerializableGuid guid)
        {
            Debug.Log($"[Anchor] LoadAsync guid={guid}");
            var result = await anchorManager.TryLoadAnchorAsync(guid);
            if (!result.status.IsSuccess())
                throw new InvalidOperationException($"TryLoadAnchorAsync failed status={result.status} guid={guid}");
            Debug.Log($"[Anchor] loaded trackableId={result.value.trackableId} from guid={guid}");
            return result.value;
        }

        public async Task EraseAsync(SerializableGuid guid)
        {
            Debug.Log($"[Anchor] EraseAsync guid={guid}");
            var status = await anchorManager.TryEraseAnchorAsync(guid);
            if (!status.IsSuccess())
                throw new InvalidOperationException($"TryEraseAnchorAsync failed status={status} guid={guid}");
        }

        // Pin the object to the physical room: erase any prior anchor for this glb, create + save a new
        // anchor at the object's current pose, reparent the object under it (so it tracks relocalization),
        // and persist the guid + world pose as fallback. On failure the object stays where it is and the
        // world-pose JSON still works, so this never breaks the non-anchored path.
        public async Task PinAsync(Transform root, string glbName)
        {
            if (anchorManager == null) { Debug.LogError("[Anchor] no ARAnchorManager; pin skipped"); return; }
            if (root == null || string.IsNullOrEmpty(glbName)) return;
            try
            {
                var prev = PlacementStore.GetAnchorGuid(glbName);
                if (!string.IsNullOrEmpty(prev) && Guid.TryParse(prev, out var pg))
                {
                    try { await EraseAsync(new SerializableGuid(pg)); }
                    catch (Exception e) { Debug.LogWarning($"[Anchor] erase prior anchor failed (continuing): {e.Message}"); }
                }

                float scale = root.localScale.x;
                var anchor = await CreateAnchorAsync(new Pose(root.position, root.rotation));
                var guid = await SaveAsync(anchor);

                root.SetParent(anchor.transform, worldPositionStays: true);   // track the anchor's drift correction
                PlacementStore.Save(glbName, root.position, root.rotation, scale);
                PlacementStore.SetAnchorGuid(glbName, guid.ToString());
                Debug.Log($"[Anchor] pinned {glbName} guid={guid}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Anchor] pin failed for {glbName} (object left as-is): {e}");
            }
        }

        // Restore a previously pinned object: load its saved anchor and reparent under it. The anchor
        // starts un-localized (far away) and snaps into place once the room is recognized; until then the
        // caller's world-pose values are the initial guess. No-op (keeps world pose) if no saved anchor.
        public async Task RestoreAsync(Transform root, string glbName)
        {
            var guidStr = PlacementStore.GetAnchorGuid(glbName);
            if (string.IsNullOrEmpty(guidStr) || !Guid.TryParse(guidStr, out var g)) return;
            if (anchorManager == null) { Debug.LogWarning("[Anchor] no ARAnchorManager; restore skipped"); return; }
            try
            {
                var anchor = await LoadAsync(new SerializableGuid(g));
                root.SetParent(anchor.transform, worldPositionStays: false);
                root.localPosition = Vector3.zero;
                root.localRotation = Quaternion.identity;
                Debug.Log($"[Anchor] restored {glbName} under anchor {guidStr} (relocalizing)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Anchor] restore failed for {glbName} (using world pose): {e}");
            }
        }
    }
}
