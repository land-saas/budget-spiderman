using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rope-swing collectathon. Tap Space near an anchor to grab it, hold Space to charge,
/// release to launch. The rope is rigid, so a swing never gains or loses energy by itself:
/// speed only changes through gravity, the gentle forward assist, and the charged launch.
/// </summary>
public sealed class TwinSwingGame : MonoBehaviour
{
    public enum Mode { Ready, Countdown, Flying, Crashed }

    public Mode State { get; private set; }
    public int Score { get; private set; }
    public int Best { get; private set; }
    public float DistanceTravelled { get; private set; }
    public bool Attached => swingJoint!=null;
    /// <summary>Consecutive perfect launches. Resets on a non-perfect launch or a crash.</summary>
    public int Streak { get; private set; }
    /// <summary>Points the next gem is worth: 1 plus the current streak, capped.</summary>
    public int GemValue => 1+Mathf.Min(Streak,maximumStreakBonus);
    /// <summary>Seconds left on the "perfect" popup, counting down from 1.</summary>
    public float PerfectPopup { get; private set; }
    public bool LastLaunchPerfect { get; private set; }
    public bool AnchorInReach { get; private set; }
    public bool Charging => Attached && spaceHolding && pumpCharge>0;
    public bool LaunchWouldBePerfect { get; private set; }
    public string LastCrashReason { get; private set; } = "";
    public bool LastCrashWasHigh { get; private set; }
    /// <summary>Session totals, kept across retries, so tutorial prompts fade once learned.</summary>
    public int TotalGrabs { get; private set; }
    public int TotalChargedLaunches { get; private set; }
    // Read-only hooks for presentation layers such as TwinlineAtmosphere.
    public IReadOnlyList<SwingAnchor> Anchors => anchors;
    public IReadOnlyList<SwingGemPickup> Gems => gems;
    public SwingAnchor ActiveAnchor => activeAnchor;
    public SwingAnchor HighlightedAnchor { get; private set; }
    public event Action<Vector3> GemCollected;
    public event Action<Vector3,Vector2,bool> Launched;
    public event Action<Vector3> Grabbed;
    public event Action<Vector3> Crashed;
    public event Action RunReset;
    public Rigidbody2D player;
    public Camera sharedCamera;
    public LineRenderer rope;
    [Header("Swing physics")]
    public float gravity=9.4f;
    public float attachRange=7f;
    [HideInInspector] public float scrollSpeed=2.1f; // Legacy serialized value; the course no longer scrolls.
    [Tooltip("Legacy generated-world half-height. In the saved scene, edit the camera Orthographic Size and boundary collider positions directly.")]
    public float corridorHalfHeight=6.1f;
    public float anchorSpacing=6.2f;
    [HideInInspector] public float gemSpacing=4.6f; // Legacy: gems now follow each anchor's perfect arc.
    [Tooltip("Minimum resting rope length. The elastic spring may compress or stretch around it.")]
    public float minRopeLength=.75f;
    [Tooltip("Maximum resting rope length at attachment, not a rigid extension limit.")]
    public float maxRopeLength=6.8f;
    [HideInInspector] public float swingBoost=0.7f; // Legacy: catching never multiplies velocity.
    [Tooltip("Safety cap for extreme repeated launches. Normal swing momentum is preserved.")]
    [Min(1)] public float maximumPlayerSpeed=12f;
    public float chargeHoldDelay=.16f;
    public float pumpChargeTime=.75f;
    public float lowMomentumSpeed=3f;
    public float minimumPumpImpulse=.4f;
    public float maximumPumpImpulse=2.8f;
    // Legacy elastic-rope settings. The rope is now rigid; these stay serialized so older
    // scenes and validation tools keep loading, but they no longer affect play.
    [HideInInspector] public float ropeFrequency=2.2f;
    [HideInInspector] public float ropeDamping=.3f;
    [HideInInspector] public float holdStretchAcceleration=20f;
    [HideInInspector] public float maximumHoldStretch=.1f;
    [HideInInspector] public float releaseRecoilStrength=.25f;
    [HideInInspector] public float maximumReleaseRecoil=1.2f;
    [Header("Readable swing")]
    [Tooltip("Top speed while on the rope. A rigid rope plus this cap stops momentum from building up by accident.")]
    [Min(1)] public float maximumSwingSpeed=9f;
    [Tooltip("Gentle push along a forward swing whenever you are slower than Swing Assist Speed, so you can never get stuck hanging still.")]
    [Min(0)] public float swingAssist=3.5f;
    [Min(0)] public float swingAssistSpeed=4.5f;
    [Tooltip("Launch speed for a quick release with no charge.")]
    [Min(0)] public float minimumLaunchSpeed=5f;
    [Tooltip("Launch speed at full charge. Launches never stack above this, so repeated launches stay predictable.")]
    [Min(0)] public float maximumLaunchSpeed=10.5f;
    [Tooltip("Tilts every launch slightly upward so releasing at the bottom of a swing still carries you forward.")]
    [Range(0,1)] public float launchLift=.35f;
    [Tooltip("A launch heading forward and up between these angles, with at least half charge, counts as perfect.")]
    public Vector2 perfectLaunchAngles=new Vector2(20,70);
    [Range(0,1)] public float perfectMinimumCharge=.5f;
    [Tooltip("Gems are worth 1 + streak, up to this extra amount.")]
    [Min(0)] public int maximumStreakBonus=4;
    [Header("Gems on perfect arcs")]
    [Tooltip("Each anchor gets this many gems, placed along the path a perfect launch from that anchor would fly.")]
    [Range(1,6)] public int gemsPerAnchor=3;
    [Tooltip("Rope lengths the gem arcs are designed for. Each anchor picks one at random.")]
    public Vector2 gemArcRopeLength=new Vector2(2.4f,3.8f);
    [Tooltip("Charge the gem arcs are designed for. Each anchor picks one at random, so some need a fuller charge.")]
    public Vector2 gemArcCharge=new Vector2(.6f,1f);
    [Tooltip("Seconds into the flight where the first gem sits.")]
    [Min(0)] public float gemArcStart=.2f;
    [Tooltip("Seconds of flight between gems on the same arc.")]
    [Min(.05f)] public float gemArcSpacing=.15f;
    [Tooltip("When you grab an anchor, its gems glide once onto its perfect path for your actual rope length. This is how long that glide takes.")]
    [Min(.05f)] public float gemGlideTime=.5f;
    [Tooltip("After a perfect launch, gems within this distance drift gently toward you, forgiving a near miss.")]
    [Min(0)] public float gemMagnetRadius=.75f;
    [Min(0)] public float gemMagnetSpeed=5f;
    [Min(0)] public float gemMagnetTime=.9f;
    [Header("Swing guides")]
    public bool showLaunchArc=true;
    [Min(.2f)] public float launchArcSeconds=1.1f;
    public bool showChargeRing=true;
    public bool showBoundaryLines=true;
    [Tooltip("Adds the on-screen how-to-play panel, prompts, gem counter and streak popups.")]
    public bool showGuide=true;
    [Tooltip("Adds the dusk sky, parallax hills, glows, player trail and particle bursts.")]
    public bool showAtmosphere=true;
    [Header("Rope appearance")]
    [Range(4,64)] public int ropeSegments=24;
    [Range(0f,.2f)] public float ropeVisualSlack=.005f;
    [Min(0)] public float ropeVisualGravity=4f;
    [HideInInspector] public float ropeVisualDamping=.97f; // Legacy strand setting.
    public float ChargeAmount => pumpCharge;
    public Vector2 AnchorWorldPosition => anchorWorldPosition;
    public const int Seed=4173;
    [Header("Course generation")]
    public int courseSeed=Seed;
    [Tooltip("Pick a new seed every run so the endless course past the opening is different each time.")]
    public bool randomizeCourseEachRun=true;
    public Vector2 anchorHeightRange=new Vector2(-1.6f,4.6f);
    [HideInInspector] public Vector2 gemHeightRange=new Vector2(-4f,4f); // Legacy.
    [HideInInspector] public float openingSlowDuration=5f;
    [HideInInspector] public float openingScrollMultiplier=.5f;
    [Header("Run timing")]
    [Min(0)] public float startCountdownDuration=1.5f;
    [Min(0)] public float retryDelay=.2f;
    [Header("Camera and visual effects")]
    [Min(0)] public float cameraFollowSpeed=6f;
    [Min(0)] public float cameraMaximumLead=2.8f;
    public float cameraLeadInset=2.2f;
    [Tooltip("How far ahead the camera looks per unit of horizontal speed.")]
    [Min(0)] public float cameraVelocityLead=.35f;
    [Tooltip("The player is never allowed closer than this to the left or right screen edge.")]
    [Min(0)] public float cameraEdgeMargin=2.5f;
    [Min(1)] public float anchorHighlightScale=1.35f;
    [Range(0,2)] public float chargeVisualGrowth=0f;
    [Header("Controls and editable interface")]
    public KeyCode actionKey=KeyCode.Space;
    public TwinlineHud hud;
    public float CountdownRemaining => countdown;
    public float FlashOpacity => flash*.35f;
    public void PressAction() => HandleSpacePressed();
    public static readonly Color PlayerRed=new Color(1f,.28f,.22f);
    public static readonly Color NodeYellow=new Color(1f,.88f,.18f);
    public static readonly Color GemGold=new Color(1f,.68f,.08f);
    public static readonly Color RopeTint=new Color(1f,.92f,.42f);
    private static readonly Color Dark=Color.black;
    private static readonly Color HudPaper=new Color(.95f,.95f,.95f);

