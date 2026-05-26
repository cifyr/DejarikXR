using System;
using System.Collections;
using System.IO;
using GLTFast;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Exercises the exact runtime glTFast path the game uses (parse -> instantiate -> legacy Animation +
// clips), in editor PlayMode, for every creature glb. De-risks on the Mac the one thing that otherwise
// needs the glasses: that these specific models load and expose animation clips at runtime.
public class LoaderSmokeTest
{
    static readonly string[] Creatures =
        { "savrip", "monnok", "ghhhk", "houjix", "strider", "ngok", "klorslug", "molator" };

    [UnityTest]
    public IEnumerator EveryCreatureLoadsWithClips()
    {
        foreach (var name in Creatures)
        {
            string path = Path.Combine(Application.streamingAssetsPath, $"Models/Creatures/{name}.glb");
            Assert.IsTrue(File.Exists(path), $"missing {path}");
            byte[] data = File.ReadAllBytes(path);

            var gltf = new GltfImport();
            var load = gltf.LoadGltfBinary(data, new Uri("file://creature"));
            yield return new WaitUntil(() => load.IsCompleted);
            Assert.IsTrue(load.Result, $"{name}: parse failed");

            var go = new GameObject($"test_{name}");
            var inst = new GameObjectInstantiator(gltf, go.transform);
            var make = gltf.InstantiateMainSceneAsync(inst);
            yield return new WaitUntil(() => make.IsCompleted);
            Assert.IsTrue(make.Result, $"{name}: instantiate failed");

            Assert.IsNotNull(go.GetComponentInChildren<SkinnedMeshRenderer>(), $"{name}: no skinned mesh");

            var anim = inst.SceneInstance?.LegacyAnimation;
            Assert.IsNotNull(anim, $"{name}: no legacy Animation component");
            int clips = 0;
            bool hasIdle = false;
            foreach (AnimationState s in anim) { clips++; if (s.name.ToLower().Contains("idle")) hasIdle = true; }
            Assert.Greater(clips, 0, $"{name}: no animation clips");
            Assert.IsTrue(hasIdle, $"{name}: no idle clip (found {clips} clips)");

            UnityEngine.Object.Destroy(go);
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator BoardGlbLoads()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "Models/board.glb");
        Assert.IsTrue(File.Exists(path), $"missing {path}");
        var gltf = new GltfImport();
        var load = gltf.LoadGltfBinary(File.ReadAllBytes(path), new Uri("file://board"));
        yield return new WaitUntil(() => load.IsCompleted);
        Assert.IsTrue(load.Result, "board parse failed");
        var go = new GameObject("test_board");
        var make = gltf.InstantiateMainSceneAsync(new GameObjectInstantiator(gltf, go.transform));
        yield return new WaitUntil(() => make.IsCompleted);
        Assert.IsTrue(make.Result, "board instantiate failed");
        Assert.Greater(go.GetComponentsInChildren<Renderer>().Length, 0, "board has no renderers");
        UnityEngine.Object.Destroy(go);
    }
}
