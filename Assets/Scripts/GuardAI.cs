using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class GuardAI : MonoBehaviour
{
    public enum State { Patrol, Investigate, Chase, Search, Return }

    [Header("Vision")]
    public float viewRange = 6f;
    [Range(10f, 180f)] public float viewAngle = 60f;
    public float detectTime = 1f;
    public float detectDecay = 0.5f;

    [Header("Movement")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;
    public float turnSpeed = 360f;
    public float catchDistance = 0.8f;

    [Header("Steering")]
    public float lookAheadMax = 1f;
    public float feelerLength = 1.2f;
    public float avoidStrength = 1.5f;

    [Header("Teamwork")]
    public float shareRadius = 12f;

    [Header("Timing")]
    public float loseSightTime = 2f;
    public float searchTime = 4f;

    [Header("Patrol route (optional)")]
    [Tooltip("Leave empty to walk forward and turn around at walls.")]
    public Transform[] waypoints;

    public State CurrentState { get; private set; } = State.Patrol;
    public float Detection { get; private set; }
    public bool HasVisualContact { get; private set; }
    public Vector2 LastKnownPosition { get { return lastKnown; } }

    static readonly List<GuardAI> all = new List<GuardAI>();

    Rigidbody2D rb;
    Transform player;
    ThiefController thief;
    Rigidbody2D thiefRb;
    SpriteRenderer cone;
    Vector2 home;
    Vector2 targetPoint;
    Vector2 lastKnown;
    Vector2 desiredVel;
    float timer;
    int wpIndex;
    float nextStuckCheck;
    Vector2 stuckCheckPos;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        home = transform.position;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            thief = p.GetComponent<ThiefController>();
            thiefRb = p.GetComponent<Rigidbody2D>();
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
        if (player == null)
        {
            HasVisualContact = false;
            return;
        }

        bool sees = CanSeePlayer();
        HasVisualContact = sees;
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
                    MoveTo(PredictedPosition(), chaseSpeed);
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
                transform.Rotate(0f, 0f, 90f * Time.deltaTime);
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

    void Enter(State s)
    {
        CurrentState = s;
        timer = 0f;
    }

    void EnterChase()
    {
        lastKnown = player.position;
        Enter(State.Chase);
        ShareAlert();
    }

    void OnEnable()
    {
        all.Add(this);

        Collider2D[] mine = GetComponents<Collider2D>();
        foreach (GuardAI other in all)
        {
            if (other == this) continue;
            foreach (Collider2D a in mine)
                foreach (Collider2D b in other.GetComponents<Collider2D>())
                    Physics2D.IgnoreCollision(a, b, true);
        }
    }

    void OnDisable()
    {
        all.Remove(this);
    }

    public static void AlertNearest(Vector2 pos, int count)
    {
        List<GuardAI> sorted = new List<GuardAI>(all);
        sorted.Sort((a, b) => Vector2.Distance(a.transform.position, pos).CompareTo(Vector2.Distance(b.transform.position, pos)));
        for (int i = 0; i < sorted.Count && i < count; i++) sorted[i].HearAlert(pos);
    }

    public static void AlertWithin(Vector2 pos, float radius)
    {
        foreach (GuardAI g in all)
            if (Vector2.Distance(g.transform.position, pos) <= radius) g.HearAlert(pos);
    }

    public void HearAlert(Vector2 pos)
    {
        if (CurrentState == State.Chase) return;
        targetPoint = pos;
        Enter(State.Investigate);
    }

    void ShareAlert()
    {
        foreach (GuardAI other in all)
        {
            if (other == this) continue;
            if (Vector2.Distance(other.transform.position, transform.position) <= shareRadius)
                other.HearAlert(lastKnown);
        }
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

        if (Time.time >= nextStuckCheck)
        {
            if (Vector2.Distance(transform.position, stuckCheckPos) < 0.1f) transform.Rotate(0f, 0f, 180f);
            stuckCheckPos = transform.position;
            nextStuckCheck = Time.time + 0.6f;
        }

        if (WallAhead(0.8f)) transform.Rotate(0f, 0f, 180f);
        desiredVel = (Vector2)transform.right * patrolSpeed;
    }

    void MoveTo(Vector2 target, float speed)
    {
        Vector2 next = NextPoint(target);
        Vector2 dir = next - (Vector2)transform.position;
        if (dir.magnitude < 0.05f) { desiredVel = Vector2.zero; return; }

        dir.Normalize();
        dir = AvoidWalls(dir);
        FaceDirection(dir);
        desiredVel = dir * speed;
    }

    Vector2 PredictedPosition()
    {
        Vector2 pos = player.position;
        if (thiefRb == null) return pos;

        float dist = Vector2.Distance(transform.position, pos);
        float t = Mathf.Min(dist / chaseSpeed, lookAheadMax);
        return pos + thiefRb.velocity * t;
    }

    Vector2 AvoidWalls(Vector2 dir)
    {
        Vector2 result = dir;
        float[] angles = { 0f, 35f, -35f };

        foreach (float angle in angles)
        {
            Vector2 feeler = Quaternion.Euler(0f, 0f, angle) * (Vector3)dir;
            RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, feeler, feelerLength);

            foreach (RaycastHit2D h in hits)
            {
                if (!IsWall(h.collider)) continue;
                float closeness = 1f - h.distance / feelerLength;
                result += h.normal * avoidStrength * closeness;
                break;
            }
        }

        if (result.sqrMagnitude < 0.05f) result = new Vector2(-dir.y, dir.x);
        return result.normalized;
    }

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

    bool CanSeePlayer()
    {
        Vector2 eye = transform.position;
        Vector2 toPlayer = (Vector2)player.position - eye;
        float dist = toPlayer.magnitude;

        if (dist > viewRange) return false;
        if (Vector2.Angle(transform.right, toPlayer) > viewAngle * 0.5f) return false;

        if (thief != null && thief.IsCrouching && dist > viewRange * 0.5f) return false;

        if (thief != null && thief.IsHidden) return false;

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

    public static bool IsWall(Collider2D c)
    {
        if (c == null || c.isTrigger) return false;
        Transform p = c.transform.parent;
        return p != null && (p.name == "Walls" || p.name == "Doors");
    }

    void FitCone()
    {
        if (cone == null) return;
        float s = viewRange / (2f * transform.lossyScale.x);
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