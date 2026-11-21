using System.Collections.Generic;
using UnityEngine;

public class CopSquadManager : MonoBehaviour
{
    // Kept for serialized scene/API compatibility. Cop behavior no longer
    // assigns tactical roles; every registered cop receives the same alert.
    public enum Role
    {
        Pursuer,
        Flanker,
        Blocker,
        Searcher,
        ExitGuard,
        Patrol,
        Stationary
    }

    public static CopSquadManager Instance { get; private set; }
    public static Vector2 SharedLastKnownPosition { get; private set; }
    public static float SharedLastKnownTime { get; private set; }
    public static int AlarmLevel { get; private set; }

    [Header("Alarm and Arrival")]
    public float pollInterval = 0.25f;
    public float policeArrivalDelay = 8f;
    public int alarmLevelToDeploy = 1;
    public bool spawnCopsAtStartForTesting;

    [Header("Alert Memory")]
    public float memoryDuration = 18f;

    [Header("Coordinated Search")]
    [Min(1f)] public float secondarySearchRadius = 9f;
    [Min(1f)] public float searchTargetSpacing = 3.5f;
    [Min(3f)] public float reassignmentDistance = 3f;

    [Header("References")]
    public MuseumBuilder museum;
    public CopSpawner spawner;

    // Retained so existing scene values do not disappear if old scenes are
    // opened and resaved. These settings are no longer used.
    [HideInInspector] public float roleReassignInterval = 1f;
    [HideInInspector] public float flankLeadDistance = 4f;
    [HideInInspector] public int alarmLevelForExitGuard = 2;

    readonly List<CopAI> cops = new List<CopAI>();
    CopPathfinder pathfinder;
    Vector2 lastAssignedPosition;
    float lastAssignmentTime;
    bool hasAssignments;
    Transform player;
    Rigidbody2D playerBody;
    float nextPoll;
    float alarmRaisedAt;
    bool deployed;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        AlarmLevel = 0;
        SharedLastKnownPosition = Vector2.zero;
        SharedLastKnownTime = 0f;

