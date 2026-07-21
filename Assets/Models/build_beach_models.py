# -*- coding: utf-8 -*-
"""
Unityサマーチャレンジ 全アセット統合生成スクリプト（Blender 3.6〜4.x対応）
==========================================================================
これまでの2本のスクリプトを1本に統合したものです。
  ・build_beach_models.py      … 環境アセット（スイカ・パラソル・ゲーム機・棒）
  ・modify_uploaded_penguin.py … 実モデルの痩身化＋くちばし黄色化＋金縁サングラス
procedural（手続き生成）だったペンギンは、実モデルを使う方式に一本化したため
このスクリプトには含まれません。

生成物（export/ に出力）:
  penguin_slim.fbx             … やせたペンギン（1匹用。目隠しプレイヤー等）
  penguin_slim_sunglasses.fbx  … やせ + 金縁サングラス（5体用。観客+ゲーマー）
    ※どちらも羽が Flipper_R / Flipper_L という子オブジェクトに分離されており、
      肩を原点に回転できます（ボーンは使わず、剛体の子として動かす方式）。
      不要な場合は SPLIT_FLIPPERS = False にすると従来通り1枚メッシュになります。
  watermelon.fbx               … スイカ（丸ごと）
  watermelon_halves.fbx        … 割れたスイカ（左右2オブジェクト）
  parasol.fbx                  … ビーチパラソル
  console.fbx                  … 携帯ゲーム機（画面は "ScreenMat" マテリアル）
  stick.fbx                    … スイカ割りの棒

使い方:
  1. Blenderで新規ファイルを、アップロード元の penguin.fbx と同じフォルダに保存
  2. 下記の TEX_DIR / FBX_NAME を環境に合わせて確認・変更する
  3. [Scripting] → 新規 → このスクリプトを貼り付け → 実行(Alt+P)
  4. blendファイルと同じ場所の export/ に7つのFBXが出力される
  5. Window → Toggle System Console でログを確認できます

※アップロード元モデル(Seaeees様)の改変モデルは、再配布・販売不可
  （自作ゲームでの使用はOK。規約はモデル同梱のReadmeを参照）
"""
import bpy, bmesh, os, math, random
from mathutils import Vector

# ================= 調整パラメータ =================
# 同梱 textures フォルダ（Unityプロジェクトの Assets/Textures を直接指定）
TEX_DIR = r"C:\Users\kuta\OneDrive - keio.jp\デスクトップ\プログラミング\Unity Hub\My project\Assets\Textures"

# アップロード元ペンギンFBX（blendファイルと同じフォルダに置く）
FBX_NAME = "penguin.fbx"

# --- ペンギンの痩身化 ---
SLIM_LR = 0.84           # 左右方向の痩せ具合
SLIM_FB = 0.90           # 前後方向（お腹）の痩せ具合

# --- サングラス（くちばし先端を基準に配置） ---
BEAK_BACK = 0.10         # くちばし先端から後ろへ（全高比）
BEAK_UP = 0.045          # くちばし先端から上へ（全高比）
GLASSES_SCALE = 1.4      # サングラス全体の大きさ倍率（1.0→1.4に拡大）
GLASSES_TILT_DEG = 30    # レンズの傾き（上端が後ろへ倒れる向き）。つるは常に水平
GLASSES_TWEAK = (0.0, 0.0, 0.0)  # 位置の微調整（前+/横+/上+ の順、全高比）

# --- くちばしの黄色化 ---
YELLOW_BEAK = True
BEAK_COLOR = (1.0, 0.82, 0.10)   # 黄色
# くちばし判定の深さ（先端からどこまでを"くちばし"とみなすか。全高比）
# 胴体と同じマテリアルを共有していても、この範囲のポリゴンだけを
# 新しい黄色マテリアルに個別に割り当て直すので、胴体は元の色のまま残る
BEAK_REGION_DEPTH = 0.075

FRONT_OVERRIDE = "+X"    # このモデルのくちばしは+X方向。"AUTO"にすると自動判定

# --- 羽（フリッパー）の切り出し ---
# ボーンを持たないモデルでも羽を動かせるように、羽のポリゴンだけを
# 別オブジェクトへ切り離し、肩を原点にして胴体に親子付けする。
# Unity側では Flipper_R / Flipper_L を回転させるだけで羽が動く。
SPLIT_FLIPPERS = True
# 羽とみなす左右方向のしきい値（胴体の左右半幅に対する比率）。
# 大きくすると羽の先端だけ、小さくすると胴体まで巻き込む。
FLIPPER_SIDE_RATIO = 0.62
# 羽とみなす高さ帯（全高に対する比率、下端0.0〜上端1.0）。
# 上限を上げすぎると頭やサングラスのつるを巻き込むので注意。
FLIPPER_Z_MIN = 0.30
FLIPPER_Z_MAX = 0.78
# 肩（回転の中心）を羽の上端からどれだけ下げるか（羽自身の高さに対する比率）
FLIPPER_PIVOT_DROP = 0.08
# ペンギンFBXの出力で Apply Transform を使うか。
# 親子構造があるとき True だと子の向きが崩れることがあるため既定は False。
# Unityで読み込んだペンギンが90度倒れている場合だけ True を試す。
PENGUIN_BAKE_TRANSFORM = False
# ==================================================


