"""기본 마네킹(베이스 바디)을 Blender에서 만들어 .blend / .fbx / 미리보기로 저장한다.

기준: docs/art/01_character_style.md (약 2등신, 머리 : 몸 ≈ 1 : 1, 목 없음, 손가락 없음)
      docs/art/03_character_customization.md (공용 뼈대 + 체격 모프 타깃)
      docs/dev/02_blender_pipeline.md (내보내기 설정)

실행 (둘 중 하나)
  blender -b -P tools/blender/build_mannequin.py -- --out-root .
  python3 tools/blender/build_mannequin.py --out-root .        (bpy 모듈이 설치된 경우)

산출물 (out-root 기준)
  Art/Blender/Mannequin.blend
  Art/Export/SK_Mannequin.fbx
  docs/art/reference/mannequin_preview.png   앞 / 옆 / 뒤
  docs/art/reference/mannequin_morphs.png    체격 모프 타깃 미리보기

좌표계: Z 위, 캐릭터는 -Y 를 바라본다. 캐릭터의 왼쪽이 +X 이다. 단위는 미터이며 키는 1.0 m (= Unreal 100 cm).
"""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

# ---------------------------------------------------------------- 치수 (미터)
TOTAL_HEIGHT = 1.0
HEAD_RADIUS = TOTAL_HEIGHT / 4            # 머리 높이 = 키의 절반 (머리 : 몸 = 1 : 1)
HEAD_CENTER_Z = TOTAL_HEIGHT - HEAD_RADIUS
NECK_Z = TOTAL_HEIGHT / 2                 # 몸통 위끝 = 머리 아래끝 (목 없음)

LEG_X = 0.07
LEG_RADIUS = 0.062
LEG_TOP_Z = 0.22                          # 다리 길이 ≈ 몸 높이의 40%
KNEE_Z = 0.11
ANKLE_Z = 0.04

TORSO_BOTTOM_Z = 0.17
TORSO_TOP_Z = 0.53                        # 머리 아래로 살짝 겹친다
TORSO_BOTTOM_WIDTH = 0.150                # 아래로 갈수록 넓어지는 몸통
TORSO_TOP_WIDTH = 0.110
TORSO_DEPTH_RATIO = 0.88

ARM_X = 0.142                             # 팔은 몸통 옆에 붙어 늘어진다 (스케치)
ARM_RADIUS = 0.030
SHOULDER_Z = 0.46
ELBOW_Z = 0.33
WRIST_Z = 0.22
HAND_END_Z = 0.15
ARM_CURL = 0.020                          # 손끝이 몸 안쪽으로 살짝 말린다

SEGMENTS = 28
RINGS = 16

# 체격 모프 타깃 크기
HEAD_BIG, HEAD_SMALL = 1.30, 0.75
TORSO_WIDE_X, TORSO_WIDE_Y, TORSO_WIDE_ARM = 0.25, 0.15, 0.030
TORSO_SLIM_X, TORSO_SLIM_ARM = 0.20, 0.020
LEGS_LONG_DZ, LEGS_SHORT_DZ = 0.10, -0.06
ARMS_LONG, ARMS_SHORT = 1.25, 0.75

HEAD_MORPHS = ["head_big", "head_small", "legs_long", "legs_short"]
BODY_MORPHS = ["torso_wide", "torso_slim", "legs_long", "legs_short", "arms_long", "arms_short"]

BONES = {
    # 이름: (head, tail, parent)
    "root": ((0, 0, 0.00), (0, 0, 0.06), None),
    "pelvis": ((0, 0, 0.20), (0, 0, 0.28), "root"),
    "spine_01": ((0, 0, 0.28), (0, 0, NECK_Z), "pelvis"),
    "head": ((0, 0, NECK_Z), (0, 0, TOTAL_HEIGHT), "spine_01"),
}
for _side, _sx in (("l", 1.0), ("r", -1.0)):
    BONES[f"upperarm_{_side}"] = ((_sx * ARM_X, 0, SHOULDER_Z), (_sx * ARM_X, 0, ELBOW_Z), "spine_01")
    BONES[f"lowerarm_{_side}"] = ((_sx * ARM_X, 0, ELBOW_Z), (_sx * ARM_X, 0, WRIST_Z), f"upperarm_{_side}")
    BONES[f"hand_{_side}"] = ((_sx * ARM_X, 0, WRIST_Z), (_sx * ARM_X, 0, HAND_END_Z), f"lowerarm_{_side}")
    BONES[f"thigh_{_side}"] = ((_sx * LEG_X, 0, LEG_TOP_Z), (_sx * LEG_X, 0, KNEE_Z), "pelvis")
    BONES[f"calf_{_side}"] = ((_sx * LEG_X, 0, KNEE_Z), (_sx * LEG_X, 0, ANKLE_Z), f"thigh_{_side}")
    BONES[f"foot_{_side}"] = ((_sx * LEG_X, 0, ANKLE_Z), (_sx * LEG_X, -0.07, 0.025), f"calf_{_side}")

