using UnityEngine;
// Keeps the distant painting covering the view while preserving world depth.
[ExecuteAlways]public sealed class BackdropHorizontalFollow:MonoBehaviour{
 public Transform player; void LateUpdate(){if(player){var p=transform.position;p.x=player.position.x;transform.position=p;}}
}
