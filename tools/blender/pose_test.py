"""마네킹 뼈대를 움직여 보고 표면이 얼마나 늘어나거나 눌리는지 재고, 자세 이미지를 만든다 (진단용).

사용법: python3 tools/blender/pose_test.py <출력 폴더>
  <출력 폴더>/pose_sheet.png 에 자세별 이미지(비스듬히 / 옆), 표준 출력에 자세별 늘어남 수치를 낸다.
  소품을 손(hand_l)에 붙인 자세는 Unreal 의 소켓 방식을 흉내 낸 것이다 (마네킹에는 손가락이 없다).
"""
import math, sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import numpy as np
import bpy
import build_mannequin as bm
from mathutils import Matrix, Vector
from PIL import Image, ImageDraw

OUT = Path(sys.argv[1])
rig, head, body = bm.build_scene()
bm.set_morphs((head, body), {})

def rot_bone(name, axis, deg):
    pb = rig.pose.bones[name]
    M = pb.bone.matrix_local.to_3x3()
    R = Matrix.Rotation(math.radians(deg), 3, Vector(axis))
    pb.rotation_mode = "QUATERNION"
    pb.rotation_quaternion = (M.inverted() @ R @ M).to_quaternion()

def reset():
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"; pb.rotation_quaternion = (1, 0, 0, 0)

# 월드(=아마추어) 축 기준 회전. 캐릭터는 -Y 를 보고, 왼쪽이 +X.
POSES = {
    "rest": [],
    "arms_V_50": [("upperarm_l", (0, 1, 0), -50), ("upperarm_r", (0, 1, 0), +50)],
    "arms_forward_60": [("upperarm_l", (0, 0, 1), -60), ("upperarm_r", (0, 0, 1), +60)],
    "step_walk_35": [("thigh_l", (1, 0, 0), -35), ("thigh_r", (1, 0, 0), +35)],
    "sit_80": [("thigh_l", (1, 0, 0), -80), ("thigh_r", (1, 0, 0), -80)],
}
HOLD = {  # 소품을 손(hand_l)에 소켓처럼 붙여 보여줄 자세
    "hold_forward_60": [("upperarm_l", (0, 0, 1), -60)],
}

def mesh_edges_stats(rest_pos):
    ev = body.evaluated_get(bpy.context.evaluated_depsgraph_get()).data
    pos = np.empty(len(ev.vertices) * 3); ev.vertices.foreach_get("co", pos); pos = pos.reshape(-1, 3)
    ed = np.empty(len(ev.edges) * 2, dtype=np.int64); ev.edges.foreach_get("vertices", ed); ed = ed.reshape(-1, 2)
    rest_len = np.linalg.norm(rest_pos[ed[:, 0]] - rest_pos[ed[:, 1]], axis=1)
    now_len = np.linalg.norm(pos[ed[:, 0]] - pos[ed[:, 1]], axis=1)
    ratio = now_len / np.maximum(rest_len, 1e-9)
    return ratio, pos

cam = bm.setup_render(420, 460)
tmp = OUT / "tmp"; tmp.mkdir(parents=True, exist_ok=True)

reset(); bpy.context.view_layer.update()
rest_eval = body.evaluated_get(bpy.context.evaluated_depsgraph_get()).data
rest_pos = np.empty(len(rest_eval.vertices) * 3); rest_eval.vertices.foreach_get("co", rest_pos); rest_pos = rest_pos.reshape(-1, 3)

# 소품 (소켓 흉내): 막대 하나
prop_mesh = bpy.data.meshes.new("prop"); 
prop = bpy.data.objects.new("prop", prop_mesh)
bpy.context.scene.collection.objects.link(prop)
import bmesh
bmx = bmesh.new(); bmesh.ops.create_cube(bmx, size=1.0); bmx.to_mesh(prop_mesh); bmx.free()
prop.scale = (0.03, 0.03, 0.20)
mat = bpy.data.materials.new("PropM"); mat.diffuse_color = (0.9, 0.55, 0.15, 1); prop.data.materials.append(mat)
prop.hide_render = True; prop.hide_viewport = True

frames, report = [], []
all_poses = dict(POSES); all_poses.update(HOLD)
for name, ops in all_poses.items():
    reset()
    for b, axis, deg in ops: rot_bone(b, axis, deg)
    bpy.context.view_layer.update()
    ratio, pos = mesh_edges_stats(rest_pos)
    report.append((name, float(ratio.max()), float(ratio.min()), int((ratio > 1.5).sum()), int((ratio < 0.6).sum())))
    # 소품: hand_l 뼈 끝에 소켓처럼 붙인다
    if name.startswith("hold"):
        pb = rig.pose.bones["hand_l"]
        mat_w = rig.matrix_world @ pb.matrix
        prop.matrix_world = mat_w @ Matrix.Translation((0, pb.length * 0.9, 0))
        prop.scale = (0.03, 0.03, 0.20)
        prop.hide_render = False; prop.hide_viewport = False
    else:
        prop.hide_render = True
    for view, direction in (("3q", Vector((0.8, -0.8, 0.25))), ("side", Vector((1, 0, 0)))):
        path = tmp / f"{name}_{view}.png"
        bm.render_view(cam, path, direction, 1.35, 0.5)
        frames.append((f"{name} {view}", Image.open(path).convert("RGB")))
cols = 4
rows = (len(frames) + cols - 1) // cols
w, h = 420, 460
sheet = Image.new("RGB", (w * cols, (h + 20) * rows), (236, 236, 240))
d = ImageDraw.Draw(sheet)
for i, (label, img) in enumerate(frames):
    r, c = divmod(i, cols)
    sheet.paste(img, (w * c, (h + 20) * r + 20)); d.text((w * c + 6, (h + 20) * r + 4), label, fill=(40, 40, 50))
sheet.save(OUT / "pose_sheet.png")
print("REPORT pose  max_stretch  min_squash  edges>1.5x  edges<0.6x")
for r in report:
    print(f"REPORT {r[0]:16s} {r[1]:.2f}        {r[2]:.2f}        {r[3]:5d}      {r[4]:5d}")