# =========================================================
# コンテキスト安全化ユーティリティ（"context is incorrect"対策）
# =========================================================
def get_view3d_override():
    """有効な3Dビューポートのコンテキストを確保する。
    見つからない場合は既存のエリアを一時的にVIEW_3Dへ切り替える（後で戻す）。
    戻り値: (window, area, region, restore_type or None)"""
    win = bpy.context.window_manager.windows[0]
    screen = win.screen
    area = next((a for a in screen.areas if a.type == 'VIEW_3D'), None)
    restore_type = None
    if area is None:
        area = screen.areas[0]
        restore_type = area.type
        area.type = 'VIEW_3D'
    region = next(r for r in area.regions if r.type == 'WINDOW')
    return win, area, region, restore_type


def ensure_object_mode(override):
    """編集モードのオブジェクトが残っていたら強制的にオブジェクトモードへ戻す"""
    with bpy.context.temp_override(**override):
        obj = bpy.context.view_layer.objects.active
        if obj is not None and obj.mode != 'OBJECT':
            bpy.ops.object.mode_set(mode='OBJECT')


def clean_scene():
    """オペレータを使わず、データAPIで直接シーンを空にする
    （編集モードのオブジェクトが残っていてもコンテキストエラーが起きない）"""
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


# =========================================================
# マテリアル（日本語UIでも壊れないノード検索方式に統一）
# =========================================================
MATS = {}
def mat(name, color, rough=0.6, metal=0.0, emit=0.0, tex=None):
    """Principledマテリアルを取得/作成。tex指定時は画像をBaseColorに接続。
    ノードは名前("Principled BSDF")ではなく種類(type)で検索するため、
    Blenderの表示言語が日本語などでも壊れない。"""
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if bsdf is None:
        bsdf = m.node_tree.nodes.new("ShaderNodeBsdfPrincipled")
        output = next((n for n in m.node_tree.nodes if n.type == 'OUTPUT_MATERIAL'), None)
        if output is None:
            output = m.node_tree.nodes.new("ShaderNodeOutputMaterial")
        m.node_tree.links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    if emit > 0 and "Emission Strength" in bsdf.inputs:
        bsdf.inputs["Emission Strength"].default_value = emit
    if tex:
        path = os.path.join(TEX_DIR, tex)
        if os.path.exists(path):
            img = bpy.data.images.load(path)
            node = m.node_tree.nodes.new("ShaderNodeTexImage")
            node.image = img
            m.node_tree.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
        else:
            print(f"[警告] テクスチャが見つかりません: {path} → 単色で代用します")
    MATS[name] = m
    return m


# マテリアル定義（環境アセット用）
MELON_S = lambda: mat("MelonSkin", (0.30, 0.55, 0.22), rough=0.35, tex="watermelon_skin.png")
MELON_I = lambda: mat("MelonInside", (0.90, 0.25, 0.28), rough=0.7)
SEED    = lambda: mat("MelonSeed", (0.08, 0.05, 0.04), rough=0.4)
PARA_R  = lambda: mat("ParasolRed", (0.85, 0.15, 0.15), rough=0.7)
PARA_W  = lambda: mat("ParasolWhite", (0.95, 0.93, 0.88), rough=0.7)
POLE    = lambda: mat("ParasolPole", (0.75, 0.75, 0.78), rough=0.4, metal=0.6)
BODY_G  = lambda: mat("ConsoleGray", (0.10, 0.10, 0.11), rough=0.45)
PAD_B   = lambda: mat("PadBlue", (0.0, 0.55, 0.85), rough=0.45)
PAD_R   = lambda: mat("PadRed", (0.90, 0.15, 0.20), rough=0.45)
SCREEN  = lambda: mat("ScreenMat", (0.02, 0.02, 0.02), rough=0.1)  # Unity側でRenderTextureを割当
WOOD    = lambda: mat("Wood", (0.45, 0.30, 0.16), rough=0.8)
# マテリアル定義（ペンギン改変用）
GOLD    = lambda: mat("SunglassGold", (1.00, 0.78, 0.15), rough=0.05, metal=1.0)
YELLOW  = lambda: mat("BeakYellow", BEAK_COLOR, rough=0.55, metal=0.0)


# =========================================================
# 形状ヘルパー（環境アセット用）
# =========================================================
def sphere(name, loc, scale, material, segs=32, rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segs, ring_count=rings,
                                         radius=1.0, location=loc)
    o = bpy.context.object
    o.name = name
    o.scale = scale
    o.data.materials.append(material)
    bpy.ops.object.shade_smooth()
    return o

