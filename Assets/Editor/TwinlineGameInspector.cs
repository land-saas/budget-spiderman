using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(TwinSwingGame))]
public sealed class TwinlineGameInspector : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Edit the placed player, anchors and gems in the Hierarchy. Drag a prefab into the course, then register it below. Opening positions are restored on retry.",MessageType.Info);
        var current=(TwinSwingGame)target;
        if(current.hud!=null && GUILayout.Button("Select editable Canvas interface")) Selection.activeGameObject=current.hud.gameObject;
        if(current.sharedCamera!=null && GUILayout.Button("Select camera and background color")) Selection.activeGameObject=current.sharedCamera.gameObject;
        if(current.musicSource!=null && GUILayout.Button("Select optional music source")) Selection.activeGameObject=current.musicSource.gameObject;
        DrawDefaultInspector();
        using(new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if(GUILayout.Button("Register anchors and gems in this scene"))
            {
                var game=(TwinSwingGame)target;
                Undo.RecordObject(game,"Register course objects");
                var roots=game.gameObject.scene.GetRootGameObjects();
                game.sceneAnchors=roots.SelectMany(root=>root.GetComponentsInChildren<SwingAnchor>(true)).OrderBy(anchor=>anchor.transform.position.x).ToArray();
                game.sceneGems=roots.SelectMany(root=>root.GetComponentsInChildren<SwingGemPickup>(true)).OrderBy(gem=>gem.transform.position.x).ToArray();
                EditorUtility.SetDirty(game);
                EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            }
            if(GUILayout.Button("Show sprite and prefab library"))
            {
                var folder=AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/Art");
                Selection.activeObject=folder;
                EditorGUIUtility.PingObject(folder);
            }
        }
    }
}