RIG_NAME = "mannequin_rig"  # "Armature" 이라는 이름은 Unreal 이 버릴 수 있다 (docs/dev/02)
BODY_NAME = "SK_Mannequin_Body"
HEAD_NAME = "SK_Mannequin_Head"


# ---------------------------------------------------------------- 수학 도우미
def smoothstep(edge0: float, edge1: float, x: float) -> float:
    if edge0 == edge1:
        return 1.0 if x >= edge1 else 0.0
    t = max(0.0, min(1.0, (x - edge0) / (edge1 - edge0)))
    return t * t * (3.0 - 2.0 * t)


def sign(x: float) -> float:
    return 1.0 if x >= 0.0 else -1.0


class Part:
    """한 덩어리 도형. 정점 좌표, 면, 그리고 정점별 영역 태그를 가진다."""

    def __init__(self, tag: str):
        self.tag = tag
        self.verts: list[tuple[float, float, float]] = []
        self.faces: list[tuple[int, ...]] = []

    def add_lathe(self, profile: list[tuple[float, float]], center=(0.0, 0.0), scale=(1.0, 1.0)) -> None:
        """(반지름, z) 단면을 z축 둘레로 돌려 만든다. 양 끝의 반지름이 0이면 꼭짓점 하나가 된다."""
        base = len(self.verts)
        ring_index: list[list[int]] = []
        for radius, z in profile:
            if radius <= 1e-9:
                self.verts.append((center[0], center[1], z))
                ring_index.append([len(self.verts) - 1])
                continue
            ids = []
            for s in range(SEGMENTS):
                a = 2.0 * math.pi * s / SEGMENTS
                self.verts.append((center[0] + radius * math.cos(a) * scale[0],
                                   center[1] + radius * math.sin(a) * scale[1], z))
                ids.append(len(self.verts) - 1)
            ring_index.append(ids)
        for lower, upper in zip(ring_index, ring_index[1:]):
            if len(lower) == 1 and len(upper) == 1:
                continue
            if len(lower) == 1:
                for s in range(SEGMENTS):
                    self.faces.append((lower[0], upper[(s + 1) % SEGMENTS], upper[s]))
            elif len(upper) == 1:
                for s in range(SEGMENTS):
                    self.faces.append((lower[s], lower[(s + 1) % SEGMENTS], upper[0]))
            else:
                for s in range(SEGMENTS):
                    t = (s + 1) % SEGMENTS
                    self.faces.append((lower[s], lower[t], upper[t], upper[s]))
        assert len(self.verts) > base


def sphere_profile(radius: float, center_z: float) -> list[tuple[float, float]]:
    out = []
    for i in range(RINGS + 1):
        a = -math.pi / 2 + math.pi * i / RINGS
        out.append((max(0.0, radius * math.cos(a)), center_z + radius * math.sin(a)))
    out[0] = (0.0, out[0][1])
    out[-1] = (0.0, out[-1][1])
    return out


def capsule_profile(radius: float, z_bottom: float, z_top: float, flat_bottom: bool = False) -> list[tuple[float, float]]:
    out: list[tuple[float, float]] = []
    half = RINGS // 2
    if flat_bottom:
        # 바닥에 닿는 발: 둥근 모서리의 납작한 바닥
        out += [(0.0, z_bottom), (radius * 0.65, z_bottom), (radius * 0.93, z_bottom + radius * 0.25)]
        out.append((radius, z_bottom + radius * 0.6))
    else:
        for i in range(half):
            a = -math.pi / 2 + (math.pi / 2) * i / half
            out.append((max(0.0, radius * math.cos(a)), z_bottom + radius + radius * math.sin(a)))
        out[0] = (0.0, out[0][1])
    for i in range(half + 1):
        a = (math.pi / 2) * i / half
        out.append((max(0.0, radius * math.cos(a)), z_top - radius + radius * math.sin(a)))
    out[-1] = (0.0, out[-1][1])
    return out


def build_head() -> Part:
    head = Part("head")
    head.add_lathe(sphere_profile(HEAD_RADIUS, HEAD_CENTER_Z))
    return head


