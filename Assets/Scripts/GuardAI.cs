using UnityEngine;

// Guard AI, version 1.
// States: Patrol -> Investigate -> Chase -> Search -> Return.
// Needs: a Rigidbody2D (Gravity Scale 0), ThiefController.cs in the project,
// the thief tagged "Player", and the vision cone as the FIRST child of this object.
[RequireComponent(typeof(Rigidbody2D))]
public class GuardAI : MonoBehaviour
{
    public enum State { Patrol, Investigate, Chase, Search, Return }

    [Header("Vision")]
    public float viewRange = 6f;                 // cells
    [Range(10f, 180f)] public float viewAngle = 60f;   // total cone angle (the cone sprite is 60)
    public float detectTime = 1f;                // seconds of continuous sight before detection
    public float detectDecay = 0.5f;             // meter drain per second when not seen

    [Header("Movement")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;
    public float turnSpeed = 360f;               // degrees per second
    public float catchDistance = 0.8f;

    [Header("Timing")]
    public float loseSightTime = 2f;
    public float searchTime = 4f;

    [Header("Patrol route (optional)")]
    [Tooltip("Leave empty to walk forward and turn around at walls.")]
    public Transform[] waypoints;

    public State CurrentState { get; private set; } = State.Patrol;
    public float Detection { get; private set; }  // 0 to 1

    Rigidbody2D rb;
    Transform player;
    ThiefController thief;
    SpriteRenderer cone;
    Vector2 home;
    Vector2 targetPoint;
    Vector2 lastKnown;
    Vector2 desiredVel;
    float timer;
    int wpIndex;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        home = transform.position;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            thief = p.GetComponent<ThiefController>();
        }
        else
        {
            Debug.LogWarning("GuardAI: no object tagged Player found.");
        }

