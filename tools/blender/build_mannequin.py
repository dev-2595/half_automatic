"""기본 마네킹(베이스 바디)을 Blender에서 만들어 .blend / .fbx / 미리보기로 저장한다.

기준: docs/art/01_character_style.md, 스케치 docs/art/reference/character_style_v2.png
      (약 2등신, 큰 타원형 머리, 목 없음, 옆으로 뻗은 통통한 잎 모양 팔, 갈라진 하체, 손가락 없음)
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

몸통·다리·팔은 **하나로 이어진 단일 표면**이다. 각 부위를 거리 함수(SDF)로 정의하고 접합부를 부드럽게 섞은 뒤(smooth union)
marching cubes 로 한 장의 메시로 뽑는다. 뼈 가중치와 체격 모프 타깃도 같은 거리 함수로 접합부에서 부드럽게 넘어가게 계산한다.
(필요: numpy, scikit-image)

좌표계: Z 위, 캐릭터는 -Y 를 바라본다. 캐릭터의 왼쪽이 +X 이다. 단위는 미터이며 키는 1.0 m (= Unreal 100 cm).
"""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector
from scipy.interpolate import PchipInterpolator
from skimage import measure

# ---------------------------------------------------------------- 치수 (미터)
# 기준: docs/art/reference/character_style_v2.png 를 픽셀로 측정한 값 (키 481px = 1.0 m)
#   머리 300 x 261px (가로/세로 1.15), 몸통 폭 위 0.38 / 최대 0.50 / 아래 0.47,
#   가랑이 꼭대기 0.22, 팔 끝~팔 끝 0.92, 팔 두께 0.22
TOTAL_HEIGHT = 1.0
HEAD_RZ = 0.27                            # 머리 높이 0.54 (머리 : 몸 ≈ 1.17 : 1)
HEAD_RX = 0.31                            # 머리 폭 0.62 → 가로/세로 ≈ 1.15 (원보다 약간 넓은 타원)
HEAD_CENTER_Z = TOTAL_HEIGHT - HEAD_RZ
NECK_Z = HEAD_CENTER_Z - HEAD_RZ          # 머리 아래끝 = 몸 위끝 (목 없음)

LEG_X = 0.125
LEG_RADIUS = 0.108                        # 두 다리 바깥 폭 ≈ 0.47, 사이에 틈(V자 자리)
LEG_TOP_Z = 0.26                          # 가랑이 꼭대기는 몸통이 시작하는 곳 (≈ 0.22)
KNEE_Z = 0.13
ANKLE_Z = 0.05

# 몸통 윤곽 (z, 반지름). 스케치처럼 몸통에서 발까지 옆선이 이어지도록, 아래쪽 반지름을 다리 바깥 끝(LEG_X + LEG_RADIUS)에 맞춘다.
TORSO_PROFILE = [(0.20, 0.208), (0.30, 0.229), (0.40, 0.222), (0.46, 0.185), (0.50, 0.100), (0.52, 0.0)]
TORSO_BOTTOM_Z = TORSO_PROFILE[0][0]
TORSO_TOP_Z = TORSO_PROFILE[-1][0]        # 머리 아래로 살짝 겹친다
TORSO_DEPTH_RATIO = 0.65

# 팔: 가늘게 늘어진 막대가 아니라, 몸 옆으로 뻗은 통통한 잎 모양 (끝이 둥글고 살짝 처진다)
ARM_ORIGIN_X = 0.14                       # 몸통 안쪽에서 시작해 옆으로 나온다
ARM_ORIGIN_Z = 0.42
ARM_LENGTH = 0.32                         # 어깨에서 팔 끝까지
ARM_DROOP_DEG = 7.0
ARM_RADIUS_Z = 0.105                      # 세로 두께 0.21
ARM_RADIUS_Y = 0.085                      # 앞뒤 두께

