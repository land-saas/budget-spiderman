using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ForestRoadtrip25D
{
    public sealed class ForestTreeInteraction : MonoBehaviour
    {
        public float reach=4f;
        ForestTreeMotion[] trees;
        void Start(){trees=FindObjectsByType<ForestTreeMotion>(FindObjectsSortMode.None);}
        void Update()
        {
            bool fell=false,reset=false;
#if ENABLE_INPUT_SYSTEM
            if(Keyboard.current!=null){fell=Keyboard.current.eKey.wasPressedThisFrame;reset=Keyboard.current.tKey.wasPressedThisFrame;}
#elif ENABLE_LEGACY_INPUT_MANAGER
            fell=Input.GetKeyDown(KeyCode.E);reset=Input.GetKeyDown(KeyCode.T);
#endif
            if(reset) foreach(var tree in trees) if(tree) tree.ResetTree();
            if(!fell) return;
            ForestTreeMotion nearest=null;float best=reach*reach;
            foreach(var tree in trees)
            {
                if(!tree || tree.IsFallen || tree.IsFalling) continue;
                float d=(tree.transform.position-transform.position).sqrMagnitude;
                if(d<best){best=d;nearest=tree;}
            }
            if(nearest) nearest.Fall(nearest.transform.position.x>=transform.position.x?1:-1);
        }
    }
}
