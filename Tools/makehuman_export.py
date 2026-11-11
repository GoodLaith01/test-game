# Converts the CC0 MakeHuman base mesh + macro targets + default weights into compact binary files for the game.
#
# Usage: python3 Tools/makehuman_export.py <folder with MakeHuman files> Assets/Resources/Humans
# The folder needs, from https://github.com/makehumancommunity/makehuman (makehuman/data):
#   3dobjs/base.obj, rigs/default_weights.mhw, and from targets/macrodetails:
#   {caucasian,african,asian}-{male,female}-young.target and
#   universal-{male,female}-young-{min,average,max}muscle-{min,average,max}weight.target
# Add a line to `variants` below for another body shape, then load it with HumanMesh.Build(..., "<name>", ...).
import json, struct, sys, os
import numpy as np
SRC = sys.argv[1]; OUT = sys.argv[2]
os.makedirs(OUT, exist_ok=True)

verts = []; groups = {}; faces = []  # faces: (group, [vidx...])
g = None
for line in open(f'{SRC}/base.obj'):
    if line.startswith('v '):
        verts.append([float(x) for x in line.split()[1:4]])
    elif line.startswith('g '):
        g = line.split()[1]
    elif line.startswith('f '):
        idx = [int(p.split('/')[0]) - 1 for p in line.split()[1:]]
        faces.append((g, idx))
base = np.array(verts)
for gname, idx in faces:
    groups.setdefault(gname, set()).update(idx)

def target(name):
    d = np.zeros_like(base)
    for line in open(f'{SRC}/{name}.target'):
        if line.startswith('#') or not line.strip(): continue
        p = line.split(); d[int(p[0])] += [float(p[1]), float(p[2]), float(p[3])]
    return d

T = {}
def get(name):
    if name not in T: T[name] = target(name)
    return T[name]

def body(gender, muscle, weight):
    """gender 'male'/'female', muscle and weight in 0..1 like MakeHuman's macro sliders."""
    v = base.copy()
    for race in ('caucasian', 'african', 'asian'):
        v += get(f'{race}-{gender}-young') / 3.0
    # bilinear blend of the universal muscle/weight targets around the average
    def axis(val, lo, mid, hi):
        return [(lo, 1 - 2 * val), (mid, 2 * val)] if val < 0.5 else [(mid, 2 - 2 * val), (hi, 2 * val - 1)]
    for m, wm in axis(muscle, 'minmuscle', 'averagemuscle', 'maxmuscle'):
        for w, ww in axis(weight, 'minweight', 'averageweight', 'maxweight'):
            if wm * ww > 0: v += get(f'universal-{gender}-young-{m}-{w}') * wm * ww
    return v

# Bones of the game rig.
BONES = ['Body', 'Torso', 'Head', 'ArmL', 'ElbowL', 'WristL', 'ArmR', 'ElbowR', 'WristR', 'LegL', 'KneeL', 'AnkleL', 'LegR', 'KneeR', 'AnkleR']
def bone_for(n):
    side = None
    if n.endswith('.L'): side = 'L'
    if n.endswith('.R'): side = 'R'
    b = n.split('.')[0]
    if side:
        # MakeHuman's .L is the character's left.
        if b.startswith(('shoulder01', 'upperarm')): return 'Arm' + side
        if b.startswith('lowerarm'): return 'Elbow' + side
        if b.startswith(('wrist', 'metacarpal', 'finger')): return 'Wrist' + side
        if b.startswith('upperleg'): return 'Leg' + side
        if b.startswith('lowerleg'): return 'Knee' + side
        if b.startswith(('foot', 'toe')): return 'Ankle' + side
        if b.startswith(('pelvis', 'breast', 'clavicle', 'levator', 'special05')):
            return 'Torso'
        return 'Head'  # face muscles, eyes
    if b == 'root' or b.startswith(('spine', 'neck01', 'neck02')): return 'Torso'
    return 'Head'  # neck03, head, jaw, tongue, oris, special04

W = json.load(open(f'{SRC}/default_weights.mhw'))['weights']
nv = len(base)
wmat = np.zeros((nv, len(BONES)))
for bname, lst in W.items():
    bi = BONES.index(bone_for(bname))
    for vi, w in lst: wmat[vi, bi] += w

# Keep the skin and the eyeballs.
keep_groups = ['body', 'helper-l-eye', 'helper-r-eye']
used = sorted(set().union(*[groups[k] for k in keep_groups]))
remap = -np.ones(nv, int); remap[used] = np.arange(len(used))
def tris(gs):
    out = []
    for gname, idx in faces:
        if gname not in gs: continue
        r = [remap[i] for i in idx]
        # MakeHuman is right-handed; flipping x for Unity also flips the winding.
        if len(r) == 4: out += [(r[0], r[2], r[1]), (r[0], r[3], r[2])]
        else: out += [(r[0], r[2], r[1])]
    return np.array(out, np.int32)
skin_t = tris({'body'}); eye_t = tris({'helper-l-eye', 'helper-r-eye'})