    [Header("Saved scene objects")]
    [Tooltip("Use the player, camera, rope and course placed in this scene.")]
    public bool useSceneObjects;
    [Tooltip("Restore the initial scene positions on retry. Spacing fields still control endless recycling.")]
    public bool useAuthoredCourse=true;
    public SwingAnchor[] sceneAnchors;
    public SwingGemPickup[] sceneGems;
    public Transform playerVisual;
    public AudioSource audioSource;
    [Header("Sound library")]
    public AudioClip attachSound,releaseSound,gemSound,crashSound;
    [Range(0,1)] public float attachVolume=.22f;
    [Range(0,1)] public float releaseVolume=.18f;
    [Range(0,1)] public float gemVolume=.24f;
    [Range(0,1)] public float crashVolume=.35f;
    [Range(0,1)] public float pumpVolume=.24f;
    [Header("Optional background music")]
    [Tooltip("Assign a separate scene AudioSource and an imported track. No music is supplied or generated by the game.")]
    public AudioSource musicSource;

    private readonly List<SwingAnchor> anchors=new List<SwingAnchor>();
    private readonly List<SwingGemPickup> gems=new List<SwingGemPickup>();
    private Vector2[] initialAnchorPositions;
    private Vector2 initialPlayerPosition;
    private Vector3 playerVisualScale;
    private Vector3[] anchorRingScales;
    private readonly List<UnityEngine.Object> resources=new List<UnityEngine.Object>();
    private DistanceJoint2D swingJoint;
    private Vector2 anchorWorldPosition;
    private Vector3[] ropePositions;
    private readonly List<BoxCollider2D> boundaries=new List<BoxCollider2D>();
    private readonly List<Vector3> initialBoundaryPositions=new List<Vector3>();
    private SwingAnchor activeAnchor;
    private Collider2D attachedPlayerCollider,attachedAnchorCollider;
    private bool previousAnchorCollisionIgnored;
    private Mesh disc;
    private Material ink;
    private System.Random rng;
    private Vector3 cameraCenter;
    private float countdown,crashAge,flash,shake,runTime,attachPulse,attachRopeLength,spaceHoldAge,pumpCharge;
    private int nextAnchor,nextGem;
    private bool built,spaceHolding,attachedThisPress;
    private GUIStyle labelStyle,hintStyle,countStyle;
    private Material guideInk;
    private LineRenderer launchArc,chargeRing;
    private readonly List<LineRenderer> boundaryLines=new List<LineRenderer>();
    private readonly Vector3[] arcPoints=new Vector3[48];
    private readonly Vector3[] ringPoints=new Vector3[41];
    private int runSeed;
    // Per-anchor gem arc design: release angle (radians from straight down) and charge.
    private float[] gemArcTheta=new float[0],gemArcDesignCharge=new float[0];
    private Vector3[] gemGlideFrom=new Vector3[0],gemGlideTo=new Vector3[0];
    private float[] gemGlideAge=new float[0];
    private float magnetTimer;

    private void Awake()
    {
        BuildWorld();
        BuildGuides();
        if(showGuide && Application.isPlaying && GetComponent<TwinlineGuide>()==null)
            gameObject.AddComponent<TwinlineGuide>().game=this;
        if(showAtmosphere && Application.isPlaying && GetComponent<TwinlineAtmosphere>()==null)
            gameObject.AddComponent<TwinlineAtmosphere>().game=this;
        ResetRun();
    }