def cone(name, loc, r1, depth, material, rot=(0, 0, 0), verts=24, r2=0.0):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2,
                                    depth=depth, location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(material)
    bpy.ops.object.shade_smooth()
    return o

def cyl(name, loc, r, depth, material, rot=(0, 0, 0), verts=24):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth,
                                        location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(material)
    bpy.ops.object.shade_smooth()
    return o

def box(name, loc, dims, material, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    o = bpy.context.object
    o.name = name
    o.scale = dims
    o.data.materials.append(material)
    if bevel > 0:
        bpy.ops.object.transform_apply(scale=True)
        b = o.modifiers.new("Bevel", 'BEVEL')
        b.width = bevel
        b.segments = 3
        bpy.ops.object.modifier_apply(modifier="Bevel")
        if hasattr(bpy.ops.object, "shade_smooth_by_angle"):
            bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
        else:
            bpy.ops.object.shade_smooth()
    return o

def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    return obj


# =========================================================
# 環境アセット: スイカ
# =========================================================
def build_watermelon():
    m = sphere("watermelon", (0, 0, 0.14), (0.17, 0.17, 0.145), MELON_S(), segs=48, rings=24)
    bpy.ops.object.transform_apply(scale=True)
    return m

def build_melon_half(name, mirror):
    h = sphere(name, (0, 0, 0.14), (0.17, 0.17, 0.145), MELON_S(), segs=48, rings=24)
    bpy.ops.object.transform_apply(scale=True)
    h.data.materials.append(MELON_I())
    bpy.ops.object.mode_set(mode='EDIT')
    bm = bmesh.from_edit_mesh(h.data)
    bmesh.ops.bisect_plane(
        bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:],
        plane_co=(0, 0, 0.14), plane_no=(mirror, 0, 0),
        clear_inner=True, use_snap_center=False)

    # 切り口に蓋を張る。
    # bisect_plane の戻り値 geom_cut を使うと、clear_inner で削除された
    # ジオメトリへの無効な参照が混ざり、holes_fill が何もしないことがある
    # （＝断面が空洞のままになる）。切断後に実際に開いている境界エッジを
    # 取り直すことで確実に塞ぐ。
    bm.edges.ensure_lookup_table()
    boundary = [e for e in bm.edges if e.is_boundary]
    caps = bmesh.ops.holes_fill(bm, edges=boundary)

    # holes_fill が作る面の法線は外向きとは限らず、内向きだと
    # Unityの裏面カリングで断面が見えなくなるため、法線を計算し直す
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])

    for f in caps["faces"]:
        f.material_index = 1  # MelonInside（赤い断面）
    print(f"      {name}: 断面 {len(caps['faces'])} 面を作成")
    bmesh.update_edit_mesh(h.data)
    bpy.ops.object.mode_set(mode='OBJECT')
    parts = [h]
    random.seed(3 if mirror > 0 else 5)
    for i in range(9):
        r = random.uniform(0.03, 0.12)
        a = random.uniform(0, 2 * math.pi)
        y, z = r * math.cos(a), 0.14 + r * math.sin(a) * 0.8
        s = sphere("seed", (mirror * 0.002, y, z), (0.004, 0.009, 0.013), SEED(), segs=8, rings=6)
        parts.append(s)
    return join(parts, name)


