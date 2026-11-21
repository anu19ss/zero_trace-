
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class DogVisionCone : MonoBehaviour
{
    [Header("Vision Settings")]
    public float visionRange = 3f;
    [Range(10f, 120f)]
    public float visionAngle = 45f;

    [Range(10, 100)]
    public int rayCount = 40;

    [Header("Wall Detection")]
    public LayerMask obstacleLayers;

    [Header("Visual Settings")]
    public Color coneColor = new Color(1f, 1f, 0f, 0.25f);

    private Mesh visionMesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    private Vector2 facingDirection = Vector2.right;
    public Vector2 FacingDirection => facingDirection;
    private Vector3 previousPosition;

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        visionMesh = new Mesh();
        visionMesh.name = "Dog Vision Cone";
        meshFilter.mesh = visionMesh;

        Material material = new Material(
            Shader.Find("Sprites/Default")
        );

        material.color = coneColor;
        meshRenderer.material = material;
        meshRenderer.sortingOrder = 5;

        previousPosition = transform.position;

        UpdateVisionMesh();
    }

    void LateUpdate()
    {
        UpdateFacingDirection();
        UpdateVisionMesh();
    }

    void UpdateFacingDirection()
    {
        Vector2 movement =
            (Vector2)transform.position -
            (Vector2)previousPosition;

        if (movement.sqrMagnitude > 0.0001f)
        {
            facingDirection = movement.normalized;

            float angle = Mathf.Atan2(
                facingDirection.y,
                facingDirection.x
            ) * Mathf.Rad2Deg;

            transform.rotation =
                Quaternion.Euler(0f, 0f, angle);
        }

        previousPosition = transform.position;
    }

    void UpdateVisionMesh()
    {
        Vector3[] vertices = new Vector3[rayCount + 2];
        int[] triangles = new int[rayCount * 3];

        vertices[0] = Vector3.zero;

        float startAngle = -visionAngle / 2f;
        float angleStep = visionAngle / rayCount;

        for (int i = 0; i <= rayCount; i++)
        {
            float angle = startAngle + angleStep * i;

            Vector2 localDirection = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            );

            Vector2 worldDirection =
                transform.TransformDirection(localDirection);

            RaycastHit2D hit = Physics2D.Raycast(
                transform.position,
                worldDirection,
                visionRange,
                obstacleLayers
            );

            float distance = hit.collider != null
                ? hit.distance
                : visionRange;

            Vector3 worldPoint =
                transform.position +
                (Vector3)(worldDirection * distance);

            vertices[i + 1] =
                transform.InverseTransformPoint(worldPoint);
        }

        for (int i = 0; i < rayCount; i++)
        {
            int triangleIndex = i * 3;

            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i + 1;
            triangles[triangleIndex + 2] = i + 2;
        }

        visionMesh.Clear();
        visionMesh.vertices = vertices;
        visionMesh.triangles = triangles;
        visionMesh.RecalculateBounds();
    }

    void OnDestroy()
    {
        if (visionMesh != null)
            Destroy(visionMesh);
    }
}
