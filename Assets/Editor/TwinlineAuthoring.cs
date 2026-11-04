using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Explicit, repeatable authoring command for the existing swing prototype.
/// The resulting scenes, prefabs, sprites, materials and audio are ordinary
/// Unity assets. This command is never run automatically on project load.
/// </summary>
public static class TwinlineAuthoring
{
    private const string FlightScene = "Assets/Scenes/Flight.unity";
    private const string LibraryScene = "Assets/Scenes/AssetLibrary.unity";
    private const string GameplaySprites = "Assets/Art/Sprites/Gameplay/";
    private const string UiSprites = "Assets/Art/Sprites/UI/";
    private const string EnvironmentSprites = "Assets/Art/Sprites/Environment/";
    private const string Materials = "Assets/Art/Materials/";
    private const string Sounds = "Assets/Audio/SFX/";
    private const string Prefabs = "Assets/Prefabs/Gameplay/";
    private const int SpriteSize = 256;
    private const int SpriteSupersampling = 4;
    private const float PixelsPerUnit = 128f;

    private static readonly Color PlayerRed = new Color(1f, .28f, .22f);
    private static readonly Color NodeYellow = new Color(1f, .88f, .18f);
    private static readonly Color GemGold = new Color(1f, .68f, .08f);
    private static readonly Color RopeTint = new Color(1f, .92f, .42f);

    private sealed class AuthoredAssets
    {
        public Material spriteMaterial;
        public Material ropeMaterial;
        public readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        public readonly Dictionary<string, AudioClip> sounds = new Dictionary<string, AudioClip>();
        public readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
    }

