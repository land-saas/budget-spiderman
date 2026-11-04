using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TwinlineProjectCapture
{
    public static void RebuildAndCaptureGallery()
    {
        TwinlineAuthoring.BuildProject();
        CaptureGallery();
    }

    public static void CaptureGalleryAndBuildMac()
    {
        CaptureGallery();
        TwinlineSetup.BuildMac();
    }

    [MenuItem("Twinline/Validation/Capture Asset Gallery")]
    public static void CaptureGallery()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/Scenes/AssetLibrary.unity");
        var camera=Object.FindAnyObjectByType<Camera>();
        try
        {
            string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../Verification"));
            Directory.CreateDirectory(directory);
            CaptureCamera(camera,Path.Combine(directory,"asset-library.png"));
        }
        finally { EditorSceneManager.OpenScene("Assets/Scenes/Flight.unity"); }
        Debug.Log("TWINLINE_ASSET_GALLERY_CAPTURED");
    }
    public static void CaptureCamera(Camera camera,string path)
    {
        var target=new RenderTexture(1600,1000,24);
        var previous=RenderTexture.active;
        var previousTarget=camera.targetTexture;
        var texture=new Texture2D(1600,1000,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=target;
            camera.Render();
            RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,1600,1000),0,0);
            texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=previousTarget;
            RenderTexture.active=previous;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(target);
        }
    }

}