# 몸 표면 추출 (SDF + marching cubes)
VOXEL = 0.011                             # 격자 간격 (m). 작을수록 매끈하지만 정점이 늘어난다
BODY_BOUNDS = ((-0.52, -0.22, -0.03), (0.52, 0.22, 0.56))
BLEND_LEG = 0.05                          # 몸통-다리 접합부를 섞는 폭
BLEND_ARM = 0.05                          # 몸통-팔 접합부를 섞는 폭
TORSO_EDGE_BLEND = 0.03                   # 몸통 아래·위 모서리를 둥글리는 폭
LEG_EDGE_ROUND = 0.035                    # 다리 발바닥 모서리의 둥글기
ARM_CENTER_ALONG = 0.12                   # 팔 타원체 중심이 어깨에서 떨어진 거리
REGION_SOFTNESS = 0.015                   # 부위(몸통/다리/팔) 가중치가 섞이는 폭
MAX_INFLUENCES = 4                        # 정점 하나에 영향을 주는 뼈의 최대 수
MIN_WEIGHT = 0.01                          # 1% 미만의 영향은 버린다 (멀리 떨어진 부위가 따라 움직이는 것을 막는다)

SEGMENTS = 28
RINGS = 16
REGIONS = ("torso", "leg_l", "leg_r", "arm_l", "arm_r")

# 체격 모프 타깃 크기
HEAD_BIG, HEAD_SMALL = 1.30, 0.75
TORSO_WIDE_X, TORSO_WIDE_Y, TORSO_WIDE_ARM = 0.25, 0.15, 0.050
TORSO_SLIM_X, TORSO_SLIM_ARM = 0.20, 0.030
LEGS_LONG_DZ, LEGS_SHORT_DZ = 0.10, -0.06
ARMS_LONG, ARMS_SHORT = 1.25, 0.75

HEAD_MORPHS = ["head_big", "head_small", "legs_long", "legs_short"]
BODY_MORPHS = ["torso_wide", "torso_slim", "legs_long", "legs_short", "arms_long", "arms_short"]

_DROOP = math.radians(ARM_DROOP_DEG)
ARM_DIR = (math.cos(_DROOP), 0.0, -math.sin(_DROOP))  # +X쪽(캐릭터 왼쪽) 팔의 방향


def arm_point(sx: float, along: float) -> tuple[float, float, float]:
    """어깨에서 `along` 미터 떨어진 팔 위의 점."""
    return (sx * (ARM_ORIGIN_X + ARM_DIR[0] * along), 0.0, ARM_ORIGIN_Z + ARM_DIR[2] * along)


BONES = {
    # 이름: (head, tail, parent)
    "root": ((0, 0, 0.00), (0, 0, 0.06), None),
    "pelvis": ((0, 0, 0.20), (0, 0, 0.28), "root"),
    "spine_01": ((0, 0, 0.28), (0, 0, NECK_Z), "pelvis"),
    "head": ((0, 0, NECK_Z), (0, 0, TOTAL_HEIGHT), "spine_01"),
}
for _side, _sx in (("l", 1.0), ("r", -1.0)):
    BONES[f"upperarm_{_side}"] = (arm_point(_sx, 0.0), arm_point(_sx, 0.12), "spine_01")
    BONES[f"lowerarm_{_side}"] = (arm_point(_sx, 0.12), arm_point(_sx, 0.23), f"upperarm_{_side}")
    BONES[f"hand_{_side}"] = (arm_point(_sx, 0.23), arm_point(_sx, ARM_LENGTH), f"lowerarm_{_side}")
    BONES[f"thigh_{_side}"] = ((_sx * LEG_X, 0, LEG_TOP_Z - 0.02), (_sx * LEG_X, 0, KNEE_Z), "pelvis")
    BONES[f"calf_{_side}"] = ((_sx * LEG_X, 0, KNEE_Z), (_sx * LEG_X, 0, ANKLE_Z), f"thigh_{_side}")
    BONES[f"foot_{_side}"] = ((_sx * LEG_X, 0, ANKLE_Z), (_sx * LEG_X, -0.09, 0.03), f"calf_{_side}")

