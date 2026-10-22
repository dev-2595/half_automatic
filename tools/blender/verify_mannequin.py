"""만들어진 마네킹(.blend, .fbx)이 문서의 요구를 지키는지 검사한다.

검사 항목 (기대값은 빌드 스크립트와 별도로 여기에 적는다)
  1. 객체 구성: 아마추어 1개 + 메시 2개 (이름 규칙)
  2. 뼈 목록, 그리고 루트가 "Armature" 가 아닌 이름인지
  3. 모든 정점이 뼈에 묶여 있고 가중치 합이 1인지
  4. 체격 모프 타깃: 이름, 기본값이 0인지, 실제로 형태가 변하는지, 값이 터무니없이 크지 않은지
  5. 비율: 키 1.0 m, 머리 : 몸 ≈ 1 : 1, 머리 가로 ≈ 세로 (거의 원형)
  6. 포즈 시험: 팔 뼈를 돌리면 팔만 움직이고 몸통은 움직이지 않는지
  7. FBX 다시 읽기: 메시·모프 타깃·뼈가 남아 있고 불필요한 끝 뼈(`_end`)가 없는지

사용법
  python3 tools/blender/verify_mannequin.py [--root .]     (성공 시 종료 코드 0)
"""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

EXPECTED_BONES = {
    "root", "pelvis", "spine_01", "head",
    "upperarm_l", "lowerarm_l", "hand_l", "thigh_l", "calf_l", "foot_l",
    "upperarm_r", "lowerarm_r", "hand_r", "thigh_r", "calf_r", "foot_r",
}
EXPECTED_MORPHS = {
    "SK_Mannequin_Head": {"head_big", "head_small", "legs_long", "legs_short"},
    "SK_Mannequin_Body": {"torso_wide", "torso_slim", "legs_long", "legs_short", "arms_long", "arms_short"},
}
EXPECTED_HEIGHT = 1.0
HEIGHT_TOLERANCE = 0.01
RATIO_TOLERANCE = 0.08       # 머리 높이 / 몸 높이 가 1 ± 이 값
ROUNDNESS_TOLERANCE = 0.05   # 머리 가로 / 세로 가 1 ± 이 값
MAX_MORPH_DISPLACEMENT = 0.5  # m. 이보다 크면 모프가 망가진 것
MIN_MORPH_DISPLACEMENT = 1e-4


def world_bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    return (Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points))),
            Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points))))


def check_structure(errors: list[str]):
    rigs = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    meshes = {o.name: o for o in bpy.data.objects if o.type == "MESH"}
    if len(rigs) != 1:
        errors.append(f"armature count must be 1, got {len(rigs)}")
        return None, meshes
    rig = rigs[0]
    if rig.name == "Armature":
        errors.append('armature object must not be named "Armature" (Unreal may discard it).')
    if set(meshes) != set(EXPECTED_MORPHS):
        errors.append(f"mesh objects must be {sorted(EXPECTED_MORPHS)}, got {sorted(meshes)}")
    bones = {b.name for b in rig.data.bones}
    if bones != EXPECTED_BONES:
        errors.append(f"bones differ: missing={sorted(EXPECTED_BONES - bones)} extra={sorted(bones - EXPECTED_BONES)}")
    return rig, meshes


def check_weights(meshes: dict, errors: list[str]) -> None:
    for name, obj in meshes.items():
        group_names = {i: g.name for i, g in enumerate(obj.vertex_groups)}
        if not set(group_names.values()) >= EXPECTED_BONES:
            errors.append(f"{name}: vertex groups must include every bone.")
        for v in obj.data.vertices:
            total = sum(g.weight for g in v.groups)
            if not v.groups or abs(total - 1.0) > 1e-3:
                errors.append(f"{name}: vertex {v.index} weight sum is {total:.4f} (must be 1).")
                break


def check_morphs(meshes: dict, errors: list[str]) -> None:
    for name, expected in EXPECTED_MORPHS.items():
        obj = meshes.get(name)
        if obj is None or obj.data.shape_keys is None:
            errors.append(f"{name}: no shape keys.")
            continue
        blocks = obj.data.shape_keys.key_blocks
        if blocks[0].name != "Basis":
            errors.append(f"{name}: first shape key must be Basis.")
        names = {k.name for k in blocks[1:]}
        if names != expected:
            errors.append(f"{name}: morphs differ: missing={sorted(expected - names)} extra={sorted(names - expected)}")
        basis = blocks[0].data
        for key in blocks[1:]:
            if abs(key.value) > 1e-9:
                errors.append(f"{name}.{key.name}: default value is {key.value}, must be 0 (all morphs would apply at once).")
            largest = max((a.co - b.co).length for a, b in zip(key.data, basis))
            if largest < MIN_MORPH_DISPLACEMENT:
                errors.append(f"{name}.{key.name}: does not change the shape.")
            if largest > MAX_MORPH_DISPLACEMENT:
                errors.append(f"{name}.{key.name}: displacement {largest:.3f} m is implausibly large.")