        if (museum == null)
            museum = FindObjectOfType<MuseumBuilder>();
        if (spawner == null)
            spawner = FindObjectOfType<CopSpawner>();
    }

    void Start()
    {
        FindPlayer();
        pathfinder = new CopPathfinder(GetMuseum());
        if (spawnCopsAtStartForTesting)
        {
            deployed = true;
            if (spawner != null)
                spawner.SpawnCops();
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (Time.time >= nextPoll)
        {
            nextPoll = Time.time + Mathf.Max(0.05f, pollInterval);
            PollAlarmSources();

            if (AlarmLevel > 0 && !IsThiefPositionFresh())
                AlarmLevel = 0;

            if (!deployed && AlarmLevel >= alarmLevelToDeploy &&
                Time.time >= alarmRaisedAt + policeArrivalDelay)
                DeployPolice();
        }
    }

    void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
            return;
        player = playerObject.transform;
        playerBody = playerObject.GetComponent<Rigidbody2D>();
    }

    void PollAlarmSources()
    {
        GuardAI[] guards = FindObjectsOfType<GuardAI>();
        for (int i = 0; i < guards.Length; i++)
        {
            if (guards[i] != null && guards[i].HasVisualContact &&
                guards[i].CurrentState == GuardAI.State.Chase)
            {
                RaiseAlarm(guards[i].LastKnownPosition, 1);
                return;
            }
        }

        SecurityCameraAI[] cameras = FindObjectsOfType<SecurityCameraAI>();
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].HasVisualContact &&
                cameras[i].Detection >= 1f)
            {
                RaiseAlarm(cameras[i].LastSeenPosition, 1);
                return;
            }
        }
    }

    public static void RaiseAlarm(Vector2 position, int level)
    {
        RaiseAlarm(position, level, false);
    }

    public static void RaiseAlarm(Vector2 position, int level, bool forceCoordination)
    {
        if (Instance == null)
            return;

        bool activeAlert = AlarmLevel > 0 && Instance.IsThiefPositionFresh();
        if (AlarmLevel == 0 || level > AlarmLevel)
            Instance.alarmRaisedAt = Time.time;
        AlarmLevel = Mathf.Max(AlarmLevel, level);
        ShareThiefPosition(position);

        bool movedEnough = Instance.hasAssignments &&
            Vector2.Distance(position, Instance.lastAssignedPosition) >= Instance.reassignmentDistance &&
            Time.time - Instance.lastAssignmentTime >= 2f;
        bool copNeedsResponse = Instance.HasIdleOrReturningCop() &&
            Time.time - Instance.lastAssignmentTime >= 2f;
        if (forceCoordination || !activeAlert || !Instance.hasAssignments ||
            movedEnough || copNeedsResponse)
            Instance.AssignCoordinatedSearch(position);

        // A live shared update moves the main pursuer's target, while the two
        // secondary cops keep searching their distinct assigned areas.
        Instance.NotifyPositionOnly(position);
    }

    public static void ShareThiefPosition(Vector2 position)
    {
        bool memoryExpired = Instance != null && SharedLastKnownTime > 0f &&
            Time.time - SharedLastKnownTime > Instance.memoryDuration;
        bool notify = SharedLastKnownTime <= 0f || memoryExpired ||
            Vector2.Distance(SharedLastKnownPosition, position) >= 0.2f;
        SharedLastKnownPosition = position;
        SharedLastKnownTime = Time.time;

        if (Instance == null || !notify)
            return;

        for (int i = Instance.cops.Count - 1; i >= 0; i--)
        {
            CopAI cop = Instance.cops[i];
            if (cop == null)
                Instance.cops.RemoveAt(i);
            else
                cop.ReceiveSharedPosition(position);
        }
    }

    void DeployPolice()
    {
        deployed = true;
        if (GameManager.Instance != null)
            GameManager.Instance.Message("Police are arriving!");
        if (spawner != null)
            spawner.SpawnCops();
    }

    public void Register(CopAI cop)
    {
        if (cop == null)
            return;
        if (!cops.Contains(cop))
            cops.Add(cop);
        if (IsThiefPositionFresh())
            AssignCoordinatedSearch(SharedLastKnownPosition);
    }

    public void Unregister(CopAI cop)
    {
        cops.Remove(cop);
    }

    bool HasIdleOrReturningCop()
    {
        for (int i = 0; i < cops.Count; i++)
        {
            if (cops[i] != null &&
                (cops[i].CurrentState == CopAI.State.Idle ||
                 cops[i].CurrentState == CopAI.State.Return))
                return true;
        }
        return false;
    }

    void NotifyPositionOnly(Vector2 position)
    {
        for (int i = cops.Count - 1; i >= 0; i--)
        {
            CopAI cop = cops[i];
            if (cop == null)
                cops.RemoveAt(i);
            else
                cop.ReceiveSharedPosition(position);
        }
    }

    void AssignCoordinatedSearch(Vector2 position)
    {
        for (int i = cops.Count - 1; i >= 0; i--)
            if (cops[i] == null)
                cops.RemoveAt(i);
        if (cops.Count == 0)
            return;
        if (pathfinder == null)
            pathfinder = new CopPathfinder(GetMuseum());

        CopAI primary = null;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < cops.Count; i++)
        {
            float distance = Vector2.Distance(cops[i].transform.position, position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                primary = cops[i];
            }
        }

        List<Vector2> claimedTargets = new List<Vector2> { position };
        if (primary != null)
            primary.ReceiveSquadAssignment(position, true, position);

        for (int i = 0; i < cops.Count; i++)
        {
            CopAI cop = cops[i];
            if (cop == primary)
                continue;

            Vector2 destination = SelectDistinctReachableTarget(cop, position,
                claimedTargets, i + 1);
            claimedTargets.Add(destination);
            cop.ReceiveSquadAssignment(position, false, destination);
        }

        lastAssignedPosition = position;
        lastAssignmentTime = Time.time;
        hasAssignments = true;
    }

    Vector2 SelectDistinctReachableTarget(CopAI cop, Vector2 center,
        List<Vector2> claimed, int phase)
    {
        if (pathfinder == null)
            return center;

        List<Vector2> candidates = pathfinder.GetSearchPoints(
            cop.transform.position, center, secondarySearchRadius,
            Mathf.Max(24, cop.searchPointCount * 4), phase);
        if (candidates.Count == 0)
            return center;

        Vector2 best = candidates[0];
        float bestScore = float.MinValue;
        float bestSeparation = -1f;
        for (int i = 0; i < candidates.Count; i++)
        {
            float separation = float.MaxValue;
            for (int j = 0; j < claimed.Count; j++)
                separation = Mathf.Min(separation,
                    Vector2.Distance(candidates[i], claimed[j]));

            float travelCost = Vector2.Distance(cop.transform.position, candidates[i]);
            float score = separation - travelCost * 0.15f;
            if (separation >= searchTargetSpacing && score > bestScore)
            {
                best = candidates[i];
                bestScore = score;
            }
            if (separation > bestSeparation)
            {
                bestSeparation = separation;
                if (bestScore == float.MinValue)
                    best = candidates[i];
            }
        }
        return best;
    }

    public MuseumBuilder GetMuseum()
    {
        if (museum == null)
            museum = FindObjectOfType<MuseumBuilder>();
        return museum;
    }

    public Transform GetPlayer()
    {
        if (player == null)
            FindPlayer();
        return player;
    }

    public Rigidbody2D GetPlayerBody()
    {
        if (player == null)
            FindPlayer();
        return playerBody;
    }

    public bool IsThiefPositionFresh()
    {
        return SharedLastKnownTime > 0f &&
            Time.time - SharedLastKnownTime <= memoryDuration;
    }
}