def build_body_parts() -> list[Part]:
    parts: list[Part] = []

    torso = Part("torso")
    steps = RINGS * 2
    profile = []
    for i in range(steps + 1):
        t = i / steps
        width = TORSO_BOTTOM_WIDTH + (TORSO_TOP_WIDTH - TORSO_BOTTOM_WIDTH) * t
        radius = math.sqrt(max(0.0, 1.0 - (2.0 * t - 1.0) ** 2)) * width
        profile.append((radius, TORSO_BOTTOM_Z + (TORSO_TOP_Z - TORSO_BOTTOM_Z) * t))
    profile[0] = (0.0, profile[0][1])
    profile[-1] = (0.0, profile[-1][1])
    torso.add_lathe(profile, scale=(1.0, TORSO_DEPTH_RATIO))
    parts.append(torso)

    for side, sx in (("l", 1.0), ("r", -1.0)):
        leg = Part("leg")
        leg.add_lathe(capsule_profile(LEG_RADIUS, 0.0, LEG_TOP_Z, flat_bottom=True), center=(sx * LEG_X, 0.0))
        parts.append(leg)

        arm = Part("arm")
        arm.add_lathe(capsule_profile(ARM_RADIUS, HAND_END_Z, SHOULDER_Z + ARM_RADIUS * 0.5), center=(sx * ARM_X, 0.0))
        # 손끝이 몸 안쪽으로 말린다
        arm.verts = [
            (x - sx * ARM_CURL * smoothstep(ELBOW_Z - 0.05, HAND_END_Z, z), y, z) for (x, y, z) in arm.verts
        ]
        parts.append(arm)
    return parts


# ---------------------------------------------------------------- 정점 가중치
def body_weights(tag: str, x: float, z: float) -> dict[str, float]:
    side = "l" if x >= 0 else "r"
    if tag == "torso":
        w_pelvis = 1.0 - smoothstep(0.24, 0.32, z)
        weights = {"spine_01": 1.0 - w_pelvis, "pelvis": w_pelvis}
    elif tag == "leg":
        side = "l" if x >= 0 else "r"
        thigh = 1.0 - smoothstep(KNEE_Z - 0.03, KNEE_Z + 0.03, z)  # 무릎 위가 1
        thigh = 1.0 - thigh                                         # 위쪽일수록 1
        foot = 1.0 - smoothstep(ANKLE_Z - 0.015, ANKLE_Z + 0.015, z)
        calf = (1.0 - thigh) - foot
        weights = {f"thigh_{side}": thigh, f"calf_{side}": max(0.0, calf), f"foot_{side}": foot}
    elif tag == "arm":
        upper = smoothstep(ELBOW_Z - 0.03, ELBOW_Z + 0.03, z)
        hand = 1.0 - smoothstep(WRIST_Z - 0.02, WRIST_Z + 0.02, z)
        lower = 1.0 - upper - hand
        weights = {f"upperarm_{side}": upper, f"lowerarm_{side}": max(0.0, lower), f"hand_{side}": hand}
    else:
        raise ValueError(tag)
    total = sum(weights.values())
    return {k: v / total for k, v in weights.items() if v / total > 1e-6}


# ---------------------------------------------------------------- Blender 객체
def make_mesh_object(name: str, parts: list[Part], collection) -> tuple[bpy.types.Object, list[str]]:
    verts: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    tags: list[str] = []
    for part in parts:
        offset = len(verts)
        verts += part.verts
        faces += [tuple(i + offset for i in f) for f in part.faces]
        tags += [part.tag] * len(part.verts)

    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    for poly in mesh.polygons:
        poly.use_smooth = True
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    return obj, tags


def create_armature(collection) -> bpy.types.Object:
    arm_data = bpy.data.armatures.new(RIG_NAME)
    rig = bpy.data.objects.new(RIG_NAME, arm_data)
    collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    edit_bones = arm_data.edit_bones
    for name, (head, tail, _parent) in BONES.items():
        bone = edit_bones.new(name)
        bone.head = Vector(head)
        bone.tail = Vector(tail)
    for name, (_head, _tail, parent) in BONES.items():
        if parent:
            edit_bones[name].parent = edit_bones[parent]
            edit_bones[name].use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    return rig


def skin(obj, rig, weights_per_vertex: list[dict[str, float]]) -> None:
    for bone_name in BONES:
        obj.vertex_groups.new(name=bone_name)
    for index, weights in enumerate(weights_per_vertex):
        for bone_name, weight in weights.items():
            obj.vertex_groups[bone_name].add([index], weight, "REPLACE")
    modifier = obj.modifiers.new("Armature", "ARMATURE")
    modifier.object = rig
    obj.parent = rig


