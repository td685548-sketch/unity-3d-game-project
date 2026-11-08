#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Records a repeatable gameplay exchange in the real SanTap scene, then restores runtime state.</summary>
public static class TienHiepPreviewTool
{
    private static bool swordsOnly, motionGameplay;
    private static bool sawChant, sawHold, sawRetract;
    private static float motionRootY;
    private static readonly System.Text.StringBuilder motionReport = new System.Text.StringBuilder();
    public static string MotionReport => motionReport.ToString();
    [MenuItem("Phàm Kiếm Lục/Animation/Quay và kiểm tra animation gameplay")]
    public static void RunMotionGameplay()
    {
        if (!EditorApplication.isPlaying || Running) return;
        motionGameplay=true;swordsOnly=true;sawChant=sawHold=sawRetract=false;motionReport.Clear();
        Run();
    }
    private static void MotionCheck(bool ok,string name)
    {motionReport.AppendLine((ok?"PASS ":"FAIL ")+name);}
    private static int Pursuing()
    {int n=0;foreach(var sword in SwordSwarm.Instance.Swords)if(sword.HasPursuitCommand)n++;return n;}
    private static void MotionTick()
    {
        var stats=PlayerStats.Instance;var swarm=SwordSwarm.Instance;var a=stats.GetComponentInChildren<Animator>();
        int layer=a.GetLayerIndex("UpperBody");var state=a.GetCurrentAnimatorStateInfo(layer);
        sawChant|=state.shortNameHash==Animator.StringToHash("NiemQuyet");
        sawHold|=state.shortNameHash==Animator.StringToHash("TayGiu");
        sawRetract|=state.shortNameHash==Animator.StringToHash("TayThu");
        if(frame==20)motionRootY=stats.transform.position.y;
        if(frame==106)
        {
            typeof(SwordSwarm).GetMethod("RequestSingle",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(swarm,null);
            MotionCheck(Pursuing()==0,"first shot waits for hand motion");
        }
        if(frame==112)MotionCheck(Pursuing()==0,"no launch before .36 second gate");
        if(frame==120)MotionCheck(Pursuing()==1,"first sword launches after gate");
        if(frame==130)
        {
            typeof(SwordSwarm).GetMethod("RequestSingle",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(swarm,null);
            MotionCheck(Pursuing()>=2,"follow-up shot fires immediately from held pose");
        }
        if(frame==155)
        {
            MotionCheck(state.shortNameHash==Animator.StringToHash("TayGiu"),"hand stays raised during pursuit");
            MotionCheck(Mathf.Abs(a.GetFloat("ShootHoldTime")-1.066667f/3f)<.002f,"hold frame matches new release clip");
            MotionCheck(Mathf.Abs(stats.transform.position.y-motionRootY)<.04f,"upper body motion does not move player root");
        }
        if(frame==170){swarm.RecallAll();MotionCheck(Pursuing()==0,"R recalls all deployed swords");}
        if(frame==253)
        {
            MotionCheck(sawChant,"new chant state plays");MotionCheck(sawHold,"new hold state plays");MotionCheck(sawRetract,"new retract state plays");
            MotionCheck(state.shortNameHash==Animator.StringToHash("Trong"),"returns to locomotion after recall");
            bool correct=true;foreach(var clip in a.runtimeAnimatorController.animationClips)
                if(clip.name=="NiemQuyet_Rokoko"||clip.name=="PhongKiem_Rokoko")correct&=clip.humanMotion;
            MotionCheck(correct,"replacement clips are humanoid");
            System.IO.File.WriteAllText("Library/MotionPilot/GameplayValidation.txt",MotionReport);
        }
    }
    private static Camera camera;
    private static RenderTexture target;
    private static Texture2D pixels;
    private static GameObject opponent, incoming;
    private static readonly List<GameObject> hidden = new List<GameObject>();
    private static Vector3 originalPosition;
    private static Quaternion originalRotation;
    private static bool god, mana, enemyGod, ai, active, noCooldown;
    private static CameraOrbit orbit;
    private static float originalYaw;
    private static int originalSwordCount;
    private static int originalRealm;
    private static bool originalMoveLock;
    private static int oldCaptureRate, lastFrame, frame;
    private static float deadline;
    private static string folder;
    public static bool Running => camera != null;
    public static string Status { get; private set; }

    [MenuItem("Phàm Kiếm Lục/Đấu phi kiếm/Quay phi kiếm trong Play")]
    public static void RunSwords()
    {
        if (!EditorApplication.isPlaying || Running) return;
        swordsOnly = true;
        Run();
    }

    [MenuItem("Phàm Kiếm Lục/Thần thông/Quay trình diễn VFX trong Play")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || Running) return;
        var stats = PlayerStats.Instance; var skill = ThanThongCombat.Instance;
        if (stats == null || skill == null) return;
        originalPosition = stats.transform.position; originalRotation = stats.transform.rotation;
        originalRealm = stats.RealmIndex; originalMoveLock = stats.GetComponent<PlayerController>().MoveLocked;
        stats.GetComponent<PlayerController>().MoveLocked = true;
        god = AdminPanel.MauVoHan; mana = AdminPanel.PhapLucVoHan; enemyGod = AdminPanel.MauDichVoHan; ai = EnemyBase.AIEnabled;
        active = SwordSwarm.Instance.IsActive; oldCaptureRate = Time.captureFramerate;
        originalSwordCount = SwordSwarm.Instance.SwordCount; noCooldown = AdminPanel.KhongHoiChieu; AdminPanel.KhongHoiChieu = true;
        AdminPanel.MauVoHan = AdminPanel.PhapLucVoHan = AdminPanel.MauDichVoHan = true; EnemyBase.AIEnabled = false;
        foreach (var enemy in Object.FindObjectsByType<EnemyBase>()) { hidden.Add(enemy.gameObject); enemy.gameObject.SetActive(false); }
        SwordSwarm.Instance.SetActive(false, false);
        stats.GetComponent<PlayerController>().TeleportTo(new Vector3(-24, 0.1f, -25), Quaternion.Euler(0, 90, 0));
        orbit = Object.FindAnyObjectByType<CameraOrbit>();
        if (orbit != null)
        {
            originalYaw = orbit.Yaw;
            typeof(CameraOrbit).GetField("yaw", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(orbit, 90f);
        }
        opponent = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/TaTu.prefab"), new Vector3(-13, 0.1f, -25), Quaternion.Euler(0, 270, 0));
        opponent.name = "VFXPreview_TaTu";
        SwordSwarm.Instance.LockTarget(opponent.GetComponent<Damageable>());
        var go = new GameObject("VFXPreview_Camera"); camera = go.AddComponent<Camera>(); camera.CopyFrom(Camera.main); camera.enabled = false;
        camera.ResetProjectionMatrix(); camera.lensShift = Vector2.zero; camera.aspect = 1.6f;
        camera.transform.position = new Vector3(-18.5f, 3.2f, -12.5f); camera.transform.LookAt(new Vector3(-18.5f, 1.7f, -25)); camera.fieldOfView = 43;
        var data = go.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = true;
        var sourceData = Camera.main.GetComponent<UniversalAdditionalCameraData>(); if (sourceData != null) data.volumeLayerMask = sourceData.volumeLayerMask;
        target = new RenderTexture(960, 600, 24); pixels = new Texture2D(960, 600, TextureFormat.RGB24, false); camera.targetTexture = target;
        folder = Path.GetFullPath("Library/TienHiepPreview/" + System.DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")); Directory.CreateDirectory(folder);
        frame = 0; lastFrame = -1; deadline = Time.realtimeSinceStartup + 55;
        Time.captureFramerate = 24; Application.runInBackground = true; Status = "Recording " + folder;
        ThanThongCombat.PreviewFrame += Tick;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.realtimeSinceStartup > deadline) { Status = "Preview interrupted"; Finish(); return; }
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            var skill = ThanThongCombat.Instance;
            if (swordsOnly)
            {
                if (frame == 2)
                {
                    PlayerStats.Instance.SetRealmIndex(2);
                    SwordSwarm.Instance.SwordCount = 20;
                    SwordSwarm.Instance.SetActive(true, true);
                    var enemy = opponent.GetComponent<TaTu>();
                    foreach (var blade in enemy.Blades) blade.gameObject.SetActive(false);
                }
                if (!motionGameplay && frame == 30) SwordSwarm.Instance.FireVolley();
            }
            if (motionGameplay) MotionTick();
            if (!swordsOnly && frame == 2) skill.BeginWard();
            if (!swordsOnly && frame == 18)
            {
                incoming = new GameObject("VFXPreview_Orb");
                TienHiepVfx.Heart(incoming.transform, skill.auraMaterial, new Color(1, 0.48f, 0.08f, 0.8f));
                var trail = incoming.AddComponent<TrailRenderer>(); trail.sharedMaterial = skill.auraMaterial; trail.time = 0.35f;
                trail.widthMultiplier = 0.09f; trail.startColor = new Color(1, 0.5f, 0.08f, 0.65f); trail.endColor = new Color(1, 0.3f, 0.04f, 0);
                incoming.AddComponent<EnemyOrb>().Launch(opponent.transform.position + Vector3.up * 1.3f,
                    PlayerStats.Instance.transform.position + Vector3.up * 1.2f, 20, 1, PlayerStats.Instance);
            }
            if (!swordsOnly && frame == 40) skill.EndWard(false);
            if (!swordsOnly && frame == 45) skill.CastLotus();
            if (!swordsOnly && frame == 105)
            {
                PlayerStats.Instance.SetRealmIndex(2); SwordSwarm.Instance.SwordCount = 16;
                SwordSwarm.Instance.SetActive(true, true);
            }
            if (!swordsOnly && frame == 130)
            {
                SwordSwarm.Instance.FireVolley();
                var enemy = opponent.GetComponent<TaTu>();
                for (int k = 2; k < enemy.Blades.Count; k++)
                    enemy.Blades[k].Launch(PlayerStats.Instance.transform.position + Vector3.up, 24, 1);
            }
            float pullback = Mathf.SmoothStep(0, 1, Mathf.Clamp01((frame - 59f) / 22f));
            camera.transform.position = Vector3.Lerp(new Vector3(-20.5f, 2.6f, -18), new Vector3(-18.5f, 3.2f, -10.5f), pullback);
            camera.transform.LookAt(Vector3.Lerp(new Vector3(-24, 1.8f, -25), new Vector3(-18.5f, 1.7f, -25), pullback));
            camera.fieldOfView = Mathf.Lerp(43, 49, pullback);
            if (swordsOnly)
            {
                camera.transform.position = new Vector3(-18.5f, 9f, -10.5f);
                camera.transform.LookAt(new Vector3(-18.5f, 0.8f, -25));
                camera.fieldOfView = 52;
            }
            if(motionGameplay)
            {
                camera.transform.position=new Vector3(-20.7f,2.0f,-21.5f);
                camera.transform.LookAt(new Vector3(-24,1.05f,-25));camera.fieldOfView=32;
            }
            var previous = RenderTexture.active;
            try
            {
                camera.Render(); RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 960, 600), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(folder, frame.ToString("D4") + ".png"), pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; }
            frame++;
            if (frame >= (motionGameplay ? 264 : swordsOnly ? 240 : 180))
            {
                Status = "Complete: " + folder; SessionState.SetString("TienHiepPreviewFolder", folder);
                Finish();
            }
        }
        catch (System.Exception e) { Status = e.ToString(); Debug.LogException(e); Finish(); }
    }

