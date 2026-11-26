using UnityEngine;

// Security camera AI: sweeps left and right, and alerts the nearest guards when it spots the thief.
// Put this on the SecurityCamera prefab. The old sprite cone (first child) is hidden;
// a wall-clipped mesh cone is built at runtime instead, so it never leaks outside the layout.
public class SecurityCameraAI : MonoBehaviour
{
    [Header("Vision")]
    public float viewRange = 5f;
    [Range(10f, 180f)] public float viewAngle = 40f;
    public float detectTime = 0.8f;
    public float detectDecay = 0.5f;

    [Header("Sweep")]
    public float sweepAngle = 30f;      // swings this many degrees either side
    public float sweepSpeed = 0.7f;

    [Header("Limits (override old prefab values)")]
    public float maxViewRange = 5f;
    public float maxViewAngle = 40f;
    public float maxSweepAngle = 30f;

    [Header("Placement")]
    public float wallInset = 0.3f;      // pushes the camera against the wall it sits next to

    [Header("Cone look")]
    public int coneRays = 24;
    public float coneAlpha = 0.4f;

    [Header("Alarm")]
    public int guardsToAlert = 2;
    public float alertCooldown = 5f;

    public float Detection { get; private set; }
    public bool HasVisualContact { get; private set; }
    public Vector2 LastSeenPosition { get; private set; }

    Transform player;
    ThiefController thief;
    float baseAngle;
    float phase;
    float nextAlertTime;

    Mesh coneMesh;
    Material coneMat;

    void Start()
    {
        // The prefab stores older, bigger values; clamp them here.
        viewRange = Mathf.Min(viewRange, maxViewRange);
        viewAngle = Mathf.Min(viewAngle, maxViewAngle);
        sweepAngle = Mathf.Min(sweepAngle, maxSweepAngle);

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            thief = p.GetComponent<ThiefController>();
        }

        // Hide the old sprite cone (first child) and build the mesh cone.
        if (transform.childCount > 0)
        {
            SpriteRenderer old = transform.GetChild(0).GetComponent<SpriteRenderer>();
            if (old != null) old.enabled = false;
        }
        BuildConeObject();

        PlaceOnWall();
        phase = transform.position.x + transform.position.y;   // so cameras do not all swing together
    }

    // Looks at the neighbouring cells, finds the walls, faces away from them
    // (snapped to 45 degrees, so corner cameras look diagonally into the room)
    // and slides the camera a little towards the wall.
    void PlaceOnWall()
    {
        Vector2 away = Vector2.zero;
        Vector2 nearestWall = Vector2.zero;
        MuseumBuilder mb = FindObjectOfType<MuseumBuilder>();

        if (mb != null)
        {
            int col = Mathf.FloorToInt(transform.position.x + mb.Width / 2f);
            int row = mb.Height - 1 - Mathf.FloorToInt(transform.position.y + mb.Height / 2f);
            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0) continue;
                    if (!mb.IsBlocked(row + dr, col + dc)) continue;
                    Vector2 toWall = new Vector2(dc, -dr);   // row grows downward, world y grows upward
                    away -= toWall;
                    if (dr == 0 || dc == 0) nearestWall += toWall;   // only side-on walls for the inset
                }
            }
        }

        if (away.sqrMagnitude < 0.01f)   // no walls around: face the middle of the museum
            away = (Vector2)transform.root.position - (Vector2)transform.position;

        float a = Mathf.Atan2(away.y, away.x) * Mathf.Rad2Deg;
        baseAngle = Mathf.Round(a / 45f) * 45f;

        if (nearestWall.sqrMagnitude > 0.01f)
            transform.position += (Vector3)(nearestWall.normalized * wallInset);
    }

    void BuildConeObject()
    {
        GameObject go = new GameObject("ConeMesh");
        go.transform.SetParent(transform, false);
        MeshFilter mf = go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();

        coneMesh = new Mesh();
        coneMesh.name = "Camera Cone";
        mf.mesh = coneMesh;

        coneMat = new Material(Shader.Find("Sprites/Default"));
        mr.material = coneMat;
        mr.sortingOrder = 5;
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

        Color c = Color.Lerp(new Color(1f, 0.9f, 0.2f), new Color(1f, 0.15f, 0.15f), Detection);
        c.a = coneAlpha;
        if (coneMat != null) coneMat.color = c;
    }

    void LateUpdate()
    {
        UpdateConeMesh();
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

        RaycastHit2D[] hits = Physics2D.RaycastAll(eye, toPlayer.normalized, dist);
        foreach (RaycastHit2D h in hits)
            if (GuardAI.IsWall(h.collider)) return false;
        return true;
    }

    // Fan of rays: each one stops at the first wall, so the cone can never poke through one.
    void UpdateConeMesh()
    {
        if (coneMesh == null) return;

        int n = Mathf.Max(4, coneRays);
        Vector3[] verts = new Vector3[n + 2];
        int[] tris = new int[n * 3];
        verts[0] = Vector3.zero;

        Vector2 origin = transform.position;
        float start = -viewAngle * 0.5f;
        float step = viewAngle / n;

        for (int i = 0; i <= n; i++)
        {
            float a = start + step * i;
            Vector2 dir = transform.TransformDirection(new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)));

            float dist = viewRange;
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dir, viewRange);
            foreach (RaycastHit2D h in hits)   // sorted nearest first
            {
                if (GuardAI.IsWall(h.collider)) { dist = h.distance; break; }
            }
            verts[i + 1] = transform.InverseTransformPoint(origin + dir * dist);
        }

        for (int i = 0; i < n; i++)
        {
            tris[i * 3] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = i + 2;
        }

        coneMesh.Clear();
        coneMesh.vertices = verts;
        coneMesh.triangles = tris;
        coneMesh.RecalculateBounds();
    }

    void OnDestroy()
    {
        if (coneMesh != null) Destroy(coneMesh);
        if (coneMat != null) Destroy(coneMat);
    }
}