# =========================================================
# 環境アセット: パラソル
# =========================================================
def build_parasol():
    parts = []
    canopy = cone("canopy", (0, 0, 1.80), 1.15, 0.55, PARA_R(), verts=16, r2=0.04)
    canopy.data.materials.append(PARA_W())
    bpy.ops.object.mode_set(mode='EDIT')
    bm = bmesh.from_edit_mesh(canopy.data)
    bm.faces.ensure_lookup_table()
    for f in [f for f in bm.faces if f.normal.z < -0.95]:
        bm.faces.remove(f)
    side = sorted([f for f in bm.faces],
                  key=lambda f: math.atan2(f.calc_center_median().y, f.calc_center_median().x))
    for i, f in enumerate(side):
        f.material_index = (i // 2) % 2
    bmesh.update_edit_mesh(canopy.data)
    bpy.ops.object.mode_set(mode='OBJECT')
    parts.append(canopy)
    parts.append(cyl("pole", (0, 0, 1.0), 0.028, 2.0, POLE()))
    parts.append(sphere("tip", (0, 0, 2.10), (0.05, 0.05, 0.05), POLE(), segs=16, rings=8))
    return join(parts, "parasol")


# =========================================================
# 環境アセット: 携帯ゲーム機・棒
# =========================================================
def screen_plane(name, loc, width, height, material):
    """ゲーム機の画面用の板。

    Cube の1面を画面に使うと、Blenderの既定UVが面ごとに向きが揃っておらず、
    UnityでRenderTextureを貼ったときに映像が90度回って表示されてしまう。
    そこで「XY平面のPlane（U=ローカルX / V=ローカルY の素直なUV）」を作り、
    X軸まわりに90度倒して -Y 向き（＝ゲーム機の正面）にする。
    こうすると U=横・V=縦 が確定し、映像が正しい向きで映る。

    倒したあとの対応:
      ローカルX（U・横） → ワールドX（画面の横方向）
      ローカルY（V・縦） → ワールドZ（画面の上方向）
    """
    bpy.ops.mesh.primitive_plane_add(size=1.0, location=loc)
    o = bpy.context.object
    o.name = name
    o.scale = (width, height, 1.0)                 # 先に横幅・縦幅を決めてから
    o.rotation_euler = (math.radians(90), 0, 0)    # 法線 +Z → -Y（正面向き）
    bpy.ops.object.transform_apply(rotation=True, scale=True)
    o.data.materials.append(material)
    return o


def build_console():
    parts = []
    parts.append(box("consoleBody", (0, 0, 0), (0.145, 0.014, 0.085), BODY_G(), bevel=0.004))
    # 画面は板で作る（UVの向きを確定させるため。詳細は screen_plane を参照）
    scr = screen_plane("screen", (0, -0.0096, 0), 0.125, 0.068, SCREEN())
    parts.append(scr)
    parts.append(box("padL", (-0.088, 0, 0), (0.032, 0.016, 0.085), PAD_B(), bevel=0.008))
    parts.append(box("padR", (0.088, 0, 0), (0.032, 0.016, 0.085), PAD_R(), bevel=0.008))
    for dz in (0.020, -0.020):
        parts.append(cyl("btnL", (-0.088, -0.011, dz), 0.009, 0.007, BODY_G(),
                         rot=(math.radians(90), 0, 0), verts=16))
    parts.append(box("dpadV", (0.088, -0.011, 0.016), (0.007, 0.006, 0.026), BODY_G(), bevel=0.002))
    parts.append(box("dpadH", (0.088, -0.011, 0.016), (0.026, 0.006, 0.007), BODY_G(), bevel=0.002))
    parts.append(cyl("btnR", (0.088, -0.011, -0.024), 0.008, 0.007, BODY_G(),
                     rot=(math.radians(90), 0, 0), verts=16))
    return join(parts, "console")

def build_stick():
    s = cyl("stick", (0, 0, 0.26), 0.014, 0.52, WOOD(), verts=16)
    bpy.ops.object.transform_apply(location=False)
    return s


# =========================================================
# ペンギン: 実モデルの読み込み・軸検出・複製
# =========================================================
def import_penguin(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    imported = [o for o in set(bpy.data.objects) - before if o.type == 'MESH']
    if not imported:
        raise RuntimeError("FBXからメッシュを読み込めませんでした")
    bpy.ops.object.select_all(action='DESELECT')
    for o in imported:
        o.select_set(True)
    bpy.context.view_layer.objects.active = imported[0]
    if len(imported) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.name = "penguin_base"
    return obj

def world_bbox(obj):
    pts = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    mins = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    maxs = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return mins, maxs

def get_up_axis(obj):
    """バウンディングボックスが最も高い軸を鉛直(up)とみなす"""
    mins, maxs = world_bbox(obj)
    dims = maxs - mins
    if dims.z >= dims.x and dims.z >= dims.y:
        return Vector((0, 0, 1))
    elif dims.y >= dims.x:
        return Vector((0, 1, 0))
    else:
        return Vector((1, 0, 0))

def set_origin_bottom_center(obj):
    mins, maxs = world_bbox(obj)
    bpy.context.scene.cursor.location = Vector(((mins.x + maxs.x) / 2,
                                                (mins.y + maxs.y) / 2, mins.z))
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')

def detect_axes(obj):
    """左右軸=対称な水平軸。正面=上半身で水平方向に最も突き出た頂点(くちばし)の向き"""
    mins, maxs = world_bbox(obj)
    center = (mins + maxs) / 2
    sym_x = abs((maxs.x - center.x) - (center.x - mins.x))
    sym_y = abs((maxs.y - center.y) - (center.y - mins.y))
    lr_axis = 'X' if sym_x <= sym_y else 'Y'

    if FRONT_OVERRIDE != "AUTO":
        d = {'+X': Vector((1, 0, 0)), '-X': Vector((-1, 0, 0)),
             '+Y': Vector((0, 1, 0)), '-Y': Vector((0, -1, 0))}[FRONT_OVERRIDE]
        return lr_axis, d

    z_lo = mins.z + 0.55 * (maxs.z - mins.z)
    fb_axis = 'Y' if lr_axis == 'X' else 'X'
    mw = obj.matrix_world
    best = 0.0; sign = 1.0
    for v in obj.data.vertices:
        p = mw @ v.co
        if p.z < z_lo:
            continue
        d = (p.x - center.x) if fb_axis == 'X' else (p.y - center.y)
        if abs(d) > best:
            best = abs(d); sign = math.copysign(1, d)
    if fb_axis == 'X':
        return lr_axis, Vector((sign, 0, 0))
    return lr_axis, Vector((0, sign, 0))

def duplicate(obj, name):
    """データAPIで独立したコピーを作る（メッシュも複製するので干渉しない）"""
    new_obj = obj.copy()
    new_obj.data = obj.data.copy()
    linked = False
    for coll in obj.users_collection:
        coll.objects.link(new_obj)
        linked = True
    if not linked:
        bpy.context.scene.collection.objects.link(new_obj)
    new_obj.name = name
    return new_obj

def slim(obj, lr_axis):
    set_origin_bottom_center(obj)
    obj.scale = (SLIM_LR, SLIM_FB, 1.0) if lr_axis == 'X' else (SLIM_FB, SLIM_LR, 1.0)
    bpy.ops.object.transform_apply(scale=True)


# =========================================================
# ペンギン: くちばしの黄色化（ポリゴン個別割り当て方式）
# =========================================================
def find_beak_polygons(obj, front, up):
    """くちばし先端に近いポリゴンだけを集める。胴体と同じマテリアルを
    共有していても、この一部分だけに絞れるよう「上半身」かつ
    「先端からBEAK_REGION_DEPTH*H以内」のポリゴンのみを選ぶ。
    見つからない場合は範囲を自動で広げてリトライする。"""
    mesh = obj.data
    mw = obj.matrix_world
    mins, maxs = world_bbox(obj)
    H = (maxs - mins).dot(up)
    up_min_val = mins.dot(up) + 0.55 * H

    tip_val = None
    for v in mesh.vertices:
        p = mw @ v.co
        if p.dot(up) < up_min_val:
            continue
        f = p.dot(front)
        if tip_val is None or f > tip_val:
            tip_val = f
    if tip_val is None:
        return [], 0.0

    centroids = []
    for poly in mesh.polygons:
        verts = [mw @ mesh.vertices[vi].co for vi in poly.vertices]
        centroids.append(sum(verts, Vector((0, 0, 0))) / len(verts))

    depth = BEAK_REGION_DEPTH
    for attempt in range(6):
        threshold = tip_val - depth * H
        idxs = [i for i, poly in enumerate(mesh.polygons)
                if centroids[i].dot(up) >= up_min_val and centroids[i].dot(front) >= threshold]
        if idxs:
            return idxs, depth
        depth *= 1.6
    return [], depth

def yellowify_beak(obj, front, up):
    """くちばし先端付近のポリゴンだけを新しい黄色マテリアルに割り当て直す。
    胴体と同じマテリアルを共有していても、胴体側は元の色のまま残る。"""
    mesh = obj.data
    print("      --- マテリアル一覧（変更前）---")
    for idx, slot in enumerate(mesh.materials):
        face_count = sum(1 for p in mesh.polygons if p.material_index == idx)
        print(f"        [{idx}] '{slot.name if slot else '(空)'}': {face_count}面")

    idxs, depth = find_beak_polygons(obj, front, up)
    if not idxs:
        print("      [注意] くちばし付近のポリゴンが見つかりませんでした")
        return 0

    beak_mat = YELLOW()
    if beak_mat.name not in mesh.materials:
        mesh.materials.append(beak_mat)
    new_idx = list(mesh.materials).index(beak_mat)

    for i in idxs:
        mesh.polygons[i].material_index = new_idx

    print(f"      → くちばし先端から深さ{depth:.3f}(全高比)以内の{len(idxs)}面を"
          f" 'BeakYellow' (マテリアル[{new_idx}]) に割り当て")
    mesh.update()
    return len(idxs)


# =========================================================
# ペンギン: 金縁サングラス
# =========================================================
def beak_metrics(obj, front):
    """くちばし先端（上半身で最も前方の頂点）と、目の高さ帯の頭幅を実測。
    side(左右軸)は front と鉛直up の外積で求める。"""
    mins, maxs = world_bbox(obj)
    up = get_up_axis(obj)
    H = (maxs - mins).dot(up)
    side = front.cross(up).normalized()

    z_lo_val = mins.dot(up) + 0.55 * H
    mw = obj.matrix_world
    beak = None; best = None
    for v in obj.data.vertices:
        p = mw @ v.co
        if p.dot(up) < z_lo_val:
            continue
        f = p.dot(front)
        if best is None or f > best:
            best = f; beak = p.copy()

    center = beak - front * (BEAK_BACK * H) + up * (BEAK_UP * H)

    c_up = center.dot(up)
    s_min = s_max = None
    for v in obj.data.vertices:
        p = mw @ v.co
        if abs(p.dot(up) - c_up) > 0.04 * H:
            continue
        s = p.dot(side)
        s_min = s if s_min is None else min(s_min, s)
        s_max = s if s_max is None else max(s_max, s)
    head_w = (s_max - s_min) if s_min is not None else 0.2 * H
    return center, head_w, H, side, up

def build_sunglasses(center, head_w, front, side, up, H):
    """ワールド座標に直接、金縁サングラスを組み立てる（レンズも金色）。
    レンズ面はfrontに正対し、GLASSES_TILT_DEGだけ前傾（上端が後ろへ）する。"""
    w = head_w * GLASSES_SCALE
    lens_r = 0.18 * w
    lens_off = 0.26 * w
    thick = 0.09 * w
    tilt = math.radians(GLASSES_TILT_DEG)
    normal = (front * math.cos(tilt) + up * math.sin(tilt)).normalized()

    parts = []

    def place_cylinder(radius, depth, pos, axis, material, smooth=True):
        bpy.ops.mesh.primitive_cylinder_add(vertices=22, radius=radius, depth=depth,
                                            location=(0, 0, 0))
        o = bpy.context.object
        z = Vector((0, 0, 1))
        rot = z.rotation_difference(axis)
        o.rotation_euler = rot.to_euler()
        o.location = pos
        o.data.materials.append(material)
        if smooth:
            bpy.ops.object.shade_smooth()
        parts.append(o)
        return o

    def place_box(size_vec, pos, x_axis, material):
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0))
        o = bpy.context.object
        o.scale = size_vec
        x = Vector((1, 0, 0))
        o.rotation_euler = x.rotation_difference(x_axis).to_euler()
        o.location = pos
        o.data.materials.append(material)
        parts.append(o)
        return o

    for sgn in (-1, 1):
        lens_center = center + side * (sgn * lens_off)
        rim = place_cylinder(lens_r, thick, lens_center, normal, GOLD())
        rim.scale = (1.0, 0.82, 1.0)
        place_cylinder(lens_r * 0.80, thick * 1.2, lens_center, normal, GOLD())
    place_box((lens_off * 0.9, thick * 0.5, lens_r * 0.28),
              center + up * (lens_r * 0.55), side, GOLD())
    arm_len = 0.7 * w
    back = (-normal).normalized()
    for sgn in (-1, 1):
        temple_start = center + side * (sgn * (lens_off + lens_r * 0.9)) + up * (lens_r * 0.25)
        arm_center = temple_start + back * (arm_len * 0.5)
        place_box((arm_len * 0.5, 0.030 * w, 0.045 * w), arm_center, back, GOLD())

    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    g = bpy.context.view_layer.objects.active
    g.name = "sunglasses"
    return g