    public void BuildWorld()
    {
        if(built) return;
        Time.fixedDeltaTime=.01f;
        Physics2D.gravity=new Vector2(0,-9.81f);
        if(useSceneObjects)
        {
            BindSceneWorld();
            built=true;
            return;
        }
        built=true;
        ink=new Material(Resources.Load<Shader>("FlatInk"));resources.Add(ink);
        disc=DiscMesh();resources.Add(disc);

        var playerObject=new GameObject("Swinger");playerObject.transform.SetParent(transform);
        player=playerObject.AddComponent<Rigidbody2D>();
        player.constraints=RigidbodyConstraints2D.FreezeRotation;
        player.mass=1;player.linearDamping=.02f;player.interpolation=RigidbodyInterpolation2D.Interpolate;
        player.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
        playerObject.AddComponent<CircleCollider2D>().radius=.205f;
        var avatar=playerObject.AddComponent<SwingPlayer>();avatar.game=this;
        playerVisual=Draw("Player body",disc,Vector2.zero,new Vector2(.235f,.235f),PlayerRed,12,playerObject.transform);

        sharedCamera=new GameObject("Shared camera").AddComponent<Camera>();
        sharedCamera.transform.SetParent(transform);sharedCamera.tag="MainCamera";
        sharedCamera.orthographic=true;sharedCamera.orthographicSize=corridorHalfHeight+.5f;
        sharedCamera.clearFlags=CameraClearFlags.SolidColor;sharedCamera.backgroundColor=Dark;
        sharedCamera.rect=new Rect(0,0,1,1);sharedCamera.cullingMask=1<<0;
        playerObject.AddComponent<AudioListener>();

        rope=Stroke("Swing rope",new Vector3[17],.024f,RopeTint,8,transform);
        rope.enabled=false;

        for(int sign=-1;sign<=1;sign+=2)
        {
            var border=new GameObject("Boundary "+sign);border.transform.SetParent(transform);
            border.transform.position=new Vector2(0,sign*(corridorHalfHeight+.5f));
            var collider=border.AddComponent<BoxCollider2D>();collider.size=new Vector2(400,1);
            boundaries.Add(collider);initialBoundaryPositions.Add(border.transform.position);
        }

        for(int n=0;n<8;n++) anchors.Add(CreateAnchor(n));
        for(int n=0;n<anchors.Count*Mathf.Max(1,gemsPerAnchor);n++) gems.Add(CreateGem(n));

        audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=0;
        attachSound=Tone(420,520,.08f);releaseSound=Tone(680,420,.1f);gemSound=Tone(880,1180,.12f);crashSound=Tone(150,42,.25f);
        playerVisualScale=playerVisual.localScale;
        initialPlayerPosition=new Vector2(0,1.2f);
    }

    private void BindSceneWorld()
    {
        if(player==null || sharedCamera==null || rope==null || playerVisual==null ||
            sceneAnchors==null || sceneAnchors.Length==0 || sceneGems==null || sceneGems.Length==0)
            throw new InvalidOperationException("Twinline scene is missing references. Assign saved objects in the Game inspector.");
        initialAnchorPositions=new Vector2[sceneAnchors.Length];
        anchorRingScales=new Vector3[sceneAnchors.Length];
        for(int i=0;i<sceneAnchors.Length;i++)
        {
            var anchor=sceneAnchors[i];
            if(anchor==null || anchor.body==null || anchor.ring==null)
                throw new InvalidOperationException("A scene anchor needs a body and a visual.");
            anchors.Add(anchor);
            initialAnchorPositions[i]=anchor.transform.position;
            anchorRingScales[i]=anchor.ring.localScale;
        }
        for(int i=0;i<sceneGems.Length;i++)
        {
            var gem=sceneGems[i];
            if(gem==null || gem.visual==null || gem.pickupCollider==null)
                throw new InvalidOperationException("A scene gem needs a visual and a pickup collider.");
            gem.game=this;gems.Add(gem);
        }
        // Every anchor needs its own set of gems; copy the saved gem to fill the pool.
        while(gems.Count<anchors.Count*Mathf.Max(1,gemsPerAnchor))
        {
            var copy=Instantiate(sceneGems[0].gameObject,sceneGems[0].transform.parent);
            copy.name=sceneGems[0].name+" "+gems.Count;
            var gem=copy.GetComponent<SwingGemPickup>();gem.game=this;gems.Add(gem);
        }
        foreach(var root in gameObject.scene.GetRootGameObjects())
            foreach(var collider in root.GetComponentsInChildren<BoxCollider2D>(true))
                if(collider.name.Contains("Boundary") && collider.size.x>=100)
                {
                    boundaries.Add(collider);initialBoundaryPositions.Add(collider.transform.position);
                }
        player.GetComponent<SwingPlayer>().game=this;
        initialPlayerPosition=player.position;
        playerVisualScale=playerVisual.localScale;
        rope.enabled=false;
    }

    public void ResetRun()
    {
        State=Mode.Ready;Score=0;DistanceTravelled=0;runTime=0;nextAnchor=0;nextGem=0;
        Best=PlayerPrefs.GetInt("TwinSwingBest",0);
        rng=new System.Random(runSeed!=0?runSeed:courseSeed);
        countdown=crashAge=flash=shake=attachPulse=spaceHoldAge=pumpCharge=0;
        Streak=0;PerfectPopup=0;LastLaunchPerfect=false;magnetTimer=0;
        for(int i=0;i<gemGlideAge.Length;i++) gemGlideAge[i]=-1;
        spaceHolding=false;
        ReleaseSwing();
        player.simulated=true;
        player.transform.position=player.position=initialPlayerPosition;
        player.linearVelocity=Vector2.zero;player.angularVelocity=0;
        player.gravityScale=0;
        cameraCenter=CameraTarget();sharedCamera.transform.position=cameraCenter;
        // Advance the same seeded sequence even for a hand-placed first course.
        LayoutCourse(0f);
        if(useSceneObjects && useAuthoredCourse)
            for(int i=0;i<anchors.Count;i++)
                anchors[i].transform.position=anchors[i].body.position=initialAnchorPositions[i];
        // Gems are always derived from the anchors, so they sit on each anchor's perfect arc.
        for(int i=0;i<anchors.Count;i++) PlaceGemsForAnchor(i);
        for(int i=0;i<boundaries.Count;i++) boundaries[i].transform.position=initialBoundaryPositions[i];
        Physics2D.SyncTransforms();
        RunReset?.Invoke();
    }

    public void StartRun(bool skipCountdown=false)
    {
        runSeed=randomizeCourseEachRun?UnityEngine.Random.Range(1,int.MaxValue):courseSeed;
        ResetRun();
        player.gravityScale=gravity/9.81f;
        player.simulated=skipCountdown;
        if(skipCountdown) State=Mode.Flying;
        else { State=Mode.Countdown;countdown=startCountdownDuration; }
    }

    private void Update()
    {
        if(Input.GetKeyDown(actionKey)) HandleSpacePressed();
        if(Input.GetKeyUp(actionKey)) HandleSpaceReleased();
        UpdateHoldCharge(Time.deltaTime);
        if(State==Mode.Crashed) crashAge+=Time.unscaledDeltaTime;
        flash=Mathf.MoveTowards(flash,0,Time.unscaledDeltaTime*2.8f);
        shake=Mathf.MoveTowards(shake,0,Time.unscaledDeltaTime*1.6f);
        attachPulse=Mathf.Max(0,attachPulse-Time.deltaTime);
        PerfectPopup=Mathf.Max(0,PerfectPopup-Time.unscaledDeltaTime);
    }

    private void FixedUpdate() => Step(Time.fixedDeltaTime);

    private void UpdateHoldCharge(float dt)
    {
        if(State!=Mode.Flying || !Attached || !spaceHolding) return;
        float previousAge=spaceHoldAge;
        spaceHoldAge+=Mathf.Max(0,dt);
        float chargingTime=Mathf.Max(0,spaceHoldAge-Mathf.Max(previousAge,chargeHoldDelay));
        pumpCharge=Mathf.MoveTowards(pumpCharge,1f,chargingTime/Mathf.Max(.1f,pumpChargeTime));
    }

    private void CancelHeldAction()
    {
        spaceHolding=attachedThisPress=false;
        spaceHoldAge=pumpCharge=0;
    }

