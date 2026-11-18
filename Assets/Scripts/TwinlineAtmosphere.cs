using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presentation only: a dusk sky with a low sun, parallax stars and hills, soft glows
/// on the player, anchors and gems, a motion trail, and particle bursts for grabs,
/// launches, gems and crashes. Everything is built at runtime and never touches physics,
/// so turning "Show Atmosphere" off on the game restores the plain look.
/// </summary>
[DefaultExecutionOrder(120)]
public sealed class TwinlineAtmosphere : MonoBehaviour
{
    public TwinSwingGame game;

    [Header("Sky")]
    public Color skyTop = new Color(.05f, .04f, .12f);
    public Color skyMiddle = new Color(.17f, .06f, .2f);
    public Color skyHorizon = new Color(.46f, .13f, .17f);
    public Color sunColor = new Color(1f, .6f, .27f);
    [Header("Hills")]
    public Color farHills = new Color(.22f, .07f, .18f);
    public Color nearHills = new Color(.1f, .035f, .1f);
    [Range(0, 1)] public float farParallax = .85f;
    [Range(0, 1)] public float nearParallax = .6f;
    [Header("Glows")]
    [Range(0, 1)] public float glowStrength = 1f;
    public bool playerTrail = true;

    private const float TileWidth = 48f;
    private Material ink;
    private Mesh disc, glow, diamond;
    private Transform sky, sun;
    private readonly List<Transform[]> layers = new List<Transform[]>();
    private readonly List<float> layerParallax = new List<float>();
    private MeshRenderer ceilingBand, floorBand;
    private TrailRenderer trail;
    private Transform playerGlow;
    private MeshRenderer playerGlowRenderer;
    private readonly List<MeshRenderer> anchorHalos = new List<MeshRenderer>();
    private readonly List<MeshRenderer> gemHalos = new List<MeshRenderer>();
    private readonly List<Transform> gemSparkles = new List<Transform>();
    private readonly List<Vector3> gemVisualRest = new List<Vector3>();
    private sealed class Spark { public MeshRenderer renderer; public Vector3 velocity; public float age = 99, life, size; public Color color; }
    private readonly List<Spark> sparks = new List<Spark>();
    private int nextSpark;
    private sealed class Ring { public LineRenderer line; public Vector3 center; public float age = 99, life, radius; public Color color; }
    private readonly List<Ring> rings = new List<Ring>();
    private MaterialPropertyBlock block;

    private static readonly Color Red = TwinSwingGame.PlayerRed;
    private static readonly Color Yellow = TwinSwingGame.NodeYellow;
    private static readonly Color Gold = TwinSwingGame.GemGold;

    private void Start()
    {
        if (game == null) game = GetComponent<TwinSwingGame>();
        var shader = Resources.Load<Shader>("FlatInk");
        if (game == null || shader == null) { enabled = false; return; }
        ink = new Material(shader) { name = "Atmosphere ink" };
        block = new MaterialPropertyBlock();
        disc = Disc(1f, Color.white, Color.white, 40);
        glow = Disc(1f, Color.white, new Color(1, 1, 1, 0), 40);
        diamond = Diamond();

        BuildSky();
        BuildDecorations();
        BuildEffects();

        game.GemCollected += OnGem;
        game.Grabbed += OnGrab;
        game.Launched += OnLaunch;
        game.Crashed += OnCrash;
        game.RunReset += OnReset;
    }

    private void OnDestroy()
    {
        if (game != null)
        {
            game.GemCollected -= OnGem;
            game.Grabbed -= OnGrab;
            game.Launched -= OnLaunch;
            game.Crashed -= OnCrash;
            game.RunReset -= OnReset;
        }
        foreach (var o in new Object[] { ink, disc, glow, diamond })
            if (o != null) Destroy(o);
    }

    // ---------- Building ----------

    private void BuildSky()
    {
        var cam = game.sharedCamera;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = skyTop;

        var skyMesh = new Mesh { name = "Sky gradient" };
        skyMesh.vertices = new[] {
            new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f),
            new Vector3(-.5f, -.1f), new Vector3(.5f, -.1f),
            new Vector3(-.5f, .5f), new Vector3(.5f, .5f) };
        skyMesh.colors = new[] { skyHorizon, skyHorizon, skyMiddle, skyMiddle, skyTop, skyTop };
        skyMesh.triangles = new[] { 0, 2, 1, 1, 2, 3, 2, 4, 3, 3, 4, 5 };
        sky = Renderer("Sky", skyMesh, -100, cam.transform).transform;
        sky.localPosition = new Vector3(0, 0, 15);

