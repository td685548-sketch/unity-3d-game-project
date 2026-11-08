"""Run in Blender after importing the raw Rokoko FBX and the game's NiemQuyet rig."""
import bpy, math, json, os
from mathutils import Vector, Quaternion
root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
src = bpy.data.objects['mixamorig:Reference']
tgt = bpy.data.objects['PilotTarget']
scene = bpy.context.scene
base = bpy.data.actions['PilotTarget_BaseIdle']
tgt.animation_data.action=base
base.use_fake_user = True
bpy.context.scene.frame_set(1)
base_pose = {p.name:(p.rotation_quaternion.copy() if p.rotation_mode == 'QUATERNION' else p.matrix_basis.to_quaternion(), p.location.copy(), p.scale.copy()) for p in tgt.pose.bones}
upper = [n for n in tgt.pose.bones.keys() if n.split(':')[-1] in ['Spine','Spine1','Spine2','Neck','Head','LeftShoulder','LeftArm','LeftForeArm','LeftHand','RightShoulder','RightArm','RightForeArm','RightHand']]
sw,tw = src.matrix_world.to_quaternion(),tgt.matrix_world.to_quaternion()
offsets={}
for n in upper:
 sr=sw@src.data.bones[n].matrix_local.to_quaternion()
 tr=tw@tgt.data.bones[n].matrix_local.to_quaternion()
 align=(tr@Vector((0,1,0))).rotation_difference(sr@Vector((0,1,0)))
 offsets[n]=sr.inverted()@align@tr
# Sample absolute rotations, correct bone roll, preserve shortest-arc continuity.
samples=[]
for frame in range(2,302):
 scene.frame_set(frame)
 row={n:tw.inverted()@sw@src.pose.bones[n].matrix.to_quaternion()@offsets[n] for n in upper}
 if samples:
  for n,q in row.items():
   if q.dot(samples[-1][n])<0:q.negate()
 samples.append(row)
# Symmetric five-frame smoothing: no phase lag and no Euler wrap at +/-180 degrees.
smooth=[]
for i,row in enumerate(samples):
 dst={}
 for n in upper:
  q=Quaternion((0,0,0,0)); total=0
  for delta,w in [(-2,1),(-1,2),(0,3),(1,2),(2,1)]:
   v=samples[max(0,min(len(samples)-1,i+delta))][n].copy()
   if v.dot(row[n])<0:v.negate()
   for k in range(4):q[k]+=v[k]*w
   total+=w
  q.normalize();dst[n]=q
 smooth.append(dst)
output=os.path.join(root,'Assets','_Game','Models','Characters','NhanVat','MotionPilot')
os.makedirs(output,exist_ok=True)
result=[]
for action_name,start,end in [('NiemQuyet_Rokoko_Pilot',31,211),('PhongKiem_Rokoko_Pilot',211,301)]:
 old=bpy.data.actions.get(action_name)
 if old:
  if tgt.animation_data.action==old:tgt.animation_data.action=None
  bpy.data.actions.remove(old)
 action=bpy.data.actions.new(action_name);action.use_fake_user=True
 tgt.animation_data.action=action
 previous_quats={}
 for f in range(start,end+1):
  out=f-start+1;scene.frame_set(out)
  for p in tgt.pose.bones:
   p.rotation_mode='QUATERNION';p.rotation_quaternion=base_pose[p.name][0];p.location=base_pose[p.name][1];p.scale=base_pose[p.name][2]
  bpy.context.view_layer.update()
  for n in upper:
   p=tgt.pose.bones[n];parent=p.parent
   desired=smooth[f-2][n]
   rest_local=(parent.bone.matrix_local.inverted()@p.bone.matrix_local) if parent else p.bone.matrix_local
   parent_rot=parent.matrix.to_quaternion() if parent else Quaternion()
   basis=rest_local.to_quaternion().inverted()@parent_rot.inverted()@desired
   blend=min(1.0,(out-1)/12.0,(end-start+1-out)/12.0)
   blend=max(0,blend);blend=blend*blend*(3-2*blend)
   p.rotation_quaternion=base_pose[n][0].slerp(basis,blend)
   bpy.context.view_layer.update()
  for p in tgt.pose.bones:
   if p.name in previous_quats:p.rotation_quaternion.make_compatible(previous_quats[p.name])
   previous_quats[p.name]=p.rotation_quaternion.copy()
   p.keyframe_insert('rotation_quaternion',frame=out,group=p.name)
   p.keyframe_insert('location',frame=out,group=p.name)
 for layer in action.layers:
  for strip in layer.strips:
   for bag in strip.channelbags:
    for fc in bag.fcurves:
     for key in fc.keyframe_points:key.interpolation='LINEAR'
 scene.frame_start=1;scene.frame_end=end-start+1;scene.render.fps=30
 bpy.ops.object.select_all(action='DESELECT');tgt.select_set(True)
 for o in bpy.data.objects:
  if o.type=='MESH' and any(m.type=='ARMATURE' and m.object==tgt for m in o.modifiers):o.select_set(True)
 bpy.context.view_layer.objects.active=tgt
 path=os.path.join(output,action_name+'.fbx')
 bpy.ops.export_scene.fbx(filepath=path,use_selection=True,apply_scale_options='FBX_SCALE_ALL',apply_unit_scale=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0.0,axis_forward='-Z',axis_up='Y')
 result.append({'action':action_name,'source_frames':[start,end],'frames':end-start+1,'seconds':(end-start)/30,'fbx':path})
# Keep target at the chanting action for manual review; source rig remains available separately.
tgt.animation_data.action=bpy.data.actions['NiemQuyet_Rokoko_Pilot'];scene.frame_start=1;scene.frame_end=181;scene.frame_set(65)
for o in bpy.data.objects:
 if o.name.startswith('mixamorig:'):o.hide_render=True;o.hide_set(True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(root,'ArtSource','Blender','NiemQuyet_Retarget_Pilot.blend'))
open(os.path.join(root,'ArtSource','MotionPipeline','retarget_result.json'),'w',encoding='utf-8').write(json.dumps(result,ensure_ascii=False,indent=2))
print(json.dumps(result,ensure_ascii=False))
