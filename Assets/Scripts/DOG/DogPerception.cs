
using UnityEngine;

public class DogPerception : MonoBehaviour
{
    [Header("Detection Settings")]
    public float visionRange = 3f;
    public float hearingMultiplier = 1f;

    [Header("Hearing Obstacles")]
    public LayerMask obstacleLayers;

    private Transform thief;
    private ThiefController thiefController;
    private DogVisionCone visionCone;

    public bool CanSeeThief { get; private set; }
    public bool CanHearThief { get; private set; }

    public Vector2 ThiefPosition =>
        thief != null ? (Vector2)thief.position : Vector2.zero;

    public Vector2 LastHeardPosition { get; private set; }

    private bool previousVision;
    private bool previousHearing;

    void Start()
    {
        GameObject player =
            GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            thief = player.transform;
            thiefController = player.GetComponent<ThiefController>();
        }
        else
        {
            Debug.LogError("DogPerception: Player not found!");
        }

        if (player != null && thiefController == null)
        {
            Debug.LogError("DogPerception: ThiefController missing!");
        }

        visionCone = GetComponentInChildren<DogVisionCone>();

        if (visionCone == null)
        {
            Debug.LogError(
                "DogPerception: DogVisionCone missing on this dog!"
            );
        }
    }

    void Update()
    {
        if (thief == null || thiefController == null ||
            visionCone == null)
            return;

        CanSeeThief = CheckVision();
        CanHearThief = CheckHearing();

        if (CanHearThief)
        {
            LastHeardPosition = thief.position;
        }

        if (CanSeeThief && !previousVision)
            Debug.Log("Dog detected thief through vision!");

        if (CanHearThief && !previousHearing)
            Debug.Log("Dog heard the thief!");

        previousVision = CanSeeThief;
        previousHearing = CanHearThief;
    }

    bool CheckVision()
    {
        Vector2 origin = transform.position;
        Vector2 targetPosition = thief.position;

        Vector2 direction = targetPosition - origin;
        float distance = direction.magnitude;

        if (distance > visionRange || distance < 0.001f)
            return false;

        float angle = Vector2.Angle(
            visionCone.FacingDirection,
            direction.normalized
        );

        if (angle > visionCone.visionAngle / 2f)
            return false;

        RaycastHit2D[] hits = Physics2D.RaycastAll(
            origin,
            direction.normalized,
            distance,
            visionCone.obstacleLayers
        );

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null)
                continue;

            Transform hitTransform = hit.collider.transform;

            if (hitTransform == thief ||
                hitTransform.IsChildOf(thief) ||
                hitTransform == transform ||
                hitTransform.IsChildOf(transform))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    bool CheckHearing()
    {
        float distance = Vector2.Distance(
            transform.position,
            thief.position
        );

        float hearingRange =
            thiefController.CurrentNoiseRadius * hearingMultiplier;

        if (hearingRange <= 0f || distance > hearingRange)
            return false;

        Vector2 direction =
            ((Vector2)thief.position - (Vector2)transform.position)
            .normalized;

        RaycastHit2D[] hits = Physics2D.RaycastAll(
            transform.position,
            direction,
            distance,
            obstacleLayers
        );

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null)
                continue;

            Transform hitTransform = hit.collider.transform;

            if (hitTransform == thief ||
                hitTransform.IsChildOf(thief) ||
                hitTransform == transform ||
                hitTransform.IsChildOf(transform))
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
