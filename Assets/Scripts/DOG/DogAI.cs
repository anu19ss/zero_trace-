
using System.Collections.Generic;
using UnityEngine;

public class DogAI : MonoBehaviour
{
    public enum DogState
    {
        Patrol,
        Chase,
        InvestigateSound,
        TrackScent
    }

    [Header("Current State")]
    public DogState currentState = DogState.Patrol;

    [Header("Patrol")]
    public Transform pointA;
    public Transform pointB;
    public float patrolSpeed = 2f;

    [Header("Detection Movement")]
    public float chaseSpeed = 3.5f;
    public float investigationSpeed = 2.5f;
    public float trackingSpeed = 2.5f;

    [Header("Sound Investigation")]
    public float soundMemoryDuration = 5f;
    public float arrivalDistance = 0.2f;

    [Header("Pathfinding")]
    public float pathUpdateInterval = 0.4f;
    public float waypointDistance = 0.15f;

    [Header("Catch Settings")]
    public float catchDistance = 0.8f;

    private bool hasCaughtThief = false;

    private Transform patrolTarget;
    private DogPerception perception;
    private DogScentTracker scentTracker;
    private DogPathfinding pathfinding;
    private Animator animator;

    private Vector2 lastHeardPosition;
    private float soundMemoryTimer;

    private List<Vector2> currentPath = new List<Vector2>();
    private int currentWaypoint;
    private float pathUpdateTimer;
    private Vector2 lastDestination;
    private bool hasPath;

    private int lastDirection = 0;

    void Start()
    {
        perception = GetComponent<DogPerception>();
        scentTracker = GetComponent<DogScentTracker>();
        pathfinding = GetComponent<DogPathfinding>();
        animator = GetComponent<Animator>();

        patrolTarget = pointA;

        if (perception == null)
            Debug.LogError("DogAI: DogPerception is missing!");

        if (scentTracker == null)
            Debug.LogError("DogAI: DogScentTracker is missing!");

        if (pathfinding == null)
            Debug.LogError("DogAI: DogPathfinding is missing!");

        if (animator == null)
            Debug.LogError("DogAI: Animator is missing!");

        if (pointA == null || pointB == null)
            Debug.LogError("DogAI: Assign both patrol points!");

        // Start with the downward-facing animation.
        if (animator != null)
            animator.SetInteger("Direction", lastDirection);
    }

    void Update()
    {
        if (perception == null || scentTracker == null ||
            pathfinding == null)
            return;

        if (hasCaughtThief)
            return;

        float distanceToThief = Vector2.Distance(
            transform.position,
            perception.ThiefPosition
        );

        if (distanceToThief <= catchDistance)
        {
            hasCaughtThief = true;

            Debug.Log("DOG CAUGHT THE THIEF!");

            if (GameManager.Instance != null)
            {
                GameManager.Instance.Caught();
            }
            else
            {
                Debug.LogError("DogAI: GameManager instance not found!");
            }

            return;
        }

        if (perception.CanHearThief)
        {
            lastHeardPosition = perception.LastHeardPosition;
            soundMemoryTimer = soundMemoryDuration;
        }
        else
        {
            soundMemoryTimer -= Time.deltaTime;
        }

        if (perception.CanSeeThief)
        {
            currentState = DogState.Chase;
        }
        else if (soundMemoryTimer > 0f)
        {
            currentState = DogState.InvestigateSound;
        }
        else if (scentTracker.HasFoundScent)
        {
            currentState = DogState.TrackScent;
        }
        else
        {
            currentState = DogState.Patrol;
        }

        switch (currentState)
        {
            case DogState.Patrol:
                Patrol();
                break;

            case DogState.Chase:
                ChaseThief();
                break;

            case DogState.InvestigateSound:
                InvestigateSound();
                break;

            case DogState.TrackScent:
                TrackScent();
                break;
        }
    }

    void Patrol()
    {
        if (pointA == null || pointB == null)
            return;

        if (patrolTarget == null)
            patrolTarget = pointA;

        bool reachedTarget = FollowDestination(
            patrolTarget.position,
            patrolSpeed
        );

        if (reachedTarget)
        {
            patrolTarget = patrolTarget == pointA ? pointB : pointA;
            ClearPath();
        }
    }

    void ChaseThief()
    {
        FollowDestination(
            perception.ThiefPosition,
            chaseSpeed
        );
    }

    void InvestigateSound()
    {
        bool reachedTarget = FollowDestination(
            lastHeardPosition,
            investigationSpeed
        );

        if (reachedTarget)
        {
            soundMemoryTimer = 0f;
            ClearPath();
        }
    }

    void TrackScent()
    {
        Vector2 direction = scentTracker.ScentDirection;

        if (direction.sqrMagnitude > 0.001f)
        {
            Vector2 scentTarget =
                (Vector2)transform.position + direction * 2f;

            FollowDestination(
                scentTarget,
                trackingSpeed
            );
        }
    }

    bool FollowDestination(Vector2 destination, float speed)
    {
        pathUpdateTimer -= Time.deltaTime;

        bool destinationChanged =
            Vector2.Distance(destination, lastDestination) > 0.5f;

        if (!hasPath || pathUpdateTimer <= 0f || destinationChanged)
        {
            currentPath = pathfinding.FindPath(
                transform.position,
                destination
            );

            currentWaypoint = currentPath.Count > 1 ? 1 : 0;

            lastDestination = destination;
            pathUpdateTimer = pathUpdateInterval;
            hasPath = currentPath.Count > 0;
        }

        if (!hasPath)
            return false;

        if (currentWaypoint >= currentPath.Count)
            return true;

        Vector2 waypoint = currentPath[currentWaypoint];

        Vector2 previousPosition = transform.position;

        Vector2 nextPosition = Vector2.MoveTowards(
            previousPosition,
            waypoint,
            speed * Time.deltaTime
        );

        transform.position = nextPosition;

        UpdateMovementAnimation(nextPosition - previousPosition);

        if (Vector2.Distance(transform.position, waypoint)
            < waypointDistance)
        {
            currentWaypoint++;
        }

        return currentWaypoint >= currentPath.Count;
    }

    void UpdateMovementAnimation(Vector2 movement)
    {
        if (animator == null || movement.sqrMagnitude < 0.000001f)
            return;

        // Choose the dominant movement direction.
        if (Mathf.Abs(movement.x) > Mathf.Abs(movement.y))
        {
            lastDirection = movement.x < 0f ? 1 : 2;
        }
        else
        {
            lastDirection = movement.y < 0f ? 0 : 3;
        }

        animator.SetInteger("Direction", lastDirection);
    }

    void ClearPath()
    {
        currentPath.Clear();
        currentWaypoint = 0;
        hasPath = false;
        pathUpdateTimer = 0f;
    }
}