RIG_NAME = "mannequin_rig"  # "Armature" 이라는 이름은 Unreal 이 버릴 수 있다 (docs/dev/02)
BODY_NAME = "SK_Mannequin_Body"
HEAD_NAME = "SK_Mannequin_Head"


# ---------------------------------------------------------------- 수학 도우미
def smoothstep(edge0: float, edge1: float, x: float) -> float:
    if edge0 == edge1:
        return 1.0 if x >= edge1 else 0.0
    t = max(0.0, min(1.0, (x - edge0) / (edge1 - edge0)))
    return t * t * (3.0 - 2.0 * t)


class Part:
    """한 덩어리 도형. 정점 좌표, 면, 그리고 정점별 영역 태그를 가진다."""

    def __init__(self, tag: str):
        self.tag = tag
        self.verts: list[tuple[float, float, float]] = []
        self.faces: list[tuple[int, ...]] = []

    def add_lathe(self, profile: list[tuple[float, float]], center=(0.0, 0.0), scale=(1.0, 1.0), transform=None) -> None:
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
        if transform is not None:
            self.verts[base:] = [transform(*v) for v in self.verts[base:]]
        assert len(self.verts) > base


def sphere_profile(radius: float, center_z: float) -> list[tuple[float, float]]:
    out = []
    for i in range(RINGS + 1):
        a = -math.pi / 2 + math.pi * i / RINGS
        out.append((max(0.0, radius * math.cos(a)), center_z + radius * math.sin(a)))
    out[0] = (0.0, out[0][1])
    out[-1] = (0.0, out[-1][1])
    return out


def build_head() -> Part:
    head = Part("head")
    head.add_lathe(sphere_profile(HEAD_RZ, HEAD_CENTER_Z), scale=(HEAD_RX / HEAD_RZ, 1.0))
    return head


# ---------------------------------------------------------------- 정점 가중치
# ---------------------------------------------------------------- 몸 표면 (SDF)
def smin(a, b, k):
    """부드러운 합집합. 두 값이 k 안쪽으로 가까우면 경계를 매끈하게 섞는다."""
    h = np.maximum(k - np.abs(a - b), 0.0) / k
    return np.minimum(a, b) - h * h * k * 0.25


def smax(a, b, k):
    return -smin(-a, -b, k)


_TORSO_Z = np.array([p[0] for p in TORSO_PROFILE])
_TORSO_R = np.array([p[1] for p in TORSO_PROFILE])
_TORSO_SPLINE = PchipInterpolator(_TORSO_Z, _TORSO_R)
_TORSO_SLOPE = _TORSO_SPLINE.derivative()