    [MenuItem("Twinline/Build Editable Project")]
    private static void BuildFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play mode before rebuilding the editable project.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorUtility.DisplayDialog("Rebuild editable project?",
            "This rebuilds the generated sprite, audio and prefab library and replaces Flight.unity and AssetLibrary.unity. " +
            "Save any scene or prefab customizations before proceeding.", "Rebuild", "Cancel")) return;
        BuildProject();
    }

    /// <summary>Batch entry point: -executeMethod TwinlineAuthoring.BuildProject.</summary>
    public static void BuildProject()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Cannot author assets while Unity is in Play mode.");

        EnsureFolders();
        var assets = BuildAssets();
        BuildPrefabs(assets);
        BuildFlightScene(assets);
        BuildLibraryScene(assets);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(FlightScene, true),
            new EditorBuildSettingsScene(LibraryScene, false)
        };
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(FlightScene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        EditorSceneManager.OpenScene(FlightScene, OpenSceneMode.Single);
        Selection.activeGameObject = GameObject.Find("Twinline");
        TwinlineInterfaceAuthoring.UpgradeInterface();
        Debug.Log("TWINLINE_AUTHORED: Saved gameplay and gallery scenes, gameplay/UI prefabs, 12 sprites, 2 materials, 4 WAV cues and editable Canvas UI.");
    }

    private static void EnsureFolders()
    {
        foreach (string folder in new[]
        {
            GameplaySprites, UiSprites, EnvironmentSprites, Materials, Sounds, Prefabs, "Assets/Scenes/"
        }) Directory.CreateDirectory(folder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static AuthoredAssets BuildAssets()
    {
        var assets = new AuthoredAssets();
        assets.spriteMaterial = SaveMaterial(Materials + "SpriteInk.mat", Shader.Find("Sprites/Default"));
        assets.ropeMaterial = SaveMaterial(Materials + "RopeInk.mat", Resources.Load<Shader>("FlatInk"));

        // Gameplay silhouettes reproduce the original 32-sided unit disc.
        // At 128 PPU these 256px sprites have diameter 2 world units, exactly
        // matching the generated unit mesh and the original transform scales.
        assets.sprites["WhiteDisc"] = SaveSprite(GameplaySprites + "WhiteDisc.png", p => Disc(p), Color.white);
        assets.sprites["Player"] = SaveSprite(GameplaySprites + "Player.png", p => Disc(p), PlayerRed);
        assets.sprites["Anchor"] = SaveSprite(GameplaySprites + "Anchor.png", p => Disc(p), NodeYellow);
        assets.sprites["AnchorCore"] = SaveSprite(GameplaySprites + "AnchorCore.png", p => Disc(p), PlayerRed);
        assets.sprites["Gem"] = SaveSprite(GameplaySprites + "Gem.png", p => Disc(p), GemGold);
        assets.sprites["GemDiamond"] = SaveSprite(GameplaySprites + "GemDiamond.png", p => Mathf.Abs(p.x) + Mathf.Abs(p.y) <= .98f, GemGold);

        assets.sprites["StartIcon"] = SaveSprite(UiSprites + "StartIcon.png", p =>
            InTriangle(p, new Vector2(-.48f, -.68f), new Vector2(-.48f, .68f), new Vector2(.72f, 0f)), Color.white);
        assets.sprites["RetryIcon"] = SaveSprite(UiSprites + "RetryIcon.png", p =>
        {
            float radius = p.magnitude;
            bool arc = radius > .48f && radius < .7f && !(p.x > .1f && p.y > .12f);
            bool arrow = InTriangle(p, new Vector2(.12f, .29f), new Vector2(.8f, .29f), new Vector2(.8f, .9f));
            return arc || arrow;
        }, Color.white);
        assets.sprites["AnchorIcon"] = SaveSprite(UiSprites + "AnchorIcon.png", p => Disc(p) && p.magnitude > .58f, NodeYellow);
        assets.sprites["GemIcon"] = SaveSprite(UiSprites + "GemIcon.png", p => Mathf.Abs(p.x) + Mathf.Abs(p.y) <= .78f, GemGold);
        assets.sprites["SpaceKey"] = SaveSprite(UiSprites + "SpaceKey.png", p =>
        {
            float x = Mathf.Abs(p.x), y = Mathf.Abs(p.y);
            bool border = x < .95f && y < .5f && (x > .85f || y > .4f);
            bool spaceBar = x < .52f && y > .04f && y < .13f;
            return border || spaceBar;
        }, Color.white);
        assets.sprites["BlackSwatch"] = SaveSprite(EnvironmentSprites + "BlackSwatch.png", p => true, Color.black);

        assets.sounds["Attach"] = SaveTone(Sounds + "Attach.wav", 420f, 520f, .08f);
        assets.sounds["Release"] = SaveTone(Sounds + "Release.wav", 680f, 420f, .1f);
        assets.sounds["Gem"] = SaveTone(Sounds + "Gem.wav", 880f, 1180f, .12f);
        assets.sounds["Crash"] = SaveTone(Sounds + "Crash.wav", 150f, 42f, .25f);
        return assets;
    }

    private static Material SaveMaterial(string path, Shader shader)
    {
        if (shader == null) throw new InvalidOperationException("Required shader is unavailable for " + path);
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.color = Color.white;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Sprite SaveSprite(string path, Func<Vector2, bool> silhouette, Color ink)
    {
        var texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
        var pixels = new Color32[SpriteSize * SpriteSize];
        float divisor = SpriteSupersampling * SpriteSupersampling;
        for (int y = 0; y < SpriteSize; y++)
        {
            for (int x = 0; x < SpriteSize; x++)
            {
                int covered = 0;
                for (int sy = 0; sy < SpriteSupersampling; sy++)
                {
                    for (int sx = 0; sx < SpriteSupersampling; sx++)
                    {
                        var sample = new Vector2(
                            (x + (sx + .5f) / SpriteSupersampling) / SpriteSize * 2f - 1f,
                            (y + (sy + .5f) / SpriteSupersampling) / SpriteSize * 2f - 1f);
                        if (silhouette(sample)) covered++;
                    }
                }
                pixels[y * SpriteSize + x] = new Color(ink.r, ink.g, ink.b, ink.a * covered / divisor);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.spritePivot = new Vector2(.5f, .5f);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("Sprite import failed: " + path);
        return sprite;
    }

    private static bool Disc(Vector2 point)
    {
        const int sides = 32;
        float angle = Mathf.Atan2(point.y, point.x);
        float sector = Mathf.PI * 2f / sides;
        float localAngle = Mathf.Repeat(angle, sector) - sector * .5f;
        float polygonRadius = Mathf.Cos(sector * .5f) / Mathf.Cos(localAngle);
        return point.sqrMagnitude <= polygonRadius * polygonRadius;
    }

    private static bool InTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(point - b, a - b);
        float d2 = Cross(point - c, b - c);
        float d3 = Cross(point - a, c - a);
        return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private static AudioClip SaveTone(string path, float from, float to, float duration)
    {
        const int rate = 22050;
        int count = (int)(rate * duration);
        using (var stream = File.Create(path))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + count * 2);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(rate);
            writer.Write(rate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(count * 2);
            float phase = 0f;
            for (int n = 0; n < count; n++)
            {
                float t = n / (float)count;
                phase += Mathf.Lerp(from, to, t) * 2f * Mathf.PI / rate;
                float sample = Mathf.Sin(phase) * Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-t * 3f) * .2f;
                writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1f, 1f) * 32767f));
            }
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    private static void BuildPrefabs(AuthoredAssets assets)
    {
        var player = new GameObject("Player");
        var playerBody = player.AddComponent<Rigidbody2D>();
        playerBody.constraints = RigidbodyConstraints2D.FreezeRotation;
        playerBody.mass = 1f;
        playerBody.linearDamping = .02f;
        playerBody.gravityScale = 0f;
        playerBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        playerBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        player.AddComponent<CircleCollider2D>().radius = .205f;
        player.AddComponent<SwingPlayer>();
        player.AddComponent<AudioListener>();
        CreateSpriteObject("Player body", assets.sprites["Player"], assets.spriteMaterial, player.transform, Vector2.zero, .235f, 12);
        SavePrefab("Player", player, assets);

        var anchor = new GameObject("Anchor");
        var anchorBody = anchor.AddComponent<Rigidbody2D>();
        anchorBody.bodyType = RigidbodyType2D.Kinematic;
        anchorBody.gravityScale = 0f;
        anchor.AddComponent<CircleCollider2D>().radius = .16f;
        var marker = anchor.AddComponent<SwingAnchor>();
        marker.body = anchorBody;
        // Original LateUpdate uses radius 1 for an idle anchor and 1.35 in range.
        marker.ring = CreateSpriteObject("Anchor ring", assets.sprites["Anchor"], assets.spriteMaterial, anchor.transform, Vector2.zero, 1f, 3).transform;
        CreateSpriteObject("Anchor core", assets.sprites["AnchorCore"], assets.spriteMaterial, anchor.transform, Vector2.zero, .08f, 4);
        SavePrefab("Anchor", anchor, assets);

        var gem = new GameObject("Gem");
        var pickupCollider = gem.AddComponent<CircleCollider2D>();
        pickupCollider.radius = .14f;
        pickupCollider.isTrigger = true;
        var pickup = gem.AddComponent<SwingGemPickup>();
        pickup.pickupCollider = pickupCollider;
        pickup.visual = CreateSpriteObject("Gem ink", assets.sprites["Gem"], assets.spriteMaterial, gem.transform, Vector2.zero, .16f, 5).transform;
        SavePrefab("Gem", gem, assets);

        var rope = new GameObject("Rope");
        var line = rope.AddComponent<LineRenderer>();
        line.sharedMaterial = assets.ropeMaterial;
        line.useWorldSpace = true;
        line.positionCount = 17;
        line.startWidth = line.endWidth = .024f;
        line.startColor = line.endColor = RopeTint;
        line.sortingOrder = 8;
        line.numCapVertices = 3;
        line.enabled = false;
        SavePrefab("Rope", rope, assets);

        var boundary = new GameObject("Boundary");
        boundary.AddComponent<BoxCollider2D>().size = new Vector2(400f, 1f);
        SavePrefab("Boundary", boundary, assets);
    }

    private static void SavePrefab(string name, GameObject root, AuthoredAssets assets)
    {
        assets.prefabs[name] = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(root);
        if (assets.prefabs[name] == null) throw new InvalidOperationException("Prefab save failed: " + name);
    }

    private static GameObject CreateSpriteObject(string name, Sprite sprite, Material material, Transform parent,
        Vector2 position, float scale, int order)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = position;
        root.transform.localScale = new Vector3(scale, scale, 1f);
        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.color = Color.white;
        renderer.sortingOrder = order;
        return root;
    }

    private static Transform Group(string name, Transform parent)
    {
        var group = new GameObject(name).transform;
        group.SetParent(parent, false);
        return group;
    }

    private static GameObject Instance(AuthoredAssets assets, string name, Transform parent)
    {
        return (GameObject)PrefabUtility.InstantiatePrefab(assets.prefabs[name], parent);
    }

    private static void BuildFlightScene(AuthoredAssets assets)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Twinline");
        var game = root.AddComponent<TwinSwingGame>();
        game.useSceneObjects = true;
        game.useAuthoredCourse = true;

        var player = Instance(assets, "Player", root.transform);
        player.name = "Swinger";
        player.transform.position = new Vector3(0f, 1.2f, 0f);
        game.player = player.GetComponent<Rigidbody2D>();
        game.player.position = new Vector2(0f, 1.2f);
        game.playerVisual = player.transform.Find("Player body");
        var avatar = player.GetComponent<SwingPlayer>();
        avatar.game = game;
        PrefabUtility.RecordPrefabInstancePropertyModifications(avatar);

        var cameraObject = new GameObject("Shared camera");
        cameraObject.transform.SetParent(root.transform, false);
        cameraObject.transform.position = new Vector3(2.8f, 0f, -10f);
        cameraObject.tag = "MainCamera";
        game.sharedCamera = cameraObject.AddComponent<Camera>();
        game.sharedCamera.orthographic = true;
        game.sharedCamera.orthographicSize = 4.9f;
        game.sharedCamera.clearFlags = CameraClearFlags.SolidColor;
        game.sharedCamera.backgroundColor = Color.black;
        game.sharedCamera.rect = new Rect(0f, 0f, 1f, 1f);
        game.sharedCamera.cullingMask = 1 << 0;

        var world = Group("Course (editable starting layout)", root.transform);
        var anchors = Group("Anchors", world);
        var gems = Group("Gems", world);
        var boundaries = Group("Boundaries", world);
        var ropeObject = Instance(assets, "Rope", root.transform);
        ropeObject.name = "Swing rope";
        game.rope = ropeObject.GetComponent<LineRenderer>();

        game.sceneAnchors = new SwingAnchor[8];
        game.sceneGems = new SwingGemPickup[10];
        var random = new System.Random(TwinSwingGame.Seed);
        for (int index = 0; index < game.sceneAnchors.Length; index++)
        {
            var anchorObject = Instance(assets, "Anchor", anchors);
            anchorObject.name = "Anchor " + index.ToString("00");
            float y;
            switch (index)
            {
                case 0: y = 2.4f; break;
                case 1: y = 1.1f; break;
                case 2: y = 2.8f; break;
                case 3: y = .4f; break;
                default: y = Mathf.Lerp(-1.2f, 3.8f, (float)random.NextDouble()); break;
            }
            var anchor = anchorObject.GetComponent<SwingAnchor>();
            Vector2 position = new Vector2(2.4f + index * game.anchorSpacing, y);
            anchorObject.transform.position = position;
            anchor.body.position = position;
            game.sceneAnchors[index] = anchor;
        }
        for (int index = 0; index < game.sceneGems.Length; index++)
        {
            var gemObject = Instance(assets, "Gem", gems);
            gemObject.name = "Gem " + index.ToString("00");
            gemObject.transform.position = new Vector3(2f + index * game.gemSpacing,
                Mathf.Lerp(-2.8f, 2.8f, (float)random.NextDouble()), 0f);
            var pickup = gemObject.GetComponent<SwingGemPickup>();
            pickup.game = game;
            pickup.index = index;
            game.sceneGems[index] = pickup;
            PrefabUtility.RecordPrefabInstancePropertyModifications(pickup);
        }
        for (int sign = -1; sign <= 1; sign += 2)
        {
            var boundary = Instance(assets, "Boundary", boundaries);
            boundary.name = sign < 0 ? "Boundary floor" : "Boundary ceiling";
            boundary.transform.position = new Vector3(0f, sign * (game.corridorHalfHeight + .5f), 0f);
        }

        game.audioSource = root.AddComponent<AudioSource>();
        game.audioSource.playOnAwake = false;
        game.audioSource.spatialBlend = 0f;
        game.attachSound = assets.sounds["Attach"];
        game.releaseSound = assets.sounds["Release"];
        game.gemSound = assets.sounds["Gem"];
        game.crashSound = assets.sounds["Crash"];
        EditorUtility.SetDirty(game);
        EditorSceneManager.SaveScene(scene, FlightScene);
    }

    private static void BuildLibraryScene(AuthoredAssets assets)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Twinline - Asset Library").transform;
        var camera = new GameObject("Library camera").AddComponent<Camera>();
        camera.transform.SetParent(root, false);
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.tag = "MainCamera";
        camera.orthographic = true;
        camera.orthographicSize = 7.2f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.055f, .065f, .085f);

        var labels = Group("Labels and editing guide", root);
        Label("TWINLINE / ASSET LIBRARY", new Vector2(-10.6f, 6.1f), .64f, Color.white, labels);
        Label("Reusable Unity assets - select an object to inspect its source prefab or sprite.", new Vector2(-10.6f, 5.15f), .3f,
            new Color(.65f, .7f, .8f), labels);
        Label("GAMEPLAY SPRITES", new Vector2(-10.6f, 4.15f), .34f, NodeYellow, labels);

        var spriteGroup = Group("Sprite previews", root);
        string[] gameplayNames = { "WhiteDisc", "Player", "Anchor", "AnchorCore", "Gem", "GemDiamond" };
        for (int index = 0; index < gameplayNames.Length; index++)
        {
            float x = -9.3f + index * 3.7f;
            string name = gameplayNames[index];
            CreateSpriteObject(name, assets.sprites[name], assets.spriteMaterial, spriteGroup, new Vector2(x, 2.8f), .55f, 1);
            Label(name, new Vector2(x - .95f, 1.85f), .28f, Color.white, labels);
        }
        Label("UI + ENVIRONMENT", new Vector2(-10.6f, .95f), .34f, NodeYellow, labels);
        string[] uiNames = { "StartIcon", "RetryIcon", "AnchorIcon", "GemIcon", "SpaceKey", "BlackSwatch" };
        for (int index = 0; index < uiNames.Length; index++)
        {
            float x = -9.3f + index * 3.7f;
            string name = uiNames[index];
            CreateSpriteObject(name, assets.sprites[name], assets.spriteMaterial, spriteGroup, new Vector2(x, -.2f), .52f, 1);
            Label(name, new Vector2(x - .95f, -1.05f), .28f, Color.white, labels);
        }

        Label("PREFABS / DRAG INTO FLIGHT.UNITY", new Vector2(-10.6f, -1.95f), .34f, NodeYellow, labels);
        var prefabGroup = Group("Connected prefab previews (physics disabled for display)", root);
        string[] prefabNames = { "Player", "Anchor", "Gem", "Rope", "Boundary" };
        for (int index = 0; index < prefabNames.Length; index++)
        {
            float x = -9.3f + index * 4.6f;
            string name = prefabNames[index];
            var preview = Instance(assets, name, prefabGroup);
            preview.name = name + " prefab preview";
            preview.transform.position = new Vector3(x, -3.3f, 0f);
            foreach (var body in preview.GetComponentsInChildren<Rigidbody2D>())
            {
                body.simulated = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(body);
            }
            foreach (var behaviour in preview.GetComponentsInChildren<MonoBehaviour>())
            {
                behaviour.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
            }
            foreach (var listener in preview.GetComponentsInChildren<AudioListener>())
            {
                listener.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(listener);
            }
            foreach (var collider in preview.GetComponentsInChildren<Collider2D>())
            {
                collider.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            }
            if (name == "Anchor") preview.transform.localScale = Vector3.one * .6f;
            if (name == "Rope")
            {
                var line = preview.GetComponent<LineRenderer>();
                line.enabled = true;
                for (int p = 0; p < line.positionCount; p++)
                {
                    float t = p / (float)(line.positionCount - 1);
                    line.SetPosition(p, new Vector3(x - .9f + 1.8f * t, -3.3f - Mathf.Sin(t * Mathf.PI) * .35f, 0f));
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(line);
            }
            if (name == "Boundary")
            {
                var swatch = CreateSpriteObject("Collider location (gallery only)", assets.sprites["BlackSwatch"], assets.spriteMaterial,
                    preview.transform, Vector2.zero, 1f, 1);
                swatch.transform.localScale = new Vector3(1.1f, .15f, 1f);
                swatch.GetComponent<SpriteRenderer>().color = new Color(.3f, .35f, .45f);
            }
            Label(name + ".prefab", new Vector2(x - 1.2f, -4.25f), .28f, Color.white, labels);
        }

        Label("EDIT THE GAME", new Vector2(-10.6f, -5.1f), .33f, NodeYellow, labels);
        Label("Open Flight.unity. Move starting anchors/gems; edit prefab art, colliders and controller settings in the Inspector.",
            new Vector2(-10.6f, -5.65f), .27f, new Color(.72f, .77f, .86f), labels);
        Label("Audio/SFX: Attach, Release, Gem, Crash. Main gameplay keeps the original circles, sizes, colors and sounds.",
            new Vector2(-10.6f, -6.15f), .27f, new Color(.72f, .77f, .86f), labels);
        EditorSceneManager.SaveScene(scene, LibraryScene);
    }

    private static void Label(string text, Vector2 position, float size, Color color, Transform parent)
    {
        var root = new GameObject(text);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(position.x, position.y, -.2f);
        var label = root.AddComponent<TextMesh>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 64;
        label.characterSize = size * 10f / label.fontSize;
        label.anchor = TextAnchor.UpperLeft;
        label.alignment = TextAlignment.Left;
        label.color = color;
        root.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        root.GetComponent<MeshRenderer>().sortingOrder = 20;
    }
}
