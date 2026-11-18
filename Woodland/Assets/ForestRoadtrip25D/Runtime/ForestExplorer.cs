using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ForestRoadtrip25D
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ForestExplorer : MonoBehaviour
    {
        public MeshRenderer artwork;
        public Texture2D[] frames;
        public float walkSpeed = 3.4f;
        public float runSpeed = 5.5f;
        CharacterController controller;
        MaterialPropertyBlock properties;
        float verticalSpeed;
        float animationTime;
        int facing;
        bool flipped;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            properties = new MaterialPropertyBlock();
        }

        void Update()
        {
            Vector2 movement = Vector2.zero;
            bool run = false, reset = false;
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k != null)
            {
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) movement.x--;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) movement.x++;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) movement.y++;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) movement.y--;
                run = k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
                reset = k.rKey.wasPressedThisFrame;
                if(k.spaceKey.wasPressedThisFrame && controller.isGrounded) verticalSpeed=7;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) movement.x--;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) movement.x++;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) movement.y++;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) movement.y--;
            run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            reset = Input.GetKeyDown(KeyCode.R);
#endif
            Step(movement, run, Time.deltaTime);
            if (reset || transform.position.y < -4) ResetPosition();
        }

        public void Step(Vector2 movement, bool run, float delta)
        {
            if (!controller) controller = GetComponent<CharacterController>();
            movement = Vector2.ClampMagnitude(movement, 1);
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed -= 18 * delta;
            float speed = run ? runSpeed : walkSpeed;
            controller.Move(new Vector3(movement.x * speed, verticalSpeed, movement.y * speed) * delta);
            int frame = facing;
            if (movement.sqrMagnitude > .01f)
            {
                animationTime += delta;
                if (Mathf.Abs(movement.x) > Mathf.Abs(movement.y))
                {
                    facing = 3;
                    flipped = movement.x < 0;
                }
                else { facing = movement.y > 0 ? 6 : 0; flipped = false; }
                frame = facing + (int)(animationTime * 9) % 3;
            }
            else animationTime = 0;
            if (artwork && frames != null && frames.Length == 9)
            {
                properties ??= new MaterialPropertyBlock();
                artwork.GetPropertyBlock(properties);
                properties.SetTexture("_MainTex", frames[frame]);
                properties.SetFloat("_FlipX", flipped ? 1 : 0);
                artwork.SetPropertyBlock(properties);
            }
        }

        public void ResetPosition()
        {
            controller.enabled = false;
            transform.position = new Vector3(0,.1f,-4);
            verticalSpeed = 0;
            controller.enabled = true;
        }
    }
}
