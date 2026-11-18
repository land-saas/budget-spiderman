using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ForestRoadtrip25D;
public static class WoodlandMomentumSetup {
 const string ScenePath="Assets/ReferenceWorld25D/WoodlandReference.unity";
 [MenuItem("Tools/Woodland/Install sky anchors and momentum")]
 public static void Install(){
 var scene=EditorSceneManager.OpenScene(ScenePath);
 var old=GameObject.Find("08 Sky anchors - editable swing route");if(old)UnityEngine.Object.DestroyImmediate(old);
 var group=new GameObject("08 Sky anchors - editable swing route");
 var matPath="Assets/ReferenceWorld25D/SkyAnchor.mat";
 var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
 if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));mat.SetColor("_BaseColor",new Color(1,.72f,.2f));AssetDatabase.CreateAsset(mat,matPath);}
 var positions=new[]{new Vector3(0,6,-2),new Vector3(7,8,0),new Vector3(15,10,4),new Vector3(17,8,12),new Vector3(8,11,13),new Vector3(-2,10,10),new Vector3(-12,9,12),new Vector3(-17,7,4),new Vector3(-10,8,-5),new Vector3(-1,10,-11),new Vector3(10,9,-10)};
 for(int i=0;i<positions.Length;i++){
 var obj=new GameObject("Sky anchor "+(i+1).ToString("00"));obj.transform.SetParent(group.transform);obj.transform.position=positions[i];obj.AddComponent<WoodlandSkyAnchor>();var col=obj.AddComponent<SphereCollider>();col.radius=.8f;col.isTrigger=true;
 var line=obj.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.positionCount=12;line.startWidth=.11f;line.endWidth=.11f;line.sharedMaterial=mat;for(int j=0;j<12;j++){float a=j*Mathf.PI*2/12;line.SetPosition(j,new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.48f);}
 }
 var rope=UnityEngine.Object.FindFirstObjectByType<WoodlandRopeShooter>();rope.range=30;rope.maxAirSpeed=24;rope.pullAcceleration=28;rope.steerAcceleration=12;rope.gravity=18;rope.springStrength=22;rope.radialDamping=2;rope.reelSpeed=2.2f;rope.minimumRopeLength=3;
 EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("MOMENTUM_INSTALL_PASS: 11 authored sky anchors");Validate();
 }
 public static void Validate(){
 EditorSceneManager.OpenScene(ScenePath);var e=UnityEngine.Object.FindFirstObjectByType<ForestExplorer>();var r=e.GetComponent<WoodlandRopeShooter>();var cc=e.GetComponent<CharacterController>();r.readInput=false;
 if(UnityEngine.Object.FindObjectsByType<WoodlandSkyAnchor>(FindObjectsSortMode.None).Length!=11)throw new Exception("Missing sky anchors");
 cc.enabled=false;e.transform.position=new Vector3(0,5,-2);cc.enabled=true;Physics.SyncTransforms();
 e.ResumeMotion(new Vector3(7,4,2));if(!r.FireAt(new Vector3(7,13,0)))throw new Exception("Shot rejected");
 for(int i=0;i<60&&!r.IsAttached;i++)r.Step(1f/120,Vector2.zero);
 if(!r.IsAttached)throw new Exception("Attach failed");if(r.Velocity.x<5)throw new Exception("Attach erased entry momentum");
 for(int i=0;i<90;i++)r.Step(1f/120,Vector2.right);
 var before=r.Velocity;var start=e.transform.position;r.Release();if((r.Velocity-before).magnitude>.001f)throw new Exception("Release erased velocity");
 for(int i=0;i<18;i++)r.Step(1f/120,Vector2.zero);
 var travel=Vector3.Distance(start,e.transform.position);if(travel<.25f)throw new Exception("No release flight");
 var airSpeed=r.Velocity;r.FireAt(e.transform.position+new Vector3(-3,7,0));for(int i=0;i<60&&!r.IsAttached;i++)r.Step(1f/120,Vector2.zero);if(!r.IsAttached)throw new Exception("Reattach failed");
 r.Release();r.Release();for(int i=0;i<480;i++)r.Step(1f/120,Vector2.zero);if(e.transform.position.y< -1)throw new Exception("Landing fell through map");r.CancelMotion();if(!e.enabled)throw new Exception("Reset did not restore walking");
 Directory.CreateDirectory("Verification");File.WriteAllText("Verification/momentum-result.txt","PASS: 11 editable sky anchors; moving attach preserves horizontal velocity; tangential swing; release preserves exact velocity; ballistic travel="+travel+"; reattach in flight; repeated release; safe landing/reset. No manual input playtest or player build.\n");Debug.Log("MOMENTUM_VALIDATION_PASS flight="+travel+" releaseVelocity="+before);
 // Discard test poses, never save test mutations.
 EditorSceneManager.OpenScene(ScenePath);
 }
}
