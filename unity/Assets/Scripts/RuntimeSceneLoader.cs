using System;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

namespace XrealAR
{
    // Loads a Blender-exported .glb from device storage at runtime and plays its baked animations.
    // glTFast's GameObjectInstantiator defaults to AnimationMethod.Legacy, so animation clips land on
    // a UnityEngine.Animation component exposed as SceneInstance.LegacyAnimation.
    public class RuntimeSceneLoader : MonoBehaviour
    {
        public async Task<GameObject> LoadAsync(string glbPath, Transform parent)
        {
            Debug.Log($"[RuntimeSceneLoader] begin load glbPath={glbPath} parent={(parent ? parent.name : "<null>")}");

            if (!File.Exists(glbPath))
                throw new FileNotFoundException($"glb not found at {glbPath}", glbPath);

            byte[] data;
            try
            {
                data = await File.ReadAllBytesAsync(glbPath);
            }
            catch (Exception e)
            {
                throw new IOException($"failed reading glb bytes glbPath={glbPath}", e);
            }
            Debug.Log($"[RuntimeSceneLoader] read {data.Length} bytes from {glbPath}, parsing");

            var gltf = new GltfImport();
            bool parsed = await gltf.LoadGltfBinary(data, new Uri(new Uri("file://"), glbPath));
            if (!parsed)
                throw new InvalidDataException($"glTFast failed to parse glb glbPath={glbPath}");

            var holder = new GameObject(Path.GetFileNameWithoutExtension(glbPath));
            holder.transform.SetParent(parent, false);

            var instantiator = new GameObjectInstantiator(gltf, holder.transform);
            bool instantiated = await gltf.InstantiateMainSceneAsync(instantiator);
            if (!instantiated)
            {
                Destroy(holder);
                throw new InvalidOperationException($"glTFast failed to instantiate scene glbPath={glbPath}");
            }

            var legacy = instantiator.SceneInstance?.LegacyAnimation;
            if (legacy != null && legacy.clip != null)
            {
                Debug.Log($"[RuntimeSceneLoader] playing (looping) animation clip={legacy.clip.name}");
                legacy.wrapMode = WrapMode.Loop;
                foreach (AnimationState st in legacy) st.wrapMode = WrapMode.Loop;
                legacy.Play();
            }
            else
            {
                Debug.Log("[RuntimeSceneLoader] no legacy animation found in glb (static scene)");
            }

            Debug.Log($"[RuntimeSceneLoader] loaded '{holder.name}' under '{(parent ? parent.name : "<null>")}'");
            return holder;
        }
    }
}