    private void OnApplicationFocus(bool focused) { if(!focused) ReleaseSwing(); }
    private void OnApplicationPause(bool paused) { if(paused) ReleaseSwing(); }
    private void OnDisable() => ReleaseSwing();

    private void HandleSpacePressed()
    {
        if(State==Mode.Ready) StartRun();
        else if(State==Mode.Crashed && crashAge>retryDelay) { ResetRun();StartRun(); }
        else if(State==Mode.Flying)
        {
            spaceHolding=true;spaceHoldAge=pumpCharge=0;
            attachedThisPress=!Attached;
            if(!Attached) TryAttach();
        }
    }

    private void HandleSpaceReleased()
    {
        if(State==Mode.Flying && spaceHolding && Attached)
        {
            // Tap to grab; any later release (or a charged first hold) launches.
            if(pumpCharge>0 || !attachedThisPress) { Launch();ReleaseSwing(); }
        }
        CancelHeldAction();
    }

    public void ToggleSwing()
    {
        if(Attached) ReleaseSwing();
        else TryAttach();
    }

    public void TryAttach()
    {
        if(Attached || State!=Mode.Flying) return;
        SwingAnchor nearest=null;
        float nearestDistance=attachRange;
        foreach(var anchor in anchors)
        {
            float distance=Vector2.Distance(player.position,anchor.body.position);
            if(distance<=nearestDistance) { nearest=anchor;nearestDistance=distance; }
        }
        if(nearest==null) return;
        activeAnchor=nearest;
        anchorWorldPosition=nearest.body.position;
        // Cancel any queued scrolling motion before pinning this pivot in world space.
        nearest.body.position=anchorWorldPosition;
        nearest.body.linearVelocity=Vector2.zero;
        nearest.body.angularVelocity=0;
        attachRopeLength=Mathf.Clamp(nearestDistance,minRopeLength,maxRopeLength);
        // Grabbing keeps your speed and turns it into swing: the part of your motion that
        // would pull against the rope is redirected along the swing instead of lost.
        Vector2 radial=player.position-anchorWorldPosition;
        if(radial.sqrMagnitude>.0001f)
        {
            Vector2 tangent=SwingTangent(radial.normalized,player.linearVelocity);
            player.linearVelocity=tangent*player.linearVelocity.magnitude;
        }
        player.linearVelocity=Vector2.ClampMagnitude(player.linearVelocity,Mathf.Max(1,maximumSwingSpeed));
        swingJoint=player.gameObject.AddComponent<DistanceJoint2D>();
        swingJoint.connectedBody=null;
        swingJoint.autoConfigureConnectedAnchor=false;
        swingJoint.autoConfigureDistance=false;
        swingJoint.anchor=Vector2.zero;
        swingJoint.connectedAnchor=anchorWorldPosition;
        swingJoint.distance=attachRopeLength;
        swingJoint.maxDistanceOnly=false;
        swingJoint.enableCollision=false;
        swingJoint.breakForce=Mathf.Infinity;
        // A world-connected joint cannot automatically suppress player/pivot collisions.
        attachedPlayerCollider=player.GetComponent<Collider2D>();
        attachedAnchorCollider=nearest.GetComponent<Collider2D>();
        if(attachedPlayerCollider!=null && attachedAnchorCollider!=null)
        {
            previousAnchorCollisionIgnored=Physics2D.GetIgnoreCollision(attachedPlayerCollider,attachedAnchorCollider);
            Physics2D.IgnoreCollision(attachedPlayerCollider,attachedAnchorCollider,true);
        }
        rope.enabled=true;
        ResetRopeVisual();
        attachPulse=.35f;
        TotalGrabs++;
        RetargetGemsOnGrab();
        Grabbed?.Invoke(anchorWorldPosition);
        Play(attachSound,attachVolume);
    }

    public void ReleaseSwing()
    {
        if(attachedPlayerCollider!=null && attachedAnchorCollider!=null)
            Physics2D.IgnoreCollision(attachedPlayerCollider,attachedAnchorCollider,previousAnchorCollisionIgnored);
        attachedPlayerCollider=attachedAnchorCollider=null;
        activeAnchor=null;
        CancelHeldAction();
        ropePositions=null;
        if(swingJoint!=null)
        {
            swingJoint.enabled=false;
            if(Application.isPlaying) Destroy(swingJoint);
            else DestroyImmediate(swingJoint);
            swingJoint=null;
            Play(releaseSound,releaseVolume);
        }
        if(rope!=null) rope.enabled=false;
    }

    public void CollectGem(SwingGemPickup gem)
    {
        if(gem.collected || State!=Mode.Flying) return;
        gem.collected=true;
        gem.visual.gameObject.SetActive(false);
        gem.pickupCollider.enabled=false;
        Score+=GemValue;
        flash=.08f;
        GemCollected?.Invoke(gem.transform.position);
        Play(gemSound,gemVolume);
    }

    public void Crash(string reason)
    {
        if(State!=Mode.Flying) return;
        State=Mode.Crashed;crashAge=0;
        LastCrashReason=reason;LastCrashWasHigh=player.position.y>0;
        Crashed?.Invoke(player.position);
        Streak=0;
        spaceHolding=false;spaceHoldAge=pumpCharge=0;
        ReleaseSwing();
        player.simulated=false;
        Best=Mathf.Max(Best,Score);
        PlayerPrefs.SetInt("TwinSwingBest",Best);PlayerPrefs.Save();
        flash=.22f;shake=.14f;
        Play(crashSound,crashVolume);
    }

    private void Step(float dt)
    {
        if(State==Mode.Ready)
        {
            player.AddForce(Vector2.up*(-player.position.y*5-player.linearVelocity.y*2)*player.mass);
            return;
        }
        if(State==Mode.Countdown)
        {
            countdown-=dt;
            if(countdown<=0)
            {
                State=Mode.Flying;player.simulated=true;
                // A real key held through the countdown can catch the opening anchor immediately.
                if(Input.GetKey(actionKey)) { spaceHolding=true;attachedThisPress=true;TryAttach(); }
            }
            return;
        }
        if(State!=Mode.Flying) return;
        if(spaceHolding && !Attached) TryAttach();
        ApplySwingAssist(dt);
        player.linearVelocity=Vector2.ClampMagnitude(player.linearVelocity,
            Mathf.Max(1,Attached?Mathf.Min(maximumSwingSpeed,maximumPlayerSpeed):maximumPlayerSpeed));
        runTime+=dt;

        // A fixed world: only the player and camera travel. Progress measures actual travel,
        // not a timer that drags anchor points and pickups past a stationary player.
        DistanceTravelled=Mathf.Max(DistanceTravelled,player.position.x-initialPlayerPosition.x);
        foreach(var anchor in anchors)
        {
            anchor.body.linearVelocity=Vector2.zero;
            anchor.body.angularVelocity=0;
        }
        if(activeAnchor!=null) activeAnchor.body.position=anchorWorldPosition;
        foreach(var boundary in boundaries)
        {
            Vector3 position=boundary.transform.position;position.x=player.position.x;
            boundary.transform.position=position;
        }
        UpdateGemMotion(dt);
        RecycleAnchors();

    }

