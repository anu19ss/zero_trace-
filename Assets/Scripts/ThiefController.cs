using UnityEngine;

// Moves the thief with WASD or arrow keys.
// Hold Shift to run (fast, loud). Hold Ctrl or C to crouch (slow, quiet).
// Guards read CurrentNoiseRadius to decide whether they can hear the thief.
[RequireComponent(typeof(Rigidbody2D))]
public class ThiefController : MonoBehaviour
{
    [Header("Speed (cells per second)")]
    public float crouchSpeed = 1.5f;
    public float walkSpeed = 3f;
    public float runSpeed = 5f;

    [Header("Noise radius (cells)")]
    public float crouchNoise = 0f;
    public float walkNoise = 3f;
    public float runNoise = 6f;

    public float CurrentNoiseRadius { get; private set; }
    public bool IsCrouching { get; private set; }

    Rigidbody2D rb;
    Vector2 input;
    float speed;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude > 1f) input.Normalize();

        bool crouch = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
        bool run = Input.GetKey(KeyCode.LeftShift) && !crouch;
        IsCrouching = crouch;

        if (crouch) { speed = crouchSpeed; CurrentNoiseRadius = crouchNoise; }
        else if (run) { speed = runSpeed; CurrentNoiseRadius = runNoise; }
        else { speed = walkSpeed; CurrentNoiseRadius = walkNoise; }

        // No movement means no noise.
        if (input == Vector2.zero) CurrentNoiseRadius = 0f;
        else
        {
            // The sprite faces right, so rotate it towards the movement direction.
            float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    void FixedUpdate()
    {
        rb.velocity = input * speed;
    }

    // Select the thief in the Scene view to see the noise circle.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, CurrentNoiseRadius);
    }
}
