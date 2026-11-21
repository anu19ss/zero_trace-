using UnityEngine;

// Security camera AI: sweeps left and right, and alerts the nearest guards when it spots the thief.
// Put this on the SecurityCamera prefab. The vision cone must be the FIRST child.
public class SecurityCameraAI : MonoBehaviour
{
    [Header("Vision")]
    public float viewRange = 7f;
    [Range(10f, 180f)] public float viewAngle = 60f;   // the cone sprite is 60
    public float detectTime = 0.8f;
    public float detectDecay = 0.5f;

    [Header("Sweep")]
    public float sweepAngle = 40f;      // swings this many degrees either side
    public float sweepSpeed = 0.7f;

    [Header("Alarm")]
    public int guardsToAlert = 2;
    public float alertCooldown = 5f;

    public float Detection { get; private set; }
    public bool HasVisualContact { get; private set; }
    public Vector2 LastSeenPosition { get; private set; }

    Transform player;
    ThiefController thief;
    SpriteRenderer cone;
    float baseAngle;
    float phase;
    float nextAlertTime;

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            thief = p.GetComponent<ThiefController>();
        }

        if (transform.childCount > 0) cone = transform.GetChild(0).GetComponent<SpriteRenderer>();

        // Face the middle of the museum, snapped to up, down, left or right.
        Vector2 toCenter = (Vector2)transform.root.position - (Vector2)transform.position;
        float a = Mathf.Atan2(toCenter.y, toCenter.x) * Mathf.Rad2Deg;
        baseAngle = Mathf.Round(a / 90f) * 90f;
        phase = transform.position.x + transform.position.y;   // so cameras do not all swing together

        FitCone();
    }

    void Update()
    {
        float angle = baseAngle + Mathf.Sin(Time.time * sweepSpeed + phase) * sweepAngle;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        if (player == null)
        {
            HasVisualContact = false;
            return;
        }

        HasVisualContact = CanSeePlayer();
        if (HasVisualContact)
        {
            LastSeenPosition = player.position;
            Detection = Mathf.Min(1f, Detection + Time.deltaTime / detectTime);
        }
        else Detection = Mathf.Max(0f, Detection - detectDecay * Time.deltaTime);

        if (Detection >= 1f && Time.time >= nextAlertTime)
        {
            nextAlertTime = Time.time + alertCooldown;
            GuardAI.AlertNearest(player.position, guardsToAlert);
            if (GameManager.Instance != null) GameManager.Instance.Message("A camera spotted you!");
        }

        if (cone != null)
            cone.color = Color.Lerp(new Color(1f, 0.9f, 0.2f, 1f), new Color(1f, 0.15f, 0.15f, 1f), Detection);
    }

    bool CanSeePlayer()
    {
        Vector2 eye = transform.position;
        Vector2 toPlayer = (Vector2)player.position - eye;
        float dist = toPlayer.magnitude;

        if (dist > viewRange) return false;
        if (Vector2.Angle(transform.right, toPlayer) > viewAngle * 0.5f) return false;
        if (thief != null && thief.IsCrouching && dist > viewRange * 0.5f) return false;

        RaycastHit2D[] hits = Physics2D.RaycastAll(eye, toPlayer.normalized, dist);
        foreach (RaycastHit2D h in hits)
            if (GuardAI.IsWall(h.collider)) return false;
        return true;
    }

    void FitCone()
    {
        if (cone == null) return;
        float s = viewRange / (2f * transform.lossyScale.x);
        cone.transform.localPosition = Vector3.zero;
        cone.transform.localRotation = Quaternion.identity;
        cone.transform.localScale = new Vector3(s, s, 1f);
    }
}
