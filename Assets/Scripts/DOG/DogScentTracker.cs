
using UnityEngine;

public class DogScentTracker : MonoBehaviour
{
    [Header("Scent Detection")]
    public float scentDetectionRange = 2.5f;
    public float minimumScentStrength = 0.3f;

    [Header("Wall Detection")]
    public LayerMask obstacleLayers;

    public bool HasFoundScent { get; private set; }

    public Vector2 ScentDirection { get; private set; }

    private ScentManager scentManager;

    void Start()
    {
        scentManager = ScentManager.Instance;

        if (scentManager == null)
        {
            Debug.LogError(
                "DogScentTracker: ScentManager not found!"
            );
        }
    }

    void Update()
    {
        if (scentManager == null)
            return;

        ScentTrail strongestScent =
            scentManager.GetStrongestScent(transform.position);

        if (strongestScent == null)
        {
            ResetScentDetection();
            return;
        }

        Vector2 scentPosition = strongestScent.Position;

        Vector2 origin = transform.position;
        Vector2 direction = scentPosition - origin;

        float distance = direction.magnitude;

        if (distance > scentDetectionRange ||
            strongestScent.Strength < minimumScentStrength ||
            distance < 0.001f)
        {
            ResetScentDetection();
            return;
        }

        // Check whether a wall or locked door blocks the scent.
        RaycastHit2D[] hits = Physics2D.RaycastAll(
            origin,
            direction.normalized,
            distance,
            obstacleLayers
        );

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null)
                continue;

            Transform hitTransform = hit.collider.transform;

            // Ignore the dog's own colliders.
            if (hitTransform == transform ||
                hitTransform.IsChildOf(transform))
            {
                continue;
            }

            // An obstacle blocks scent detection.
            ResetScentDetection();
            return;
        }

        HasFoundScent = true;
        ScentDirection = direction.normalized;
    }

    void ResetScentDetection()
    {
        HasFoundScent = false;
        ScentDirection = Vector2.zero;
    }
}