W4i = np.zeros((len(used), 4), np.uint8); W4w = np.zeros((len(used), 4), np.float32)
for k, vi in enumerate(used):
    w = wmat[vi]
    if w.sum() <= 0: w = np.zeros(len(BONES)); w[1] = 1
    top = np.argsort(-w)[:4]; tw = w[top]; tw = tw / tw.sum()
    W4i[k] = top; W4w[k] = tw

def centre(v, name): return v[sorted(groups[name])].mean(axis=0)

# Regions for fitting clothes: same order as the game's BodyRegion enum.
REG = ['Head', 'Neck', 'Chest', 'Pelvis', 'UpperArm', 'Forearm', 'Hand', 'Thigh', 'Shin', 'Foot']

def export(name, v):
    v = v * 0.1                      # decimetres -> metres
    v[:, 0] = -v[:, 0]               # right-handed -> Unity's left-handed
    v[:, 1] -= v[:, 1][used].min()   # soles on the ground
    J = lambda n: centre(v, 'joint-' + n)
    joints = {
        'Torso': J('pelvis'), 'Head': J('neck'),
        'ArmL': J('l-shoulder'), 'ElbowL': J('l-elbow'), 'WristL': J('l-hand'),
        'ArmR': J('r-shoulder'), 'ElbowR': J('r-elbow'), 'WristR': J('r-hand'),
        'LegL': J('l-upper-leg'), 'KneeL': J('l-knee'), 'AnkleL': J('l-ankle'),
        'LegR': J('r-upper-leg'), 'KneeR': J('r-knee'), 'AnkleR': J('r-ankle'),
    }
    joints['Body'] = np.array([0.0, 0.0, 0.0])
    marks = {'GripL': (J('l-hand-2') + J('l-hand-3')) / 2, 'GripR': (J('r-hand-2') + J('r-hand-3')) / 2,
             'EyeL': J('l-eye'), 'EyeR': J('r-eye'), 'Mouth': J('mouth'), 'HeadTop': np.array([0, v[used][:, 1].max(), 0]),
             'Skull': J('head')}
    vv = v[used]
    # regions per vertex from dominant bone plus height cuts
    dom = np.argmax(W4w, 1); dom = W4i[np.arange(len(used)), dom]
    waist = joints['Torso'][1] + (joints['ArmL'][1] - joints['Torso'][1]) * 0.33
    neck_lo = joints['Head'][1] - 0.04
    reg = np.zeros(len(used), np.uint8)
    for k in range(len(used)):
        b = BONES[dom[k]]; y = vv[k, 1]
        if b == 'Head': r = 'Head' if y > joints['Head'][1] + 0.03 else 'Neck'
        elif b in ('Torso', 'Body'): r = 'Neck' if y > neck_lo else ('Chest' if y > waist else 'Pelvis')
        elif b.startswith('Arm'): r = 'UpperArm'
        elif b.startswith('Elbow'): r = 'Forearm'
        elif b.startswith('Wrist'): r = 'Hand'
        elif b.startswith('Leg'): r = 'Thigh'
        elif b.startswith('Knee'): r = 'Shin'
        else: r = 'Foot'
        reg[k] = REG.index(r)
    with open(f'{OUT}/{name}.bytes', 'wb') as f:
        f.write(struct.pack('<i', len(used)))
        f.write(vv.astype('<f4').tobytes())
        for bname in BONES: f.write(np.asarray(joints[bname], '<f4').tobytes())
        for m in ['GripL', 'GripR', 'EyeL', 'EyeR', 'Mouth', 'HeadTop', 'Skull']: f.write(np.asarray(marks[m], '<f4').tobytes())
    return reg, vv, joints, marks

variants = {
    'male': ('male', 0.5, 0.5), 'male_slim': ('male', 0.3, 0.2), 'male_muscular': ('male', 1.0, 0.55),
    'male_giant': ('male', 1.0, 0.85), 'female': ('female', 0.5, 0.5), 'female_slim': ('female', 0.35, 0.25),
}
regions = None; dump = {}
for name, (gen, m, w) in variants.items():
    reg, vv, joints, marks = export(name, body(gen, m, w))
    if regions is None: regions = reg
    dump[name] = (vv, joints, marks)
    print(name, 'height %.2f' % vv[:, 1].max(), 'hip %.2f' % joints['LegL'][1], 'shoulderL x %.3f' % joints['ArmL'][0], 'eyeL', marks['EyeL'].round(3))

with open(f'{OUT}/topology.bytes', 'wb') as f:
    f.write(struct.pack('<iii', len(used), len(skin_t), len(eye_t)))
    f.write(skin_t.astype('<i4').tobytes()); f.write(eye_t.astype('<i4').tobytes())
    f.write(W4i.tobytes()); f.write(W4w.astype('<f4').tobytes()); f.write(regions.tobytes())
print('verts', len(used), 'skin tris', len(skin_t), 'eye tris', len(eye_t))