def add_morph(obj, name: str, offsets: list[Vector]) -> None:
    key = obj.shape_key_add(name=name, from_mix=False)
    key.slider_min = 0.0
    key.slider_max = 1.0
    key.value = 0.0  # 기본값이 0 이라고 가정하지 않는다 (bpy 5.x 에서 1.0 으로 저장되는 것을 확인함)
    for point, delta in zip(key.data, offsets):
        point.co = point.co + delta


def head_morphs(coords: list[Vector]) -> dict[str, list[Vector]]:
    pivot = Vector((0.0, 0.0, NECK_Z))
    return {
        "head_big": [(c - pivot) * (HEAD_BIG - 1.0) for c in coords],
        "head_small": [(c - pivot) * (HEAD_SMALL - 1.0) for c in coords],
        "legs_long": [Vector((0.0, 0.0, LEGS_LONG_DZ)) for _ in coords],
        "legs_short": [Vector((0.0, 0.0, LEGS_SHORT_DZ)) for _ in coords],
    }


def body_morphs(coords: list[Vector], tags: list[str]) -> dict[str, list[Vector]]:
    out: dict[str, list[Vector]] = {name: [] for name in BODY_MORPHS}
    for c, tag in zip(coords, tags):
        s = sign(c.x)
        zero = Vector((0.0, 0.0, 0.0))

        out["torso_wide"].append(
            Vector((c.x * TORSO_WIDE_X, c.y * TORSO_WIDE_Y, 0.0)) if tag == "torso"
            else Vector((s * TORSO_WIDE_ARM, 0.0, 0.0)) if tag == "arm" else zero)
        out["torso_slim"].append(
            Vector((-c.x * TORSO_SLIM_X, 0.0, 0.0)) if tag == "torso"
            else Vector((-s * TORSO_SLIM_ARM, 0.0, 0.0)) if tag == "arm" else zero)

        for key, dz in (("legs_long", LEGS_LONG_DZ), ("legs_short", LEGS_SHORT_DZ)):
            # 다리: 발은 바닥에 두고 위쪽을 늘이거나 줄인다. 나머지(몸통·팔)는 통째로 위아래로 이동한다.
            out[key].append(Vector((0.0, 0.0, dz * (c.z / LEG_TOP_Z if tag == "leg" else 1.0))))

        for key, factor in (("arms_long", ARMS_LONG), ("arms_short", ARMS_SHORT)):
            out[key].append(Vector((0.0, 0.0, (c.z - SHOULDER_Z) * (factor - 1.0))) if tag == "arm" else zero)
    return out


def make_material() -> bpy.types.Material:
    material = bpy.data.materials.new("M_Mannequin")
    material.diffuse_color = (0.55, 0.58, 0.64, 1.0)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (0.55, 0.58, 0.64, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.85
    return material


def build_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    collection = bpy.data.collections.new("Mannequin")
    bpy.context.scene.collection.children.link(collection)
    material = make_material()

    rig = create_armature(collection)

    head_obj, head_tags = make_mesh_object(HEAD_NAME, [build_head()], collection)
    body_parts = build_body_parts()
    body_obj, body_tags = make_mesh_object(BODY_NAME, body_parts, collection)

    head_coords = [v.co.copy() for v in head_obj.data.vertices]
    body_coords = [v.co.copy() for v in body_obj.data.vertices]

    skin(head_obj, rig, [{"head": 1.0} for _ in head_coords])
    skin(body_obj, rig, [body_weights(t, c.x, c.z) for t, c in zip(body_tags, body_coords)])

    for obj in (head_obj, body_obj):
        obj.data.materials.append(material)

    head_obj.shape_key_add(name="Basis", from_mix=False)
    for name, offsets in head_morphs(head_coords).items():
        add_morph(head_obj, name, offsets)
    body_obj.shape_key_add(name="Basis", from_mix=False)
    for name, offsets in body_morphs(body_coords, body_tags).items():
        add_morph(body_obj, name, offsets)

    return rig, head_obj, body_obj


# ---------------------------------------------------------------- 저장 / 내보내기
def export_fbx(path: Path, rig, head_obj, body_obj) -> None:
    try:
        bpy.ops.preferences.addon_enable(module="io_scene_fbx")
    except Exception:
        pass  # 이미 켜져 있거나 모듈 방식이면 필요 없다
    bpy.ops.object.select_all(action="DESELECT")
    for obj in (rig, head_obj, body_obj):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        use_mesh_modifiers=False,   # 켜면 Shape Key 가 내보내지지 않는다
        add_leaf_bones=False,       # 손·발 끝에 불필요한 뼈가 생기지 않게 한다
        bake_anim=False,
        apply_scale_options="FBX_SCALE_ALL",
        mesh_smooth_type="FACE",
        path_mode="COPY",
    )


