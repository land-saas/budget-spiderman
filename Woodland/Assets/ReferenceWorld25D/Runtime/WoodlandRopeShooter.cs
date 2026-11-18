using UnityEngine;
using UnityEngine.InputSystem;
using ForestRoadtrip25D;

/// Fixed-world pin and max-distance rope adapted from the user's Budget Spiderman starter.
/// CharacterController traversal rather than the source project's 2D joint simulation.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(CharacterController))]
public sealed class WoodlandRopeShooter : MonoBehaviour {
 public float range=24,shotSpeed=45,pullAcceleration=28,maxAirSpeed=12,steerAcceleration=12;
 public Texture2D shootingTexture;
 public GameObject shootingVisual;
 public LineRenderer rope;
 public bool readInput=true;
 public bool IsAttached {get;private set;}
 public bool IsShooting {get;private set;}
 public Vector3 Anchor {get;private set;}
 public Vector3 HandPosition => transform.position+Vector3.up*.9f+(Camera.main?Camera.main.transform.right:Vector3.right)*(Anchor.x<transform.position.x?-.42f:.42f);
 CharacterController controller; ForestExplorer explorer; Vector3 velocity,tip; float ropeLength;bool ownsMotion;
 MaterialPropertyBlock properties;
 void Awake(){Cache();if(rope)rope.enabled=false;}
 void Cache(){if(!controller)controller=GetComponent<CharacterController>();if(!explorer)explorer=GetComponent<ForestExplorer>();properties??=new MaterialPropertyBlock();}
 void Update(){Cache();if(readInput&&Mouse.current!=null){if(Mouse.current.leftButton.wasPressedThisFrame)TryShootFromScreen(Mouse.current.position.ReadValue());if(Mouse.current.leftButton.wasReleasedThisFrame)Release();} Vector2 move=Vector2.zero;var k=Keyboard.current;if(readInput&&k!=null){move.x=(k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0);move.y=(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0);if(k.rKey.wasPressedThisFrame){CancelMotion();explorer.ResetPosition();return;}}Step(Time.deltaTime,move);}
 public bool TryShootFromScreen(Vector2 screen){if(!Camera.main)return false;var hits=Physics.RaycastAll(Camera.main.ScreenPointToRay(screen),100,~0,QueryTriggerInteraction.Collide);System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));foreach(var h in hits){if(h.collider.transform.IsChildOf(transform)||h.collider.name.Contains("boundary"))continue;if(h.point.y<transform.position.y+.4f)return false;return FireAt(h.point);}return false;}
 public bool FireAt(Vector3 point){Cache();float distance=Vector3.Distance(HandPosition,point);if(distance<1||distance>range||point.y<transform.position.y+.4f)return false;Anchor=point;tip=HandPosition;ropeLength=distance;IsShooting=true;IsAttached=false;if(rope)rope.enabled=true;return true;}
 public void Step(float dt,Vector2 move){Cache();dt=Mathf.Clamp(dt,0,.05f);if(IsShooting){tip=Vector3.MoveTowards(tip,Anchor,shotSpeed*dt);if(Vector3.Distance(tip,Anchor)<.02f){IsShooting=false;IsAttached=true;ownsMotion=true;explorer.enabled=false;velocity=Vector3.up*3;}}
 if(!ownsMotion)return;move=Vector2.ClampMagnitude(move,1);velocity+=new Vector3(move.x*steerAcceleration,-18,move.y*steerAcceleration)*dt;
 if(IsAttached){Vector3 toward=Anchor-HandPosition;float d=toward.magnitude;ropeLength=Mathf.Max(1.4f,ropeLength-3.5f*dt);if(d>.1f){velocity+=toward.normalized*pullAcceleration*dt;if(d>=ropeLength){float outward=Vector3.Dot(velocity,-toward.normalized);if(outward>0)velocity+=toward.normalized*outward;}}}
 velocity=Vector3.ClampMagnitude(velocity,maxAirSpeed);var flags=controller.Move(velocity*dt);if((flags&CollisionFlags.Above)!=0&&velocity.y>0)velocity.y=0;
 if(IsAttached){Vector3 from=HandPosition-Anchor;if(from.magnitude>ropeLength)controller.Move(-from.normalized*Mathf.Min(from.magnitude-ropeLength,maxAirSpeed*dt));}
 if(!IsAttached&&controller.isGrounded&&velocity.y<=0){ownsMotion=false;explorer.enabled=true;velocity=Vector3.zero;}if(transform.position.y< -5){CancelMotion();explorer.ResetPosition();}}
 void LateUpdate(){UpdateVisuals();}
 public void UpdateVisuals(){if(shootingVisual){bool active=IsShooting||IsAttached;shootingVisual.SetActive(active);if(explorer&&explorer.artwork)explorer.artwork.enabled=!active;if(Camera.main)shootingVisual.transform.rotation=Camera.main.transform.rotation;shootingVisual.transform.localScale=new Vector3(Anchor.x<transform.position.x?-1:1,1,1);}if(rope){rope.enabled=IsShooting||IsAttached;if(rope.enabled){rope.positionCount=2;rope.SetPosition(0,HandPosition);rope.SetPosition(1,IsAttached?Anchor:tip);}}if((IsAttached||IsShooting)&&shootingTexture&&explorer&&explorer.artwork){explorer.artwork.GetPropertyBlock(properties);properties.SetTexture("_MainTex",shootingTexture);properties.SetFloat("_FlipX",Anchor.x<transform.position.x?1:0);explorer.artwork.SetPropertyBlock(properties);}}
 public void Release(){IsShooting=false;IsAttached=false;if(rope)rope.enabled=false;RestoreNativePose();}
 void RestoreNativePose(){Cache();if(explorer&&explorer.artwork){explorer.artwork.enabled=true;explorer.artwork.GetPropertyBlock(properties);if(explorer.frames!=null&&explorer.frames.Length>0)properties.SetTexture("_MainTex",explorer.frames[0]);properties.SetFloat("_FlipX",0);explorer.artwork.SetPropertyBlock(properties);}if(shootingVisual)shootingVisual.SetActive(false);}
 public void CancelMotion(){Release();ownsMotion=false;velocity=Vector3.zero;if(explorer){explorer.enabled=true;if(explorer.artwork)explorer.artwork.enabled=true;}if(shootingVisual)shootingVisual.SetActive(false);}
 void OnDisable(){CancelMotion();}
 void OnApplicationFocus(bool focus){if(!focus)CancelMotion();}
}