    /// <summary>The swing direction: the way you are already moving, or forward when nearly still.</summary>
    private static Vector2 SwingTangent(Vector2 radial,Vector2 velocity)
    {
        Vector2 tangent=new Vector2(-radial.y,radial.x);
        float along=Vector2.Dot(velocity,tangent);
        if(Mathf.Abs(along)>.25f) return tangent*Mathf.Sign(along);
        return tangent.x<0?-tangent:tangent;
    }

    /// <summary>
    /// A small, capped push along forward swings. It only acts below Swing Assist Speed,
    /// so it rescues a stalled swing without ever building runaway momentum.
    /// </summary>
    private void ApplySwingAssist(float dt)
    {
        if(!Attached || swingAssist<=0) return;
        Vector2 radial=player.position-anchorWorldPosition;
        if(radial.sqrMagnitude<.0001f) return;
        Vector2 tangent=SwingTangent(radial.normalized,player.linearVelocity);
        if(tangent.x<0) return; // Only forward swings are helped.
        float speed=Mathf.Abs(Vector2.Dot(player.linearVelocity,tangent));
        if(speed>=swingAssistSpeed) return;
        float strength=1-speed/Mathf.Max(.1f,swingAssistSpeed);
        player.linearVelocity+=tangent*(swingAssist*strength*dt);
    }

    /// <summary>
    /// The velocity a release would give right now. The launch arc preview and the real
    /// launch both use this, so what you see is exactly what you get.
    /// </summary>
    public Vector2 PredictLaunchVelocity()
    {
        if(!Attached) return player.linearVelocity;
        Vector2 radial=player.position-anchorWorldPosition;
        if(radial.sqrMagnitude<.0001f) return player.linearVelocity;
        Vector2 tangent=SwingTangent(radial.normalized,player.linearVelocity);
        Vector2 direction=(tangent+Vector2.up*launchLift).normalized;
        float charged=Mathf.Lerp(minimumLaunchSpeed,maximumLaunchSpeed,pumpCharge);
        float speed=Mathf.Max(player.linearVelocity.magnitude,charged);
        return direction*Mathf.Min(speed,Mathf.Max(maximumLaunchSpeed,1),maximumPlayerSpeed);
    }

    private bool IsPerfect(Vector2 velocity,float charge)
    {
        if(velocity.x<=0 || charge<perfectMinimumCharge) return false;
        float angle=Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg;
        return angle>=perfectLaunchAngles.x && angle<=perfectLaunchAngles.y;
    }

    private void Launch()
    {
        if(!Attached || activeAnchor==null) return;
        float charge=pumpCharge;
        Vector2 velocity=PredictLaunchVelocity();
        player.linearVelocity=velocity;
        LastLaunchPerfect=IsPerfect(velocity,charge);
        if(LastLaunchPerfect) { Streak++;PerfectPopup=1f;flash=Mathf.Max(flash,.12f);magnetTimer=gemMagnetTime; }
        else Streak=0;
        if(charge>0) TotalChargedLaunches++;
        Launched?.Invoke(player.position,velocity,LastLaunchPerfect);
        flash=Mathf.Max(flash,.045f+charge*.055f);
        shake=Mathf.Max(shake,.02f+charge*.025f);
        attachPulse=.25f;
        Play(attachSound,pumpVolume);
        pumpCharge=0;
    }

    private float RecycleBehindX()
    {
        float halfWidth=sharedCamera.orthographicSize*sharedCamera.aspect;
        // Recycle only after an object is both offscreen and out of reach behind the player.
        return Mathf.Min(sharedCamera.transform.position.x-halfWidth-2,player.position.x-attachRange-2);
    }

    private void RecycleAnchors()
    {
        for(int i=0;i<anchors.Count;i++)
        {
            var anchor=anchors[i];
            if(anchor==activeAnchor) continue;
            if(anchor.body.position.x<RecycleBehindX() && GemsBehind(i))
            {
                float furthest=player.position.x+sharedCamera.orthographicSize*sharedCamera.aspect;
                foreach(var other in anchors) furthest=Mathf.Max(furthest,other.body.position.x);
                PlaceAnchor(anchor,furthest+anchorSpacing);
                PlaceGemsForAnchor(i);
            }
        }
    }

    /// <summary>An anchor only moves ahead once its gems are collected or offscreen behind.</summary>
    private bool GemsBehind(int anchorIndex)
    {
        int per=Mathf.Max(1,gemsPerAnchor);
        for(int j=0;j<per;j++)
        {
            int k=anchorIndex*per+j;
            if(k>=gems.Count) break;
            if(!gems[k].collected && gems[k].transform.position.x>=RecycleBehindX()) return false;
        }
        return true;
    }

    /// <summary>
    /// Puts an anchor's gems on the path a perfect launch from that anchor flies: swing
    /// forward on a rope of a typical length, let go moving up and forward with a solid
    /// charge. The rope length, charge and angle vary per anchor, so lining the gold
    /// launch arc up with the gems is the skill, and perfects are how you collect them.
    /// </summary>
    private void PlaceGemsForAnchor(int anchorIndex)
    {
        if(gemArcTheta.Length!=anchors.Count)
        {
            gemArcTheta=new float[anchors.Count];
            gemArcDesignCharge=new float[anchors.Count];
        }
        float Pick(Vector2 range)=>Mathf.Lerp(range.x,range.y,(float)rng.NextDouble());
        float ropeLength=Mathf.Clamp(Pick(gemArcRopeLength),minRopeLength,maxRopeLength);
        gemArcDesignCharge[anchorIndex]=Mathf.Clamp(Pick(gemArcCharge),perfectMinimumCharge,1f);
        float lowAngle=Mathf.Min(perfectLaunchAngles.x+10,perfectLaunchAngles.y);
        float highAngle=Mathf.Max(perfectLaunchAngles.y-10,lowAngle);
        float targetAngle=Mathf.Lerp(lowAngle,highAngle,(float)rng.NextDouble());

        // Find where on the forward upswing a release heads off at the target angle.
        float bestTheta=0,bestError=float.MaxValue;
        for(float theta=0;theta<=85f;theta+=.5f)
        {
            float r=theta*Mathf.Deg2Rad;
            float angle=Mathf.Atan2(Mathf.Sin(r)+launchLift,Mathf.Cos(r))*Mathf.Rad2Deg;
            float error=Mathf.Abs(angle-targetAngle);
            if(error<bestError) { bestError=error;bestTheta=r; }
        }
        gemArcTheta[anchorIndex]=bestTheta;

        int per=Mathf.Max(1,gemsPerAnchor);
        for(int j=0;j<per;j++)
        {
            int k=anchorIndex*per+j;
            if(k>=gems.Count) break;
            var gem=gems[k];
            gem.collected=false;
            gem.pickupCollider.enabled=true;
            gem.visual.gameObject.SetActive(true);
            gem.transform.position=DesignedGemPosition(anchorIndex,j,ropeLength,0);
            if(k<gemGlideAge.Length) gemGlideAge[k]=-1;
            gem.index=nextGem++;
        }
    }

