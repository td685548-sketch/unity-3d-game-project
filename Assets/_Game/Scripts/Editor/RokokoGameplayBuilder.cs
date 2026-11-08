#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using Object=UnityEngine.Object;

/// <summary>Bakes the verified direct rig poses to humanoid muscles using the game's own Avatar.</summary>
public static class RokokoGameplayBuilder
{
    public const string Root="Assets/_Game/Models/Characters/NhanVat/";
    public static string Report {get;private set;}
    public const float GazePitchDegrees=3f;
    [MenuItem("Phàm Kiếm Lục/Animation/Apply Rokoko gameplay animations")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
        var chant=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"MotionPilot/NiemQuyet_Rokoko.anim");
        var release=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"MotionPilot/PhongKiem_Rokoko.anim");
        if(chant==null||release==null||!chant.humanMotion||!release.humanMotion)throw new Exception("Bake humanoid clips first.");
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Root+"NhanVat_Animator.controller");
        Undo.RecordObject(controller,"Apply Rokoko animations");
        foreach(var layer in controller.layers)foreach(var child in layer.stateMachine.states)
        {
            var state=child.state;
            if(state.name=="NiemQuyet")
            {
                Undo.RecordObject(state,"Replace chanting motion");state.motion=chant;state.speed=1.5f;
                foreach(var transition in state.transitions)if(transition.hasExitTime)
                {transition.exitTime=.92f;transition.duration=.22f;EditorUtility.SetDirty(transition);}
                EditorUtility.SetDirty(state);
            }
            if(state.name=="TayGiu"||state.name=="TayThu")
            {Undo.RecordObject(state,"Replace sword release motion");state.motion=release;EditorUtility.SetDirty(state);}
        }
        EditorUtility.SetDirty(controller);
        foreach(var component in Object.FindObjectsByType<PlayerAnimator>())
        {
            var so=new SerializedObject(component);so.FindProperty("shootPoseClip").objectReferenceValue=release;
            so.FindProperty("shootKeyTime").floatValue=1.066667f;so.FindProperty("raiseTime").floatValue=.36f;
            so.FindProperty("retractSpeed").floatValue=1.4f;so.FindProperty("retractEndClipTime").floatValue=3f;
            so.FindProperty("retractBlendTime").floatValue=.2f;so.FindProperty("kickForearmAngle").floatValue=2f;
            so.FindProperty("kickHandAngle").floatValue=2f;so.FindProperty("kickTime").floatValue=.16f;
            so.ApplyModifiedProperties();EditorUtility.SetDirty(component);
        }
        AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Report="Applied chant/hold/retract humanoid clips; release gate .36s; source hold at 1.066667s.";
    }
    [MenuItem("Phàm Kiếm Lục/Animation/Build Rokoko humanoid clips")]
    public static void Bake()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"NhanVat@Idle.fbx");
        var avatar=prefab.GetComponent<Animator>().avatar;
        var report=new System.Text.StringBuilder();
        foreach(var name in new[]{"NiemQuyet","PhongKiem"})
        {
            var source=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"MotionPilot/"+name+"_GameRig_Pilot.anim");
            var go=Object.Instantiate(prefab);go.hideFlags=HideFlags.HideAndDontSave;
            Object.DestroyImmediate(go.GetComponent<Animator>());
            var handler=new HumanPoseHandler(avatar,go.transform);
            Transform head=null,neck=null;
            foreach(var bone in go.GetComponentsInChildren<Transform>())
            {if(bone.name=="mixamorig:Head")head=bone;if(bone.name=="mixamorig:Neck")neck=bone;}
            if(head==null||neck==null)throw new Exception("Missing head/neck for gaze correction.");
            Vector3 faceAxis=Quaternion.Inverse(head.rotation)*go.transform.forward;
            try
            {
                int count=HumanTrait.MuscleCount;
                var curves=new AnimationCurve[count+7];for(int c=0;c<curves.Length;c++)curves[c]=new AnimationCurve();
                var pose=new HumanPose();Quaternion previous=Quaternion.identity;
                for(int frame=0;frame<=Mathf.RoundToInt(source.length*30);frame++)
                {
                    float t=frame/30f;source.SampleAnimation(go,t);
                    // Lift the gaze in clip space, sharing the correction across neck and head.
                    // Preserve the source yaw/roll and ease to the untouched rest pose at both ends.
                    Vector3 gaze=head.rotation*faceAxis;
                    Vector3 flat=Vector3.ProjectOnPlane(gaze,Vector3.up);
                    if(flat.sqrMagnitude>.001f)
                    {
                        float pitch=Mathf.Atan2(gaze.y,flat.magnitude)*Mathf.Rad2Deg;
                        float envelope=Mathf.SmoothStep(0f,1f,Mathf.Min(t/.4f,(source.length-t)/.4f));
                        float angle=Mathf.Clamp(pitch-GazePitchDegrees,-40f,25f)*envelope;
                        Vector3 axis=Vector3.Cross(Vector3.up,flat.normalized);
                        neck.rotation=Quaternion.AngleAxis(angle*.55f,axis)*neck.rotation;
                        head.rotation=Quaternion.AngleAxis(angle*.45f,axis)*head.rotation;
                    }
                    handler.GetHumanPose(ref pose);
                    for(int m=0;m<count;m++)curves[m].AddKey(new Keyframe(t,pose.muscles[m]));
                    Vector3 pos=pose.bodyPosition;Quaternion q=pose.bodyRotation;
                    if(Quaternion.Dot(previous,q)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);previous=q;
                    for(int k=0;k<3;k++)curves[count+k].AddKey(new Keyframe(t,pos[k]));
                    for(int k=0;k<4;k++)curves[count+3+k].AddKey(new Keyframe(t,q[k]));
                }
                // Smooth muscle velocity between the 30 fps samples without overshooting joint poses.
                for(int m=0;m<count;m++)for(int k=0;k<curves[m].length;k++)
                {AnimationUtility.SetKeyLeftTangentMode(curves[m],k,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curves[m],k,AnimationUtility.TangentMode.ClampedAuto);}
                var clip=new AnimationClip();clip.name=name+"_Rokoko";clip.frameRate=30;
                for(int m=0;m<count;m++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),HumanTrait.MuscleName[m]),curves[m]);
                for(int k=0;k<3;k++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),"RootT."+"xyz"[k]),curves[count+k]);
                for(int k=0;k<4;k++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),"RootQ."+"xyzw"[k]),curves[count+3+k]);
                clip.EnsureQuaternionContinuity();
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;settings.loopBlendPositionY=true;settings.loopBlendPositionXZ=true;settings.loopBlendOrientation=true;settings.keepOriginalPositionY=true;settings.keepOriginalPositionXZ=true;settings.keepOriginalOrientation=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
                string path=Root+"MotionPilot/"+clip.name+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if(existing!=null){EditorUtility.CopySerialized(clip,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(clip);clip=existing;}
                else AssetDatabase.CreateAsset(clip,path);
                if(!clip.humanMotion)throw new Exception("Failed humanoid conversion: "+name);
                report.AppendLine(clip.name+" human="+clip.humanMotion+" seconds="+clip.length+" curves="+AnimationUtility.GetCurveBindings(clip).Length);
            }
            finally{handler.Dispose();Object.DestroyImmediate(go);}
        }
        AssetDatabase.SaveAssets();Report=report.ToString();
    }
}
#endif
