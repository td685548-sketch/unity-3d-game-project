#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Object = UnityEngine.Object;

public static class MotionPilotTool
{
    const string Root="Assets/_Game/Models/Characters/NhanVat/";
    public static string LastReport { get; private set; }
    static AnimationClip Clip(string path)
    {
        foreach(var a in AssetDatabase.LoadAllAssetsAtPath(path))
            if(a is AnimationClip && !a.name.StartsWith("__preview__")) return (AnimationClip)a;
        throw new Exception("No clip: "+path);
    }
    static void Layer(GameObject go) { foreach(var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=31; }
    [MenuItem("Phàm Kiếm Lục/Animation/Build direct rig pilot clips")]
    public static void BuildDirectClips()
    {
        foreach(var name in new[]{"NiemQuyet","PhongKiem"})
        {
            string fbx=Root+"MotionPilot/"+name+"_Rokoko_Pilot.fbx";
            var imp=(ModelImporter)AssetImporter.GetAtPath(fbx);imp.animationType=ModelImporterAnimationType.Generic;imp.SaveAndReimport();
            var source=Clip(fbx);var output=new AnimationClip();output.name=name+"_GameRig_Pilot";output.frameRate=30;
            foreach(var binding in AnimationUtility.GetCurveBindings(source))
            {
                var b=binding;if(!b.path.StartsWith("PilotTarget/"))continue;
                b.path=b.path.Substring("PilotTarget/".Length);
                AnimationUtility.SetEditorCurve(output,b,AnimationUtility.GetEditorCurve(source,binding));
            }
            output.EnsureQuaternionContinuity();
            string path=Root+"MotionPilot/"+name+"_GameRig_Pilot.anim";
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(existing!=null){EditorUtility.CopySerialized(output,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(output);}
            else AssetDatabase.CreateAsset(output,path);
        }
        AssetDatabase.SaveAssets();
    }
    [MenuItem("Phàm Kiếm Lục/Animation/Quay so sánh Rokoko Pilot")]
    public static void Record()
    {
        if(EditorApplication.isPlaying) throw new Exception("Stop Play before recording.");
        if(AnimationMode.InAnimationMode()) throw new Exception("Close other Animation preview first.");
        var oldCast=Clip(Root+"NhanVat@NiemQuyet.fbx"); var oldShot=Clip(Root+"NhanVat@PhongKiem2.fbx");
        var newCast=Clip(Root+"MotionPilot/NiemQuyet_GameRig_Pilot.anim"); var newShot=Clip(Root+"MotionPilot/PhongKiem_GameRig_Pilot.anim");
        var previous=SceneManager.GetActiveScene(); var preview=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        RenderTexture target=null; Texture2D pixels=null; Camera cam=null; PlayableGraph graph=default(PlayableGraph);
        try
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"NhanVat@Idle.fbx");
            var left=Object.Instantiate(prefab);left.name="Original";left.transform.position=new Vector3(1.05f,0,0);Layer(left);
            var right=Object.Instantiate(prefab);right.name="Rokoko Pilot";right.transform.position=new Vector3(-1.05f,0,0);Layer(right);
            foreach(var go in new[]{left,right}) {var a=go.GetComponent<Animator>();a.applyRootMotion=true;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;}
            Object.DestroyImmediate(right.GetComponent<Animator>());
            var skins=new[]{left.GetComponentInChildren<SkinnedMeshRenderer>(),right.GetComponentInChildren<SkinnedMeshRenderer>()};
            var baked=new[]{new Mesh(),new Mesh()};
            for(int k=0;k<2;k++)
            {
                skins[k].updateWhenOffscreen=true;
                var display=new GameObject("CPU baked preview");display.layer=31;display.transform.SetParent(skins[k].transform,false);
                display.AddComponent<MeshFilter>().sharedMesh=baked[k];display.AddComponent<MeshRenderer>().sharedMaterials=skins[k].sharedMaterials;
                skins[k].enabled=false;
            }
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.layer=31;ground.transform.position=new Vector3(0,-0.02f,0);
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=new Color(.13f,.15f,.18f);ground.GetComponent<Renderer>().sharedMaterial=mat;
            var light=new GameObject("Pilot light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(45,-25,0);light.cullingMask=1<<31;
            cam=new GameObject("Pilot camera").AddComponent<Camera>();cam.cullingMask=1<<31;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.045f,.055f,.075f);cam.orthographic=true;cam.orthographicSize=1.22f;
            cam.transform.position=new Vector3(0,1.15f,5.5f);cam.transform.LookAt(new Vector3(0,.9f,0));
            target=new RenderTexture(960,600,24);pixels=new Texture2D(960,600,TextureFormat.RGB24,false);cam.targetTexture=target;
            string folder=Path.GetFullPath("Library/MotionPilot/UnityCompare");Directory.CreateDirectory(folder);
            graph=PlayableGraph.Create("MotionPilotComparison");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var oldPlayable=AnimationClipPlayable.Create(graph,oldCast);
            oldPlayable.SetApplyFootIK(false);
            var oldOutput=AnimationPlayableOutput.Create(graph,"Original",left.GetComponent<Animator>());oldOutput.SetSourcePlayable(oldPlayable);
            graph.Play();
            for(int f=0;f<216;f++)
            {
                float t=f/24f;bool cast=t<6;float local=cast?t:t-6;
                var original=cast?oldCast:oldShot;var pilot=cast?newCast:newShot;
                if(f==144)
                {
                    oldPlayable.Destroy();
                    oldPlayable=AnimationClipPlayable.Create(graph,oldShot);
                    oldOutput.SetSourcePlayable(oldPlayable);
                    oldPlayable.SetApplyFootIK(false);
                }
                oldPlayable.SetTime(local/pilot.length*original.length);
                graph.Evaluate(1f/24f);
                pilot.SampleAnimation(right,Mathf.Min(local,pilot.length));
                left.transform.position=new Vector3(1.05f,0,0);
                var animator=left.GetComponent<Animator>();
                float footY=Mathf.Min(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y,animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y);
                left.transform.position+=Vector3.up*(0.09f-footY);right.transform.position=new Vector3(-1.05f,0,0);right.transform.rotation=Quaternion.identity;
                for(int k=0;k<2;k++) skins[k].BakeMesh(baked[k]);
                cam.Render();var oldRT=RenderTexture.active;
                try {RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,960,600),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(folder,f.ToString("D4")+".png"),pixels.EncodeToPNG());}
                finally {RenderTexture.active=oldRT;}
            }
            LastReport="216 frames at 24 fps; cast 6s + shot 3s. Original clips normalized to pilot duration; original root grounded for side-by-side presentation. Direct game-rig clips; human="+newCast.humanMotion+","+newShot.humanMotion+". Frames: "+folder;
            Object.DestroyImmediate(mat);foreach(var mesh in baked)Object.DestroyImmediate(mesh);
        }
        finally
        {
            if(graph.IsValid()) graph.Destroy();
            if(cam!=null) cam.targetTexture=null;
            if(target!=null) Object.DestroyImmediate(target);if(pixels!=null) Object.DestroyImmediate(pixels);
            EditorSceneManager.CloseScene(preview,true);SceneManager.SetActiveScene(previous);
        }
    }
}
#endif