def attach_sunglasses(pen, front):
    center, head_w, H, side, up = beak_metrics(pen, front)
    tweak_dir = front * GLASSES_TWEAK[0] + side * GLASSES_TWEAK[1] + up * GLASSES_TWEAK[2]
    center = center + tweak_dir * H
    g = build_sunglasses(center, head_w, front, side, up, H)
    bpy.ops.object.select_all(action='DESELECT')
    pen.select_set(True)
    g.select_set(True)
    bpy.context.view_layer.objects.active = pen
    bpy.ops.object.join()


# =========================================================
# ペンギン: 羽（フリッパー）の切り出し
# =========================================================
def find_flipper_polygons(obj, front, up):
    """羽のポリゴンを左右それぞれ集める。
    「胴体の中心線から左右に大きく張り出していて、かつ上半身の高さ帯にある」
    ポリゴンを羽とみなす。ボーンが無いモデルでも羽を可動部として切り出せる。

    戻り値: (右羽のポリゴン番号, 左羽のポリゴン番号, 診断情報dict)
    """
    mesh = obj.data
    mw = obj.matrix_world
    side = front.cross(up).normalized()   # ペンギンから見て「右」方向

    mins, maxs = world_bbox(obj)
    H = (maxs - mins).dot(up)
    base_val = mins.dot(up)

    # 胴体の中心線（左右方向）と最大半幅を実測
    s_all = [(mw @ v.co).dot(side) for v in mesh.vertices]
    s_center = (min(s_all) + max(s_all)) / 2.0
    half_w = max(abs(s - s_center) for s in s_all)

    lo = base_val + FLIPPER_Z_MIN * H
    hi = base_val + FLIPPER_Z_MAX * H
    thr = FLIPPER_SIDE_RATIO * half_w

    right, left = [], []
    for i, poly in enumerate(mesh.polygons):
        verts = [mw @ mesh.vertices[vi].co for vi in poly.vertices]
        c = sum(verts, Vector((0, 0, 0))) / len(verts)
        u = c.dot(up)
        if u < lo or u > hi:
            continue
        d = c.dot(side) - s_center
        if d > thr:
            right.append(i)
        elif d < -thr:
            left.append(i)

    info = {"H": H, "half_w": half_w, "thr": thr,
            "band": (lo - base_val, hi - base_val), "total": len(mesh.polygons)}
    return right, left, info


