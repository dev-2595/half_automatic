"""마네킹에 옷을 입혀 보고, 체격 슬라이더와 자세에서 옷이 어떻게 되는지 확인한다 (옷 입히기 시험).

옷은 몸과 **같은 뼈대, 같은 이름의 체격 모프 타깃**을 쓰는 별도 메시다. (docs/art/03_character_customization.md 의
"슬롯별 부품 + 공용 뼈대" 구조를 실제로 해 본 것이다.) 몸 표면에서 일정 거리 바깥의 표면을 뽑아 옷감 모양을 만들고,
뼈 가중치와 모프 타깃은 몸과 같은 계산으로 구한다. 그래서 옷이 몸과 같은 방식으로 움직이고 변한다.

옷 (outfit)
  casual : 셔츠 + 바지 + 모자        몸에 붙는 옷
  robe   : 로브 + 모자               아래로 갈수록 넓게 퍼지는 옷 (몸을 따라가지 않는다)

사용법: python3 tools/blender/build_outfit.py --out-root .
산출물
  Art/Blender/Mannequin_Outfits.blend
  docs/art/reference/outfit_casual.png, outfit_robe.png   기본 모습(앞/비스듬/옆) + 체격 슬라이더 8종
  docs/art/reference/outfit_poses.png                      자세 4종
표준 출력: 상태별로 옷이 몸을 뚫고 들어간 깊이 (옷 정점이 몸 안쪽에 있는 정도). 머리 안쪽이라 보이지 않는 정점은 따로 센다.
종료 코드: 기본 자세와 체격 슬라이더 9가지에서 옷이 몸을 뚫으면 1 (자세 시험은 한계가 알려져 있어 보고만 한다).
"""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy  # noqa: F401  (bmesh 보다 먼저 불러와야 한다)
import bmesh  # noqa: E402
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_mannequin as bm  # noqa: E402

PENETRATION_REPORT_MM = 3.0   # 이보다 깊이 뚫린 정점을 센다
MORPH_STATES = ["base", "head_big", "head_small", "torso_wide", "torso_slim", "legs_long", "legs_short", "arms_long", "arms_short"]
POSES = {
    "arms_V_50": [("upperarm_l", (0, 1, 0), -50), ("upperarm_r", (0, 1, 0), +50)],
    "arms_forward_60": [("upperarm_l", (0, 0, 1), -60), ("upperarm_r", (0, 0, 1), +60)],
    "step_walk_35": [("thigh_l", (1, 0, 0), -35), ("thigh_r", (1, 0, 0), +35)],
    "sit_80": [("thigh_l", (1, 0, 0), -80), ("thigh_r", (1, 0, 0), -80)],
}


