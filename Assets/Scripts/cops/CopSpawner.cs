using System.Collections.Generic;
using UnityEngine;

public class CopSpawner : MonoBehaviour
{
    [Header("Map and Prefab")]
    public TextAsset copMapFile;
    public GameObject copPrefab;
    public MuseumBuilder museum;
    public bool spawnAtStart;
    public int fallbackCopCount = 3;

    [Header("Spawn Appearance")]
    public Color copColor = new Color(0.12f, 0.32f, 0.95f, 1f);
    public float copScale = 0.62f;
    public float coneAlpha = 0.75f;

    readonly List<GameObject> spawned = new List<GameObject>();
    Sprite square;
    Sprite coneSprite;
    bool hasSpawned;

    void Awake()
    {
        if (museum == null) museum = FindObjectOfType<MuseumBuilder>();
        if (copMapFile == null && museum != null) copMapFile = museum.mapFile;
        square = MakeSquareSprite();
        coneSprite = MakeConeSprite();
    }

    void Start() { if (spawnAtStart) SpawnCops(); }

    public void SpawnCops()
    {
        if (hasSpawned) return;
        List<Vector2> points = ReadPolicePoints();
        int desiredCount = Mathf.Max(1, fallbackCopCount);
        if (points.Count > desiredCount)
            points.RemoveRange(desiredCount, points.Count - desiredCount);

        while (points.Count < desiredCount && museum != null &&
               museum.Blocked != null && museum.Width > 0 && museum.Height > 0)
        {
            Vector2 spawnPoint;
            if (!TryFindSpawnPoint(points, points.Count, out spawnPoint))
                break;
            points.Add(spawnPoint);
        }

        if (points.Count == 0)
        {
            Debug.LogError("CopSpawner: no valid walkable cop spawn positions were found.", this);
            return;
        }

        hasSpawned = true;
        for (int i = 0; i < points.Count; i++)
        {
            GameObject cop;
            if (copPrefab != null) cop = Instantiate(copPrefab, points[i], Quaternion.identity);
            else cop = CreatePlaceholder(points[i]);
            cop.name = "Cop_" + (i + 1);
            if (cop.GetComponent<Rigidbody2D>() == null) cop.AddComponent<Rigidbody2D>();
            Rigidbody2D body = cop.GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            if (cop.GetComponent<CircleCollider2D>() == null) cop.AddComponent<CircleCollider2D>();
            if (cop.GetComponent<CopAI>() == null) cop.AddComponent<CopAI>();
            EnsureCone(cop.transform);
            spawned.Add(cop);
        }
    }

    bool TryFindSpawnPoint(List<Vector2> existing, int index, out Vector2 result)
    {
        result = transform.position;
        if (museum == null || museum.Blocked == null || museum.Width <= 0 || museum.Height <= 0)
            return false;

        int preferredRows = index == 0 ? 1 :
            index == 1 ? museum.Height - 2 : museum.Height / 2;
        preferredRows = Mathf.Clamp(preferredRows, 0, museum.Height - 1);
        int preferredCol = Mathf.Clamp(museum.Width - 3, 0, museum.Width - 1);
        float bestScore = float.MaxValue;
        bool found = false;

        for (int row = 0; row < museum.Height; row++)
        {
            for (int col = 0; col < museum.Width; col++)
            {
                if (museum.IsBlocked(row, col))
                    continue;

                Vector2 candidate = museum.CellToWorld(row, col);
                float score = Mathf.Abs(row - preferredRows) +
                    Mathf.Abs(col - preferredCol) * 2f;

                for (int i = 0; i < existing.Count; i++)
                {
                    float distance = Vector2.Distance(candidate, existing[i]);
                    if (distance < 2f)
                        score += 100f + (2f - distance) * 10f;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    result = candidate;
                    found = true;
                }
            }
        }

        return found;
    }

    List<Vector2> ReadPolicePoints()
    {
        List<Vector2> result = new List<Vector2>();
        if (copMapFile == null || museum == null) return result;
        string[] lines = copMapFile.text.Replace("\r", "").Split('\n');
        for (int r = 0; r < lines.Length && r < museum.Height; r++)
            for (int c = 0; c < lines[r].Length && c < museum.Width; c++)
                if (lines[r][c] == 'P' && !museum.IsBlocked(r, c))
                    result.Add(museum.CellToWorld(r, c));
        return result;
    }

    GameObject CreatePlaceholder(Vector2 pos)
    {
        GameObject go = new GameObject("Cop");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * copScale;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = copColor;
        sr.sortingOrder = 4;
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.45f;
        return go;
    }

    void EnsureCone(Transform parent)
    {
        SpriteRenderer sr = null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == "VisionCone")
            {
                sr = child.GetComponent<SpriteRenderer>();
                if (sr == null)
                    sr = child.gameObject.AddComponent<SpriteRenderer>();
                break;
            }
        }

        if (sr == null)
        {
            GameObject cone = new GameObject("VisionCone");
            cone.transform.SetParent(parent, false);
            sr = cone.AddComponent<SpriteRenderer>();
        }
        sr.transform.SetAsFirstSibling();
        sr.enabled = true;
        sr.sprite = coneSprite;
        sr.color = new Color(1f, 0.9f, 0f, coneAlpha);
        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null && sr.sharedMaterial == null)
            sr.sharedMaterial = new Material(spriteShader);
        sr.sortingLayerID = 0;
        // The cop sets a small negative local Z offset at runtime, bringing
        // this order-0 sprite in front of the floor (also order 0). Wall art
        // (order 1), cop sprites and UI remain above it.
        sr.sortingOrder = 0;
        sr.transform.localRotation = Quaternion.identity;
        sr.transform.localScale = Vector3.one;
    }

    static Sprite MakeConeSprite()
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float along = (x + 0.5f) / size;
                float halfWidth = (x + 0.5f) * 0.48f;
                float edgeDistance = halfWidth - Mathf.Abs(y + 0.5f - size * 0.5f);
                float edgeAlpha = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01((edgeDistance + 2f) / 4f));
                float tipAlpha = 1f - Mathf.SmoothStep(0.92f, 1f, along);
                float alpha = edgeAlpha * tipAlpha;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0f, 0.5f), 64f);
    }

    static Sprite MakeSquareSprite()
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.filterMode = FilterMode.Point;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}