        sun = new GameObject("Sun").transform;
        sun.SetParent(transform, false);
        var halo = Renderer("Sun glow", glow, -96, sun); halo.transform.localScale = Vector3.one * 5.5f;
        Tint(halo, new Color(sunColor.r, sunColor.g * .8f, sunColor.b, .45f));
        var core = Renderer("Sun", disc, -95, sun); core.transform.localScale = Vector3.one * 1.25f;
        Tint(core, sunColor);

        AddLayer("Stars", StarsMesh(), -97, .95f);
        AddLayer("Far hills", HillsMesh(-3.3f, new[] { 1.0f, .55f, .3f }, new[] { 2, 5, 9 }, farHills, 1), -90, farParallax);
        AddLayer("Near hills", HillsMesh(-5.0f, new[] { .8f, .4f, .22f }, new[] { 3, 7, 13 }, nearHills, 2), -85, nearParallax);

        ceilingBand = Renderer("Ceiling glow", BandMesh(), 1, transform);
        floorBand = Renderer("Floor glow", BandMesh(), 1, transform);
    }

    private void AddLayer(string name, Mesh mesh, int order, float parallax)
    {
        var copies = new Transform[3];
        for (int i = 0; i < copies.Length; i++)
            copies[i] = Renderer(name + " " + i, mesh, order, transform).transform;
        layers.Add(copies);
        layerParallax.Add(parallax);
    }

    private void BuildDecorations()
    {
        var player = game.player.transform;
        if (playerTrail)
        {
            trail = game.player.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = ink;
            trail.time = .32f;
            trail.minVertexDistance = .04f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, .36f), new Keyframe(1, 0));
            trail.colorGradient = Gradient2(new Color(Red.r, Red.g, Red.b, .55f), new Color(Gold.r, Gold.g, Gold.b, 0));
            trail.sortingOrder = 7;
            trail.numCapVertices = 4;
            trail.emitting = false;
        }
        playerGlowRenderer = Renderer("Player glow", glow, 11, player);
        playerGlow = playerGlowRenderer.transform;
        playerGlow.localScale = Vector3.one * .8f;

        foreach (var anchor in game.Anchors)
        {
            var halo = Renderer("Anchor glow", glow, 2, anchor.transform);
            halo.transform.localScale = Vector3.one * .6f;
            anchorHalos.Add(halo);
        }
        foreach (var gem in game.Gems)
        {
            var halo = Renderer("Gem glow", glow, 4, gem.transform);
            halo.transform.localScale = Vector3.one * .42f;
            gemHalos.Add(halo);
            var sparkle = Renderer("Gem sparkle", diamond, 6, gem.visual).transform;
            // The visual is scaled; undo that so the sparkle has a consistent world size.
            Vector3 s = gem.visual.lossyScale;
            sparkle.localScale = new Vector3(.08f / Mathf.Max(.001f, s.x), .11f / Mathf.Max(.001f, s.y), 1);
            gemSparkles.Add(sparkle);
            gemVisualRest.Add(gem.visual.localPosition);
        }

        if (game.rope != null)
        {
            game.rope.colorGradient = Gradient2(Red, Yellow);
            game.rope.widthMultiplier = 1f;
            game.rope.startWidth = .06f; game.rope.endWidth = .035f;
        }
    }

    private void BuildEffects()
    {
        // A small pool of disc "particles", simulated by hand (no particle module needed).
        var holder = new GameObject("Sparks").transform;
        holder.SetParent(transform, false);
        for (int i = 0; i < 160; i++)
        {
            var r = Renderer("Spark", disc, 14, holder);
            r.enabled = false;
            sparks.Add(new Spark { renderer = r });
        }

        for (int i = 0; i < 8; i++)
        {
            var line = new GameObject("Ring").AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = ink; line.useWorldSpace = true; line.loop = true;
            line.positionCount = 36; line.sortingOrder = 13; line.enabled = false;
            rings.Add(new Ring { line = line });
        }
    }

    // ---------- Per frame ----------

    private void LateUpdate()
    {
        if (game == null || ink == null) return;
        var cam = game.sharedCamera;
        float halfHeight = cam.orthographicSize, halfWidth = halfHeight * cam.aspect;
        Vector3 c = cam.transform.position;
        float t = Time.time;

        sky.localScale = new Vector3(halfWidth * 2 + 2, halfHeight * 2 + 2, 1);
        sun.position = new Vector3(c.x + halfWidth * .42f, -1.6f, 0);

        for (int l = 0; l < layers.Count; l++)
        {
            float origin = c.x * layerParallax[l];
            float first = origin + Mathf.Floor((c.x - halfWidth - origin) / TileWidth) * TileWidth;
            for (int i = 0; i < layers[l].Length; i++)
                layers[l][i].position = new Vector3(first + i * TileWidth, 0, 0);
        }

        UpdateBand(ceilingBand, game.FloorCeilingInnerEdge(true), -1, c.x, halfWidth);
        UpdateBand(floorBand, game.FloorCeilingInnerEdge(false), 1, c.x, halfWidth);

        bool flying = game.State == TwinSwingGame.Mode.Flying;
        if (trail != null) trail.emitting = flying || game.State == TwinSwingGame.Mode.Countdown;

        // Player: a soft glow that warms to gold as the launch charges.
        float charge = game.ChargeAmount;
        playerGlow.localScale = Vector3.one * (.75f + .35f * charge + .04f * Mathf.Sin(t * 6));
        Tint(playerGlowRenderer, Color.Lerp(new Color(Red.r, Red.g, Red.b, .4f), new Color(Gold.r, Gold.g, Gold.b, .7f), charge));
        playerGlow.gameObject.SetActive(game.State != TwinSwingGame.Mode.Crashed);

        var anchors = game.Anchors;
        for (int i = 0; i < anchorHalos.Count && i < anchors.Count; i++)
        {
            var anchor = anchors[i];
            float pulse = 1 + .07f * Mathf.Sin(t * 2.2f + i * 1.7f);
            float scale = .6f; Color color = new Color(Yellow.r, Yellow.g, Yellow.b, .22f);
            if (anchor == game.ActiveAnchor) { scale = .8f; color = new Color(Gold.r, Gold.g, Gold.b, .4f); }
            else if (anchor == game.HighlightedAnchor) { scale = 1.05f; color = new Color(Yellow.r, Yellow.g, Yellow.b, .45f); pulse = 1 + .12f * Mathf.Sin(t * 7); }
            anchorHalos[i].transform.localScale = Vector3.one * scale * pulse;
            Tint(anchorHalos[i], color);
        }

        var gems = game.Gems;
        for (int i = 0; i < gemHalos.Count && i < gems.Count; i++)
        {
            var gem = gems[i];
            bool visible = gem.visual.gameObject.activeSelf;
            gemHalos[i].enabled = visible;
            if (!visible) continue;
            float phase = t * 3f + i * .9f;
            gem.visual.localPosition = gemVisualRest[i] + Vector3.up * (Mathf.Sin(phase) * .06f);
            gemHalos[i].transform.localPosition = gem.visual.localPosition;
            gemHalos[i].transform.localScale = Vector3.one * (.4f + .05f * Mathf.Sin(phase * 1.3f));
            Tint(gemHalos[i], new Color(Gold.r, Gold.g, Gold.b, .3f));
            float twinkle = .55f + .45f * Mathf.Max(0, Mathf.Sin(t * 4.5f + i * 2.3f));
            gemSparkles[i].localRotation = Quaternion.Euler(0, 0, Mathf.Sin(phase * .5f) * 15);
            Tint(gemSparkles[i].GetComponent<MeshRenderer>(), new Color(1, .97f, .85f, twinkle));
        }

        float dt = Time.deltaTime;
        foreach (var spark in sparks)
        {
            if (spark.age >= spark.life) { spark.renderer.enabled = false; continue; }
            spark.age += dt;
            spark.velocity += Vector3.down * (3.3f * dt);
            spark.velocity *= 1 - 1.5f * dt;
            var tr = spark.renderer.transform;
            tr.position += spark.velocity * dt;
            float k = Mathf.Clamp01(spark.age / spark.life);
            tr.localScale = Vector3.one * spark.size * (1 - k);
            Color col = spark.color; col.a *= 1 - k * k;
            Tint(spark.renderer, col);
            spark.renderer.enabled = true;
        }

        foreach (var ring in rings)
        {
            if (ring.age >= ring.life) { ring.line.enabled = false; continue; }
            ring.age += Time.deltaTime;
            float k = Mathf.Clamp01(ring.age / ring.life);
            float ease = 1 - (1 - k) * (1 - k);
            float radius = ring.radius * ease;
            for (int p = 0; p < ring.line.positionCount; p++)
            {
                float a = p / (float)ring.line.positionCount * Mathf.PI * 2;
                ring.line.SetPosition(p, ring.center + new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
            Color col = ring.color; col.a *= 1 - k;
            ring.line.startColor = ring.line.endColor = col;
            ring.line.startWidth = ring.line.endWidth = Mathf.Lerp(.09f, .02f, k);
            ring.line.enabled = true;
        }
    }

    private void UpdateBand(MeshRenderer band, float edge, float inward, float x, float halfWidth)
    {
        band.transform.position = new Vector3(x, edge, 0);
        band.transform.localScale = new Vector3(halfWidth * 2 + 4, inward * 1.4f, 1);
        float distance = Mathf.Abs(game.player.position.y - edge);
        float danger = Mathf.Clamp01(1 - distance / 3f);
        Tint(band, new Color(Red.r, Red.g * .7f, Red.b, (.16f + .4f * danger) * glowStrength));
    }

    // ---------- Event effects ----------

    private void OnGem(Vector3 position)
    {
        Burst(position, Gold, 14, 3.2f, .1f);
        Burst(position, Color.white, 5, 2f, .06f);
        RingAt(position, Gold, .9f, .35f);
    }

    private void OnGrab(Vector3 anchor) => RingAt(anchor, Yellow, .7f, .3f);

    private void OnLaunch(Vector3 position, Vector2 velocity, bool perfect)
    {
        Vector2 back = -velocity.normalized;
        for (int i = 0; i < (perfect ? 16 : 7); i++)
        {
            Vector2 dir = Quaternion.Euler(0, 0, Random.Range(-40f, 40f)) * back;
            Emit(position, dir * Random.Range(1.5f, 3.5f), perfect ? Gold : Yellow, Random.Range(.05f, .1f), .4f);
        }
        if (perfect) { RingAt(position, Gold, 1.6f, .5f); RingAt(position, Color.white, 1.0f, .35f); }
    }

    private void OnCrash(Vector3 position)
    {
        Burst(position, Red, 26, 5f, .13f);
        Burst(position, Gold, 10, 3f, .08f);
        RingAt(position, Red, 2.2f, .55f);
    }

    private void OnReset()
    {
        if (trail != null) trail.Clear();
        foreach (var spark in sparks) { spark.age = 99; spark.renderer.enabled = false; }
        foreach (var ring in rings) { ring.age = 99; ring.line.enabled = false; }
    }

    private void Burst(Vector3 position, Color color, int count, float speed, float size)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            Emit(position, dir * speed * Random.Range(.4f, 1f), color, size * Random.Range(.6f, 1.2f), Random.Range(.35f, .6f));
        }
    }

    private void Emit(Vector3 position, Vector2 velocity, Color color, float size, float life)
    {
        var spark = sparks[nextSpark];
        nextSpark = (nextSpark + 1) % sparks.Count;
        spark.renderer.transform.position = position;
        spark.velocity = velocity; spark.color = color; spark.size = size * .7f;
        spark.life = life; spark.age = 0;
    }

    private void RingAt(Vector3 center, Color color, float radius, float life)
    {
        Ring pick = rings[0];
        foreach (var ring in rings) if (ring.age >= ring.life) { pick = ring; break; }
        pick.center = center; pick.color = color; pick.radius = radius; pick.life = life; pick.age = 0;
    }

    // ---------- Mesh helpers ----------

    private MeshRenderer Renderer(string name, Mesh mesh, int order, Transform parent)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = obj.AddComponent<MeshRenderer>();
        r.sharedMaterial = ink; r.sortingOrder = order;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
    }

    private void Tint(MeshRenderer renderer, Color color)
    {
        renderer.GetPropertyBlock(block);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }

    private static Gradient Gradient2(Color a, Color b)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) },
                  new[] { new GradientAlphaKey(a.a, 0), new GradientAlphaKey(b.a, 1) });
        return g;
    }

    private static Mesh Disc(float radius, Color center, Color edge, int count)
    {
        var v = new Vector3[count + 1]; var c = new Color[count + 1]; var tri = new int[count * 3];
        c[0] = center;
        for (int i = 0; i < count; i++)
        {
            float a = i / (float)count * Mathf.PI * 2;
            v[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            c[i + 1] = edge;
            tri[i * 3] = 0; tri[i * 3 + 1] = (i + 1) % count + 1; tri[i * 3 + 2] = i + 1;
        }
        var m = new Mesh { name = "Disc", vertices = v, colors = c, triangles = tri };
        m.RecalculateBounds(); return m;
    }

    private static Mesh Diamond()
    {
        var m = new Mesh { name = "Diamond" };
        m.vertices = new[] { new Vector3(0, 1), new Vector3(.6f, 0), new Vector3(0, -1), new Vector3(-.6f, 0) };
        m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        m.RecalculateBounds(); return m;
    }

    /// <summary>A vertical band: full color at y=0, transparent at y=1 (scaled and flipped per edge).</summary>
    private static Mesh BandMesh()
    {
        var m = new Mesh { name = "Band" };
        m.vertices = new[] { new Vector3(-.5f, 0), new Vector3(.5f, 0), new Vector3(-.5f, 1), new Vector3(.5f, 1) };
        m.colors = new[] { Color.white, Color.white, new Color(1, 1, 1, 0), new Color(1, 1, 1, 0) };
        m.triangles = new[] { 0, 2, 1, 1, 2, 3, 0, 1, 2, 1, 3, 2 };
        m.RecalculateBounds(); return m;
    }

    /// <summary>A rolling silhouette spanning [0, TileWidth]; integer wave counts make it tile seamlessly.</summary>
    private static Mesh HillsMesh(float baseHeight, float[] amplitudes, int[] waves, Color color, int seed)
    {
        const int steps = 160;
        var rng = new System.Random(seed);
        var phases = new float[waves.Length];
        for (int i = 0; i < phases.Length; i++) phases[i] = (float)rng.NextDouble() * Mathf.PI * 2;
        var v = new Vector3[(steps + 1) * 2]; var c = new Color[v.Length]; var tri = new int[steps * 6];
        Color top = Color.Lerp(color, Color.white, .06f);
        for (int s = 0; s <= steps; s++)
        {
            float x = s / (float)steps * TileWidth;
            float y = baseHeight;
            for (int w = 0; w < waves.Length; w++)
                y += amplitudes[w] * Mathf.Sin(x / TileWidth * Mathf.PI * 2 * waves[w] + phases[w]);
            v[s * 2] = new Vector3(x, -30); v[s * 2 + 1] = new Vector3(x, y);
            c[s * 2] = color; c[s * 2 + 1] = top;
            if (s < steps)
            {
                int i = s * 6, b = s * 2;
                tri[i] = b; tri[i + 1] = b + 1; tri[i + 2] = b + 2;
                tri[i + 3] = b + 2; tri[i + 4] = b + 1; tri[i + 5] = b + 3;
            }
        }
        var m = new Mesh { name = "Hills", vertices = v, colors = c, triangles = tri };
        m.RecalculateBounds(); return m;
    }

    private static Mesh StarsMesh()
    {
        const int count = 90;
        var rng = new System.Random(7);
        var v = new List<Vector3>(); var c = new List<Color>(); var tri = new List<int>();
        for (int i = 0; i < count; i++)
        {
            var center = new Vector3((float)rng.NextDouble() * TileWidth, Mathf.Lerp(-.5f, 7f, (float)rng.NextDouble()));
            float r = Mathf.Lerp(.015f, .05f, (float)(rng.NextDouble() * rng.NextDouble()));
            var color = new Color(1, .93f, .85f, Mathf.Lerp(.25f, .85f, (float)rng.NextDouble()));
            int b = v.Count;
            v.Add(center + new Vector3(0, r)); v.Add(center + new Vector3(r, 0));
            v.Add(center + new Vector3(0, -r)); v.Add(center + new Vector3(-r, 0));
            for (int k = 0; k < 4; k++) c.Add(color);
            tri.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }
        var m = new Mesh { name = "Stars" };
        m.SetVertices(v); m.SetColors(c); m.SetTriangles(tri, 0);
        m.RecalculateBounds(); return m;
    }
}
