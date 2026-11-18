using UnityEngine;

namespace ForestRoadtrip25D
{
    public sealed class ForestTreeMotion : MonoBehaviour
    {
        public Transform fallPivot;
        public MeshRenderer canopy;
        public GameObject stump;
        public GameObject fallenWood;
        public Collider standingCollider;
        public float windAmplitude=.018f;
        public float windSpeed=1.5f;
        public float phase;
        public float fallDuration=1.15f;
        public bool IsFalling {get;private set;}
        public bool IsFallen {get;private set;}
        float elapsed;
        float direction=1;
        MaterialPropertyBlock properties;

        void OnEnable() { ResetTree(); }
        void Update() { if(IsFalling) Advance(Time.deltaTime); }

        void Wind(float strength)
        {
            if(!canopy) return;
            properties ??= new MaterialPropertyBlock();
            canopy.GetPropertyBlock(properties);
            properties.SetFloat("_SwayAmplitude",strength);
            properties.SetFloat("_SwaySpeed",windSpeed);
            properties.SetFloat("_SwayPhase",phase);
            canopy.SetPropertyBlock(properties);
        }

        public void ResetTree()
        {
            IsFalling=false;IsFallen=false;elapsed=0;
            if(fallPivot) {fallPivot.gameObject.SetActive(true);fallPivot.localRotation=Quaternion.identity;fallPivot.localPosition=Vector3.zero;}
            if(stump) stump.SetActive(false);
            if(fallenWood) fallenWood.SetActive(false);
            if(standingCollider) standingCollider.enabled=true;
            Wind(windAmplitude);
        }

        public void Fall(float horizontalDirection=1)
        {
            if(IsFalling || IsFallen) return;
            direction=horizontalDirection<0?-1:1;
            IsFalling=true;elapsed=0;Wind(0);
        }

        public void Advance(float delta)
        {
            if(!IsFalling) return;
            elapsed+=Mathf.Max(0,delta);
            ApplyFallPose(Mathf.Clamp01(elapsed/Mathf.Max(.1f,fallDuration)));
        }

        public void ApplyFallPose(float normalizedTime)
        {
            float t=Mathf.Clamp01(normalizedTime);
            // A short recoil precedes an accelerating fall, then the fallen wood appears.
            float angle=t<.17f?Mathf.Sin(t/.17f*Mathf.PI)*-4:Mathf.Pow((t-.17f)/.83f,1.7f)*88;
            if(fallPivot)
            {
                fallPivot.localRotation=Quaternion.Euler(0,0,-direction*angle);
                fallPivot.localPosition=new Vector3(0,Mathf.SmoothStep(0,.45f,t),0);
            }
            if(standingCollider) standingCollider.enabled=t<.35f;
            if(stump) stump.SetActive(t>.25f);
            if(t>=1)
            {
                IsFalling=false;IsFallen=true;
                if(fallPivot) fallPivot.gameObject.SetActive(false);
                if(fallenWood)
                {
                    var p=fallenWood.transform.localPosition;p.x=Mathf.Abs(p.x)*direction;fallenWood.transform.localPosition=p;
                    fallenWood.SetActive(true);
                }
            }
        }

        [ContextMenu("Fall right")]
        void FallRight(){Fall(1);}
        [ContextMenu("Reset standing tree")]
        void ResetStanding(){ResetTree();}
    }
}
