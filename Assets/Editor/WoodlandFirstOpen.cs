using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Local, one-time convenience only. Never modifies or saves a scene.
[InitializeOnLoad]
public static class WoodlandFirstOpen
{
    public const string MainScene = "Assets/ReferenceWorld25D/WoodlandReference.unity";
    const string Marker = "Library/WoodlandFirstOpen.done";

    static WoodlandFirstOpen()
    {
        if (!Application.isBatchMode)
            EditorApplication.delayCall += OpenWhenReady;
    }

    static void OpenWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += OpenWhenReady;
            return;
        }
        TryOpen(Marker);
    }

    public static bool TryOpen(string marker)
    {
        if (File.Exists(marker) || EditorApplication.isPlayingOrWillChangePlaymode)
            return false;
        // Record the attempt even when skipping, so later recompilation cannot
        // unexpectedly take over an editor in which the user is already working.
        File.WriteAllText(marker, "First-open attempt completed.\n");
        if (SceneManager.sceneCount != 1) return false;
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.isDirty || !string.IsNullOrEmpty(scene.path)) return false;
        // Unity may start with an empty scene or its pristine camera/light template.
        // Reject any other objects or components, including unsaved user content.
        foreach (var root in scene.GetRootGameObjects())
        {
            var components = root.GetComponents<Component>();
            bool camera = root.name == "Main Camera" && root.GetComponent<Camera>() != null;
            bool light = root.name == "Directional Light" && root.GetComponent<Light>() != null;
            if (!camera && !light) return false;
            foreach (var component in components)
                if (!(component is Transform) && !(component is Camera) &&
                    !(component is AudioListener) && !(component is Light) &&
                    component.GetType().FullName != "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData" &&
                    component.GetType().FullName != "UnityEngine.Rendering.Universal.UniversalAdditionalLightData")
                    return false;
            if (root.transform.childCount != 0) return false;
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScene) == null) return false;
        EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
        Debug.Log("Woodland: opened the playable scene on first project import.");
        return true;
    }
}