    private static void Finish()
    {
        ThanThongCombat.PreviewFrame -= Tick; EditorApplication.playModeStateChanged -= OnPlayMode; Time.captureFramerate = oldCaptureRate;
        if (camera != null) { camera.targetTexture = null; Object.DestroyImmediate(camera.gameObject); }
        if (target != null) Object.DestroyImmediate(target); if (pixels != null) Object.DestroyImmediate(pixels);
        camera = null; target = null; pixels = null; swordsOnly = false; motionGameplay=false;
        if (opponent != null) Object.Destroy(opponent); if (incoming != null) Object.Destroy(incoming);
        if (ThanThongCombat.Instance != null) ThanThongCombat.Instance.EndWard(false);
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.GetComponent<PlayerController>().TeleportTo(originalPosition, originalRotation);
            PlayerStats.Instance.GetComponent<PlayerController>().MoveLocked = originalMoveLock;
            PlayerStats.Instance.SetRealmIndex(originalRealm);
        }
        if (SwordSwarm.Instance != null) { SwordSwarm.Instance.RecallAll(); SwordSwarm.Instance.LockTarget(null); SwordSwarm.Instance.SwordCount = originalSwordCount; SwordSwarm.Instance.SetActive(active, false); }
        if (orbit != null) typeof(CameraOrbit).GetField("yaw", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(orbit, originalYaw);
        foreach (var go in hidden) if (go != null) go.SetActive(true); hidden.Clear();
        AdminPanel.MauVoHan = god; AdminPanel.PhapLucVoHan = mana; AdminPanel.MauDichVoHan = enemyGod; AdminPanel.KhongHoiChieu = noCooldown; EnemyBase.AIEnabled = ai;
    }
    private static void OnPlayMode(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode && Running) Finish(); }
}
#endif
