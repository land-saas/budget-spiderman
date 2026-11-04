using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>One-time hierarchy migration; the saved scene remains the source of its objects.</summary>
public static class TwinlineSceneEditing
{
    public static void ValidateAndPlay()
    {
        TwinlineProjectValidation.ValidateCurrentSavedProject();
        TwinlineProjectValidation.RunPlayMode();
    }

    public static void ArrangeScene(TwinSwingGame game)
    {
        if(game==null) throw new InvalidOperationException("No saved swing controller was assigned.");
        var scene=game.gameObject.scene;
        game.player.transform.SetParent(null,true);
        game.player.name="Player";
        game.player.transform.SetSiblingIndex(0);
        game.sharedCamera.transform.SetParent(null,true);
        game.sharedCamera.name="Main Camera";
        game.sharedCamera.transform.SetSiblingIndex(1);
        Transform course=null;
        foreach(var anchor in game.sceneAnchors)
        {
            if(anchor.transform.parent!=null && anchor.transform.parent.parent!=null)
            {
                course=anchor.transform.parent.parent;
                break;
            }
        }
        if(course!=null && course!=game.transform)
        {
            course.SetParent(null,true);
            course.name="Course";
            course.SetSiblingIndex(2);
        }
        game.rope.transform.SetParent(course,true);
        if(game.hud!=null)
        {
            game.hud.transform.SetParent(null,true);
            game.hud.name="Interface (Canvas)";
            game.hud.transform.SetSiblingIndex(3);
        }
        var audio=GameObject.Find("Audio");
        if(audio==null) audio=new GameObject("Audio");
        audio.transform.SetParent(null,true);
        audio.transform.SetSiblingIndex(4);
        if(game.audioSource!=null && game.audioSource.gameObject==game.gameObject)
        {
            var effects=new GameObject("Sound Effects");
            effects.transform.SetParent(audio.transform,false);
            var source=effects.AddComponent<AudioSource>();
            EditorUtility.CopySerialized(game.audioSource,source);
            UnityEngine.Object.DestroyImmediate(game.audioSource);
            game.audioSource=source;
        }
        else if(game.audioSource!=null) game.audioSource.transform.SetParent(audio.transform,true);
        if(game.musicSource!=null) game.musicSource.transform.SetParent(audio.transform,true);
        game.name="Game Controller (settings and logic)";
        game.transform.SetSiblingIndex(5);
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject=game.player.gameObject;
        FocusStartingArea();
    }

    [MenuItem("Twinline/Show Starting Scene For Editing")]
    public static void ShowStartingScene()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/Scenes/Flight.unity");
        var game=UnityEngine.Object.FindAnyObjectByType<TwinSwingGame>();
        if(game!=null) Selection.activeGameObject=game.player.gameObject;
        FocusStartingArea();
    }

    private static void FocusStartingArea()
    {
        var view=SceneView.lastActiveSceneView;
        if(view==null) return;
        view.in2DMode=true;
        view.orthographic=true;
        view.pivot=new Vector3(2.8f,0,0);
        view.size=5.5f;
        view.Repaint();
    }
}
