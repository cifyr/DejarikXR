# Blender → glTF pipeline rules

The whole reason this app is tractable: your scene content (keyframed transforms, PBR materials,
armatures — no sims/particles) maps cleanly onto **glTF 2.0**. Follow these rules so every scene
exports identically and loads correctly via glTFast on the Air 2 Ultra.

## What travels and what doesn't

| Travels via glTF | Does NOT travel — handle before export |
|---|---|
| Keyframed object transforms (loc/rot/scale) | **Procedural node materials** → bake to image textures first |
| Armature / skeletal animation | Blender **lights, cameras, world** → author lighting in Unity |
| Principled BSDF → PBR metallic-roughness | Particle systems, physics sims, fluids, cloth → bake to mesh sequence (out of scope) |
| Image textures, normal/roughness/metallic maps | Drivers / constraints not baked → enable sampling (the export script does) |
| Shape keys / morphs | Geometry nodes that aren't applied → apply first |

## Authoring checklist (before export)

1. **Apply transforms**: select all → `Ctrl+A` → All Transforms. glTF bakes world transforms; unapplied
   scale/rotation causes surprises in Unity.
2. **Scale = real world**: 1 Blender unit = 1 meter (Unity matches). Size the scene as you want it to
   appear in your room; anchored content should stay within ~3 m of its anchor.
3. **Bake procedural materials**: any Principled input driven by procedural nodes (noise, musgrave,
   gradients) → bake to an image texture, then plug the baked image into the Principled BSDF. Only image
   textures + Principled scalar/color inputs export.
4. **Armatures**: keep the rig parented; the export script forces sampling so bone animation is reliable.
5. **One action per object** is simplest; multiple actions export as separate glTF animations (the player
   plays the first/legacy clip — extend `RuntimeSceneLoader` if you need clip selection).

## Export

Headless (preferred, reproducible):
```
blender scene.blend --background --python blender/export_glb.py -- assets/scenes/scene.glb
```

The script pins: GLB binary, modifiers applied, Y-up, materials EXPORT, animations on with forced
sampling, skins + morphs on, cameras/lights off.

GUI equivalent (File → Export → glTF 2.0): Format **GLB**, Transform **+Y Up**, Geometry **Apply
Modifiers**, Animation **on** + **Always Sample Animations**, **Skinning** on.

## Verify

After export, import the `.glb` into Unity via glTFast and confirm in Game view that materials look
right and the animation plays. Then `scripts/push-scene.sh assets/scenes/scene.glb` to the device.
