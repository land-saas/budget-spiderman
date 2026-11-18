using UnityEngine;
/// Authored, stationary sky target. Move these objects in the scene to edit the route.
public sealed class WoodlandSkyAnchor : MonoBehaviour {
 void LateUpdate(){if(Camera.main)transform.rotation=Camera.main.transform.rotation;}
 void OnDrawGizmosSelected(){Gizmos.color=Color.cyan;Gizmos.DrawWireSphere(transform.position,1);}
}
