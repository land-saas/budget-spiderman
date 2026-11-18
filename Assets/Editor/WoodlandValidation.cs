using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ForestRoadtrip25D;
public static class WoodlandValidation {
 public static void Validate(){
  var scene=EditorSceneManager.OpenScene("Assets/ReferenceWorld25D/WoodlandReference.unity");
  int missing=0;
  foreach(var root in scene.GetRootGameObjects()) foreach(var tr in root.GetComponentsInChildren<Transform>(true)) missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject);
  if(missing!=0) throw new Exception("Missing scripts: "+missing);
  var e=UnityEngine.Object.FindFirstObjectByType<ForestExplorer>();var rope=e.GetComponent<WoodlandRopeShooter>();var mesh=e.artwork.GetComponent<MeshFilter>().sharedMesh;
  if(e.frames.Length!=9)throw new Exception("Expected nine character frames");
  foreach(var f in e.frames)if(!f||f.width!=14||f.height!=16)throw new Exception("Missing or mismatched sprite");
  if(rope.shootingVisual)throw new Exception("Mismatched shooting visual active");
  foreach(var side in new[]{-1,1}) {if(!rope.FireAt(e.transform.position+new Vector3(side*5,5,0)))throw new Exception("Fire failed");rope.UpdateVisuals();if(e.artwork.GetComponent<MeshFilter>().sharedMesh!=mesh||!e.artwork.enabled)throw new Exception("Sprite mismatch");rope.Release();}
  if(rope.FireAt(e.transform.position+Vector3.up*100))throw new Exception("Range limit failed");
  Physics.SyncTransforms(); if(!Physics.Raycast(e.transform.position+Vector3.up*3,Vector3.down,10))throw new Exception("No ground under player");
  Directory.CreateDirectory("Verification");
  if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null){var cam=Camera.main;var rt=new RenderTexture(1280,800,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(1280,800,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,800),0,0);tex.Apply();File.WriteAllBytes("Verification/public-woodland.png",tex.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;}
  File.WriteAllText("Verification/result.txt","PASS: scene loads; no missing scripts; nine 14x16 sprites; matching firing geometry; both-direction fire/release; range rejection; ground raycast. Manual input playtest not performed.\n");Debug.Log("PUBLIC_WOODLAND_VALIDATION_PASS");
 }
}