# ---------------------------------------------------------------- 옷 정의
def smoothstep_np(edge0: float, edge1: float, x: np.ndarray) -> np.ndarray:
    t = np.clip((x - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def robe_offset(z: np.ndarray) -> np.ndarray:
    """가슴은 몸에 가깝고, 아래로 갈수록 넓게 퍼지는 로브의 몸 표면에서의 거리."""
    return 0.016 + 0.065 * smoothstep_np(0.38 * bm.Z, 0.06 * bm.Z, z)


def plane_keep_above(z: float):
    return ((0.0, 0.0, z), (0.0, 0.0, 1.0))      # z 이상만 남긴다


def plane_keep_below(z: float):
    return ((0.0, 0.0, z), (0.0, 0.0, -1.0))     # z 이하만 남긴다


def plane_keep_inside_x(x: float):
    """|x| 가 x 이하인 부분만 남긴다 (소매 길이)."""
    return [((x, 0.0, 0.0), (-1.0, 0.0, 0.0)), ((-x, 0.0, 0.0), (1.0, 0.0, 0.0))]


GARMENTS = {
    # 이름: (옷감 거리, 자르는 면, 두께, 색, 입는 곳)
    "shirt": dict(offset=0.016, thickness=0.004, color=(0.20, 0.45, 0.85),
                  cuts=[plane_keep_above(0.15 * bm.Z), plane_keep_below(0.52 * bm.Z), *plane_keep_inside_x(0.30 * bm.W)], base="body"),
    "pants": dict(offset=0.008, thickness=0.003, color=(0.40, 0.28, 0.16),
                  cuts=[plane_keep_above(0.05 * bm.Z), plane_keep_below(0.28 * bm.Z)], base="body"),
    "robe": dict(offset=robe_offset, thickness=0.004, color=(0.48, 0.22, 0.62),
                 cuts=[plane_keep_above(0.06 * bm.Z), plane_keep_below(0.52 * bm.Z), *plane_keep_inside_x(0.38 * bm.W)],
                 base="body", margin=0.10),
    "hat": dict(offset=0.014, thickness=0.004, color=(0.93, 0.72, 0.12), cuts=[plane_keep_above(bm.HEAD_CENTER_Z + 0.03)], base="head"),
}
OUTFITS = {"casual": ["shirt", "pants", "hat"], "robe": ["robe", "hat"]}


# ---------------------------------------------------------------- 메시 만들기
def cut_mesh(verts: np.ndarray, faces: np.ndarray, cuts) -> tuple[np.ndarray, list[tuple[int, ...]]]:
    """평면으로 잘라 옷단·소매 끝·목 둘레를 깨끗한 직선으로 만든다."""
    bmx = bmesh.new()
    made = [bmx.verts.new(tuple(float(c) for c in v)) for v in verts]
    for f in faces:
        try:
            bmx.faces.new([made[int(i)] for i in f])
        except ValueError:
            pass  # 이미 있는 면
    for co, normal in cuts:
        geom = list(bmx.verts) + list(bmx.edges) + list(bmx.faces)
        bmesh.ops.bisect_plane(bmx, geom=geom, plane_co=co, plane_no=normal, clear_inner=True)
    bmesh.ops.delete(bmx, geom=[v for v in bmx.verts if not v.link_faces], context="VERTS")
    bmx.verts.ensure_lookup_table()
    index = {v: i for i, v in enumerate(bmx.verts)}
    out_verts = np.array([v.co[:] for v in bmx.verts])
    out_faces = [tuple(index[v] for v in f.verts) for f in bmx.faces]
    bmx.free()
    return out_verts, out_faces


def head_shell(offset: float):
    """머리 타원체보다 `offset` 만큼 큰 껍질 (모자용)."""
    part = bm.Part("hat")
    part.add_lathe(bm.sphere_profile(bm.HEAD_RZ + offset, bm.HEAD_CENTER_Z), scale=((bm.HEAD_RX + offset) / (bm.HEAD_RZ + offset), 1.0))
    return np.array(part.verts), part.faces  # 면은 삼각형과 사각형이 섞여 있어 배열로 묶지 않는다


def solidify(obj: bpy.types.Object, thickness: float) -> None:
    """옷감에 두께를 준다 (표면에서 안쪽으로)."""
    for other in bpy.context.view_layer.objects:
        other.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    modifier = obj.modifiers.new("Solidify", "SOLIDIFY")
    modifier.thickness = thickness
    modifier.offset = -1.0
    bpy.ops.object.modifier_apply(modifier="Solidify")


def make_garment(name: str, spec: dict, rig, collection) -> bpy.types.Object:
    if spec["base"] == "head":
        verts, faces = head_shell(spec["offset"])
    else:
        verts, faces, _ = bm.build_body_mesh(offset=spec["offset"], margin=spec.get("margin", 0.0))
    verts, faces = cut_mesh(verts, faces, spec["cuts"])

    obj = bm.make_mesh_object(f"CL_{name}", verts, faces, collection)
    solidify(obj, spec["thickness"])

    material = bpy.data.materials.new(f"M_{name}")
    material.diffuse_color = (*spec["color"], 1.0)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*spec["color"], 1.0)
        bsdf.inputs["Roughness"].default_value = 0.8
    obj.data.materials.append(material)

    coords = np.array([v.co[:] for v in obj.data.vertices])
    if spec["base"] == "head":
        bm.skin(obj, rig, [{"head": 1.0} for _ in coords])
        morphs = bm.head_morphs([Vector(c) for c in coords])
    else:
        regions = bm.region_weights(bm.body_fields(coords)[1])
        bm.skin(obj, rig, bm.body_vertex_weights(coords, regions))
        morphs = bm.body_morphs(coords, regions)
    obj.shape_key_add(name="Basis", from_mix=False)
    for key, offsets in morphs.items():
        bm.add_morph(obj, key, offsets)
    return obj


# ---------------------------------------------------------------- 자세 / 모프
def reset_pose(rig) -> None:
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = (1, 0, 0, 0)


def apply_pose(rig, rotations) -> None:
    reset_pose(rig)
    for bone_name, axis, degrees in rotations:
        pb = rig.pose.bones[bone_name]
        rest = pb.bone.matrix_local.to_3x3()
        rotation = Matrix.Rotation(math.radians(degrees), 3, Vector(axis))
        pb.rotation_quaternion = (rest.inverted() @ rotation @ rest).to_quaternion()


def penetration(garment: bpy.types.Object, base: bpy.types.Object, head: bpy.types.Object | None = None) -> tuple[float, int, int]:
    """옷 정점이 몸 안쪽으로 파고든 최대 깊이(mm), 기준(3 mm)보다 깊은 정점 수, 머리에 가려 제외한 정점 수.
    머리(head)를 주면 머리 안쪽에 있어 눈에 보이지 않는 정점은 센 뒤 제외한다."""
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    tree = BVHTree.FromObject(base, depsgraph)
    head_tree = BVHTree.FromObject(head, depsgraph) if head is not None else None
    evaluated = garment.evaluated_get(depsgraph)
    matrix = evaluated.matrix_world
    worst, count, hidden = 0.0, 0, 0
    for vertex in evaluated.data.vertices:
        point = matrix @ vertex.co
        location, normal, _index, distance = tree.find_nearest(point)
        if location is None or (point - location).dot(normal) >= 0.0:
            continue  # 몸 바깥이다
        if head_tree is not None:
            h_loc, h_nor, _hi, _hd = head_tree.find_nearest(point)
            if h_loc is not None and (point - h_loc).dot(h_nor) < 0.0:
                hidden += 1  # 머리 안쪽: 눈에 보이지 않는다
                continue
        depth_mm = distance * 1000.0
        worst = max(worst, depth_mm)
        if depth_mm > PENETRATION_REPORT_MM:
            count += 1
    return worst, count, hidden


# ---------------------------------------------------------------- 렌더
def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out-root", type=Path, default=Path("."))
    parser.add_argument("--scratch", type=Path, default=Path("/tmp/outfit_build"))
    args = parser.parse_args()
    out_root: Path = args.out_root.resolve()
    args.scratch.mkdir(parents=True, exist_ok=True)

    from PIL import Image, ImageDraw

    rig, head, body = bm.build_scene()
    collection = bpy.data.collections["Mannequin"]
    garments = {name: make_garment(name, spec, rig, collection) for name, spec in GARMENTS.items()}
    base_of = {name: (head if spec["base"] == "head" else body) for name, spec in GARMENTS.items()}
    everything = [head, body, *garments.values()]

    blend = out_root / "Art" / "Blender" / "Mannequin_Outfits.blend"
    blend.parent.mkdir(parents=True, exist_ok=True)

    def show(outfit: str) -> None:
        for name, obj in garments.items():
            obj.hide_render = name not in OUTFITS[outfit]
            obj.hide_viewport = obj.hide_render

    # --- 상태별 뚫림 측정 + 렌더
    cam = bm.setup_render(380, 440)
    ref = out_root / "docs" / "art" / "reference"
    ref.mkdir(parents=True, exist_ok=True)
    report: dict[str, dict[str, tuple[float, int, int]]] = {}

    for outfit, names in OUTFITS.items():
        show(outfit)
        report[outfit] = {}
        frames = []

        # 기본 모습: 앞 / 비스듬 / 옆
        reset_pose(rig)
        bm.set_morphs(everything, {})
        views = [("front", Vector((0, -1, 0))), ("3q", Vector((0.8, -0.8, 0.25))), ("side", Vector((1, 0, 0)))]
        base_frames = []
        for label, direction in views:
            path = args.scratch / f"{outfit}_base_{label}.png"
            bm.render_view(cam, path, direction, 1.3, 0.5)
            base_frames.append((f"{outfit} {label}", Image.open(path).convert("RGB")))

        # 체격 슬라이더
        morph_frames = []
        for state in MORPH_STATES:
            reset_pose(rig)
            bm.set_morphs(everything, {} if state == "base" else {state: 1.0})
            worst = [penetration(garments[n], base_of[n], head if base_of[n] is body else None) for n in names]
            report[outfit][state] = (max(w[0] for w in worst), sum(w[1] for w in worst), sum(w[2] for w in worst))
            path = args.scratch / f"{outfit}_morph_{state}.png"
            bm.render_view(cam, path, Vector((0, -1, 0)), 1.45, 0.55)
            morph_frames.append((state, Image.open(path).convert("RGB")))
        bm.set_morphs(everything, {})

        w, h = 380, 440
        sheet = Image.new("RGB", (w * 3, (h + 22) * 4 + 0), (236, 236, 240))
        draw = ImageDraw.Draw(sheet)
        for i, (label, img) in enumerate(base_frames):
            sheet.paste(img, (w * i, 22)); draw.text((w * i + 6, 6), label, fill=(40, 40, 50))
        for i, (label, img) in enumerate(morph_frames):
            r, c = divmod(i, 3)
            y = (h + 22) * (1 + r)
            sheet.paste(img, (w * c, y + 22)); draw.text((w * c + 6, y + 6), f"{outfit} morph: {label}", fill=(40, 40, 50))
        sheet.save(ref / f"outfit_{outfit}.png")

    # 자세 (두 옷 모두)
    pose_frames = []
    for outfit, names in OUTFITS.items():
        show(outfit)
        bm.set_morphs(everything, {})
        for pose, rotations in POSES.items():
            apply_pose(rig, rotations)
            worst = [penetration(garments[n], base_of[n], head if base_of[n] is body else None) for n in names]
            report[outfit][pose] = (max(w[0] for w in worst), sum(w[1] for w in worst), sum(w[2] for w in worst))
            path = args.scratch / f"{outfit}_pose_{pose}.png"
            bm.render_view(cam, path, Vector((0.8, -0.8, 0.25)), 1.3, 0.5)
            pose_frames.append((f"{outfit}: {pose}", Image.open(path).convert("RGB")))
    reset_pose(rig)
    w, h = 380, 440
    sheet = Image.new("RGB", (w * 4, (h + 22) * 2), (236, 236, 240))
    draw = ImageDraw.Draw(sheet)
    for i, (label, img) in enumerate(pose_frames):
        r, c = divmod(i, 4)
        sheet.paste(img, (w * c, (h + 22) * r + 22)); draw.text((w * c + 6, (h + 22) * r + 6), label, fill=(40, 40, 50))
    sheet.save(ref / "outfit_poses.png")

    show("casual")
    for obj in garments.values():
        obj.hide_render = False
        obj.hide_viewport = False
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)

    print("REPORT outfit  state              max_penetration_mm  vertices_over_3mm  hidden_by_head")
    for outfit, states in report.items():
        for state, (worst, count, hidden) in states.items():
            print(f"REPORT {outfit:7s} {state:18s} {worst:8.1f}            {count:6d}           {hidden:6d}")
    # 검사: 기본 자세와 체격 슬라이더 9가지에서는 옷이 몸을 뚫으면 안 된다 (자세는 한계가 알려져 있어 보고만 한다)
    problems = []
    for outfit, states in report.items():
        for state in MORPH_STATES:
            worst, count, _hidden = states[state]
            if count > 0 or worst > PENETRATION_REPORT_MM:
                problems.append(f"{outfit}/{state}: clothes sink {worst:.1f} mm into the body ({count} vertices)")
    if problems:
        print("FAIL: clothes clip the body at rest or with body morphs:", file=sys.stderr)
        for line in problems:
            print(f"FAIL:   {line}", file=sys.stderr)
        return 1
    print(f"OK: {blend}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