def check_proportions(meshes: dict, errors: list[str]) -> None:
    head, body = meshes.get("SK_Mannequin_Head"), meshes.get("SK_Mannequin_Body")
    if head is None or body is None:
        return
    h_min, h_max = world_bounds(head)
    b_min, b_max = world_bounds(body)
    total = max(h_max.z, b_max.z) - min(h_min.z, b_min.z)
    if abs(total - EXPECTED_HEIGHT) > HEIGHT_TOLERANCE:
        errors.append(f"total height {total:.3f} m, expected {EXPECTED_HEIGHT} m.")
    head_height = h_max.z - h_min.z
    body_height = h_min.z - min(b_min.z, h_min.z)  # 머리 아래끝에서 발바닥까지
    ratio = head_height / body_height if body_height > 0 else math.inf
    if abs(ratio - 1.0) > RATIO_TOLERANCE:
        errors.append(f"head : body = {ratio:.3f} : 1, expected about 1 : 1 (2 heads tall).")
    roundness = (h_max.x - h_min.x) / head_height
    if abs(roundness - 1.0) > ROUNDNESS_TOLERANCE:
        errors.append(f"head width/height = {roundness:.3f}, expected a near-circle.")
    if min(b_min.z, h_min.z) < -1e-3:
        errors.append("feet go below the ground plane (z < 0).")


def check_pose(rig, meshes: dict, errors: list[str]) -> None:
    body = meshes.get("SK_Mannequin_Body")
    if rig is None or body is None:
        return
    depsgraph = bpy.context.evaluated_depsgraph_get()

    def positions() -> list[Vector]:
        bpy.context.view_layer.update()
        evaluated = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
        return [v.co.copy() for v in evaluated.data.vertices]

    rest = positions()
    bone = rig.pose.bones["upperarm_l"]
    bone.rotation_mode = "XYZ"
    bone.rotation_euler = (math.radians(60.0), 0.0, 0.0)  # 뼈의 세로축(Y)이 아닌 X축으로 돌려야 팔이 움직인다
    posed = positions()
    bone.rotation_euler = (0.0, 0.0, 0.0)

    moved = [(a - b).length for a, b in zip(rest, posed)]
    arm_moved = max(moved)
    torso_center_moved = max(
        (m for m, c in zip(moved, rest) if abs(c.x) < 0.05 and 0.25 < c.z < 0.45), default=0.0)
    right_arm_moved = max((m for m, c in zip(moved, rest) if c.x < -0.12 and c.z > 0.2), default=0.0)
    if arm_moved < 0.05:
        errors.append(f"posing upperarm_l moved nothing (max {arm_moved:.4f} m): skinning is broken.")
    if torso_center_moved > 1e-4:
        errors.append(f"posing upperarm_l moved the torso by {torso_center_moved:.4f} m.")
    if right_arm_moved > 1e-4:
        errors.append(f"posing upperarm_l moved the right arm by {right_arm_moved:.4f} m.")


def check_fbx(fbx_path: Path, errors: list[str]) -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        bpy.ops.preferences.addon_enable(module="io_scene_fbx")
    except Exception:
        pass
    bpy.ops.import_scene.fbx(filepath=str(fbx_path))

    rigs = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    if len(meshes) != 2:
        errors.append(f"FBX: expected 2 meshes, got {len(meshes)}.")
    if len(rigs) != 1:
        errors.append(f"FBX: expected 1 armature, got {len(rigs)}.")
        return
    bone_names = {b.name for b in rigs[0].data.bones}
    leaf = sorted(n for n in bone_names if n.endswith("_end"))
    if leaf:
        errors.append(f"FBX: unwanted leaf bones {leaf}.")
    if not EXPECTED_BONES <= bone_names:
        errors.append(f"FBX: bones missing {sorted(EXPECTED_BONES - bone_names)}.")

    all_morphs: set[str] = set()
    for mesh in meshes:
        if mesh.data.shape_keys:
            all_morphs |= {k.name for k in mesh.data.shape_keys.key_blocks[1:]}
    needed = set().union(*EXPECTED_MORPHS.values())
    if not needed <= all_morphs:
        errors.append(f"FBX: morph targets missing {sorted(needed - all_morphs)}.")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--root", type=Path, default=Path("."))
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:])
    root = args.root.resolve()
    blend, fbx = root / "Art" / "Blender" / "Mannequin.blend", root / "Art" / "Export" / "SK_Mannequin.fbx"

    errors: list[str] = []
    for path in (blend, fbx):
        if not path.is_file():
            errors.append(f"missing file: {path}")
    if errors:
        print("\n".join(f"FAIL: {e}" for e in errors), file=sys.stderr)
        return 1

    bpy.ops.wm.open_mainfile(filepath=str(blend))
    rig, meshes = check_structure(errors)
    check_weights(meshes, errors)
    check_morphs(meshes, errors)
    check_proportions(meshes, errors)
    check_pose(rig, meshes, errors)
    check_fbx(fbx, errors)

    if errors:
        print("\n".join(f"FAIL: {e}" for e in errors), file=sys.stderr)
        return 1
    print("OK: mannequin passes all checks.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
