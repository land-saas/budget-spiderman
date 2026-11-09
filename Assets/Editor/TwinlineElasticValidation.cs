using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Deterministic checks against the saved scene and Unity's real 2D spring solver.</summary>
public static class TwinlineElasticValidation
{
    private static readonly StringBuilder report=new StringBuilder();
    private static TwinSwingGame game;
    private static readonly MethodInfo step=typeof(TwinSwingGame).GetMethod("Step",BindingFlags.NonPublic|BindingFlags.Instance);
    private static readonly MethodInfo charge=typeof(TwinSwingGame).GetMethod("UpdateHoldCharge",BindingFlags.NonPublic|BindingFlags.Instance);
    private const float Dt=.01f;

    [MenuItem("Twinline/Validation/Run All Rope Checks")]
    public static void RunAll()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Validate();
        TwinlineProjectValidation.ValidateCurrentSavedProject();
        TwinlineProjectValidation.RunPlayMode();
    }

    [MenuItem("Twinline/Validation/Validate Elastic Rope")]
    public static void Validate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        report.Clear();report.AppendLine("Budget Spiderman elastic rope checks\nUnity "+Application.unityVersion);
        var simulation=Physics2D.simulationMode;
        var gravity=Physics2D.gravity;
        float fixedStep=Time.fixedDeltaTime;
        bool hadBest=PlayerPrefs.HasKey("TwinSwingBest");
        int best=PlayerPrefs.GetInt("TwinSwingBest",0);
        string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../Verification"));
        Directory.CreateDirectory(directory);
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Flight.unity");
            game=UnityEngine.Object.FindAnyObjectByType<TwinSwingGame>();
            game.BuildWorld();
            Physics2D.simulationMode=SimulationMode2D.Script;
            // Isolate rope dynamics from authored hazards; the separate Play mode suite tests real collisions.
            foreach(var collider in UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None)) collider.enabled=false;
            game.gravity=0;
            game.StartRun(true);
            Vector2[] worldAnchors=game.sceneAnchors.Select(a=>a.body.position).ToArray();
            Vector3[] worldGems=game.sceneGems.Select(g=>g.transform.position).ToArray();
            Vector2 pivot=game.sceneAnchors[0].body.position;
            PositionPlayer(pivot+Vector2.down*2);
            game.player.linearVelocity=Vector2.down*2;
            game.TryAttach();
            var joint=game.player.GetComponent<SpringJoint2D>();
            Check(joint!=null && joint.connectedBody==null && !joint.autoConfigureConnectedAnchor,"A spring is pinned to a world point, not a scrolling Rigidbody2D.");
            Check(Vector2.Distance(joint.connectedAnchor,pivot)<.00001f,"The world pin equals the selected anchor's initial position.");
            Check(game.player.linearVelocity.y < -1.99f,"Attachment preserves radial momentum for a natural catch and bounce.");
            Check(Mathf.Abs(joint.frequency-game.ropeFrequency)<.001f && Mathf.Abs(joint.dampingRatio-game.ropeDamping)<.001f,"Inspector spring frequency and damping drive the actual joint.");
            PositionPlayer(pivot+Vector2.down*2.6f);
            float minLength=10,maxLength=0,maxInward=0,maxOutward=0,maxDrift=0;
            for(int i=0;i<180;i++)
            {
                Tick(false);
                float length=Vector2.Distance(game.player.position,pivot);
                minLength=Mathf.Min(minLength,length);maxLength=Mathf.Max(maxLength,length);
                float radialSpeed=Vector2.Dot(game.player.linearVelocity,(game.player.position-pivot).normalized);
                maxInward=Mathf.Min(maxInward,radialSpeed);maxOutward=Mathf.Max(maxOutward,radialSpeed);
                maxDrift=Mathf.Max(maxDrift,Vector2.Distance(game.sceneAnchors[0].body.position,pivot));
            }
            Check(minLength<1.95f && maxLength>2.4f && maxInward < -.5f && maxOutward>.5f,
                $"Real spring simulation rebounds on both sides of rest: length {minLength:F3}–{maxLength:F3}, radial velocity {maxInward:F3}–{maxOutward:F3}.");
            Check(game.sceneAnchors.Select((a,i)=>Vector2.Distance(a.body.position,worldAnchors[i])).All(d=>d<.0001f) &&
                game.sceneGems.Select((g,i)=>Vector3.Distance(g.transform.position,worldGems[i])).All(d=>d<.0001f),
                "Every anchor and gem stays stationary in world space while swinging.");
            Check(Mathf.Abs(game.DistanceTravelled-pivot.x)<.001f,"Hanging in place does not generate fake scrolling distance.");
            Check(maxDrift<.0001f && Vector2.Distance(joint.connectedAnchor,pivot)<.00001f,$"Active anchor and world pin remain fixed through the full bounce (maximum drift {maxDrift:F6}).");
            Invoke("LateUpdate");
            Check(Vector3.Distance(game.rope.GetPosition(0),game.player.transform.position)<.0001f &&
                Vector3.Distance(game.rope.GetPosition(game.rope.positionCount-1),pivot)<.0001f,"Both rendered rope endpoints stay pinned to the player and world anchor.");
            // A hold on the initial attach press must work too, not require a hidden second press.
            game.ReleaseSwing();game.StartRun(true);pivot=game.sceneAnchors[0].body.position;
            PositionPlayer(pivot+Vector2.down*2);
            Invoke("HandleSpacePressed");
            for(int i=0;i<15;i++) Tick(true);
            Check(game.Attached && game.ChargeAmount==0,"A short attaching press observes the configured charge delay.");
            for(int i=0;i<130;i++) Tick(true);
            float heldLength=Vector2.Distance(game.player.position,pivot);
            Check(game.ChargeAmount>.99f && heldLength>2.02f && heldLength<2.16f,$"Continuing that same first press charges fully and adds controlled physical rope tension ({heldLength:F3} at rest length 2.000).");
            float beforeSpeed=game.player.linearVelocity.magnitude;
            Invoke("HandleSpaceReleased");
            Tick(false);
            Check(!game.Attached && !game.rope.enabled && game.ChargeAmount==0 && game.player.linearVelocity.magnitude>beforeSpeed+1,
                "Charged key-up detaches immediately, hides the rope, clears charge and launches with added momentum.");
            float inward=0;
            for(int i=0;i<20;i++)
            {
                Tick(false);
                inward=Mathf.Min(inward,Vector2.Dot(game.player.linearVelocity,(game.player.position-pivot).normalized));
            }
            Check(inward<-.05f,$"Stored spring stretch is carried into the detached launch (radial speed {inward:F3}).");
            Check(!game.Attached,"The launched player does not automatically reattach after release.");
            // Cancellation cannot accidentally apply the pending boost or keep stretching.
            game.StartRun(true);pivot=game.sceneAnchors[0].body.position;PositionPlayer(pivot+Vector2.down*2);
            Invoke("HandleSpacePressed");for(int i=0;i<50;i++) Tick(true);
            Check(game.ChargeAmount>0,"A subsequent press can charge again.");
            Vector2 beforeCancel=game.player.linearVelocity;
            Invoke("OnApplicationFocus",false);
            Check(!game.Attached && game.ChargeAmount==0 && game.player.linearVelocity==beforeCancel,"Losing focus safely detaches and cancels charge without a surprise impulse.");
            Invoke("HandleSpaceReleased");
            Check(!game.Attached,"A stale key-up after focus loss neither launches nor reattaches.");
            Invoke("HandleSpacePressed");Invoke("HandleSpaceReleased");
            Check(game.Attached,"A short first tap attaches and stays connected.");
            Invoke("HandleSpacePressed");Invoke("HandleSpaceReleased");
            Check(!game.Attached && !game.rope.enabled && game.player.GetComponent<SpringJoint2D>()==null,"A second quick press/release detaches the previously attached rope.");
            for(int i=0;i<6;i++)
            {
                game.StartRun(true);pivot=game.sceneAnchors[0].body.position;PositionPlayer(pivot+Vector2.down*2);
                game.TryAttach();game.TryAttach();
                Check(game.player.GetComponents<SpringJoint2D>().Length==1,"Repeated attach request keeps exactly one spring (cycle "+(i+1)+").");
                game.ResetRun();
                Check(game.State==TwinSwingGame.Mode.Ready && !game.Attached && game.ChargeAmount==0 && game.player.linearVelocity.sqrMagnitude<.00001f,"Retry clears spring, charge and motion (cycle "+(i+1)+").");
            }
            game.StartRun(true);pivot=game.sceneAnchors[0].body.position;
            PositionPlayer(new Vector2(-20,0));Invoke("HandleSpacePressed");
            Check(!game.Attached,"Pressing outside reach does not create a distant rope.");
            PositionPlayer(pivot+Vector2.down*2);Tick(true);
            Check(game.Attached,"Continuing the same hold catches an anchor when it enters reach.");
            Invoke("HandleSpaceReleased");
            Check(game.Attached,"A first short attaching press remains latched after catching within reach.");
            Invoke("HandleSpacePressed");Invoke("HandleSpaceReleased");Tick(true);
            Check(!game.Attached,"Released detach input stays detached even when an anchor remains in reach.");
            game.StartRun(true);pivot=game.sceneAnchors[0].body.position;PositionPlayer(pivot+Vector2.down*2);
            game.player.linearVelocity=new Vector2(6,-2);
            Vector2 catchVelocity=game.player.linearVelocity;
            for(int i=0;i<50;i++) { game.TryAttach();game.ReleaseSwing(); }
            Check(Vector2.Distance(game.player.linearVelocity,catchVelocity)<.0001f,
                "Fifty rapid catches/releases cannot multiply player velocity.");
            // High-energy entry and prolonged pumping must stay finite; force taper avoids unbounded hold extension.
            game.StartRun(true);pivot=game.sceneAnchors[0].body.position;PositionPlayer(pivot+Vector2.down*2);
            game.player.linearVelocity=new Vector2(9,-5);Invoke("HandleSpacePressed");
            for(int i=0;i<3000;i++)
            {
                Tick(true);
                if(i%90==89)
                {
                    Invoke("HandleSpaceReleased");Tick(false);
                    if(game.Attached) throw new InvalidOperationException("Release left an attached spring.");
                    game.StartRun(true);pivot=game.sceneAnchors[0].body.position;PositionPlayer(pivot+Vector2.down*2);
                    Invoke("HandleSpacePressed");
                }
                if(i%3==0) Invoke("UpdateRopeVisual",.03f);
                if(!Finite(game.player.position.x) || !Finite(game.player.position.y) || game.player.linearVelocity.magnitude>100)
                    throw new InvalidOperationException("Unstable spring state at tick "+i);
                for(int n=0;n<game.rope.positionCount;n++)
                    if(!Finite(game.rope.GetPosition(n).x) || !Finite(game.rope.GetPosition(n).y)) throw new InvalidOperationException("Non-finite rope visual.");
                if(game.Attached && game.rope.positionCount>1)
                {
                    Vector3 a=game.rope.GetPosition(0),b=game.rope.GetPosition(game.rope.positionCount-1);
                    for(int n=1;n<game.rope.positionCount-1;n++)
                        if(Vector3.Distance(game.rope.GetPosition(n),Vector3.Lerp(a,b,n/(float)(game.rope.positionCount-1)))>.251f)
                            throw new InvalidOperationException("Rope visual escaped its bounded endpoint curve.");
                }
            }
            Check(Vector2.Distance(game.sceneAnchors[0].body.position,pivot)<.0001f,"Thirty seconds of repeated charge/launch/retry cycles stay finite with fixed anchors.");
            foreach(int count in new[]{4,64})
            {
                game.ropeSegments=count;
                foreach(float delta in new[]{.008f,.016f,.033f,.1f})
                    for(int i=0;i<50;i++) Invoke("UpdateRopeVisual",delta);
                var before=new Vector3[game.rope.positionCount];game.rope.GetPositions(before);
                for(int i=0;i<30;i++) Invoke("UpdateRopeVisual",0f);
                bool stable=true;
                for(int i=0;i<before.Length;i++)
                    stable &= Finite(before[i].x) && Finite(before[i].y) && Vector3.Distance(before[i],game.rope.GetPosition(i))<.000001f;
                Check(stable,"Variable-frame-time strand stays finite and freezes correctly at segment count "+count+".");
            }
            game.gravity=9.4f;
            float shortTravel=MeasureLaunchTravel(.22f);
            float longTravel=MeasureLaunchTravel(1.1f);
            Check(longTravel>shortTravel+.8f && longTravel>shortTravel*1.5f,
                $"Comparable real-gravity launches travel farther after a longer hold: short {shortTravel:F3}, long {longTravel:F3} world units before the same landing height.");
            game.gravity=0;
            game.ReleaseSwing();game.StartRun(true);
            PositionPlayer(new Vector2(70,0));game.sharedCamera.transform.position=new Vector3(70,0,-10);
            Tick(false);
            Check(game.sceneAnchors.All(a=>a.body.position.x>70) && game.sceneGems.All(g=>g.transform.position.x>70),
                "Only passed, offscreen course objects recycle ahead to continue the fixed-world level.");
            game.ResetRun();
            Check(game.sceneAnchors.Select((a,i)=>Vector2.Distance(a.body.position,worldAnchors[i])).All(d=>d<.0001f),
                "Retry restores the authored anchor positions after distant course recycling.");
            report.AppendLine("RESULT: PASS");
            Debug.Log("TWINLINE_ELASTIC_VALIDATION_PASSED\n"+report);
        }
        catch(Exception error)
        {
            report.AppendLine("RESULT: FAIL\n"+error);throw;
        }
        finally
        {
            if(game!=null) game.ReleaseSwing();
            Physics2D.simulationMode=simulation;Physics2D.gravity=gravity;Time.fixedDeltaTime=fixedStep;
            if(hadBest) PlayerPrefs.SetInt("TwinSwingBest",best);else PlayerPrefs.DeleteKey("TwinSwingBest");
            PlayerPrefs.Save();
            File.WriteAllText(Path.Combine(directory,"elastic-rope-report.txt"),report.ToString());
            EditorSceneManager.OpenScene("Assets/Scenes/Flight.unity");
        }
    }

    private static float MeasureLaunchTravel(float holdTime)
    {
        game.StartRun(true);
        Vector2 pivot=game.sceneAnchors[0].body.position;
        PositionPlayer(pivot+Vector2.down*2);
        Invoke("HandleSpacePressed");
        for(int i=0;i<Mathf.CeilToInt(holdTime/Dt);i++) Tick(true);
        Invoke("HandleSpaceReleased");
        if(game.Attached) throw new InvalidOperationException("Charged comparison launch did not detach.");
        float startX=game.player.position.x;
        for(int i=0;i<300;i++)
        {
            Tick(false);
            if(game.Attached) throw new InvalidOperationException("Comparison launch unexpectedly reattached.");
            if(game.player.position.y<=-3.5f) return game.player.position.x-startX;
        }
        throw new InvalidOperationException("Comparison launch did not reach the shared landing height.");
    }

    private static void PositionPlayer(Vector2 position)
    {
        game.player.position=position;game.player.transform.position=position;
        game.player.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
    }
    private static void Tick(bool holding)
    {
        if(holding) charge.Invoke(game,new object[]{Dt});
        step.Invoke(game,new object[]{Dt});
        Physics2D.Simulate(Dt);
    }
    private static void Invoke(string name,params object[] args) => typeof(TwinSwingGame).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(game,args);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static void Check(bool condition,string message)
    {
        if(!condition) throw new InvalidOperationException(message);
        report.AppendLine("PASS: "+message);
    }
}
