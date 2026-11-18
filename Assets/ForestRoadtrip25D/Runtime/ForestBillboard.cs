using UnityEngine;

namespace ForestRoadtrip25D
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ForestBillboard : MonoBehaviour
    {
        void LateUpdate()
        {
            Camera camera = Camera.main;
            if (camera) transform.rotation = camera.transform.rotation;
        }
    }
}
