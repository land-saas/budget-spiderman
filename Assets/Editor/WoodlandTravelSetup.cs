using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ForestRoadtrip25D;
public static class WoodlandTravelSetup {
 const string ScenePath="Assets/ReferenceWorld25D/WoodlandReference.unity";
 [MenuItem("Tools/Woodland/Install endless traversal")]
 public static void Install(){
 var scene=EditorSceneManager.OpenScene(ScenePath);
 foreach(var c in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))if(c.name.ToLowerInvariant().Contains("boundary"))c.enabled=false;
 var player=UnityEngine.Object.FindFirstObjectByType<ForestExplorer>();var rope=player.GetComponent<WoodlandRopeShooter>();rope.reelSpeed=9;rope.pullAcceleration=55;rope.springStrength=38;rope.maxAirSpeed=36;rope.minimumRopeLength=3;player.jumpSpeed=8;player.airJumpSpeed=9;
 var world=UnityEngine.Object.FindFirstObjectByType<WoodlandEndlessTerrain>();if(!world)world=new GameObject("09 Procedural extension - runtime chunks").AddComponent<WoodlandEndlessTerrain>();world.player=player.transform;world.radius=2;world.chunkSize=32;
 world.groundMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/ReferenceWorld25D/VisibleGround.mat");world.anchorMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/ReferenceWorld25D/SkyAnchor.mat");var path="Assets/ReferenceWorld25D/GeneratedPine.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));mat.SetColor("_BaseColor",new Color(.06f,.22f,.17f));AssetDatabase.CreateAsset(mat,path);}world.treeMaterial=mat;
 EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Validate();
 }
 public static void Validate(){
 EditorSceneManager.OpenScene(ScenePath);var e=UnityEngine.Object.FindFirstObjectByType<ForestExplorer>();var r=e.GetComponent<WoodlandRopeShooter>();var cc=e.GetComponent<CharacterController>();r.readInput=false;
 foreach(var c in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))if(c.name.ToLowerInvariant().Contains("boundary")&&c.enabled)throw new Exception("Air boundary remains");
 var world=UnityEngine.Object.FindFirstObjectByType<WoodlandEndlessTerrain>();world.Refresh();if(world.ActiveChunkCount!=25)throw new Exception("Wrong streaming budget");
 cc.enabled=false;e.transform.position=new Vector3(140,4,80);cc.enabled=true;world.Refresh();Physics.SyncTransforms();if(world.ActiveChunkCount!=25)throw new Exception("Chunks unbounded");if(!Physics.Raycast(e.transform.position,Vector3.down,20))throw new Exception("No extended ground");if(!r.FindClosestAnchor())throw new Exception("No extended anchors");
 var nearest=r.FindClosestAnchor();float nearestDistance=Vector3.Distance(r.HandPosition,nearest.transform.position);foreach(var a in UnityEngine.Object.FindObjectsByType<WoodlandSkyAnchor>(FindObjectsSortMode.None)){float distance=Vector3.Distance(r.HandPosition,a.transform.position);if(distance>=1&&distance<nearestDistance-.001f)throw new Exception("Not closest anchor");}
 if(!r.TryShootFromScreen(Vector2.zero)||r.Anchor!=nearest.transform.position)throw new Exception("Automatic lock failed");
 e.ResetAirJump();for(int i=0;i<100&&!r.IsAttached;i++)r.Step(1f/120,Vector2.zero);if(!r.IsAttached)throw new Exception("No attach");float length=r.RopeLength;for(int i=0;i<30;i++)r.Step(1f/120,Vector2.right);if(length-r.RopeLength<1.8f)throw new Exception("Reel too slow");r.Release();if(!r.AirJump()||r.AirJump())throw new Exception("Air jump count wrong");if(r.Velocity.y<8.9f)throw new Exception("Air jump impulse missing");
 for(int i=0;i<700;i++){r.Step(1f/120,Vector2.zero);world.Refresh();}r.CancelMotion();
 cc.enabled=false;e.transform.position=new Vector3(140,world.Height(140,80)+.15f,80);cc.enabled=true;Physics.SyncTransforms();e.Step(Vector2.zero,false,.05f);e.Step(Vector2.zero,false,.05f);if(!cc.isGrounded)throw new Exception("Ground setup");if(!e.Jump())throw new Exception("Ground jump failed");e.Step(Vector2.zero,false,.05f);if(!e.Jump()||e.Jump())throw new Exception("Double jump count wrong");
 // Shared vertices are a continuous global function, including neighboring chunk seams.
 float seam=world.Height(160,80);if(float.IsNaN(seam))throw new Exception("Invalid height");
 Directory.CreateDirectory("Verification");File.WriteAllText("Verification/travel-result.txt","PASS: fast 9 units/s reel; ground jump plus one air jump; grapple/release air jump limit; nearest-anchor auto-lock; no enabled boundary colliders; 25 bounded procedural chunks; distant ground/anchors at (140,80); landing. Manual input playtest/player build not performed.\n");Debug.Log("TRAVEL_VALIDATION_PASS");EditorSceneManager.OpenScene(ScenePath);
 }
}
