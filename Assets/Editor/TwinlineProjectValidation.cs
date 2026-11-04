using System;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Validation of the persisted swing project, without an external test-package dependency.</summary>
public static class TwinlineProjectValidation
{
    private const string ScenePath="Assets/Scenes/Flight.unity";
    private const string RunKey="TwinlineProjectValidation.Running";
    private const string ExitKey="TwinlineProjectValidation.AwaitingExit";
    private const string LogKey="TwinlineProjectValidation.Checks";
    private const string ErrorKey="TwinlineProjectValidation.Errors";
    private const string BestKey="TwinSwingBest";
    private const string AuthoredKey="TwinlineProjectValidation.AuthoredSettings";
    private static TwinSwingGame game;
    private static Vector2[] anchorPositions;
    private static Vector3[] gemPositions;
    private static int sceneObjectCount,stage;
    private static double started,stageStarted;
    private static Vector2 pumpVelocity;
    private static bool finishing;
    private static string OutputDirectory => Path.GetFullPath(Path.Combine(Application.dataPath,"../Verification"));

    [InitializeOnLoadMethod]
    private static void InstallHooks()
    {
        EditorApplication.playModeStateChanged-=OnPlayState;
        EditorApplication.playModeStateChanged+=OnPlayState;
        EditorApplication.update-=Tick;
        EditorApplication.update+=Tick;
        Application.logMessageReceived-=OnLog;
        Application.logMessageReceived+=OnLog;
        if(SessionState.GetBool(RunKey,false) && SessionState.GetBool(ExitKey,false) && !EditorApplication.isPlaying)
            EditorApplication.delayCall+=CompleteAfterPlay;
    }