        if (transform.childCount > 0) cone = transform.GetChild(0).GetComponent<SpriteRenderer>();
        FitCone();
    }

    void Update()
    {
        if (player == null) return;

        bool sees = CanSeePlayer();
        if (sees)
        {
            lastKnown = player.position;
            Detection = Mathf.Min(1f, Detection + Time.deltaTime / detectTime);
        }
        else
        {
            Detection = Mathf.Max(0f, Detection - detectDecay * Time.deltaTime);
        }

        bool heard = HearsPlayer();

        switch (CurrentState)
        {
            case State.Patrol:
                Patrol();
                if (Detection >= 1f) EnterChase();
                else if (heard) { targetPoint = player.position; Enter(State.Investigate); }
                break;

            case State.Investigate:
                if (heard) targetPoint = player.position;
                MoveTo(targetPoint, patrolSpeed);
                if (Detection >= 1f) EnterChase();
                else if (Arrived(targetPoint)) Enter(State.Search);
                break;

            case State.Chase:
                if (sees)
                {
                    timer = 0f;
                    MoveTo(player.position, chaseSpeed);
                    if (Vector2.Distance(transform.position, player.position) < catchDistance) Caught();
                }
                else
                {
                    timer += Time.deltaTime;
                    MoveTo(lastKnown, chaseSpeed);
                    if (Arrived(lastKnown) || timer > loseSightTime) Enter(State.Search);
                }
                break;

            case State.Search:
                desiredVel = Vector2.zero;
                transform.Rotate(0f, 0f, 90f * Time.deltaTime);   // look around
                timer += Time.deltaTime;
                if (Detection >= 1f) EnterChase();
                else if (heard) { targetPoint = player.position; Enter(State.Investigate); }
                else if (timer >= searchTime) Enter(State.Return);
                break;

            case State.Return:
                MoveTo(home, patrolSpeed);
                if (Detection >= 1f) EnterChase();
                else if (heard) { targetPoint = player.position; Enter(State.Investigate); }
                else if (Arrived(home)) Enter(State.Patrol);
                break;
        }

        UpdateConeColor();
    }

    void FixedUpdate()
    {
        rb.velocity = desiredVel;
    }

    // ---------- state helpers ----------

    void Enter(State s)
    {
        CurrentState = s;
        timer = 0f;
    }

    void EnterChase()
    {
        lastKnown = player.position;
        Enter(State.Chase);
    }

    void Caught()
    {
        Debug.Log("Thief caught!");
        if (GameManager.Instance != null) GameManager.Instance.Caught();
        else Time.timeScale = 0f;
    }

    void Patrol()
    {
        if (waypoints != null && waypoints.Length > 0)
        {
            Vector2 wp = waypoints[wpIndex].position;
            MoveTo(wp, patrolSpeed);
            if (Arrived(wp)) wpIndex = (wpIndex + 1) % waypoints.Length;
            return;
        }

        // No waypoints: walk forward and turn around at a wall.
        if (WallAhead(0.8f)) transform.Rotate(0f, 0f, 180f);
        desiredVel = (Vector2)transform.right * patrolSpeed;
    }

    // ---------- movement ----------

    void MoveTo(Vector2 target, float speed)
    {
        Vector2 next = NextPoint(target);
        Vector2 dir = next - (Vector2)transform.position;
        if (dir.magnitude < 0.05f) { desiredVel = Vector2.zero; return; }

        dir.Normalize();
        FaceDirection(dir);
        desiredVel = dir * speed;
    }

    // TODO: replace with member 1's A* (FindPath) so the guard walks around walls.
    // For now the guard walks in a straight line to the target.
    Vector2 NextPoint(Vector2 target)
    {
        return target;
    }

    void FaceDirection(Vector2 dir)
    {
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float z = Mathf.MoveTowardsAngle(transform.eulerAngles.z, angle, turnSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, 0f, z);
    }

    bool Arrived(Vector2 point)
    {
        return Vector2.Distance(transform.position, point) < 0.3f;
    }

    // ---------- perception ----------

    bool CanSeePlayer()
    {
        Vector2 eye = transform.position;
        Vector2 toPlayer = (Vector2)player.position - eye;
        float dist = toPlayer.magnitude;

        if (dist > viewRange) return false;
        if (Vector2.Angle(transform.right, toPlayer) > viewAngle * 0.5f) return false;

        // A crouching thief is only seen from half the range.
        if (thief != null && thief.IsCrouching && dist > viewRange * 0.5f) return false;

        return !WallBetween(eye, player.position);
    }

    bool HearsPlayer()
    {
        if (thief == null || thief.CurrentNoiseRadius <= 0f) return false;
        return Vector2.Distance(transform.position, player.position) <= thief.CurrentNoiseRadius;
    }

    bool WallBetween(Vector2 a, Vector2 b)
    {
        Vector2 d = b - a;
        RaycastHit2D[] hits = Physics2D.RaycastAll(a, d.normalized, d.magnitude);
        foreach (RaycastHit2D h in hits)
            if (IsWall(h.collider)) return true;
        return false;
    }

    bool WallAhead(float distance)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, transform.right, distance);
        foreach (RaycastHit2D h in hits)
            if (IsWall(h.collider)) return true;
        return false;
    }

    // Walls and doors are created by MuseumBuilder under groups named "Walls" and "Doors".
    static bool IsWall(Collider2D c)
    {
        if (c == null || c.isTrigger) return false;
        Transform p = c.transform.parent;
        return p != null && (p.name == "Walls" || p.name == "Doors");
    }

    // ---------- cone visuals ----------

    // Scales the cone child so its length matches viewRange.
    void FitCone()
    {
        if (cone == null) return;
        float s = viewRange / (2f * transform.lossyScale.x);   // cone sprite is 2 units long at scale 1
        cone.transform.localPosition = Vector3.zero;
        cone.transform.localRotation = Quaternion.identity;
        cone.transform.localScale = new Vector3(s, s, 1f);
    }

    void UpdateConeColor()
    {
        if (cone == null) return;
        Color c;
        if (CurrentState == State.Chase) c = new Color(1f, 0.15f, 0.15f, 1f);
        else c = Color.Lerp(new Color(1f, 0.9f, 0.2f, 1f), new Color(1f, 0.45f, 0.1f, 1f), Detection);
        cone.color = c;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 p = transform.position;
        float half = viewAngle * 0.5f;
        Gizmos.DrawLine(p, p + Quaternion.Euler(0f, 0f, half) * transform.right * viewRange);
        Gizmos.DrawLine(p, p + Quaternion.Euler(0f, 0f, -half) * transform.right * viewRange);
    }
}