def set_morphs(objs, active: dict[str, float]) -> None:
    for obj in objs:
        for key in obj.data.shape_keys.key_blocks[1:]:
            key.value = active.get(key.name, 0.0)


def setup_render(width: int, height: int) -> bpy.types.Object:
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 24
    scene.cycles.use_denoising = False
    scene.render.resolution_x, scene.render.resolution_y = width, height
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "Standard"  # 기본(AgX)은 대비가 낮아 회색으로 보인다

    world = bpy.data.worlds.new("W")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.90, 0.91, 0.94, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.75
    scene.world = world

    light_data = bpy.data.lights.new("Key", "SUN")
    light_data.energy = 3.0
    light = bpy.data.objects.new("Key", light_data)
    light.rotation_euler = (math.radians(55), 0, math.radians(-30))
    scene.collection.objects.link(light)

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def render_view(cam, path: Path, direction: Vector, ortho_scale: float, target_z: float) -> None:
    cam.data.ortho_scale = ortho_scale
    target = Vector((0.0, 0.0, target_z))
    cam.location = target + direction.normalized() * 4.0
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def make_previews(out_root: Path, objs, scratch: Path) -> None:
    from PIL import Image, ImageDraw

    out_dir = out_root / "docs" / "art" / "reference"
    out_dir.mkdir(parents=True, exist_ok=True)
    scratch.mkdir(parents=True, exist_ok=True)

    cam = setup_render(360, 520)
    set_morphs(objs, {})
    views = [("FRONT", Vector((0, -1, 0))), ("SIDE", Vector((1, 0, 0))), ("BACK", Vector((0, 1, 0)))]
    frames = []
    for label, direction in views:
        tmp = scratch / f"view_{label}.png"
        render_view(cam, tmp, direction, 1.25, 0.5)
        frames.append((label, Image.open(tmp).convert("RGB")))
    sheet = Image.new("RGB", (360 * 3, 520 + 28), (236, 236, 240))
    draw = ImageDraw.Draw(sheet)
    for i, (label, img) in enumerate(frames):
        sheet.paste(img, (360 * i, 28))
        draw.text((360 * i + 8, 8), label, fill=(40, 40, 50))
    sheet.save(out_dir / "mannequin_preview.png")

    cam = bpy.context.scene.camera
    setup_w, setup_h = 240, 380
    bpy.context.scene.render.resolution_x, bpy.context.scene.render.resolution_y = setup_w, setup_h
    variants = [("base", {})] + [(name, {name: 1.0}) for name in
                                 ["head_big", "head_small", "torso_wide", "torso_slim",
                                  "legs_long", "legs_short", "arms_long", "arms_short"]]
    strip = Image.new("RGB", (setup_w * len(variants), setup_h + 28), (236, 236, 240))
    draw = ImageDraw.Draw(strip)
    for i, (label, active) in enumerate(variants):
        set_morphs(objs, active)
        tmp = scratch / f"morph_{label}.png"
        render_view(cam, tmp, Vector((0, -1, 0)), 1.45, 0.55)
        strip.paste(Image.open(tmp).convert("RGB"), (setup_w * i, 28))
        draw.text((setup_w * i + 8, 8), label, fill=(40, 40, 50))
    strip.save(out_dir / "mannequin_morphs.png")
    set_morphs(objs, {})


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out-root", type=Path, default=Path("."), help="저장소 루트 (기본: 현재 폴더)")
    parser.add_argument("--scratch", type=Path, default=Path("/tmp/mannequin_build"), help="미리보기 임시 폴더")
    parser.add_argument("--no-preview", action="store_true", help="미리보기 이미지를 만들지 않는다")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    out_root: Path = args.out_root.resolve()

    rig, head_obj, body_obj = build_scene()

    blend_path = out_root / "Art" / "Blender" / "Mannequin.blend"
    blend_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), compress=True)

    export_fbx(out_root / "Art" / "Export" / "SK_Mannequin.fbx", rig, head_obj, body_obj)

    if not args.no_preview:
        make_previews(out_root, (head_obj, body_obj), args.scratch)

    print(f"OK: {blend_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