    [MenuItem("Twinline/Validation/Build And Validate Editable Project")]
    public static void BuildAndValidate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before validating saved project assets.");
        TwinlineAuthoring.BuildProject();
        TwinlineInterfaceAuthoring.UpgradeInterface();
        ValidateCurrentSavedProject();
    }

    [MenuItem("Twinline/Validation/Validate Current Saved Project")]
    public static void ValidateCurrentSavedProject()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before validating the current saved project.");
        Directory.CreateDirectory(OutputDirectory);
        var report=new StringBuilder("Twinline saved-project validation\nUnity "+Application.unityVersion+"\n");
        bool hadBest=PlayerPrefs.HasKey(BestKey);
        int savedBest=PlayerPrefs.GetInt(BestKey,0);
        float fixedStep=Time.fixedDeltaTime;
        Vector2 gravity=Physics2D.gravity;
        try
        {
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var current=FindGame();
            ValidateStructure(current,message=>report.AppendLine("PASS: "+message));
            Ensure(current.hud!=null,"The upgraded project includes its saved native Canvas interface.",report);
            // Deliberate non-default edits, kept only in this unsaved test scene,
            // catch scripts that silently restore their own style/settings.
            current.hud.distanceText.fontSize=43;
            current.hud.distanceText.color=new Color(.2f,.8f,1f,.9f);
            current.hud.distanceText.rectTransform.anchoredPosition+=new Vector2(9f,-7f);
            current.startCountdownDuration=.9f;
            current.gemVolume=.13f;
            var authored=CaptureAuthoredSettings(current);
            var initialAnchors=current.sceneAnchors.Select(a=>(Vector2)a.transform.position).ToArray();
            var initialGems=current.sceneGems.Select(g=>g.transform.position).ToArray();
            int before=AllSceneObjects().Length;
            current.BuildWorld();current.BuildWorld();current.ResetRun();current.hud.Refresh();
            Ensure(AllSceneObjects().Length==before,"Binding the saved world creates no duplicate objects.",report);
            ValidateAuthoredSettings(current,authored,message=>report.AppendLine("PASS: "+message));
            current.StartRun();
            Ensure(Mathf.Abs(current.CountdownRemaining-.9f)<.0001f,"A non-default Inspector countdown duration drives the run.",report);
            current.StartRun(true);
            current.TryAttach();
            Ensure(current.Attached,"The saved player can attach to the saved course.",report);
            var joint=current.player.GetComponent<DistanceJoint2D>();
            Ensure(joint!=null && joint.connectedBody!=null && joint.distance>=current.minRopeLength && joint.distance<=current.maxRopeLength,
                "Attachment creates a connected DistanceJoint2D within rope limits.",report);
            current.ReleaseSwing();
            Ensure(!current.Attached && !current.rope.enabled && current.player.GetComponent<DistanceJoint2D>()==null,
                "Release removes the joint and hides the rope.",report);
            var gem=current.sceneGems[0];
            current.CollectGem(gem);current.CollectGem(gem);
            Ensure(current.Score==1 && gem.collected && !gem.pickupCollider.enabled && !gem.visual.gameObject.activeSelf,
                "A saved gem scores once and disables its collider and visual.",report);
            current.sceneAnchors[0].body.position+=Vector2.right*3f;
            current.sceneAnchors[0].transform.position=current.sceneAnchors[0].body.position;
            gem.transform.position+=Vector3.up*1f;
            current.Crash("Validation");
            Ensure(current.State==TwinSwingGame.Mode.Crashed && !current.player.simulated,"Crash stops the saved player.",report);
            current.ResetRun();
            ValidateReset(current,initialAnchors,initialGems,message=>report.AppendLine("PASS: "+message));
            Ensure(AllSceneObjects().Length==before,"Attach, collection, crash and reset preserve the saved object count.",report);
            ValidateAuthoredSettings(current,authored,message=>report.AppendLine("PASS: "+message));
            report.AppendLine("RESULT: PASS");
            File.WriteAllText(Path.Combine(OutputDirectory,"saved-project-report.txt"),report.ToString());
            Debug.Log("TWINLINE_SAVED_PROJECT_VALIDATION_PASSED\n"+report);
        }
        catch(Exception error)
        {
            report.AppendLine("RESULT: FAIL\n"+error);
            File.WriteAllText(Path.Combine(OutputDirectory,"saved-project-report.txt"),report.ToString());
            throw;
        }
        finally
        {
            RestoreBest(hadBest,savedBest);
            Time.fixedDeltaTime=fixedStep;Physics2D.gravity=gravity;
            // Test mutations never replace the saved scene or its prefab overrides.
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        }
    }

    [MenuItem("Twinline/Validation/Run Swing Play Mode Checks")]
    public static void RunPlayMode()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before starting the swing validation run.");
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(OutputDirectory);
        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        SessionState.SetString(AuthoredKey,JsonUtility.ToJson(CaptureAuthoredSettings(FindGame())));
        SessionState.SetBool(RunKey,true);SessionState.SetBool(ExitKey,false);
        SessionState.SetBool("TwinlineProjectValidation.HadBest",PlayerPrefs.HasKey(BestKey));
        SessionState.SetInt("TwinlineProjectValidation.Best",PlayerPrefs.GetInt(BestKey,0));
        SessionState.SetString(LogKey,"Twinline swing Play mode validation\nUnity "+Application.unityVersion+"\n");
        SessionState.SetString(ErrorKey,"");
        finishing=false;started=EditorApplication.timeSinceStartup;
        InstallHooks();
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        EditorApplication.isPlaying=true;
    }

    private static void OnPlayState(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(RunKey,false)) return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            finishing=false;stage=0;game=null;
            started=stageStarted=EditorApplication.timeSinceStartup;
        }
        if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ExitKey,false))
            EditorApplication.delayCall+=CompleteAfterPlay;
    }

    private static void Tick()
    {
        if(!SessionState.GetBool(RunKey,false) || finishing || !EditorApplication.isPlaying || SessionState.GetBool(ExitKey,false)) return;
        double now=EditorApplication.timeSinceStartup;
        try
        {
            if(now-started>45) throw new TimeoutException("Swing Play mode validation exceeded 45 seconds at stage "+stage+".");
            if(!string.IsNullOrEmpty(SessionState.GetString(ErrorKey,"")))
                throw new InvalidOperationException("Unity reported runtime errors:\n"+SessionState.GetString(ErrorKey,""));
            switch(stage)
            {
                case 0:
                    game=FindGame();ValidateStructure(game,Pass);
                    Check(game.State==TwinSwingGame.Mode.Ready,"Awake starts the saved scene in Ready state.");
                    ValidateAuthoredSettings(game,JsonUtility.FromJson<AuthoredSettings>(SessionState.GetString(AuthoredKey,"")),Pass);
                    anchorPositions=game.sceneAnchors.Select(a=>(Vector2)a.transform.position).ToArray();
                    gemPositions=game.sceneGems.Select(g=>g.transform.position).ToArray();
                    sceneObjectCount=AllSceneObjects().Length;
                    CaptureFrame("ready.png");
                    Advance(now);break;
                case 1:
                    if(now-stageStarted<.3) break;
                    if(game.hud!=null)
                    {
                        game.hud.actionButton.onClick.Invoke();
                        Check(game.State==TwinSwingGame.Mode.Countdown,"The persisted native Start button begins the real countdown.");
                        Check(Mathf.Abs(game.CountdownRemaining-game.startCountdownDuration)<.05f,"The countdown uses the Inspector's configured duration.");
                    }
                    else
                    {
                        game.SendMessage("HandleSpacePressed");game.SendMessage("HandleSpaceReleased");
                        Check(game.State==TwinSwingGame.Mode.Countdown,"The Space input handler begins the real countdown.");
                    }
                    Advance(now);break;
                case 2:
                    if(game.State==TwinSwingGame.Mode.Countdown) break;
                    Check(game.State==TwinSwingGame.Mode.Flying,"The real fixed-update countdown enters Flying.");
                    game.player.position=game.sceneAnchors[0].body.position+new Vector2(0,-1.4f);
                    game.player.transform.position=game.player.position;game.player.linearVelocity=Vector2.zero;
                    Physics2D.SyncTransforms();
                    game.SendMessage("HandleSpacePressed");game.SendMessage("HandleSpaceReleased");
                    Check(game.Attached && game.rope.enabled,"Space attaches the saved player and shows the saved rope.");
                    var joint=game.player.GetComponent<DistanceJoint2D>();
                    Check(joint!=null && joint.connectedBody!=null && joint.distance>=game.minRopeLength && joint.distance<=game.maxRopeLength,
                        "The live physics joint respects configured rope limits.");
                    Advance(now);break;
                case 3:
                    if(now-stageStarted<.25) break;
                    Check(game.State==TwinSwingGame.Mode.Flying && game.Attached,"The saved joint remains active during physics simulation.");
                    Check(Vector3.Distance(game.rope.GetPosition(0),game.player.transform.position)<.3f,
                        "LateUpdate renders the rope at the live player position.");
                    CaptureFrame("swinging.png");
                    game.SendMessage("HandleSpacePressed");
                    Advance(now);break;
                case 4:
                    if(now-stageStarted<1f) break;
                    float charge=(float)typeof(TwinSwingGame).GetField("pumpCharge",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(game);
                    Check(game.Attached && charge>.4f,"Holding Space charges the pump through the real Update loop.");
                    pumpVelocity=game.player.linearVelocity;
                    game.SendMessage("HandleSpaceReleased");
                    Check(game.Attached,"Releasing a charged hold pumps while keeping the rope attached.");
                    Advance(now);break;
                case 5:
                    if(now-stageStarted<.08) break;
                    Check(Vector2.Distance(pumpVelocity,game.player.linearVelocity)>.1f,"The charged pump changes live Rigidbody2D velocity.");
                    game.SendMessage("HandleSpacePressed");game.SendMessage("HandleSpaceReleased");
                    Check(!game.Attached && !game.rope.enabled,"A quick second tap releases instead of pumping.");
                    Advance(now);break;
                case 6:
                    if(now-stageStarted<.08) break;
                    Check(game.player.GetComponent<DistanceJoint2D>()==null,"The released joint is destroyed after the frame.");
                    game.StartRun(true);
                    var gem=game.sceneGems[0];gem.transform.position=Vector3.zero;
                    game.player.position=Vector2.zero;game.player.transform.position=Vector3.zero;game.player.linearVelocity=Vector2.zero;
                    Physics2D.SyncTransforms();
                    Advance(now);break;
                case 7:
                    if(now-stageStarted<.2) break;
                    Check(game.Score==1 && game.sceneGems[0].collected && !game.sceneGems[0].pickupCollider.enabled,
                        "An actual trigger overlap collects the saved gem.");
                    game.CollectGem(game.sceneGems[0]);
                    Check(game.Score==1,"A repeated pickup callback cannot score the same gem twice.");
                    game.player.position=new Vector2(0,-game.corridorHalfHeight-.25f);
                    game.player.transform.position=game.player.position;game.player.linearVelocity=Vector2.down;
                    Physics2D.SyncTransforms();
                    Advance(now);break;
                case 8:
                    if(now-stageStarted<.2) break;
                    Check(game.State==TwinSwingGame.Mode.Crashed && !game.player.simulated,"An actual boundary collision crashes the run.");
                    Advance(now);break;
                case 9:
                    if(now-stageStarted<Mathf.Max(.3f,game.retryDelay+.05f)) break;
                    if(game.hud!=null)
                    {
                        game.hud.actionButton.onClick.Invoke();
                        Check(game.State==TwinSwingGame.Mode.Countdown,"The persisted native Retry button restarts after a collision.");
                    }
                    else
                    {
                        game.SendMessage("HandleSpacePressed");game.SendMessage("HandleSpaceReleased");
                        Check(game.State==TwinSwingGame.Mode.Countdown,"Space retries after a collision through the real input handler.");
                    }
                    ValidateReset(game,anchorPositions,gemPositions,Pass,false);
                    ValidateAuthoredSettings(game,JsonUtility.FromJson<AuthoredSettings>(SessionState.GetString(AuthoredKey,"")),Pass);
                    Check(AllSceneObjects().Length==sceneObjectCount,"Play, pump, pickup, crash and retry create no duplicate world objects.");
                    Check(File.Exists(Path.Combine(OutputDirectory,"ready.png")) && File.Exists(Path.Combine(OutputDirectory,"swinging.png")),
                        "Ready and swinging camera frame captures were written.");
                    SessionState.SetBool(ExitKey,true);
                    EditorApplication.isPlaying=false;
                    break;
            }
        }
        catch(Exception error) { FailPlay(error); }
    }

    private static void CaptureFrame(string filename)
    {
        string path=Path.Combine(OutputDirectory,filename);
        // Batch mode does not render a Game view for ScreenCapture. Render the
        // actual scene camera explicitly. A screen-space-overlay Canvas is not
        // included by Camera.Render; a screen-space-camera Canvas is included.
        if(Application.isBatchMode) TwinlineProjectCapture.CaptureCamera(game.sharedCamera,path);
        else ScreenCapture.CaptureScreenshot(path);
    }

    private static void CompleteAfterPlay()
    {
        if(!SessionState.GetBool(RunKey,false) || !SessionState.GetBool(ExitKey,false) || EditorApplication.isPlaying || finishing) return;
        finishing=true;
        try
        {
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            ValidateStructure(FindGame(),Pass);
            Check(string.IsNullOrEmpty(SessionState.GetString(ErrorKey,"")),"No unhandled gameplay errors or exceptions were logged.");
            Pass("The saved scene and prefab references remain intact after exiting Play mode.");
            RestoreBest(SessionState.GetBool("TwinlineProjectValidation.HadBest",false),SessionState.GetInt("TwinlineProjectValidation.Best",0));
            SessionState.SetBool(RunKey,false);SessionState.SetBool(ExitKey,false);
            File.WriteAllText(Path.Combine(OutputDirectory,"playmode-report.txt"),SessionState.GetString(LogKey,"")+"RESULT: PASS\n");
            Debug.Log("TWINLINE_PLAYMODE_VALIDATION_PASSED: countdown, attachment, pump, release, actual pickup trigger, boundary crash, retry, saved assets and screenshots.");
            if(Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch(Exception error) { finishing=false;FailPlay(error); }
    }

    private static void FailPlay(Exception error)
    {
        if(finishing) return;
        finishing=true;
        RestoreBest(SessionState.GetBool("TwinlineProjectValidation.HadBest",false),SessionState.GetInt("TwinlineProjectValidation.Best",0));
        SessionState.SetBool(RunKey,false);SessionState.SetBool(ExitKey,false);
        Directory.CreateDirectory(OutputDirectory);
        File.WriteAllText(Path.Combine(OutputDirectory,"playmode-report.txt"),SessionState.GetString(LogKey,"")+"RESULT: FAIL\n"+error+"\n"+SessionState.GetString(ErrorKey,""));
        Debug.LogException(error);
        if(Application.isBatchMode) EditorApplication.Exit(1);
        else EditorApplication.isPlaying=false;
    }

    private static TwinSwingGame FindGame()
    {
        var games=UnityEngine.Object.FindObjectsByType<TwinSwingGame>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        if(games.Length!=1) throw new InvalidOperationException("Expected exactly one saved TwinSwingGame; found "+games.Length+".");
        return games[0];
    }

    private static GameObject[] AllSceneObjects()
    {
        return SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject).ToArray();
    }

    private static void ValidateStructure(TwinSwingGame current,Action<string> pass)
    {
        Action<bool,string> test=(condition,message)=> { if(!condition) throw new InvalidOperationException(message);pass(message); };
        test(current.useSceneObjects && current.useAuthoredCourse,"The game binds saved objects and restores the authored opening course.");
        test(UnityEngine.Object.FindObjectsByType<TwinlineRoot>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==0,
            "The authored scene contains no runtime-only TwinlineRoot.");
        test(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1 && current.sharedCamera!=null,
            "Exactly one persisted game camera is present.");
        test(current.sceneAnchors!=null && current.sceneAnchors.Length==8 && current.sceneAnchors.All(a=>a!=null && a.body!=null && a.ring!=null),
            "Eight saved anchors have assigned physics bodies and visual references.");
        test(current.sceneGems!=null && current.sceneGems.Length==10 && current.sceneGems.All(g=>g!=null && g.visual!=null && g.pickupCollider!=null && g.pickupCollider.isTrigger),
            "Ten saved gems have assigned visuals and trigger colliders.");
        test(current.player!=null && current.player.GetComponent<SwingPlayer>()!=null && current.player.GetComponent<CircleCollider2D>()!=null && current.playerVisual!=null,
            "The persisted player includes its behaviour, visual and collider.");
        test(current.rope!=null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(current.rope.sharedMaterial)),"The saved rope references a persistent material asset.");
        var sources=new[]{current.attachSound,current.releaseSound,current.gemSound,current.crashSound};
        test(current.audioSource!=null && sources.All(c=>c!=null && AssetDatabase.GetAssetPath(c).EndsWith(".wav",StringComparison.OrdinalIgnoreCase)),
            "Four imported WAV cues and the saved audio source are assigned.");
        // Unity's live Play-mode scene objects do not retain editable prefab-instance links.
        // Check those links on the persisted scene before and after Play, while the live
        // assertions below still require the actual sprite/material/audio asset references.
        if(!Application.isPlaying)
        {
            var prefabObjects=current.sceneAnchors.Select(a=>a.gameObject).Concat(current.sceneGems.Select(g=>g.gameObject)).Concat(new[]{current.player.gameObject,current.rope.gameObject});
            test(prefabObjects.All(o=>PrefabUtility.GetCorrespondingObjectFromSource(o)!=null),"Gameplay objects retain their prefab asset connections.");
        }
        var sprites=AllSceneObjects().SelectMany(o=>o.GetComponents<SpriteRenderer>()).ToArray();
        test(sprites.Length>=19 && sprites.All(r=>r.sprite!=null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(r.sprite))),
            "The scene uses imported SpriteRenderer assets for its player, anchors and gems.");
        test(AllSceneObjects().All(o=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(o)==0),"The saved scene has no missing component scripts.");
        test(AssetDatabase.FindAssets("t:Sprite",new[]{"Assets/Art/Sprites"}).Length>=12 && AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs"}).Length>=5,
            "The browsable library includes at least twelve sprites and five prefab assets.");
        test(File.Exists("Assets/Scenes/AssetLibrary.unity"),"The visual asset gallery is saved as a separate Unity scene.");
        if(current.hud!=null) ValidateNativeInterface(current,pass);
    }

    [Serializable]
    private sealed class AuthoredSettings
    {
        public ConfigValue[] config;
        public bool hasHud;
        public int objectCount;
        public TextStyle[] texts;
        public GraphicStyle[] graphics;
        public RectStyle[] rects;
        public ButtonStyle[] buttons;
    }
    [Serializable] private sealed class ConfigValue { public string name,value; }
    [Serializable] private sealed class TextStyle
    {
        public string path,fontPath,fontName;
        public int fontSize,fontStyle,alignment,horizontalOverflow,verticalOverflow,minSize,maxSize;
        public float lineSpacing;
        public bool richText,bestFit;
    }
    [Serializable] private sealed class GraphicStyle
    {
        public string path,spritePath;
        public Color color;
        public bool raycastTarget,preserveAspect;
    }
    [Serializable] private sealed class RectStyle
    {
        public string path;
        public Vector2 anchorMin,anchorMax,pivot,sizeDelta;
        public Vector3 anchoredPosition,localScale;
        public Quaternion localRotation;
    }
    [Serializable] private sealed class ButtonStyle
    {
        public string path;
        public Color normal,highlighted,pressed,selected,disabled;
        public float multiplier,fade;
        public int transition;
    }
    private static readonly string[] ConfigFields={
        "gravity","attachRange","scrollSpeed","corridorHalfHeight","anchorSpacing","gemSpacing","minRopeLength","maxRopeLength",
        "swingBoost","chargeHoldDelay","pumpChargeTime","lowMomentumSpeed","minimumPumpImpulse","maximumPumpImpulse",
        "courseSeed","anchorHeightRange","gemHeightRange","openingSlowDuration","openingScrollMultiplier",
        "startCountdownDuration","retryDelay","cameraFollowSpeed","cameraMaximumLead","cameraLeadInset","anchorHighlightScale","chargeVisualGrowth",
        "attachVolume","releaseVolume","gemVolume","crashVolume","pumpVolume"
    };

    private static AuthoredSettings CaptureAuthoredSettings(TwinSwingGame current)
    {
        var saved=new AuthoredSettings{
            config=ConfigFields.Select(name=>new ConfigValue{name=name,value=ConfigValueFor(current,name)}).ToArray(),
            hasHud=current.hud!=null,objectCount=AllSceneObjects().Length
        };
        if(!saved.hasHud) return saved;
        var canvas=current.hud.GetComponentInParent<Canvas>();
        if(canvas==null) throw new InvalidOperationException("The saved HUD must be inside its native Canvas.");
        saved.texts=canvas.GetComponentsInChildren<Text>(true).Select(text=>new TextStyle{
            path=ObjectPath(text.transform),fontPath=AssetDatabase.GetAssetPath(text.font),fontName=text.font!=null?text.font.name:"",
            fontSize=text.fontSize,fontStyle=(int)text.fontStyle,alignment=(int)text.alignment,
            horizontalOverflow=(int)text.horizontalOverflow,verticalOverflow=(int)text.verticalOverflow,
            minSize=text.resizeTextMinSize,maxSize=text.resizeTextMaxSize,lineSpacing=text.lineSpacing,
            richText=text.supportRichText,bestFit=text.resizeTextForBestFit
        }).ToArray();
        saved.graphics=canvas.GetComponentsInChildren<Graphic>(true).Select(graphic=>new GraphicStyle{
            path=ObjectPath(graphic.transform),color=graphic.color,raycastTarget=graphic.raycastTarget,
            spritePath=graphic is Image image?AssetDatabase.GetAssetPath(image.sprite):null,
            preserveAspect=graphic is Image img && img.preserveAspect
        }).ToArray();
        saved.rects=canvas.GetComponentsInChildren<RectTransform>(true).Where(rect=>rect.GetComponent<Canvas>()==null).Select(rect=>new RectStyle{
            path=ObjectPath(rect),anchorMin=rect.anchorMin,anchorMax=rect.anchorMax,pivot=rect.pivot,sizeDelta=rect.sizeDelta,
            anchoredPosition=rect.anchoredPosition3D,localScale=rect.localScale,localRotation=rect.localRotation
        }).ToArray();
        saved.buttons=canvas.GetComponentsInChildren<Button>(true).Select(button=>new ButtonStyle{
            path=ObjectPath(button.transform),normal=button.colors.normalColor,highlighted=button.colors.highlightedColor,
            pressed=button.colors.pressedColor,selected=button.colors.selectedColor,disabled=button.colors.disabledColor,
            multiplier=button.colors.colorMultiplier,fade=button.colors.fadeDuration,transition=(int)button.transition
        }).ToArray();
        return saved;
    }

    private static string ConfigValueFor(TwinSwingGame current,string name)
    {
        var field=typeof(TwinSwingGame).GetField(name,BindingFlags.Public|BindingFlags.Instance);
        if(field==null) throw new InvalidOperationException("Missing editable gameplay field "+name+".");
        object value=field.GetValue(current);
        if(value is float number) return number.ToString("R",CultureInfo.InvariantCulture);
        if(value is Vector2 vector) return vector.x.ToString("R",CultureInfo.InvariantCulture)+","+vector.y.ToString("R",CultureInfo.InvariantCulture);
        return Convert.ToString(value,CultureInfo.InvariantCulture);
    }

    private static void ValidateNativeInterface(TwinSwingGame current,Action<string> pass)
    {
        var objects=AllSceneObjects();
        if(objects.SelectMany(o=>o.GetComponents<Canvas>()).Count()!=1 ||
            objects.SelectMany(o=>o.GetComponents<TwinlineHud>()).Count()!=1 ||
            objects.SelectMany(o=>o.GetComponents<EventSystem>()).Count()!=1)
            throw new InvalidOperationException("Expected exactly one saved Canvas, TwinlineHud and EventSystem.");
        pass("Exactly one native Canvas, HUD and EventSystem are present.");
        var hud=current.hud;
        if(hud.game!=current || hud.distanceText==null || hud.countdownText==null || hud.actionLabel==null ||
            hud.actionButton==null || hud.flashImage==null || hud.readyGroup==null || hud.flashGroup==null ||
            hud.GetComponentInParent<Canvas>()?.GetComponent<GraphicRaycaster>()==null)
            throw new InvalidOperationException("The native HUD has missing game, text, button, image, group or raycaster references.");
        if(hud.distanceText.font==null || hud.countdownText.font==null || hud.actionLabel.font==null)
            throw new InvalidOperationException("The saved native Text objects need font references.");
        pass("The saved native HUD has assigned Text, Button, Image, CanvasGroup and font references.");
        var click=hud.actionButton.onClick;
        bool persistentAction=false;
        for(int i=0;i<click.GetPersistentEventCount();i++)
            if((click.GetPersistentTarget(i)==hud || click.GetPersistentTarget(i)==current) &&
                click.GetPersistentMethodName(i)=="PressAction" &&
                click.GetPersistentListenerState(i)!=UnityEngine.Events.UnityEventCallState.Off)
                persistentAction=true;
        if(!persistentAction) throw new InvalidOperationException("The native action Button needs a saved persistent PressAction callback.");
        pass("The native Start/Retry Button retains its persistent PressAction callback.");
    }

    private static void ValidateAuthoredSettings(TwinSwingGame current,AuthoredSettings saved,Action<string> pass)
    {
        if(saved==null || saved.config==null) throw new InvalidOperationException("No pre-Play authored-settings snapshot was recorded.");
        foreach(var item in saved.config)
            if(ConfigValueFor(current,item.name)!=item.value)
                throw new InvalidOperationException("Play or retry replaced the Inspector value of "+item.name+".");
        pass("Inspector-authored course, timing, camera, visual and audio settings survive initialization and retry.");
        if(!saved.hasHud) return;
        if(current.hud==null || AllSceneObjects().Length!=saved.objectCount)
            throw new InvalidOperationException("Initialization or retry added/removed saved scene objects or the native HUD.");
        var objects=AllSceneObjects();
        Func<string,GameObject> find=path=>objects.SingleOrDefault(o=>ObjectPath(o.transform)==path);
        foreach(var item in saved.texts)
        {
            var text=find(item.path)?.GetComponent<Text>();
            if(text==null || text.font==null || AssetDatabase.GetAssetPath(text.font)!=item.fontPath || text.font.name!=item.fontName ||
                text.fontSize!=item.fontSize || (int)text.fontStyle!=item.fontStyle || (int)text.alignment!=item.alignment ||
                (int)text.horizontalOverflow!=item.horizontalOverflow || (int)text.verticalOverflow!=item.verticalOverflow ||
                text.resizeTextMinSize!=item.minSize || text.resizeTextMaxSize!=item.maxSize ||
                Mathf.Abs(text.lineSpacing-item.lineSpacing)>.0001f || text.supportRichText!=item.richText || text.resizeTextForBestFit!=item.bestFit)
                throw new InvalidOperationException("Play or retry replaced native Text styling at "+item.path+".");
        }
        foreach(var item in saved.graphics)
        {
            var graphic=find(item.path)?.GetComponent<Graphic>();
            if(graphic==null || !SameColor(graphic.color,item.color) || graphic.raycastTarget!=item.raycastTarget ||
                (graphic is Image image && (AssetDatabase.GetAssetPath(image.sprite)!=item.spritePath || image.preserveAspect!=item.preserveAspect)))
                throw new InvalidOperationException("Play or retry replaced native UI color or image styling at "+item.path+".");
        }
        foreach(var item in saved.rects)
        {
            var rect=find(item.path)?.GetComponent<RectTransform>();
            if(rect==null || Vector2.Distance(rect.anchorMin,item.anchorMin)>.001f || Vector2.Distance(rect.anchorMax,item.anchorMax)>.001f ||
                Vector2.Distance(rect.pivot,item.pivot)>.001f || Vector2.Distance(rect.sizeDelta,item.sizeDelta)>.001f ||
                Vector3.Distance(rect.anchoredPosition3D,item.anchoredPosition)>.001f || Vector3.Distance(rect.localScale,item.localScale)>.001f ||
                Quaternion.Angle(rect.localRotation,item.localRotation)>.001f)
                throw new InvalidOperationException("Play or retry replaced authored UI RectTransform values at "+item.path+".");
        }
        foreach(var item in saved.buttons)
        {
            var button=find(item.path)?.GetComponent<Button>();
            if(button==null || !SameColor(button.colors.normalColor,item.normal) || !SameColor(button.colors.highlightedColor,item.highlighted) ||
                !SameColor(button.colors.pressedColor,item.pressed) || !SameColor(button.colors.selectedColor,item.selected) ||
                !SameColor(button.colors.disabledColor,item.disabled) || Mathf.Abs(button.colors.colorMultiplier-item.multiplier)>.0001f ||
                Mathf.Abs(button.colors.fadeDuration-item.fade)>.0001f || (int)button.transition!=item.transition)
                throw new InvalidOperationException("Play or retry replaced native Button color settings at "+item.path+".");
        }
        pass("Native UI fonts, colors, images, button styling and authored RectTransforms survive Awake and retry without duplicate objects.");
    }
    private static string ObjectPath(Transform transform)
    {
        string path=transform.name;
        while(transform.parent!=null) { transform=transform.parent;path=transform.name+"/"+path; }
        return path;
    }
    private static bool SameColor(Color first,Color second) =>
        Mathf.Abs(first.r-second.r)<.0001f && Mathf.Abs(first.g-second.g)<.0001f &&
        Mathf.Abs(first.b-second.b)<.0001f && Mathf.Abs(first.a-second.a)<.0001f;

    private static void ValidateReset(TwinSwingGame current,Vector2[] anchors,Vector3[] gems,Action<string> pass,bool expectReady=true)
    {
        if((expectReady && current.State!=TwinSwingGame.Mode.Ready) || current.Score!=0 || current.DistanceTravelled!=0 || current.Attached)
            throw new InvalidOperationException("Retry did not reset the run state, distance, score and attachment.");
        for(int i=0;i<anchors.Length;i++)
            if(Vector2.Distance(current.sceneAnchors[i].transform.position,anchors[i])>.001f)
                throw new InvalidOperationException("Retry changed authored anchor position "+i+".");
        for(int i=0;i<gems.Length;i++)
            if(Vector3.Distance(current.sceneGems[i].transform.position,gems[i])>.001f || current.sceneGems[i].collected || !current.sceneGems[i].pickupCollider.enabled || !current.sceneGems[i].visual.gameObject.activeSelf)
                throw new InvalidOperationException("Retry failed to restore saved gem "+i+".");
        pass("Retry resets score, distance and attachment and restores all authored anchors and collectible gems.");
    }

    private static void Check(bool condition,string message)
    {
        if(!condition) throw new InvalidOperationException(message);
        Pass(message);
    }
    private static void Ensure(bool condition,string message,StringBuilder report)
    {
        if(!condition) throw new InvalidOperationException(message);
        report.AppendLine("PASS: "+message);
    }
    private static void Pass(string message) => SessionState.SetString(LogKey,SessionState.GetString(LogKey,"")+"PASS: "+message+"\n");
    private static void Advance(double now) { stage++;stageStarted=now; }
    private static void OnLog(string condition,string trace,LogType type)
    {
        if(!SessionState.GetBool(RunKey,false) || (type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert)) return;
        string normalizedTrace=(trace??"").Replace('\\','/');
        // Unity 6000.6 can report a QuickSearch startup-index exception unrelated to
        // this scene. Preserve that exact editor-only failure as an environment
        // warning; never suppress a stack containing a runtime project script.
        if(condition.Contains("ArgumentOutOfRangeException") &&
            normalizedTrace.Contains("UnityEditor.Search.SearchDatabase") &&
            normalizedTrace.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup") &&
            !normalizedTrace.Contains("Assets/Scripts/"))
        {
            SessionState.SetString(LogKey,SessionState.GetString(LogKey,"")+"ENVIRONMENT WARNING: UnityEditor QuickSearch startup index exception.\n"+condition+"\n"+trace+"\n");
            return;
        }
        SessionState.SetString(ErrorKey,SessionState.GetString(ErrorKey,"")+condition+"\n"+trace+"\n");
    }
    private static void RestoreBest(bool existed,int value)
    {
        if(existed) PlayerPrefs.SetInt(BestKey,value);else PlayerPrefs.DeleteKey(BestKey);
        PlayerPrefs.Save();
    }
}
