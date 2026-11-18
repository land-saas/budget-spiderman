using UnityEngine;
using UnityEngine.InputSystem;
public sealed class ReferenceWorldCamera : MonoBehaviour {
 public Transform target; public Light sun; public bool night; Vector3 focus;
 public void ApplyLighting(){ GetComponent<Camera>().backgroundColor=night?new Color(.025f,.04f,.09f):new Color(.36f,.58f,.68f); RenderSettings.ambientLight=night?new Color(.38f,.45f,.62f):new Color(.65f,.7f,.8f); sun.intensity=night?.65f:1.1f; foreach(var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None)){if(r.name=="Ground"){var gb=new MaterialPropertyBlock();r.GetPropertyBlock(gb);gb.SetColor("_BaseColor",night?new Color(.55f,.7f,.78f):new Color(.38f,.52f,.44f));r.SetPropertyBlock(gb);}if(r.sharedMaterial&&r.sharedMaterial.HasProperty("_Tint")){var b=new MaterialPropertyBlock();r.GetPropertyBlock(b);b.SetColor("_Tint",night?new Color(.5f,.62f,.78f):new Color(.9f,.96f,1));r.SetPropertyBlock(b);}} sun.color=night?new Color(.4f,.55f,1):new Color(1,.91f,.76f); }
 void Start(){focus=target.position;ApplyLighting();}
 void LateUpdate(){ if(Keyboard.current!=null && Keyboard.current.nKey.wasPressedThisFrame){night=!night;ApplyLighting();} var p=target.position; p.y=Mathf.Max(0,p.y*.8f); focus=Vector3.Lerp(focus,p,1-Mathf.Exp(-Time.deltaTime*4)); transform.position=focus+new Vector3(0,8,-23);transform.LookAt(focus+Vector3.up*2); }
}