    /// <summary>
    /// Where gem j of an anchor sits on that anchor's designed perfect arc, for a given
    /// rope length and the swing speed you would have at the release point.
    /// </summary>
    private Vector3 DesignedGemPosition(int anchorIndex,int j,float ropeLength,float swingSpeed)
    {
        float theta=gemArcTheta[anchorIndex];
        Vector2 anchor=anchors[anchorIndex].body.position;
        Vector2 release=anchor+new Vector2(Mathf.Sin(theta),-Mathf.Cos(theta))*ropeLength;
        Vector2 direction=new Vector2(Mathf.Cos(theta),Mathf.Sin(theta)+launchLift).normalized;
        float speed=Mathf.Max(swingSpeed,Mathf.Lerp(minimumLaunchSpeed,maximumLaunchSpeed,gemArcDesignCharge[anchorIndex]));
        speed=Mathf.Min(speed,maximumLaunchSpeed,maximumPlayerSpeed);
        return ArcPoint(release,direction*speed,gemArcStart+j*gemArcSpacing);
    }

    private Vector3 ArcPoint(Vector2 start,Vector2 velocity,float t)
    {
        Vector2 p=start+velocity*t+.5f*new Vector2(0,-gravity)*t*t;
        p.y=Mathf.Clamp(p.y,FloorCeilingInnerEdge(false)+.5f,FloorCeilingInnerEdge(true)-.5f);
        return new Vector3(p.x,p.y,0);
    }

    /// <summary>
    /// On grab, the rope length is finally known, so the anchor's gems glide once (eased,
    /// about half a second) onto its designed perfect path for that rope. After that they
    /// stay put: hitting them is about matching the timing and charge, not chasing them.
    /// </summary>
    private void RetargetGemsOnGrab()
    {
        if(gemGlideAge.Length!=gems.Count)
        {
            gemGlideFrom=new Vector3[gems.Count];gemGlideTo=new Vector3[gems.Count];
            gemGlideAge=new float[gems.Count];
            for(int i=0;i<gemGlideAge.Length;i++) gemGlideAge[i]=-1;
        }
        int anchorIndex=anchors.IndexOf(activeAnchor);
        if(anchorIndex<0 || anchorIndex>=gemArcTheta.Length) return;
        float ropeLength=swingJoint.distance;
        // Speed you'd have at the designed release point, from energy conservation.
        float theta=gemArcTheta[anchorIndex];
        float releaseHeight=anchorWorldPosition.y-Mathf.Cos(theta)*ropeLength;
        float speedSquared=player.linearVelocity.sqrMagnitude+2*gravity*(player.position.y-releaseHeight);
        float swingSpeed=Mathf.Min(Mathf.Sqrt(Mathf.Max(0,speedSquared)),maximumSwingSpeed);
        int per=Mathf.Max(1,gemsPerAnchor);
        for(int j=0;j<per;j++)
        {
            int k=anchorIndex*per+j;
            if(k>=gems.Count || gems[k].collected) continue;
            gemGlideFrom[k]=gems[k].transform.position;
            gemGlideTo[k]=DesignedGemPosition(anchorIndex,j,ropeLength,swingSpeed);
            gemGlideAge[k]=0;
        }
    }

    private void UpdateGemMotion(float dt)
    {
        for(int k=0;k<gemGlideAge.Length && k<gems.Count;k++)
        {
            if(gemGlideAge[k]<0) continue;
            gemGlideAge[k]+=dt;
            float x=Mathf.Clamp01(gemGlideAge[k]/gemGlideTime);
            float ease=x*x*x*(x*(6*x-15)+10); // Smootherstep: no jolt at either end.
            gems[k].transform.position=Vector3.LerpUnclamped(gemGlideFrom[k],gemGlideTo[k],ease);
            if(x>=1) gemGlideAge[k]=-1;
        }
        if(magnetTimer<=0) return;
        magnetTimer-=dt;
        Vector3 p=player.position;
        foreach(var gem in gems)
        {
            if(gem.collected) continue;
            Vector3 toPlayer=p-gem.transform.position;toPlayer.z=0;
            float d=toPlayer.magnitude;
            if(d>gemMagnetRadius || d<.001f) continue;
            float pull=gemMagnetSpeed*(1-d/gemMagnetRadius)*dt;
            gem.transform.position+=toPlayer/d*Mathf.Min(d,pull);
        }
    }

    private void LayoutCourse(float startX)
    {
        float anchorX=startX+2.4f;
        for(int i=0;i<anchors.Count;i++,anchorX+=anchorSpacing) PlaceAnchor(anchors[i],anchorX);
    }

    private void PlaceAnchor(SwingAnchor anchor,float x)
    {
        float y=nextAnchor switch
        {
            0=>2.4f,
            1=>1.1f,
            2=>2.8f,
            3=>0.4f,
            _=>Mathf.Lerp(anchorHeightRange.x,anchorHeightRange.y,(float)rng.NextDouble())
        };
        anchor.body.position=new Vector2(x,y);
        anchor.body.transform.position=new Vector3(x,y,0);
        nextAnchor++;
    }

    private SwingAnchor CreateAnchor(int index)
    {
        var root=new GameObject("Anchor "+index);root.transform.SetParent(transform);
        var body=root.AddComponent<Rigidbody2D>();
        body.bodyType=RigidbodyType2D.Kinematic;
        root.AddComponent<CircleCollider2D>().radius=.16f;
        var ring=Draw("Anchor ring",disc,Vector2.zero,new Vector2(.19f,.19f),NodeYellow,3,root.transform);
        Draw("Anchor core",disc,Vector2.zero,new Vector2(.08f,.08f),PlayerRed,4,root.transform);
        var anchor=root.AddComponent<SwingAnchor>();anchor.body=body;anchor.ring=ring;return anchor;
    }

    private SwingGemPickup CreateGem(int index)
    {
        var root=new GameObject("Gem "+index);root.transform.SetParent(transform);
        var collider=root.AddComponent<CircleCollider2D>();
        collider.radius=.14f;collider.isTrigger=true;
        var visual=Draw("Gem ink",disc,Vector2.zero,new Vector2(.16f,.16f),GemGold,5,root.transform);
        var gem=root.AddComponent<SwingGemPickup>();
        gem.game=this;gem.visual=visual;gem.pickupCollider=collider;
        return gem;
    }

