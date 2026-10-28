using UnityEngine;

namespace ChainboundRaid
{
    [RequireComponent(typeof(CharacterController))]
    public class PrototypePlayer : MonoBehaviour
    {
        public float speed = 5f;
        public float dodgeSpeed = 9f;
        public float gravity = -20f;
        public float stamina = 100f;
        public bool braced;
        public bool pulling;
        public float health = 100f;
        public Transform cameraPivot;
        public KeyCode forward = KeyCode.W, back = KeyCode.S, left = KeyCode.A, right = KeyCode.D;
        public KeyCode dodge = KeyCode.Space, brace = KeyCode.LeftShift, pull = KeyCode.E;
        public KeyCode attack = KeyCode.Mouse0;
        public ElephantBossPrototype boss;
        float nextAttack;
        CharacterController controller;
        float verticalVelocity;
        float dodgeUntil;
        Vector3 dodgeDirection;

        void Awake() { controller = GetComponent<CharacterController>(); }
        void Update()
        {
            if (health <= 0) return;
            if (Input.GetKeyDown(brace)) braced = !braced;
            if (Input.GetKeyDown(attack) && Time.time >= nextAttack && boss != null &&
                Vector3.Distance(transform.position, boss.transform.position) < 3.5f)
            { boss.health = Mathf.Max(0, boss.health - 18f); nextAttack = Time.time + 0.65f; }
            pulling = braced && controller.isGrounded && Input.GetKey(pull);
            Vector2 input = new Vector2((Input.GetKey(right) ? 1 : 0) - (Input.GetKey(left) ? 1 : 0),
                (Input.GetKey(forward) ? 1 : 0) - (Input.GetKey(back) ? 1 : 0));
            Vector3 direction = new Vector3(input.x, 0, input.y).normalized;
            if (cameraPivot != null)
            {
                direction = cameraPivot.forward * input.y + cameraPivot.right * input.x;
                direction.y = 0;
                direction.Normalize();
            }
            if (Input.GetKeyDown(dodge) && stamina >= 20 && direction.sqrMagnitude > 0.01f)
            { stamina -= 20; dodgeDirection = direction; dodgeUntil = Time.time + 0.3f; }
            stamina = Mathf.Min(100, stamina + 14 * Time.deltaTime);
            Vector3 horizontal = Time.time < dodgeUntil ? dodgeDirection * dodgeSpeed : direction * speed * (braced ? 0.85f : 1f);
            if (horizontal.sqrMagnitude > 0.01f) transform.forward = Vector3.Slerp(transform.forward, horizontal.normalized, 12 * Time.deltaTime);
            verticalVelocity = controller.isGrounded ? -1f : verticalVelocity + gravity * Time.deltaTime;
            controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        public void ApplyDamage(float amount) { health = Mathf.Max(0, health - amount); }
    }
}
