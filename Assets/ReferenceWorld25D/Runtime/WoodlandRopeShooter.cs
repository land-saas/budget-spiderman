using UnityEngine;
using UnityEngine.InputSystem;
using ForestRoadtrip25D;

/// Momentum-preserving elastic rope. Release keeps the complete current velocity.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(CharacterController))]
public sealed class WoodlandRopeShooter : MonoBehaviour {
 public float range=30,shotSpeed=45,pullAcceleration=55,maxAirSpeed=36,steerAcceleration=12;
 [Header("Swing and launch tuning")]
 public float gravity=18, springStrength=38, radialDamping=2, reelSpeed=9f, minimumRopeLength=3;
 public Texture2D shootingTexture;
 public GameObject shootingVisual;
 public LineRenderer rope;
 public bool readInput=true;
 public bool IsAttached {get;private set;}
 public bool IsShooting {get;private set;}
 public Vector3 Anchor {get;private set;}
 public Vector3 Velocity => ownsMotion ? velocity : (explorer ? explorer.CurrentVelocity : Vector3.zero);
 public float RopeLength => ropeLength;
 public Vector3 HandPosition => transform.position+Vector3.up*.9f+(Camera.main?Camera.main.transform.right:Vector3.right)*(Anchor.x<transform.position.x?-.42f:.42f);
 CharacterController controller; ForestExplorer explorer; Vector3 velocity,tip; float ropeLength;bool ownsMotion;
 MaterialPropertyBlock properties;
 void Awake(){Cache();if(rope)rope.enabled=false;}
 void Cache(){if(!controller)controller=GetComponent<CharacterController>();if(!explorer)explorer=GetComponent<ForestExplorer>();properties??=new MaterialPropertyBlock();}
 void Update(){Cache();if(readInput&&Mouse.current!=null){if(Mouse.current.leftButton.wasPressedThisFrame)TryShootFromScreen(Mouse.current.position.ReadValue());if(Mouse.current.leftButton.wasReleasedThisFrame)Release();} Vector2 move=Vector2.zero;var k=Keyboard.current;if(readInput&&k!=null){move.x=(k.dKey.isPressed||k.rightArrowKey.isPressed?1:0)-(k.aKey.isPressed||k.leftArrowKey.isPressed?1:0);move.y=(k.wKey.isPressed||k.upArrowKey.isPressed?1:0)-(k.sKey.isPressed||k.downArrowKey.isPressed?1:0);if(k.spaceKey.wasPressedThisFrame && ownsMotion) AirJump();if(k.rKey.wasPressedThisFrame){CancelMotion();explorer.ResetPosition();return;}}Step(Time.deltaTime,move);}
 public WoodlandSkyAnchor FindClosestAnchor(){Cache();WoodlandSkyAnchor best=null;float nearest=range;foreach(var a in FindObjectsByType<WoodlandSkyAnchor>(FindObjectsSortMode.None)){float d=Vector3.Distance(HandPosition,a.transform.position);if(d<1||d>=nearest)continue;best=a;nearest=d;}return best;}
 public bool TryShootFromScreen(Vector2 screen){var nearest=FindClosestAnchor();if(nearest)return FireAt(nearest.transform.position);if(!Camera.main)return false;var hits=Physics.RaycastAll(Camera.main.ScreenPointToRay(screen),100,~0,QueryTriggerInteraction.Collide);System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));foreach(var h in hits){if(h.collider.transform.IsChildOf(transform)||h.collider.name.Contains("boundary"))continue;var sky=h.collider.GetComponent<WoodlandSkyAnchor>();if(sky)return FireAt(sky.transform.position);if(h.point.y<transform.position.y+.4f)return false;return FireAt(h.point);}return false;}
 public bool FireAt(Vector3 point){Cache();float distance=Vector3.Distance(transform.position+Vector3.up*.9f,point);if(distance<1||distance>range)return false;Anchor=point;tip=HandPosition;ropeLength=Vector3.Distance(HandPosition,Anchor);IsShooting=true;IsAttached=false;if(rope)rope.enabled=true;return true;}
 public bool AirJump(){Cache();if(!ownsMotion||!explorer.TryConsumeJump(out float speed))return false;velocity.y=Mathf.Max(velocity.y,speed);return true;}
 public void Step(float dt,Vector2 move){Cache();dt=Mathf.Clamp(dt,0,.05f);if(dt<=0)return;
 if(IsShooting){tip=Vector3.MoveTowards(tip,Anchor,shotSpeed*dt);if(Vector3.Distance(tip,Anchor)<.02f){IsShooting=false;IsAttached=true;if(!ownsMotion){velocity=explorer.CurrentVelocity;explorer.enabled=false;ownsMotion=true;}ropeLength=Mathf.Max(minimumRopeLength,Vector3.Distance(HandPosition,Anchor));}}
 if(!ownsMotion)return;
 move=Vector2.ClampMagnitude(move,1);
 Vector3 steer=new Vector3(move.x,0,move.y)*steerAcceleration;
 velocity+=Vector3.down*gravity*dt;
 if(IsAttached){Vector3 radial=HandPosition-Anchor;float distance=radial.magnitude;if(distance>.001f){Vector3 outward=radial/distance;ropeLength=Mathf.Max(minimumRopeLength,ropeLength-reelSpeed*dt);float extension=Mathf.Max(0,distance-ropeLength);float outwardSpeed=Vector3.Dot(velocity,outward);float tension=Mathf.Clamp(extension*springStrength+Mathf.Max(0,outwardSpeed)*radialDamping,0,pullAcceleration*3);velocity-=outward*tension*dt;steer=Vector3.ProjectOnPlane(steer,outward);}}
 velocity+=steer*dt;velocity=Vector3.ClampMagnitude(velocity,maxAirSpeed);
 var flags=controller.Move(velocity*dt);
 if((flags&CollisionFlags.Above)!=0&&velocity.y>0)velocity.y=0;
 if((flags&CollisionFlags.Below)!=0&&velocity.y<0){velocity.y=0;explorer.ResetAirJump();}
 // Safety constraint permits a little elastic stretch, removes only outward velocity.
 if(IsAttached){Vector3 radial=HandPosition-Anchor;float limit=ropeLength+1.2f;if(radial.magnitude>limit){Vector3 normal=radial.normalized;controller.Move(-normal*(radial.magnitude-limit));float outward=Vector3.Dot(velocity,normal);if(outward>0)velocity-=normal*outward;}}
 if(!IsAttached&&controller.isGrounded&&velocity.y<=0){ownsMotion=false;explorer.ResumeMotion(velocity);explorer.ResetAirJump();explorer.enabled=true;velocity=Vector3.zero;}
 if(transform.position.y< -5){CancelMotion();explorer.ResetPosition();}}
 void LateUpdate(){UpdateVisuals();}
 public void UpdateVisuals(){if(shootingVisual){bool active=IsShooting||IsAttached;shootingVisual.SetActive(active);if(explorer&&explorer.artwork)explorer.artwork.enabled=!active;if(Camera.main)shootingVisual.transform.rotation=Camera.main.transform.rotation;shootingVisual.transform.localScale=new Vector3(Anchor.x<transform.position.x?-1:1,1,1);}if(rope){rope.enabled=IsShooting||IsAttached;if(rope.enabled){rope.positionCount=2;rope.SetPosition(0,HandPosition);rope.SetPosition(1,IsAttached?Anchor:tip);}}if((IsAttached||IsShooting)&&shootingTexture&&explorer&&explorer.artwork){explorer.artwork.GetPropertyBlock(properties);properties.SetTexture("_MainTex",shootingTexture);properties.SetFloat("_FlipX",Anchor.x<transform.position.x?1:0);explorer.artwork.SetPropertyBlock(properties);}}
 public void Release(){IsShooting=false;IsAttached=false;if(rope)rope.enabled=false;RestoreNativePose();}
 void RestoreNativePose(){Cache();if(explorer&&explorer.artwork){explorer.artwork.enabled=true;explorer.artwork.GetPropertyBlock(properties);if(explorer.frames!=null&&explorer.frames.Length>0)properties.SetTexture("_MainTex",explorer.frames[0]);properties.SetFloat("_FlipX",0);explorer.artwork.SetPropertyBlock(properties);}if(shootingVisual)shootingVisual.SetActive(false);}
 public void CancelMotion(){Release();ownsMotion=false;velocity=Vector3.zero;if(explorer){explorer.enabled=true;if(explorer.artwork)explorer.artwork.enabled=true;}if(shootingVisual)shootingVisual.SetActive(false);}
 void OnDisable(){CancelMotion();}
 void OnApplicationFocus(bool focus){if(!focus)CancelMotion();}
}