    private void LateUpdate()
    {
        if(!built) return;
        if(Attached && activeAnchor!=null) UpdateRopeVisual(Time.deltaTime);
        float distance=attachRange;
        SwingAnchor highlight=Attached?activeAnchor:FindNearestAnchor(out distance);
        if(Attached && activeAnchor!=null)
            distance=Vector2.Distance(player.position,activeAnchor.body.position);
        for(int i=0;i<anchors.Count;i++)
        {
            var anchor=anchors[i];
            bool inRange=!Attached && anchor==highlight && distance<=attachRange;
            var baseScale=useSceneObjects?anchorRingScales[i]:Vector3.one;
            anchor.ring.localScale=baseScale*(inRange?anchorHighlightScale:1f);
        }
        Vector3 playerTarget=playerVisualScale*(1+pumpCharge*chargeVisualGrowth);
        playerTarget.z=playerVisualScale.z;
        playerVisual.localScale=Vector3.Lerp(playerVisual.localScale,playerTarget,1-Mathf.Exp(-12*Time.unscaledDeltaTime));
        cameraCenter=Vector3.Lerp(cameraCenter,CameraTarget(),1-Mathf.Exp(-cameraFollowSpeed*Time.unscaledDeltaTime));
        cameraCenter=KeepPlayerOnScreen(cameraCenter);
        AnchorInReach=!Attached && highlight!=null && distance<=attachRange;
        HighlightedAnchor=AnchorInReach?highlight:null;
        UpdateGuides();
        sharedCamera.transform.position=cameraCenter+
            new Vector3(Mathf.Sin(Time.unscaledTime*67),Mathf.Cos(Time.unscaledTime*73),0)*shake;
    }

    private void ResetRopeVisual()
    {
        int count=Mathf.Clamp(ropeSegments,4,64)+1;
        ropePositions=new Vector3[count];
        rope.useWorldSpace=true;rope.positionCount=count;
        for(int i=0;i<count;i++)
            ropePositions[i]=Vector3.Lerp(player.transform.position,anchorWorldPosition,i/(float)(count-1));
        rope.SetPositions(ropePositions);
    }

    private void UpdateRopeVisual(float dt)
    {
        if(ropePositions==null || ropePositions.Length!=Mathf.Clamp(ropeSegments,4,64)+1) ResetRopeVisual();
        Vector3 start=player.transform.position,end=anchorWorldPosition;
        int last=ropePositions.Length-1;
        if(dt<=0)
        {
            ropePositions[0]=start;ropePositions[last]=end;rope.SetPositions(ropePositions);return;
        }
        float distance=Vector3.Distance(start,end);
        float rest=swingJoint.distance;
        float slack=Mathf.Max(0,rest-distance)+Mathf.Clamp(ropeVisualSlack,0,.2f)*rest;
        float sag=Mathf.Min(Mathf.Min(.25f,distance*.12f),slack*.35f*Mathf.Clamp(ropeVisualGravity*.25f,0,2));
        // A bounded curve, never an independently simulated rope. It cannot whip away,
        // accumulate energy, or lag behind the two real spring endpoints.
        for(int i=1;i<last;i++)
        {
            float t=i/(float)last;
            float envelope=Mathf.Sin(Mathf.PI*t);
            ropePositions[i]=Vector3.Lerp(start,end,t)+Vector3.down*(sag*envelope);
        }
        ropePositions[0]=start;ropePositions[last]=end;rope.SetPositions(ropePositions);
    }

    private SwingAnchor FindNearestAnchor(out float distance)
    {
        SwingAnchor best=null;
        distance=attachRange;
        foreach(var anchor in anchors)
        {
            float d=Vector2.Distance(player.position,anchor.body.position);
            if(d<=distance) { best=anchor;distance=d; }
        }
        return best;
    }

    private Vector3 CameraTarget()
    {
        // Look ahead in the direction of travel, so fast launches stay on screen.
        float lead=Mathf.Clamp(player.linearVelocity.x*cameraVelocityLead,-1f,cameraMaximumLead);
        if(State==Mode.Ready || State==Mode.Countdown)
        {
            float halfWidth=sharedCamera.orthographicSize*sharedCamera.aspect;
            lead=Mathf.Clamp(halfWidth-cameraLeadInset,0,cameraMaximumLead);
        }
        return new Vector3(player.position.x+lead,0,-10);
    }

    /// <summary>Hard limit that keeps the player inside the visible area horizontally.</summary>
    private Vector3 KeepPlayerOnScreen(Vector3 center)
    {
        float halfWidth=sharedCamera.orthographicSize*sharedCamera.aspect;
        float margin=Mathf.Min(cameraEdgeMargin,halfWidth*.45f);
        center.x=Mathf.Clamp(center.x,player.position.x-halfWidth+margin,player.position.x+halfWidth-margin);
        return center;
    }

    private void BuildGuides()
    {
        if(!Application.isPlaying || launchArc!=null) return;
        var shader=Resources.Load<Shader>("FlatInk");
        if(shader==null) return;
        guideInk=new Material(shader);resources.Add(guideInk);
        launchArc=GuideLine("Launch arc",arcPoints.Length,.05f,10);
        chargeRing=GuideLine("Charge ring",ringPoints.Length,.06f,13);
        for(int i=0;i<2;i++) boundaryLines.Add(GuideLine(i==0?"Ceiling line":"Floor line",2,.045f,2));
    }

    private LineRenderer GuideLine(string name,int count,float width,int order)
    {
        var obj=new GameObject(name);obj.transform.SetParent(transform,false);
        var line=obj.AddComponent<LineRenderer>();
        line.sharedMaterial=guideInk;line.useWorldSpace=true;line.positionCount=count;
        line.startWidth=line.endWidth=width;line.sortingOrder=order;line.numCapVertices=2;
        line.enabled=false;
        return line;
    }

    private void UpdateGuides()
    {
        if(launchArc==null) return;
        bool flying=State==Mode.Flying;

        // Launch arc: where you would fly if you let go right now.
        LaunchWouldBePerfect=false;
        launchArc.enabled=showLaunchArc && flying && Attached;
        if(launchArc.enabled)
        {
            Vector2 velocity=PredictLaunchVelocity();
            LaunchWouldBePerfect=IsPerfect(velocity,pumpCharge);
            Vector2 g=Physics2D.gravity*player.gravityScale;
            Vector2 p=player.position;
            float step=launchArcSeconds/(arcPoints.Length-1);
            float ceiling=FloorCeilingInnerEdge(true),floor=FloorCeilingInnerEdge(false);
            int used=arcPoints.Length;
            for(int i=0;i<arcPoints.Length;i++)
            {
                arcPoints[i]=p;
                if(p.y>=ceiling || p.y<=floor) { used=i+1;break; }
                p+=velocity*step+.5f*g*step*step;velocity+=g*step;
            }
            launchArc.positionCount=used;
            launchArc.SetPositions(arcPoints);
            Color tint=LaunchWouldBePerfect?GemGold:Color.white;
            float alpha=Mathf.Lerp(.25f,.7f,pumpCharge);
            launchArc.colorGradient=Fade(tint,alpha);
        }

        // Charge ring: fills clockwise as you hold Space.
        chargeRing.enabled=showChargeRing && flying && Charging;
        if(chargeRing.enabled)
        {
            int count=Mathf.Max(2,Mathf.CeilToInt(pumpCharge*(ringPoints.Length-1))+1);
            Vector3 center=player.transform.position;
            for(int i=0;i<count;i++)
            {
                float a=Mathf.PI*.5f-(i/(float)(ringPoints.Length-1))*Mathf.PI*2;
                ringPoints[i]=center+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.42f;
            }
            chargeRing.positionCount=count;
            chargeRing.SetPositions(ringPoints);
            Color ring=pumpCharge>=.999f?GemGold:Color.Lerp(Color.white,NodeYellow,pumpCharge);
            chargeRing.startColor=chargeRing.endColor=ring;
        }

        // Floor and ceiling: visible so crashes are never a surprise off-screen.
        for(int i=0;i<boundaryLines.Count;i++)
        {
            var line=boundaryLines[i];
            line.enabled=showBoundaryLines && boundaries.Count>=2;
            if(!line.enabled) continue;
            float y=FloorCeilingInnerEdge(i==0);
            float halfWidth=sharedCamera.orthographicSize*sharedCamera.aspect+2;
            float x=sharedCamera.transform.position.x;
            line.SetPosition(0,new Vector3(x-halfWidth,y,0));
            line.SetPosition(1,new Vector3(x+halfWidth,y,0));
            float danger=Mathf.Clamp01(1-Mathf.Abs(player.position.y-y)/2.5f);
            Color c=PlayerRed;c.a=Mathf.Lerp(.35f,1f,danger);
            line.startColor=line.endColor=c;
        }
    }

