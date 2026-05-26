"""Generate an animated test .glb (Suzanne, rotating + PBR material) to validate the runtime loader.

Usage:
    blender --background --python blender/make_test_scene.py -- assets/scenes/test_suzanne.glb

Headless: builds the scene from scratch (no .blend needed) and exports with the same settings as
export_glb.py, so it exercises the full Blender -> glTF -> XR path with something recognizable.
"""

import sys
import math
import logging

import bpy

logging.basicConfig(level=logging.INFO, format="%(levelname)s %(message)s")
log = logging.getLogger("make_test_scene")


def _out_path() -> str:
    argv = sys.argv
    if "--" not in argv or not argv[argv.index("--") + 1:]:
        raise SystemExit("expected output .glb path after '--'")
    return argv[argv.index("--") + 1:][0]


def main() -> None:
    out = _out_path()

    bpy.ops.wm.read_factory_settings(use_empty=True)

    bpy.ops.mesh.primitive_monkey_add(size=0.3, location=(0.0, 0.0, 0.0))
    suzanne = bpy.context.active_object
    suzanne.name = "Suzanne"
    bpy.ops.object.shade_smooth()

    mat = bpy.data.materials.new("SuzanneMat")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (0.9, 0.45, 0.1, 1.0)
    bsdf.inputs["Metallic"].default_value = 0.2
    bsdf.inputs["Roughness"].default_value = 0.4
    suzanne.data.materials.append(mat)

    # spin around Z over 120 frames
    suzanne.rotation_euler = (0.0, 0.0, 0.0)
    suzanne.keyframe_insert("rotation_euler", frame=1)
    suzanne.rotation_euler = (0.0, 0.0, math.radians(360.0))
    suzanne.keyframe_insert("rotation_euler", frame=120)

    log.info("exporting test scene -> %s", out)
    try:
        bpy.ops.export_scene.gltf(
            filepath=out,
            export_format="GLB",
            export_apply=True,
            export_yup=True,
            export_materials="EXPORT",
            export_animations=True,
            export_force_sampling=True,
            export_animation_mode="ACTIONS",
            export_cameras=False,
            export_lights=False,
        )
    except Exception as e:
        log.exception("export failed")
        raise SystemExit(f"export failed: {e}") from e
    log.info("done: %s", out)


if __name__ == "__main__":
    main()