def _separate_by_polygons(obj, idxs, new_name):
    """指定したポリゴンを別オブジェクトへ切り出して返す（失敗時 None）"""
    if not idxs:
        return None
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    # OBJECTモードで選択状態を作ってから EDIT へ入る（選択の取りこぼしを防ぐ）
    bpy.ops.object.mode_set(mode='OBJECT')
    for p in obj.data.polygons:
        p.select = False
    for i in idxs:
        obj.data.polygons[i].select = True

    before = set(bpy.data.objects)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_mode(type='FACE')
    bpy.ops.mesh.separate(type='SELECTED')
    bpy.ops.object.mode_set(mode='OBJECT')

    created = [o for o in set(bpy.data.objects) - before]
    if not created:
        return None
    part = created[0]
    part.name = new_name
    return part


def _set_shoulder_origin(part, body_center_s, front, up):
    """切り出した羽の原点を「肩」に置く。
    肩＝羽の上端付近で、もっとも胴体に近い側（内側）。ここが回転の中心になる。"""
    side = front.cross(up).normalized()
    mins, maxs = world_bbox(part)
    h = (maxs - mins).dot(up)

    # 上端から少しだけ下げた高さ
    up_val = maxs.dot(up) - FLIPPER_PIVOT_DROP * h
    # 左右方向は「胴体中心に近い側」＝内側の端
    s_lo, s_hi = mins.dot(side), maxs.dot(side)
    s_val = s_lo if abs(s_lo - body_center_s) < abs(s_hi - body_center_s) else s_hi
    # 前後方向は中央
    f_val = (mins.dot(front) + maxs.dot(front)) / 2.0

    # 3方向の成分からワールド座標を組み立てる
    loc = side * s_val + up * up_val + front * f_val

    bpy.context.scene.cursor.location = loc
    bpy.ops.object.select_all(action='DESELECT')
    part.select_set(True)
    bpy.context.view_layer.objects.active = part
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    return loc


