"""Headless Blender -> glTF (.glb) exporter for the XrealAR pipeline.

Usage:
    blender scene.blend --background --python export_glb.py -- out/scene.glb

Encodes the pipeline rules so every scene exports the same way: binary GLB, modifiers
applied, animations baked/sampled (keyframed transforms + armature actions), PBR materials
and image textures embedded, Y-up for glTF. Procedural node materials are NOT baked here --
bake them to image textures in Blender first (see docs/BLENDER_PIPELINE.md).
"""

import sys
import logging

import bpy

logging.basicConfig(level=logging.INFO, format="%(levelname)s %(message)s")
log = logging.getLogger("export_glb")


def _output_path() -> str:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("expected output path after '--', e.g. ... --python export_glb.py -- out/scene.glb")
    after = argv[argv.index("--") + 1:]
    if not after:
        raise SystemExit("missing output .glb path after '--'")
    return after[0]


def main() -> None:
    out = _output_path()
    log.info("exporting blend=%s -> %s", bpy.data.filepath or "<unsaved>", out)
    try:
        bpy.ops.export_scene.gltf(
            filepath=out,
            export_format="GLB",
            export_apply=True,            # apply modifiers; matches "Apply Transforms" expectation
            export_yup=True,              # glTF Y-up; glTFast converts to Unity space
            export_materials="EXPORT",    # Principled BSDF -> glTF PBR metallic-roughness
            export_image_format="AUTO",
            export_animations=True,
            export_force_sampling=True,   # "Always Sample Animations" -- reliable armature + keyframe export
            export_animation_mode="ACTIONS",
            export_skins=True,            # armature / skeletal
            export_morph=True,
            export_cameras=False,         # Blender cameras/lights/world do not travel; author lighting in Unity
            export_lights=False,
        )
    except Exception as e:
        log.exception("glTF export failed for %s", out)
        raise SystemExit(f"export failed: {e}") from e
    log.info("export complete: %s", out)


if __name__ == "__main__":
    main()