def torso_radius_np(z: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    """TORSO_PROFILE 의 조절점을 부드러운 곡선(PCHIP)으로 이어 z 높이의 (반지름, 기울기)를 구한다.
    조절점마다 기울기가 0이 되는 보간(smoothstep)은 몸통에 물결 같은 굴곡을 만들어서 쓰지 않는다."""
    zc = np.clip(z, _TORSO_Z[0], _TORSO_Z[-1])
    inside = (z >= _TORSO_Z[0]) & (z <= _TORSO_Z[-1])
    return _TORSO_SPLINE(zc), np.where(inside, _TORSO_SLOPE(zc), 0.0)


def field_torso(p: np.ndarray) -> np.ndarray:
    x, y, z = p[:, 0], p[:, 1], p[:, 2]
    rho = np.sqrt(x * x + (y / TORSO_DEPTH_RATIO) ** 2)
    radius, slope = torso_radius_np(z)
    radial = (rho - radius) / np.sqrt(1.0 + slope * slope) * TORSO_DEPTH_RATIO
    d = smax(radial, TORSO_BOTTOM_Z - z, TORSO_EDGE_BLEND)
    return smax(d, z - TORSO_TOP_Z, TORSO_EDGE_BLEND)


def field_leg(p: np.ndarray, sx: float) -> np.ndarray:
    """바닥은 납작하고 모서리가 둥근 원기둥."""
    rho = np.hypot(p[:, 0] - sx * LEG_X, p[:, 1])
    half = LEG_TOP_Z / 2.0
    qx = rho - (LEG_RADIUS - LEG_EDGE_ROUND)
    qy = np.abs(p[:, 2] - half) - (half - LEG_EDGE_ROUND)
    return (np.minimum(np.maximum(qx, qy), 0.0)
            + np.hypot(np.maximum(qx, 0.0), np.maximum(qy, 0.0)) - LEG_EDGE_ROUND)


def arm_frame(sx: float):
    direction = np.array([sx * ARM_DIR[0], 0.0, ARM_DIR[2]])
    up = np.array([sx * math.sin(_DROOP), 0.0, math.cos(_DROOP)])
    origin = np.array([sx * ARM_ORIGIN_X, 0.0, ARM_ORIGIN_Z])
    return origin, direction, up


def field_arm(p: np.ndarray, sx: float) -> np.ndarray:
    """끝이 둥근 통통한 잎 모양 = 타원체 (어깨 쪽은 몸통 안에 묻힌다)."""
    origin, direction, up = arm_frame(sx)
    q = p - (origin + direction * ARM_CENTER_ALONG)
    local = np.stack([q @ direction, q @ up, q[:, 1]], axis=1)
    radii = np.array([ARM_LENGTH - ARM_CENTER_ALONG, ARM_RADIUS_Z, ARM_RADIUS_Y])
    k0 = np.linalg.norm(local / radii, axis=1)
    k1 = np.linalg.norm(local / (radii * radii), axis=1)
    return k0 * (k0 - 1.0) / np.maximum(k1, 1e-9)


def body_fields(p: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    """(합쳐진 거리, 부위별 거리 [N, 5])."""
    torso = field_torso(p)
    leg_l, leg_r = field_leg(p, 1.0), field_leg(p, -1.0)
    arm_l, arm_r = field_arm(p, 1.0), field_arm(p, -1.0)
    # 두 다리는 서로 섞지 않는다 (가랑이가 갈라진 채 유지되어야 한다)
    merged = smin(torso, np.minimum(leg_l, leg_r), BLEND_LEG)
    merged = smin(merged, np.minimum(arm_l, arm_r), BLEND_ARM)
    return merged, np.stack([torso, leg_l, leg_r, arm_l, arm_r], axis=1)


def region_weights(parts: np.ndarray) -> np.ndarray:
    """각 정점이 몸통/왼다리/오른다리/왼팔/오른팔에 속하는 정도 (합 1). 접합부에서는 부드럽게 섞인다."""
    w = np.exp(-(parts - parts.min(axis=1, keepdims=True)) / REGION_SOFTNESS)
    return w / w.sum(axis=1, keepdims=True)


def project_to_surface(verts: np.ndarray, iterations: int = 3, eps: float = 0.0015) -> np.ndarray:
    """격자 보간 오차를 줄이기 위해 각 정점을 거리 함수의 정확한 표면(거리 0)으로 옮긴다."""
    v = verts.copy()
    for _ in range(iterations):
        d = body_fields(v)[0]
        grad = np.stack([
            (body_fields(v + np.eye(3)[i] * eps)[0] - body_fields(v - np.eye(3)[i] * eps)[0]) / (2 * eps)
            for i in range(3)], axis=1)
        norm2 = np.maximum((grad * grad).sum(axis=1, keepdims=True), 1e-12)
        v = v - d[:, None] * grad / norm2
    return v


def build_body_mesh() -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    """단일 표면 메시 (정점, 삼각형 면, 정점별 부위 가중치 [N, 5])."""
    lo, hi = (np.array(b) for b in BODY_BOUNDS)
    counts = np.ceil((hi - lo) / VOXEL).astype(int) + 1
    axes = [lo[i] + np.arange(counts[i]) * VOXEL for i in range(3)]
    grid = np.stack(np.meshgrid(*axes, indexing="ij"), axis=-1).reshape(-1, 3)
    volume = body_fields(grid)[0].reshape(tuple(counts))
    verts, faces, _normals, _values = measure.marching_cubes(volume, level=0.0, spacing=(VOXEL, VOXEL, VOXEL))
    verts = project_to_surface(verts + lo)

    # 면의 방향이 바깥을 향하도록 맞춘다 (부호 있는 부피가 양수여야 한다)
    a, b, c = verts[faces[:, 0]], verts[faces[:, 1]], verts[faces[:, 2]]
    if np.einsum("ij,ij->i", a, np.cross(b, c)).sum() < 0.0:
        faces = faces[:, ::-1]

    regions = region_weights(body_fields(verts)[1])
    return verts, np.ascontiguousarray(faces), regions


# ---------------------------------------------------------------- 정점 가중치
def region_bone_weights(region: str, x: float, z: float) -> dict[str, float]:
    """한 부위 안에서 높이(또는 팔은 몸에서 떨어진 거리)에 따라 뼈에 나누는 가중치."""
    if region == "torso":
        pelvis = 1.0 - smoothstep(0.22, 0.30, z)
        return {"spine_01": 1.0 - pelvis, "pelvis": pelvis}
    side = region[-1]
    if region.startswith("leg"):
        upper = smoothstep(KNEE_Z - 0.03, KNEE_Z + 0.03, z)          # 무릎 위쪽일수록 1
        foot = 1.0 - smoothstep(ANKLE_Z - 0.015, ANKLE_Z + 0.015, z)  # 발목 아래쪽일수록 1
        return {f"thigh_{side}": upper, f"calf_{side}": max(0.0, 1.0 - upper - foot), f"foot_{side}": foot}
    reach = abs(x)  # 팔은 거의 수평이므로 몸에서 떨어진 거리로 구간을 나눈다
    upper = 1.0 - smoothstep(0.22, 0.30, reach)
    hand = smoothstep(0.35, 0.42, reach)
    return {f"upperarm_{side}": upper, f"lowerarm_{side}": max(0.0, 1.0 - upper - hand), f"hand_{side}": hand}


def body_vertex_weights(verts: np.ndarray, regions: np.ndarray) -> list[dict[str, float]]:
    out: list[dict[str, float]] = []
    for (x, _y, z), region_w in zip(verts, regions):
        merged: dict[str, float] = {}
        for name, rw in zip(REGIONS, region_w):
            if rw < MIN_WEIGHT:
                continue
            for bone, bw in region_bone_weights(name, x, z).items():
                merged[bone] = merged.get(bone, 0.0) + rw * bw
        kept = sorted(((b, w) for b, w in merged.items() if w >= MIN_WEIGHT), key=lambda kv: -kv[1])[:MAX_INFLUENCES]
        total = sum(w for _b, w in kept)
        out.append({b: w / total for b, w in kept})
    return out


# ---------------------------------------------------------------- 체격 모프 타깃 (몸)
def body_morphs(coords: np.ndarray, regions: np.ndarray) -> dict[str, np.ndarray]:
    x, y, z = coords[:, 0], coords[:, 1], coords[:, 2]
    w_torso, w_leg_l, w_leg_r, w_arm_l, w_arm_r = regions.T
    w_leg = w_leg_l + w_leg_r
    arm_side = w_arm_l - w_arm_r  # 왼팔 쪽이면 +, 오른팔 쪽이면 -
    out = {name: np.zeros_like(coords) for name in BODY_MORPHS}

    out["torso_wide"][:, 0] = x * TORSO_WIDE_X * w_torso + arm_side * TORSO_WIDE_ARM
    out["torso_wide"][:, 1] = y * TORSO_WIDE_Y * w_torso
    out["torso_slim"][:, 0] = -x * TORSO_SLIM_X * w_torso - arm_side * TORSO_SLIM_ARM

    for key, dz in (("legs_long", LEGS_LONG_DZ), ("legs_short", LEGS_SHORT_DZ)):
        # 다리: 발은 바닥에 두고 위쪽을 늘이거나 줄인다. 나머지(몸통·팔)는 통째로 위아래로 이동한다.
        out[key][:, 2] = dz * (w_leg * (z / LEG_TOP_Z) + (1.0 - w_leg))

    # 팔: 어깨를 기준으로 팔 방향으로만 늘이고 줄인다 (두께는 그대로)
    for key, factor in (("arms_long", ARMS_LONG), ("arms_short", ARMS_SHORT)):
        for sx, w_arm in ((1.0, w_arm_l), (-1.0, w_arm_r)):
            origin, direction, _up = arm_frame(sx)
            along = (coords - origin) @ direction
            out[key] += np.outer(w_arm * along * (factor - 1.0), direction)
    return out


# ---------------------------------------------------------------- Blender 객체
def make_mesh_object(name: str, verts, faces, collection) -> bpy.types.Object:
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([tuple(float(c) for c in v) for v in verts], [], [tuple(int(i) for i in f) for f in faces])
    mesh.update()
    for poly in mesh.polygons:
        poly.use_smooth = True
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    return obj


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
        point.co = point.co + Vector(tuple(float(c) for c in delta))


def head_morphs(coords: list[Vector]) -> dict[str, list[Vector]]:
    pivot = Vector((0.0, 0.0, NECK_Z))  # 머리 아래끝(목 자리)을 기준으로 키우고 줄인다
    return {
        "head_big": [(c - pivot) * (HEAD_BIG - 1.0) for c in coords],
        "head_small": [(c - pivot) * (HEAD_SMALL - 1.0) for c in coords],
        "legs_long": [Vector((0.0, 0.0, LEGS_LONG_DZ)) for _ in coords],
        "legs_short": [Vector((0.0, 0.0, LEGS_SHORT_DZ)) for _ in coords],
    }


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

    head = build_head()
    head_obj = make_mesh_object(HEAD_NAME, head.verts, head.faces, collection)
    body_verts, body_faces, body_regions = build_body_mesh()
    body_obj = make_mesh_object(BODY_NAME, body_verts, body_faces, collection)

    head_coords = [Vector(v) for v in head.verts]

    skin(head_obj, rig, [{"head": 1.0} for _ in head.verts])
    skin(body_obj, rig, body_vertex_weights(body_verts, body_regions))

    for obj in (head_obj, body_obj):
        obj.data.materials.append(material)

    head_obj.shape_key_add(name="Basis", from_mix=False)
    for name, offsets in head_morphs(head_coords).items():
        add_morph(head_obj, name, offsets)
    body_obj.shape_key_add(name="Basis", from_mix=False)
    for name, offsets in body_morphs(body_verts, body_regions).items():
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

    cam = setup_render(460, 520)
    set_morphs(objs, {})
    views = [("FRONT", Vector((0, -1, 0))), ("SIDE", Vector((1, 0, 0))), ("BACK", Vector((0, 1, 0)))]
    frames = []
    for label, direction in views:
        tmp = scratch / f"view_{label}.png"
        render_view(cam, tmp, direction, 1.3, 0.5)
        frames.append((label, Image.open(tmp).convert("RGB")))
    sheet = Image.new("RGB", (460 * 3, 520 + 28), (236, 236, 240))
    draw = ImageDraw.Draw(sheet)
    for i, (label, img) in enumerate(frames):
        sheet.paste(img, (460 * i, 28))
        draw.text((460 * i + 8, 8), label, fill=(40, 40, 50))
    sheet.save(out_dir / "mannequin_preview.png")

    cam = bpy.context.scene.camera
    setup_w, setup_h = 320, 360
    bpy.context.scene.render.resolution_x, bpy.context.scene.render.resolution_y = setup_w, setup_h
    variants = [("base", {})] + [(name, {name: 1.0}) for name in
                                 ["head_big", "head_small", "torso_wide", "torso_slim",
                                  "legs_long", "legs_short", "arms_long", "arms_short"]]
    strip = Image.new("RGB", (setup_w * len(variants), setup_h + 28), (236, 236, 240))
    draw = ImageDraw.Draw(strip)
    for i, (label, active) in enumerate(variants):
        set_morphs(objs, active)
        tmp = scratch / f"morph_{label}.png"
        render_view(cam, tmp, Vector((0, -1, 0)), 1.45, 0.58)
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
