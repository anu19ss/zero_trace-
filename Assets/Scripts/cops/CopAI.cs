using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class CopAI : MonoBehaviour
{
    public enum State { Idle, Chase, Search, Return }

    [Header("Vision")]
    public float viewRange = 7f;
    [Range(10f, 180f)] public float viewAngle = 80f;
    public float detectTime = 1.2f;
    public float detectDecay = 0.8f;
    [Range(0.1f, 1f)] public float crouchVisionRangeMultiplier = 0.35f;
    [Range(0.1f, 1f)] public float crouchDetectionRateMultiplier = 0.35f;
    [Min(0f)] public float crouchCloseDetectionDistance = 1.1f;

    [Header("Hearing")]
    public float hearingBonus = 1.3f;

    [Header("Movement")]
    public float patrolSpeed = 2.2f;
    public float chaseSpeed = 3.8f;
    public float turnSpeed = 360f;
    public float catchDistance = 0.6f;
    public float arriveDistance = 0.3f;

    [Header("Steering")]
    public float feelerLength = 1.25f;
    public float avoidStrength = 1.6f;
    public float separationRadius = 1.1f;
    public float separationStrength = 1.2f;

    [Header("Pathfinding")]
    public float replanInterval = 0.4f;
    public float replanTargetDistance = 1.5f;
    public float waypointDistance = 0.35f;

    [Header("Search")]
    [Min(8f)] public float searchDuration = 12f;
    [Min(1f)] public float searchRadius = 7f;
    [Range(2, 16)] public int searchPointCount = 8;
    public float hidingSpotDrainMultiplier = 3f;
    public float hidingSafeRadius = 3f;

    [Header("Boundary")]
    public float boundaryMargin = 0.12f;
    [Min(0f)] public float wallClearance = 0.035f;
    [Min(0f)] public float minimumCopSpacing = 0.32f;

    public State CurrentState { get; private set; } = State.Idle;
    public float Detection { get; private set; }
    public bool HasVisualContact { get; private set; }

    static readonly List<CopAI> allCops = new List<CopAI>();

    Rigidbody2D rb;
    Transform player;
    ThiefController thief;
    Animator animator;
    SpriteRenderer copSprite;
    SpriteRenderer cone;
    Vector2 facingDirection = Vector2.right;
    MuseumBuilder museum;
    CopPathfinder pathfinder;
    Vector2 home;
    Vector2 desiredVelocity;
    Vector2 lastKnownPosition;
    Vector2 lastPathTarget;
    Vector2 assignedSearchDestination;
    readonly List<Vector2> path = new List<Vector2>();
    readonly List<Vector2> searchPoints = new List<Vector2>();
    int pathIndex;
    int searchPointIndex;
    int searchPhase;
    float nextPlan;
    float searchStartedAt;
    bool hasLastKnownPosition;
    bool searchTimerStarted;
    bool isPrimaryPursuer;
    bool hasPersonallyDetectedThief;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        copSprite = GetComponent<SpriteRenderer>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.constraints |= RigidbodyConstraints2D.FreezeRotation;
        }

        if (transform.childCount > 0)
            cone = transform.GetChild(0).GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        searchPhase = allCops.Count % 3;
        for (int i = 0; i < allCops.Count; i++)
            IgnoreCollisionWith(allCops[i]);
        if (!allCops.Contains(this))
            allCops.Add(this);

        if (CopSquadManager.Instance != null)
            CopSquadManager.Instance.Register(this);
    }

    void Start()
    {
        CopSquadManager squad = CopSquadManager.Instance;
        if (squad != null)
        {
            museum = squad.GetMuseum();
            player = squad.GetPlayer();
        }

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        if (player != null)
            thief = player.GetComponent<ThiefController>();
        if (museum == null)
            museum = FindObjectOfType<MuseumBuilder>();

        pathfinder = new CopPathfinder(museum);
        home = FindNearestWalkable(rb.position);
        assignedSearchDestination = home;
        SetPosition(home);
        if (cone == null && transform.childCount > 0)
            cone = transform.GetChild(0).GetComponent<SpriteRenderer>();
        if (CurrentState == State.Search)
            BeginSearch(assignedSearchDestination);
        FitCone();

        if (squad != null)
            squad.Register(this);
    }

    void OnDisable()
    {
        allCops.Remove(this);
        if (CopSquadManager.Instance != null)
            CopSquadManager.Instance.Unregister(this);
    }

    void IgnoreCollisionWith(CopAI other)
    {
        if (other == null || other == this)
            return;

        Collider2D[] mine = GetComponentsInChildren<Collider2D>();
        Collider2D[] theirs = other.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < mine.Length; i++)
        {
            for (int j = 0; j < theirs.Length; j++)
            {
                if (mine[i] != null && theirs[j] != null)
                    Physics2D.IgnoreCollision(mine[i], theirs[j], true);
            }
        }
    }

    void Update()
    {
        if (player == null)
        {
            CopSquadManager squad = CopSquadManager.Instance;
            if (squad != null)
            {
                player = squad.GetPlayer();
                if (player != null)
                {
                    thief = player.GetComponent<ThiefController>();
                }
            }
        }

        if (player == null || (GameManager.Instance != null && GameManager.Instance.GameOver))
        {
            desiredVelocity = Vector2.zero;
            HasVisualContact = false;
            return;
        }

        HasVisualContact = CanSeePlayer();
        if (HasVisualContact)
        {
            if (CurrentState == State.Chase && hasPersonallyDetectedThief)
            {
                lastKnownPosition = player.position;
                CopSquadManager.ShareThiefPosition(lastKnownPosition);
            }
            else
            {
                float detectionRate = thief != null && thief.IsCrouching
                    ? crouchDetectionRateMultiplier
                    : 1f;
                Detection = Mathf.Min(1f,
                    Detection + Time.deltaTime * detectionRate /
                    Mathf.Max(0.05f, detectTime));

                // A cop that has already been alerted resumes pursuit as soon
                // as it sees the thief again. Initial detection still uses the
                // configured detection time.
                bool reacquired = hasPersonallyDetectedThief &&
                    (CurrentState == State.Search || CurrentState == State.Return);
                if (reacquired || Detection >= 1f)
                {
                    hasPersonallyDetectedThief = true;
                    lastKnownPosition = player.position;
                    hasLastKnownPosition = true;
                    Enter(State.Chase);
                    CopSquadManager.RaiseAlarm(lastKnownPosition, 1, true);
                }
            }
        }
        else
        {
            Detection = Mathf.Max(0f, Detection -
                DetectionDrainMultiplier() * detectDecay * Time.deltaTime);
        }

        // Hearing only identifies a nearby noise. It does not publish the
        // thief's hidden coordinates to the squad.
        if (!HasVisualContact && CurrentState == State.Idle && HearsPlayer())
            BeginSearch((Vector2)transform.position);

        switch (CurrentState)
        {
            case State.Idle:
                desiredVelocity = Vector2.zero;
                break;

            case State.Chase:
                if (HasVisualContact && hasPersonallyDetectedThief)
                {
                    if (Vector2.Distance(transform.position, player.position) <=
                        Mathf.Max(catchDistance, 1f) && GameManager.Instance != null)
                    {
                        desiredVelocity = Vector2.zero;
                        GameManager.Instance.Caught();
                        break;
                    }

                    MoveAlongPath(player.position, chaseSpeed);
                }
                else if (hasLastKnownPosition)
                {
                    Vector2 pursuitTarget = isPrimaryPursuer
                        ? lastKnownPosition
                        : assignedSearchDestination;
                    MoveAlongPath(pursuitTarget, patrolSpeed);
                    if (AtTargetOrClosestReachable(pursuitTarget))
                        BeginSearch(pursuitTarget);
                }
                else
                {
                    Enter(State.Idle);
                }
                break;

            case State.Search:
                if (HasVisualContact && hasPersonallyDetectedThief)
                {
                    Enter(State.Chase);
                    CopSquadManager.RaiseAlarm(lastKnownPosition, 1, true);
                    break;
                }

                if (!searchTimerStarted)
                {
                    Vector2 firstPoint = searchPoints[0];
                    MoveAlongPath(firstPoint, patrolSpeed);
                    if (Arrived(firstPoint))
                    {
                        searchTimerStarted = true;
                        searchStartedAt = Time.time;
                        searchPointIndex = searchPoints.Count > 1 ? 1 : 0;
                        InvalidatePath();
                    }
                    break;
                }

                if (Time.time - searchStartedAt >= Mathf.Max(8f, searchDuration))
                {
                    Enter(State.Return);
                    break;
                }

                if (searchPoints.Count == 0)
                {
                    desiredVelocity = Vector2.zero;
                    break;
                }

                Vector2 searchTarget = searchPoints[searchPointIndex];
                MoveAlongPath(searchTarget, patrolSpeed);
                if (Arrived(searchTarget))
                {
                    searchPointIndex = (searchPointIndex + 1) % searchPoints.Count;
                    InvalidatePath();
                }
                break;

            case State.Return:
                MoveAlongPath(home, patrolSpeed);
                if (Arrived(home))
                    Enter(State.Idle);
                break;
        }

        UpdateConeColor();
    }

    void FixedUpdate()
    {
        if (rb == null)
            return;

        if (CurrentState == State.Idle ||
            (GameManager.Instance != null && GameManager.Instance.GameOver))
        {
            desiredVelocity = Vector2.zero;
            rb.velocity = Vector2.zero;
            rb.constraints |= RigidbodyConstraints2D.FreezePositionX |
                RigidbodyConstraints2D.FreezePositionY |
                RigidbodyConstraints2D.FreezeRotation;
            if (animator != null)
            {
                animator.speed = 0f;
                animator.enabled = false;
            }
            KeepInsideLayout();
            return;
        }

        rb.constraints &= ~(RigidbodyConstraints2D.FreezePositionX |
            RigidbodyConstraints2D.FreezePositionY);
        rb.constraints |= RigidbodyConstraints2D.FreezeRotation;
        if (animator != null)
            animator.enabled = true;

        Vector2 velocity = desiredVelocity;
        Vector2 next = rb.position + velocity * Time.fixedDeltaTime;
        if (!CanMoveTo(next))
        {
            Vector2 horizontal = rb.position + Vector2.right * velocity.x * Time.fixedDeltaTime;
            Vector2 vertical = rb.position + Vector2.up * velocity.y * Time.fixedDeltaTime;
            if (Mathf.Abs(velocity.x) > Mathf.Abs(velocity.y) && CanMoveTo(horizontal))
                velocity.y = 0f;
            else if (CanMoveTo(vertical))
                velocity.x = 0f;
            else if (CanMoveTo(horizontal))
                velocity.y = 0f;
            else
                velocity = Vector2.zero;
        }

        rb.velocity = velocity;
        UpdateMovementAnimation();
    }

    void Enter(State state)
    {
        if (CurrentState == state)
            return;

        CurrentState = state;
        desiredVelocity = Vector2.zero;
        InvalidatePath();
        if (state == State.Idle)
        {
            hasPersonallyDetectedThief = false;
            Detection = 0f;
        }

        if (animator != null)
        {
            animator.enabled = true;
            animator.speed = 1f;
        }
    }

    void BeginSearch(Vector2 center)
    {
        center = FindNearestWalkable(center);
        searchPoints.Clear();
        if (pathfinder != null)
        {
            List<Vector2> candidates = pathfinder.GetSearchPoints(
                rb.position, center, searchRadius, searchPointCount * 3, searchPhase);
            searchPoints.Add(center);
            for (int i = 0; i < candidates.Count && searchPoints.Count < searchPointCount; i++)
            {
                if (Vector2.Distance(candidates[i], center) < 0.75f)
                    continue;
                searchPoints.Add(candidates[i]);
            }
        }
        if (searchPoints.Count == 0)
            searchPoints.Add(FindNearestWalkable(rb.position));

        searchPointIndex = 0;
        searchTimerStarted = false;
        searchStartedAt = 0f;
        InvalidatePath();
        Enter(State.Search);
    }

    bool AtTargetOrClosestReachable(Vector2 destination)
    {
        if (Arrived(destination))
            return true;
        if (pathfinder == null || path.Count == 0 || pathIndex < path.Count)
            return false;

        Vector2 endpoint = path[path.Count - 1];
        return !pathfinder.AreInSameWalkableCell(endpoint, destination) &&
            Vector2.Distance(rb.position, endpoint) <= arriveDistance;
    }

    public void ReceiveSharedPosition(Vector2 position)
    {
        bool moved = !hasLastKnownPosition ||
            Vector2.Distance(lastKnownPosition, position) >= 0.2f;
        lastKnownPosition = position;
        hasLastKnownPosition = true;
        if (moved && isPrimaryPursuer && !HasVisualContact && CurrentState != State.Chase)
            Enter(State.Chase);
    }

    public bool IsPrimaryPursuer { get { return isPrimaryPursuer; } }
    public Vector2 AssignedSearchDestination { get { return assignedSearchDestination; } }

    public void ReceiveSquadAssignment(Vector2 position, bool primary, Vector2 searchDestination)
    {
        ReceiveSharedPosition(position);
        bool sameSecondaryArea = !isPrimaryPursuer && !primary &&
            Vector2.Distance(assignedSearchDestination, searchDestination) < 0.5f;
        bool wasPrimary = isPrimaryPursuer;
        isPrimaryPursuer = primary;

        if (primary)
        {
            if (CurrentState != State.Chase)
                Enter(State.Chase);
            return;
        }

        assignedSearchDestination = FindNearestWalkable(searchDestination);
        if (sameSecondaryArea && (CurrentState == State.Search || HasVisualContact))
            return;
        if (!wasPrimary && CurrentState == State.Chase && HasVisualContact)
            return;
        BeginSearch(assignedSearchDestination);
    }

    void InvalidatePath()
    {
        path.Clear();
        pathIndex = 0;
        nextPlan = 0f;
    }

    void MoveAlongPath(Vector2 destination, float speed)
    {
        destination = FindNearestWalkable(destination);
        Vector2 position = rb.position;

        if (Vector2.Distance(position, destination) <= arriveDistance)
        {
            desiredVelocity = Vector2.zero;
            return;
        }

        if (pathfinder == null)
        {
            desiredVelocity = Vector2.zero;
            return;
        }

        if (Time.time >= nextPlan ||
            Vector2.Distance(destination, lastPathTarget) >= replanTargetDistance)
        {
            path.Clear();
            path.AddRange(pathfinder.FindPath(position, destination));
            pathIndex = path.Count > 1 ? 1 : 0;
            lastPathTarget = destination;
            nextPlan = Time.time + Mathf.Max(0.1f, replanInterval);
        }

        if (path.Count == 0)
        {
            desiredVelocity = Vector2.zero;
            nextPlan = Time.time + 0.15f;
            return;
        }

        Vector2 waypoint = path[path.Count - 1];
        while (pathIndex < path.Count)
        {
            waypoint = path[pathIndex];
            if (Vector2.Distance(position, waypoint) <= waypointDistance)
                pathIndex++;
            else
                break;
        }
        if (pathIndex >= path.Count &&
            pathfinder.AreInSameWalkableCell(waypoint, destination))
            waypoint = destination;

        Vector2 delta = waypoint - position;
        if (delta.sqrMagnitude < 0.0025f)
        {
            desiredVelocity = Vector2.zero;
            return;
        }

        Vector2 routeDirection = delta.normalized;
        Vector2 direction = SelectSafeDirection(routeDirection, speed);
        if (direction.sqrMagnitude < 0.001f)
        {
            desiredVelocity = Vector2.zero;
            return;
        }
        FaceDirection(direction);
        desiredVelocity = direction * speed;
    }

    Vector2 SelectSafeDirection(Vector2 routeDirection, float speed)
    {
        Vector2 separation = Separation();
        Vector2 avoidance = AvoidObstacles(routeDirection);
        float step = speed * Time.fixedDeltaTime;
        float[] angles = { 0f, 25f, -25f, 50f, -50f, 80f, -80f, 110f, -110f, 180f };
        Vector2 best = Vector2.zero;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < angles.Length; i++)
        {
            Vector2 candidate = Quaternion.Euler(0f, 0f, angles[i]) * routeDirection;
            candidate = (candidate + separation * 0.65f + (avoidance - routeDirection) * 0.4f).normalized;
            Vector2 next = rb.position + candidate * step;
            if (!CanMoveTo(next) || !HasCopSpacing(next))
                continue;

            float score = Vector2.Dot(candidate, routeDirection) * 2f +
                Vector2.Dot(candidate, separation.normalized) *
                    Mathf.Clamp01(separation.magnitude) * 0.7f;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }
        return best;
    }

    Vector2 Separation()
    {
        Vector2 result = Vector2.zero;
        float radius = Mathf.Max(0.1f, separationRadius);
        for (int i = 0; i < allCops.Count; i++)
        {
            CopAI other = allCops[i];
            if (other == null || other == this)
                continue;

            Vector2 away = rb.position - other.rb.position;
            float distance = away.magnitude;
            if (distance >= radius)
                continue;

            if (distance < 0.001f)
            {
                float angle = (GetInstanceID() < other.GetInstanceID() ? 0f : 180f) +
                    searchPhase * 120f;
                away = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
            }
            else
            {
                away /= distance;
            }

            float urgency = 1f - distance / radius;
            result += away * urgency * urgency;
        }
        return result * separationStrength;
    }

    Vector2 AvoidObstacles(Vector2 direction)
    {
        Vector2 result = direction;
        float[] angles = { 0f, 35f, -35f };
        for (int i = 0; i < angles.Length; i++)
        {
            Vector2 feeler = Quaternion.Euler(0f, 0f, angles[i]) * direction;
            RaycastHit2D[] hits = Physics2D.CircleCastAll(
                rb.position, CopWorldRadius() + wallClearance, feeler,
                feelerLength, Physics2D.DefaultRaycastLayers);
            for (int j = 0; j < hits.Length; j++)
            {
                Collider2D hitCollider = hits[j].collider;
                Door door = hitCollider != null
                    ? hitCollider.GetComponentInParent<Door>()
                    : null;
                // Cops must contact closed doors so the existing Door logic
                // can open them. Other static obstacles steer the cop away.
                if (door == null && BlocksCopVision(hitCollider))
                {
                    result += hits[j].normal * avoidStrength *
                        (1f - hits[j].distance / Mathf.Max(0.1f, feelerLength));
                    break;
                }
            }
        }
        return result.sqrMagnitude < 0.01f ? direction : result.normalized;
    }

    bool CanMoveTo(Vector2 position)
    {
        if (museum == null || museum.Blocked == null)
            return false;

        int fromRow, fromCol, toRow, toCol;
        if (!WorldToCell(rb.position, out fromRow, out fromCol) ||
            !WorldToCell(position, out toRow, out toCol) ||
            pathfinder == null || pathfinder.IsBlockedCell(toRow, toCol))
            return false;

        int rowDelta = toRow - fromRow;
        int colDelta = toCol - fromCol;
        if (Mathf.Abs(rowDelta) > 1 || Mathf.Abs(colDelta) > 1)
            return false;
        if (rowDelta != 0 && colDelta != 0 &&
            (pathfinder.IsBlockedCell(fromRow + rowDelta, fromCol) ||
             pathfinder.IsBlockedCell(fromRow, fromCol + colDelta)))
            return false;
        return HasWallClearance(rb.position, position) && HasCopSpacing(position);
    }

    bool HasWallClearance(Vector2 from, Vector2 to)
    {
        float radius = CopWorldRadius() + Mathf.Max(0f, wallClearance);
        Vector2 movement = to - from;
        float distance = movement.magnitude;
        float startingPenetration = GridFootprintPenetration(from, radius);
        float previousPenetration = startingPenetration;
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(0.02f, radius * 0.5f)));
        for (int i = 1; i <= steps; i++)
        {
            Vector2 sample = Vector2.Lerp(from, to, (float)i / steps);
            float penetration = GridFootprintPenetration(sample, radius);
            if (startingPenetration <= 0.0001f && penetration > 0.0001f)
                return false;
            if (startingPenetration > 0.0001f &&
                penetration > previousPenetration + 0.0001f)
                return false;
            previousPenetration = penetration;
        }
        if (startingPenetration > 0.0001f &&
            previousPenetration >= startingPenetration - 0.0001f)
            return false;

        if (distance > 0.0001f)
        {
            Vector2 direction = movement / distance;
            RaycastHit2D[] sweepHits = Physics2D.CircleCastAll(
                from, radius, direction, distance, Physics2D.DefaultRaycastLayers);
            for (int i = 0; i < sweepHits.Length; i++)
            {
                Collider2D hit = sweepHits[i].collider;
                if (!BlocksCopMovement(hit))
                    continue;
                // A cast may report a wall already touching the cop. Allow
                // tangent or outward movement so it can slide clear safely.
                if (sweepHits[i].distance <= 0.001f &&
                    Vector2.Dot(direction, sweepHits[i].normal) >= -0.01f)
                    continue;
                return false;
            }
        }

        Collider2D[] overlaps = Physics2D.OverlapCircleAll(to, radius,
            Physics2D.DefaultRaycastLayers);
        for (int i = 0; i < overlaps.Length; i++)
        {
            if (BlocksCopMovement(overlaps[i]))
                return false;
        }
        return true;
    }

    float GridFootprintPenetration(Vector2 position, float radius)
    {
        int row, col;
        if (!WorldToCell(position, out row, out col) || pathfinder == null)
            return float.PositiveInfinity;

        float penetration = 0f;
        for (int r = row - 1; r <= row + 1; r++)
        {
            for (int c = col - 1; c <= col + 1; c++)
            {
                // Door cells are reported walkable by CopPathfinder so the
                // existing door-opening route remains available.
                if (!pathfinder.IsBlockedCell(r, c))
                    continue;

                Vector2 cellCenter = museum.CellToWorld(r, c);
                float nearestX = Mathf.Clamp(position.x, cellCenter.x - 0.5f,
                    cellCenter.x + 0.5f);
                float nearestY = Mathf.Clamp(position.y, cellCenter.y - 0.5f,
                    cellCenter.y + 0.5f);
                Vector2 nearestPoint = new Vector2(nearestX, nearestY);
                float overlap = radius - Vector2.Distance(position, nearestPoint);
                if (overlap > 0f)
                    penetration += overlap;
            }
        }
        return penetration;
    }

    bool BlocksCopMovement(Collider2D hit)
    {
        if (hit == null || hit.transform.root == transform.root || hit.isTrigger ||
            hit.GetComponentInParent<Door>() != null ||
            hit.GetComponentInParent<CopAI>() != null)
            return false;
        return BlocksCopVision(hit);
    }

    bool HasCopSpacing(Vector2 position)
    {
        float required = Mathf.Max(Mathf.Max(0f, minimumCopSpacing),
            CopWorldRadius() * 2f + wallClearance);
        if (required <= 0f)
            return true;
        for (int i = 0; i < allCops.Count; i++)
        {
            CopAI other = allCops[i];
            if (other == null || other == this || other.rb == null)
                continue;
            Vector2 offset = position - other.rb.position;
            float distance = offset.magnitude;
            if (distance >= required)
                continue;
            // Let a cop already inside the spacing radius move only when its
            // step increases the gap; this avoids trapping a pair in place.
            float currentDistance = Vector2.Distance(rb.position, other.rb.position);
            if (distance <= currentDistance + 0.001f)
                return false;
        }
        return true;
    }

    float CopWorldRadius()
    {
        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle != null)
            return circle.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y));
        Collider2D bodyCollider = GetComponent<Collider2D>();
        return bodyCollider != null ? bodyCollider.bounds.extents.magnitude : 0.1f;
    }

    bool WorldToCell(Vector2 position, out int row, out int col)
    {
        col = Mathf.FloorToInt(position.x + museum.Width * 0.5f);
        row = museum.Height - 1 - Mathf.FloorToInt(position.y + museum.Height * 0.5f);
        return row >= 0 && col >= 0 && row < museum.Height && col < museum.Width;
    }

    bool CanSeePlayer()
    {
        Vector2 delta = (Vector2)player.position - (Vector2)transform.position;
        float distance = delta.magnitude;
        if (distance <= 0.01f)
            return true;
        if (distance > viewRange ||
            Vector2.Angle(facingDirection, delta) > viewAngle * 0.5f)
            return false;
        if (thief != null && thief.IsCrouching)
        {
            float crouchRange = Mathf.Max(crouchCloseDetectionDistance,
                viewRange * crouchVisionRangeMultiplier);
            if (distance > crouchRange)
                return false;
        }

        RaycastHit2D[] hits = Physics2D.RaycastAll(
            transform.position, delta / distance, distance, Physics2D.DefaultRaycastLayers);
        for (int i = 0; i < hits.Length; i++)
        {
            if (BlocksCopVision(hits[i].collider) &&
                hits[i].distance < distance - 0.02f)
                return false;
        }
        return true;
    }

    static bool BlocksCopVision(Collider2D collider)
    {
        if (collider == null || collider.isTrigger)
            return false;

        Door door = collider.GetComponentInParent<Door>();
        if (door != null)
            return !door.IsOpen;

        if (collider.GetComponentInParent<ThiefController>() != null ||
            collider.GetComponentInParent<GuardAI>() != null ||
            collider.GetComponentInParent<CopAI>() != null)
            return false;

        return collider.attachedRigidbody == null;
    }

    bool HearsPlayer()
    {
        return thief != null && thief.CurrentNoiseRadius > 0f &&
            Vector2.Distance(transform.position, player.position) <=
            thief.CurrentNoiseRadius * hearingBonus;
    }

    float DetectionDrainMultiplier()
    {
        if (thief == null || !thief.IsCrouching || !IsThiefOnHidingSpot())
            return 1f;
        for (int i = 0; i < allCops.Count; i++)
            if (Vector2.Distance(player.position, allCops[i].transform.position) < hidingSafeRadius)
                return 1f;
        return hidingSpotDrainMultiplier;
    }

    bool IsThiefOnHidingSpot()
    {
        if (museum == null || museum.mapFile == null || player == null)
            return false;
        string[] lines = museum.mapFile.text.Replace("\r", "").Split(
            new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        int col = Mathf.FloorToInt(player.position.x + museum.Width * 0.5f);
        int row = museum.Height - 1 -
            Mathf.FloorToInt(player.position.y + museum.Height * 0.5f);
        return row >= 0 && col >= 0 && row < lines.Length && col < lines[row].Length &&
            lines[row][col] == 'H';
    }

    bool Arrived(Vector2 position)
    {
        return Vector2.Distance(rb.position, position) <= arriveDistance;
    }

    void FaceDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.001f)
            return;
        facingDirection = direction.normalized;
        if (cone != null)
        {
            float angle = Mathf.Atan2(facingDirection.y, facingDirection.x) * Mathf.Rad2Deg;
            cone.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    void UpdateMovementAnimation()
    {
        if (animator == null)
            return;
        if (desiredVelocity.sqrMagnitude < 0.01f)
        {
            animator.speed = 0f;
            return;
        }
        animator.speed = 1f;
        Vector2 direction = desiredVelocity.normalized;
        bool horizontal = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y);
        if (horizontal)
        {
            animator.SetFloat("MoveX", 1f);
            animator.SetFloat("MoveY", 0f);
            if (copSprite != null)
                copSprite.flipX = direction.x < 0f;
        }
        else
        {
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", direction.y >= 0f ? 1f : -1f);
            if (copSprite != null)
                copSprite.flipX = false;
        }
    }

    Vector2 FindNearestWalkable(Vector2 position)
    {
        if (museum == null || museum.Blocked == null || museum.Width <= 0 || museum.Height <= 0)
            return position;

        int col = Mathf.FloorToInt(position.x + museum.Width * 0.5f);
        int row = museum.Height - 1 - Mathf.FloorToInt(position.y + museum.Height * 0.5f);
        col = Mathf.Clamp(col, 0, museum.Width - 1);
        row = Mathf.Clamp(row, 0, museum.Height - 1);

        if (pathfinder == null || pathfinder.IsWalkable(museum.CellToWorld(row, col)))
            return ClampToCell(position, row, col);

        for (int radius = 1; radius < Mathf.Max(museum.Width, museum.Height); radius++)
        {
            for (int r = row - radius; r <= row + radius; r++)
            {
                for (int c = col - radius; c <= col + radius; c++)
                {
                    if (r < 0 || c < 0 || r >= museum.Height || c >= museum.Width ||
                        (Mathf.Abs(r - row) != radius && Mathf.Abs(c - col) != radius))
                        continue;
                    if (pathfinder == null || pathfinder.IsWalkable(museum.CellToWorld(r, c)))
                        return museum.CellToWorld(r, c);
                }
            }
        }
        return home;
    }

    Vector2 ClampToCell(Vector2 position, int row, int col)
    {
        Vector2 center = museum.CellToWorld(row, col);
        float margin = Mathf.Clamp(boundaryMargin, 0.01f, 0.45f);
        return new Vector2(
            Mathf.Clamp(position.x, center.x - 0.5f + margin, center.x + 0.5f - margin),
            Mathf.Clamp(position.y, center.y - 0.5f + margin, center.y + 0.5f - margin));
    }

    void SetPosition(Vector2 position)
    {
        rb.position = position;
        transform.position = position;
    }

    void KeepInsideLayout()
    {
        if (museum == null)
            return;
        Vector2 safe = FindNearestWalkable(rb.position);
        if (Vector2.Distance(rb.position, safe) > 0.05f)
            SetPosition(safe);
    }

    void FitCone()
    {
        if (cone == null || cone.sprite == null)
            return;
        Vector2 spriteSize = cone.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            return;
        float parentScaleX = Mathf.Max(0.0001f, transform.lossyScale.x);
        float parentScaleY = Mathf.Max(0.0001f, transform.lossyScale.y);
        float scaleX = viewRange / (spriteSize.x * parentScaleX);
        // CopSpawner's cone sprite has a 0.48 half-width-to-length ratio.
        // Scale its width independently so the visible cone matches viewAngle.
        float scaleY = scaleX * (parentScaleX / parentScaleY) *
            Mathf.Tan(viewAngle * 0.5f * Mathf.Deg2Rad) / 0.48f;
        // The camera is at negative Z, so a small negative offset draws the
        // filled cone in front of the floor while order 0 leaves walls above it.
        cone.transform.localPosition = new Vector3(0f, 0f, -1f);
        cone.transform.localRotation = Quaternion.identity;
        cone.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        FaceDirection(facingDirection);
    }

    void UpdateConeColor()
    {
        if (cone == null)
            return;
        float alpha = cone.color.a;
        Color tint = HasVisualContact && hasPersonallyDetectedThief
            ? new Color(1f, 0.18f, 0.12f, alpha)
            : new Color(1f, 0.9f, 0f, alpha);
        cone.color = tint;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = CurrentState == State.Chase ? Color.red :
            CurrentState == State.Search ? Color.yellow : Color.cyan;
        Vector3 position = transform.position;
        Gizmos.DrawLine(position, position + Quaternion.Euler(0f, 0f, viewAngle * 0.5f) *
            (Vector3)facingDirection * viewRange);
        Gizmos.DrawLine(position, position + Quaternion.Euler(0f, 0f, -viewAngle * 0.5f) *
            (Vector3)facingDirection * viewRange);
        Gizmos.color = Color.blue;
        for (int i = 0; i < path.Count - 1; i++)
            Gizmos.DrawLine(path[i], path[i + 1]);
    }
}
