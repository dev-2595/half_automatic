"""만들어진 마네킹(.blend, .fbx)이 문서의 요구를 지키는지 검사한다.

검사 항목 (기대값은 빌드 스크립트와 별도로 여기에 적는다)
  1. 객체 구성: 아마추어 1개 + 메시 2개 (이름 규칙)
  2. 뼈 목록, 그리고 루트가 "Armature" 가 아닌 이름인지
  3. 모든 정점이 뼈에 묶여 있고 가중치 합이 1이며 영향 뼈가 4개 이하인지
  3-1. 몸이 **하나로 이어진 닫힌 표면**인지 (팔·다리가 떨어진 덩어리가 아님)
  4. 체격 모프 타깃: 이름, 기본값이 0인지, 실제로 형태가 변하는지, 값이 터무니없이 크지 않은지
  5. 비율: 키 1.0 m, 머리 : 몸, 머리 가로/세로, 팔 끝~팔 끝 폭, 몸통 폭 (스케치 v2 측정값)
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
# 기준: docs/art/reference/character_style_v2.png 를 측정한 값 (키 1.0 m 로 환산)
EXPECTED_HEIGHT = 1.0
HEIGHT_TOLERANCE = 0.01
EXPECTED_HEAD_TO_BODY = 1.17     # 머리 높이 / (머리 아래 ~ 발바닥)
HEAD_TO_BODY_TOLERANCE = 0.08
EXPECTED_HEAD_ASPECT = 1.15      # 머리 가로 / 세로 (원보다 약간 넓은 타원)
HEAD_ASPECT_TOLERANCE = 0.05
EXPECTED_SPAN = 0.92             # 팔 끝 ~ 팔 끝 / 키
SPAN_TOLERANCE = 0.06
EXPECTED_TORSO_WIDTH = 0.50      # 몸통 최대 폭 / 키
TORSO_WIDTH_TOLERANCE = 0.05
MAX_MORPH_DISPLACEMENT = 0.5  # m. 이보다 크면 모프가 망가진 것
MIN_MORPH_DISPLACEMENT = 1e-4
MAX_INFLUENCES = 4          # 정점 하나에 영향을 주는 뼈의 최대 수 (게임 엔진 스키닝 한계)
MAX_BODY_TRIANGLES = 30000  # 마네킹 몸 삼각형 수 상한 (너무 무거워지지 않게)


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
            if len(v.groups) > MAX_INFLUENCES:
                errors.append(f"{name}: vertex {v.index} has {len(v.groups)} bone influences (max {MAX_INFLUENCES}).")
                break


def check_connected_surface(meshes: dict, errors: list[str]) -> None:
    """몸이 하나로 이어진(연결 요소 1개), 구멍 없는(모든 모서리가 면 2개에 공유) 표면인지 검사한다."""
    body = meshes.get("SK_Mannequin_Body")
    if body is None:
        return
    mesh = body.data
    parent = list(range(len(mesh.vertices)))

    def find(i: int) -> int:
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    edge_faces: dict[tuple[int, int], int] = {}
    for poly in mesh.polygons:
        ids = list(poly.vertices)
        for a, b in zip(ids, ids[1:] + ids[:1]):
            edge_faces[(min(a, b), max(a, b))] = edge_faces.get((min(a, b), max(a, b)), 0) + 1
            ra, rb = find(a), find(b)
            if ra != rb:
                parent[ra] = rb
    used = {v for poly in mesh.polygons for v in poly.vertices}
    components = len({find(v) for v in used})
    if components != 1:
        errors.append(f"SK_Mannequin_Body: {components} separate pieces; arms and legs must be welded into one surface.")
    open_edges = sum(1 for n in edge_faces.values() if n != 2)
    if open_edges:
        errors.append(f"SK_Mannequin_Body: {open_edges} edges are not shared by exactly 2 faces (holes or non-manifold).")
    if len(mesh.polygons) > MAX_BODY_TRIANGLES:
        errors.append(f"SK_Mannequin_Body: {len(mesh.polygons)} triangles exceeds the budget {MAX_BODY_TRIANGLES}.")


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
    low = min(h_min.z, b_min.z)
    total = max(h_max.z, b_max.z) - low
    if abs(total - EXPECTED_HEIGHT) > HEIGHT_TOLERANCE:
        errors.append(f"total height {total:.3f} m, expected {EXPECTED_HEIGHT} m.")

    head_height = h_max.z - h_min.z
    body_height = h_min.z - low  # 머리 아래끝에서 발바닥까지
    ratio = head_height / body_height if body_height > 0 else math.inf
    if abs(ratio - EXPECTED_HEAD_TO_BODY) > HEAD_TO_BODY_TOLERANCE:
        errors.append(f"head : body = {ratio:.3f} : 1, expected {EXPECTED_HEAD_TO_BODY} : 1.")
    aspect = (h_max.x - h_min.x) / head_height
    if abs(aspect - EXPECTED_HEAD_ASPECT) > HEAD_ASPECT_TOLERANCE:
        errors.append(f"head width/height = {aspect:.3f}, expected {EXPECTED_HEAD_ASPECT}.")
    if low < -1e-3:
        errors.append("feet go below the ground plane (z < 0).")

    span = (max(h_max.x, b_max.x) - min(h_min.x, b_min.x)) / total
    if abs(span - EXPECTED_SPAN) > SPAN_TOLERANCE:
        errors.append(f"arm span / height = {span:.3f}, expected {EXPECTED_SPAN} (arms stick out sideways).")

    # 몸통 최대 폭: 몸통·골반 뼈에 묶인 정점만으로 잰다 (팔·다리 제외)
    torso_x = []
    for v in body.data.vertices:
        w = {body.vertex_groups[g.group].name: g.weight for g in v.groups}
        if w.get("spine_01", 0.0) + w.get("pelvis", 0.0) > 0.5:
            torso_x.append(abs(v.co.x))
    torso_width = 2.0 * max(torso_x) / total if torso_x else 0.0
    if abs(torso_width - EXPECTED_TORSO_WIDTH) > TORSO_WIDTH_TOLERANCE:
        errors.append(f"torso width / height = {torso_width:.3f}, expected {EXPECTED_TORSO_WIDTH}.")
    head_width = (h_max.x - h_min.x) / total
    if torso_width >= head_width:
        errors.append("torso must be narrower than the head.")


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
    check_connected_surface(meshes, errors)
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
