
using UnityEngine;

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
    SpriteRenderer sr;

    [Header("Hiding")]
    public KeyCode hideKey = KeyCode.E;

    public float CurrentNoiseRadius { get; private set; }
    public bool IsCrouching { get; private set; }
    public bool IsHidden { get; private set; }

    Rigidbody2D rb;
    Vector2 input;
    float speed;

    HidingSpot nearbyHidingSpot;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
    }

    void Update()
    {
        if (Input.GetKeyDown(hideKey))
        {
            if (IsHidden)
                ExitHiding();
            else if (nearbyHidingSpot != null)
                EnterHiding();
        }

        if (IsHidden)
        {
            input = Vector2.zero;
            CurrentNoiseRadius = 0f;
            return;
        }

        input = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        if (input.sqrMagnitude > 1f)
            input.Normalize();

        bool crouch =
            Input.GetKey(KeyCode.LeftControl) ||
            Input.GetKey(KeyCode.C);

        bool run = Input.GetKey(KeyCode.LeftShift) && !crouch;

        IsCrouching = crouch;

        if (crouch)
        {
            speed = crouchSpeed;
            CurrentNoiseRadius = crouchNoise;
        }
        else if (run)
        {
            speed = runSpeed;
            CurrentNoiseRadius = runNoise;
        }
        else
        {
            speed = walkSpeed;
            CurrentNoiseRadius = walkNoise;
        }

        if (input == Vector2.zero)
        {
            CurrentNoiseRadius = 0f;
        }
        else
        {
            float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    void FixedUpdate()
    {
        rb.velocity = IsHidden ? Vector2.zero : input * speed;
    }

    public void SetNearbyHidingSpot(HidingSpot spot)
    {
        nearbyHidingSpot = spot;
    }

    public void RemoveNearbyHidingSpot(HidingSpot spot)
    {
        if (nearbyHidingSpot == spot)
            nearbyHidingSpot = null;
    }

    void EnterHiding()
    {
        IsHidden = true;
        input = Vector2.zero;
        CurrentNoiseRadius = 0f;
        rb.velocity = Vector2.zero;
        if (nearbyHidingSpot != null) transform.position = nearbyHidingSpot.transform.position;
        SetAlpha(0.35f);
    }

    void ExitHiding()
    {
        IsHidden = false;
        SetAlpha(1f);
    }

    void SetAlpha(float a)
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = a;
        sr.color = c;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, CurrentNoiseRadius);
    }
}