def separate_flippers(obj, front, up, label=""):
    """羽を左右2つのオブジェクトへ切り出し、肩を原点にして obj の子にする。
    戻り値: 切り出した羽オブジェクトのリスト（0〜2個）"""
    if not SPLIT_FLIPPERS:
        return []

    side = front.cross(up).normalized()
    mw = obj.matrix_world
    s_all = [(mw @ v.co).dot(side) for v in obj.data.vertices]
    body_center_s = (min(s_all) + max(s_all)) / 2.0

    right_idx, left_idx, info = find_flipper_polygons(obj, front, up)
    print(f"      --- 羽の切り出し{label} ---")
    print(f"        全高={info['H']:.3f} 左右半幅={info['half_w']:.3f} "
          f"しきい値={info['thr']:.3f}")
    print(f"        高さ帯={info['band'][0]:.3f}〜{info['band'][1]:.3f} "
          f"(全{info['total']}面)")
    print(f"        右羽={len(right_idx)}面 / 左羽={len(left_idx)}面")

    if not right_idx or not left_idx:
        print("        [注意] 羽が検出できませんでした。"
              "FLIPPER_SIDE_RATIO を小さく(例0.5)してみてください")
        return []
    if len(right_idx) + len(left_idx) > info['total'] * 0.5:
        print("        [注意] 選択が多すぎます（胴体を巻き込んでいる可能性）。"
              "FLIPPER_SIDE_RATIO を大きく(例0.75)してみてください")

    parts = []
    # 右→左の順に切り出す（先に切ると番号がずれるため、都度取り直す）
    r = _separate_by_polygons(obj, right_idx, "Flipper_R")
    if r:
        parts.append(r)
    # 左羽は右羽を切った後のメッシュで番号を取り直す
    _, left_idx2, _ = find_flipper_polygons(obj, front, up)
    l = _separate_by_polygons(obj, left_idx2, "Flipper_L")
    if l:
        parts.append(l)

    for p in parts:
        loc = _set_shoulder_origin(p, body_center_s, front, up)
        print(f"        {p.name} の肩(原点) = {tuple(round(v, 3) for v in loc)}")

    # 胴体に親子付け（見た目の位置を保ったまま）
    if parts:
        bpy.ops.object.select_all(action='DESELECT')
        for p in parts:
            p.select_set(True)
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.parent_set(type='OBJECT', keep_transform=True)

    return parts


# =========================================================
# 共通: FBXエクスポート
# =========================================================
def export_fbx(objs, filename, folder, bake=True):
    """bake=False は親子構造を持つオブジェクト用。
    Apply Transform(bake_space_transform) は親子があると子の向きを崩すことがある。"""
    if not isinstance(objs, (list, tuple)):
        objs = [objs]
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(folder, filename),
        use_selection=True,
        apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=bake,
        path_mode='COPY', embed_textures=True)