    /// <summary>World height where the ceiling (or floor) collider starts.</summary>
    public float FloorCeilingInnerEdge(bool ceiling)
    {
        float edge=ceiling?float.PositiveInfinity:float.NegativeInfinity;
        foreach(var b in boundaries)
        {
            float y=b.transform.position.y+b.offset.y*b.transform.lossyScale.y;
            float half=b.size.y*.5f*Mathf.Abs(b.transform.lossyScale.y);
            if(ceiling && y>0) edge=Mathf.Min(edge,y-half);
            if(!ceiling && y<0) edge=Mathf.Max(edge,y+half);
        }
        if(float.IsInfinity(edge)) edge=ceiling?corridorHalfHeight:-corridorHalfHeight;
        return edge;
    }

    private static Gradient Fade(Color color,float alpha)
    {
        var gradient=new Gradient();
        gradient.SetKeys(new[]{new GradientColorKey(color,0),new GradientColorKey(color,1)},
            new[]{new GradientAlphaKey(alpha,0),new GradientAlphaKey(alpha*.6f,.6f),new GradientAlphaKey(0,1)});
        return gradient;
    }

    private void OnGUI()
    {
        // Authored scenes use editable Canvas objects. Keep the legacy fallback
        // for the older runtime-only prototype mode.
        if(hud!=null) return;
        float scale=Screen.height/900f;
        float guiWidth=Screen.width/scale;
        GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(scale,scale,1));
        if(labelStyle==null) labelStyle=new GUIStyle(GUI.skin.label){fontSize=34,alignment=TextAnchor.MiddleLeft};
        if(hintStyle==null) hintStyle=new GUIStyle(GUI.skin.label){fontSize=21,alignment=TextAnchor.MiddleCenter};
        if(countStyle==null) countStyle=new GUIStyle(GUI.skin.label){fontSize=112,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
        float center=guiWidth*.5f;
        Box(12,10,245,68,new Color(0,0,0,.78f));
        FloatingText(new Rect(24,18,225,52),$"DIST {Mathf.FloorToInt(DistanceTravelled):000}m",labelStyle,new Color(.85f,.85f,.85f));

        if(State==Mode.Ready || State==Mode.Crashed)
        {
            string action=State==Mode.Ready?"SPACE · BEGIN":"SPACE · RETRY";
            hintStyle.normal.textColor=HudPaper;
            if(GUI.Button(new Rect(center-160,830,320,48),action,hintStyle)) HandleSpacePressed();
        }

        if(State==Mode.Countdown)
        {
            FloatingText(new Rect(center-125,122,250,157),Mathf.CeilToInt(countdown).ToString(),countStyle,HudPaper);
        }

        if(flash>0) Box(0,0,guiWidth,900,new Color(1,1,1,flash*.35f));
    }

    private void OnDestroy() => DisposeWorld();
    public void DisposeWorld()
    {
        foreach(var resource in resources)
            if(resource!=null)
            {
                if(Application.isPlaying) Destroy(resource);
                else DestroyImmediate(resource);
            }
        resources.Clear();
    }

    private static void Box(float x,float y,float w,float h,Color c)
    {
        GUI.color=c;GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);GUI.color=Color.white;
    }

    private static void FloatingText(Rect rect,string text,GUIStyle style,Color color)
    {
        style.normal.textColor=new Color(0,0,0,color.a);
        GUI.Label(new Rect(rect.x+2,rect.y+2,rect.width,rect.height),text,style);
        style.normal.textColor=color;GUI.Label(rect,text,style);
    }

    private void SmallHint(float x,float y,float width,string text,float alpha)
    {
        FloatingText(new Rect(x,y,width,32),text,hintStyle,new Color(.7f,.7f,.7f,alpha));
    }

    private Transform Draw(string name,Mesh mesh,Vector2 position,Vector2 scale,Color color,int order,Transform parent)
    {
        var obj=new GameObject(name);obj.transform.SetParent(parent,false);
        obj.transform.localPosition=position;obj.transform.localScale=new Vector3(scale.x,scale.y,1);
        obj.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=ink;renderer.sortingOrder=order;
        var props=new MaterialPropertyBlock();props.SetColor("_Color",color);renderer.SetPropertyBlock(props);
        return obj.transform;
    }

    private LineRenderer Stroke(string name,Vector3[] positions,float width,Color color,int order,Transform parent)
    {
        var obj=new GameObject(name);obj.transform.SetParent(parent,false);
        var line=obj.AddComponent<LineRenderer>();line.sharedMaterial=ink;line.useWorldSpace=true;
        line.positionCount=positions.Length;line.SetPositions(positions);
        line.startWidth=line.endWidth=width;line.startColor=line.endColor=color;line.sortingOrder=order;
        line.numCapVertices=3;return line;
    }

    private static Mesh DiscMesh()
    {
        const int count=32;
        var vertices=new Vector3[count+1];var triangles=new int[count*3];var colors=new Color[count+1];
        colors[0]=Color.white;
        for(int n=0;n<count;n++)
        {
            float a=n/(float)count*Mathf.PI*2;
            vertices[n+1]=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
            colors[n+1]=Color.white;
            triangles[n*3]=0;triangles[n*3+1]=n+1;triangles[n*3+2]=(n+1)%count+1;
        }
        var mesh=new Mesh{name="Unit disc",vertices=vertices,triangles=triangles,colors=colors};
        mesh.RecalculateBounds();return mesh;
    }

    private AudioClip Tone(float from,float to,float duration)
    {
        const int rate=22050;
        var samples=new float[(int)(rate*duration)];float phase=0;
        for(int n=0;n<samples.Length;n++)
        {
            float t=n/(float)samples.Length;
            phase+=Mathf.Lerp(from,to,t)*2*Mathf.PI/rate;
            samples[n]=Mathf.Sin(phase)*Mathf.Sin(t*Mathf.PI)*Mathf.Exp(-t*3)*.2f;
        }
        var clip=AudioClip.Create("Swing cue",samples.Length,1,rate,false);
        clip.SetData(samples,0);resources.Add(clip);return clip;
    }

    private void Play(AudioClip clip,float volume)
    {
        if(Application.isPlaying && audioSource!=null && clip!=null) audioSource.PlayOneShot(clip,volume);
    }

}