# =========================================================
# メイン処理
# =========================================================
def run():
    base_dir = bpy.path.abspath("//") or os.path.expanduser("~")
    out = os.path.join(base_dir, "export")
    os.makedirs(out, exist_ok=True)

    clean_scene()
    y_gap = 0.0  # ビューポート内での並べ配置用オフセット

    # ---------- ペンギン（実モデルを痩身化＋くちばし黄色化＋サングラス） ----------
    fbx_path = os.path.join(base_dir, FBX_NAME)
    if not os.path.exists(fbx_path):
        raise FileNotFoundError(
            f"{fbx_path} が見つかりません。blendファイルを{FBX_NAME}と同じフォルダに保存してください")

    print("[1/6] penguin.fbx を読み込み中...")
    base = import_penguin(fbx_path)
    lr_axis, front = detect_axes(base)
    up = get_up_axis(base)
    print(f"      検出: 左右軸={lr_axis} 正面={front.to_tuple(1)} 上方向={up.to_tuple(1)}")

    if YELLOW_BEAK:
        print("      くちばしを黄色化中...")
        yellowify_beak(base, front, up)

    print("[2/6] スリム版（1匹用）を作成中...")
    slim_pen = duplicate(base, "penguin_slim")
    slim(slim_pen, lr_axis)

    # サングラス版の元は「羽を切り出す前」の状態から複製する。
    # attach_sunglasses が join() を使うため、先に羽を分離してしまうと
    # 羽が胴体に再結合されてしまうため。
    glasses_pen = duplicate(slim_pen, "penguin_slim_sunglasses")

    slim_parts = separate_flippers(slim_pen, front, up, "（スリム版）")
    export_fbx([slim_pen] + slim_parts, "penguin_slim.fbx", out,
               bake=PENGUIN_BAKE_TRANSFORM)
    print("      → penguin_slim.fbx 出力OK")

    print("[3/6] サングラス版（5体用）を作成中...")
    try:
        attach_sunglasses(glasses_pen, front)
        glasses_parts = separate_flippers(glasses_pen, front, up, "（サングラス版）")
        export_fbx([glasses_pen] + glasses_parts, "penguin_slim_sunglasses.fbx", out,
                   bake=PENGUIN_BAKE_TRANSFORM)
        print("      → penguin_slim_sunglasses.fbx 出力OK")
    except Exception as e:
        import traceback
        traceback.print_exc()
        print(f"      !! サングラス取り付けでエラー: {e}")

    mins, maxs = world_bbox(base)
    pen_w = (maxs.x - mins.x) * 1.4
    slim_pen.location.y = 2
    glasses_pen.location.y = 3

    # ---------- 環境アセット ----------
    print("[4/6] スイカを作成中...")
    melon = build_watermelon()
    export_fbx(melon, "watermelon.fbx", out)
    melon.location.y = 4

    hl = build_melon_half("melon_half_L", 1)
    hr = build_melon_half("melon_half_R", -1)
    export_fbx([hl, hr], "watermelon_halves.fbx", out)
    hl.location.y = 4.6
    hr.location.y = 4.6
    hr.location.x = 0.5

    print("[5/6] パラソル・ゲーム機・棒を作成中...")
    para = build_parasol()
    export_fbx(para, "parasol.fbx", out)
    para.location.y = 6

    con = build_console()
    export_fbx(con, "console.fbx", out)
    con.location.y = 7

    st = build_stick()
    export_fbx(st, "stick.fbx", out)
    st.location.y = 7.5

    print("[6/6] 完了レポート")
    print(f"\n=== 完了: {out} に7つのFBXを出力しました ===")
    print("  penguin_slim.fbx / penguin_slim_sunglasses.fbx")
    print("  watermelon.fbx / watermelon_halves.fbx")
    print("  parasol.fbx / console.fbx / stick.fbx")
    if SPLIT_FLIPPERS:
        print("\n  ペンギンFBXには Flipper_R / Flipper_L が子オブジェクトとして")
        print("  含まれます。Unity側でこれを回転させると羽が動きます。")
        print("  切り出しがおかしい場合は FLIPPER_SIDE_RATIO / FLIPPER_Z_MIN /")
        print("  FLIPPER_Z_MAX を調整して再実行してください（上のログの面数が目安）")
    print("  ※オブジェクトが重なって見える場合はビューポートで Home キー（全体表示）")


def main():
    win, area, region, restore_type = get_view3d_override()
    override = {"window": win, "area": area, "region": region}
    ensure_object_mode(override)
    try:
        with bpy.context.temp_override(**override):
            run()
    finally:
        if restore_type is not None:
            area.type = restore_type


main()