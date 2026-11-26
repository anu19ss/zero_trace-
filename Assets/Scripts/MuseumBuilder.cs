using System;
using System.Collections.Generic;
using UnityEngine;

public class MuseumBuilder : MonoBehaviour
{
    public TextAsset mapFile;
    public bool setupCamera = true;

  
    public Sprite floorSprite;
    public Sprite wallSprite;

    [Header("Key Sprites")]
    public Sprite keySprite1;
    public Sprite keySprite2;

    [Header("Loot Sprites")]
    public Sprite lootSprite;

    
    public GameObject doorPrefab1;
    public GameObject doorPrefab2;

   
    public GameObject thiefPrefab;
    public GameObject guardPrefab;
    public GameObject dogPrefab;
    public GameObject cameraPrefab;
    public bool spawnDogsFromMap;
    public float dogScale = 0.6f;   

    [Header("Dog Visuals (used when no dog prefab is assigned)")]
    public Sprite dogSprite;
    public RuntimeAnimatorController dogAnimatorController;
    public Vector2[] dogSpawnPositions;
    public Vector2[] dogPatrolPointA;
    public Vector2[] dogPatrolPointB;
    public GameObject scentMarkerPrefab;

    public int Width { get; private set; }
    public int Height { get; private set; }

   
    public bool[,] Blocked { get; private set; }

    Sprite square;
    readonly Dictionary<string, int> counters = new Dictionary<string, int>();

    void Awake()
    {
        Build();
    }

    public Vector3 CellToWorld(int row, int col)
    {
        return new Vector3(col - Width / 2f + 0.5f, (Height - 1 - row) - Height / 2f + 0.5f, 0f);
    }

    public bool IsBlocked(int row, int col)
    {
        if (row < 0 || col < 0 || row >= Height || col >= Width) return true;
        return Blocked[row, col];
    }

    void Build()
    {
        if (mapFile == null)
        {
            Debug.LogError("MuseumBuilder: assign the map file.");
            return;
        }

        string[] lines = mapFile.text.Replace("\r", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Height = lines.Length;
        Width = lines[0].Length;
        Blocked = new bool[Height, Width];
        square = MakeSquareSprite();

        Transform walls = NewGroup("Walls");
        Transform doors = NewGroup("Doors");
        Transform lasers = NewGroup("Lasers");
        Transform markers = NewGroup("Markers");
        Transform dogs = NewGroup("Dogs");
        List<Vector2Int> dogSpawnCells = new List<Vector2Int>();

        
        GameObject floorObj = Make("Floor", transform, Vector3.zero, new Vector2(Width, Height), new Color(0.18f, 0.18f, 0.22f), 0, false, false);
        ApplyTiled(floorObj, floorSprite, new Vector2(Width, Height));

        for (int r = 0; r < Height; r++)
        {
            int c = 0;
            while (c < Width)
            {
                char ch = lines[r][c];

                if (ch == '#')
                {
                    
                    int end = c;
                    while (end + 1 < Width && lines[r][end + 1] == '#') end++;
                    int len = end - c + 1;
                    for (int k = c; k <= end; k++) Blocked[r, k] = true;
                    Vector3 pos = CellToWorld(r, c) + new Vector3((len - 1) / 2f, 0f, 0f);
                    GameObject wallObj = Make("Wall_r" + r + "_c" + c, walls, pos, new Vector2(len, 1), new Color(0.33f, 0.35f, 0.4f), 1, true, false);
                    ApplyTiled(wallObj, wallSprite, new Vector2(len, 1));
                    c = end + 1;
                    continue;
                }

                Vector3 p = CellToWorld(r, c);
                switch (ch)
                {
                    case '1':
                    case '2':
                        {
                            Blocked[r, c] = true;
                            GameObject prefab = (ch == '1') ? doorPrefab1 : doorPrefab2;
                            GameObject doorObj;
                            if (prefab != null)
                            {
                                doorObj = Instantiate(prefab, doors);
                                doorObj.name = "KeycardDoor" + ch;
                                doorObj.transform.localPosition = p;
                            }
                            else
                            {
                                doorObj = Make("KeycardDoor" + ch, doors, p, Vector2.one, new Color(0.95f, 0.65f, 0.1f), 2, true, false);
                            }
                            Door doorScript = doorObj.GetComponent<Door>();
                            if (doorScript == null) doorScript = doorObj.AddComponent<Door>();
                            doorScript.number = (ch == '1') ? 1 : 2;
                            break;
                        }
                    case 'L':
                        {
                            bool horiz = IsLaser(lines, r, c - 1) || IsLaser(lines, r, c + 1);
                            bool vert = IsLaser(lines, r - 1, c) || IsLaser(lines, r + 1, c);
                            Color red = new Color(0.9f, 0.2f, 0.2f);
                            if (horiz || !vert) Make("Laser", lasers, p, new Vector2(1f, 0.25f), red, 2, true, true).AddComponent<Laser>();
                            if (vert) Make("Laser", lasers, p, new Vector2(0.25f, 1f), red, 2, true, true).AddComponent<Laser>();
                            break;
                        }
                    case 'S': Marker("ThiefStart", markers, p, new Color(0.2f, 0.8f, 0.4f), thiefPrefab); break;
                    case 'E': AddPickup(Marker("Exit", markers, p, new Color(0.2f, 0.8f, 0.4f)), Pickup.Kind.Exit); break;
                    case '$':
                        {
                            GameObject loot = Marker("Loot", markers, p,
                                new Color(0.98f, 0.82f, 0.25f));
                            ApplyPickupSprite(loot, lootSprite);
                            AddPickup(loot, Pickup.Kind.Loot);
                            break;
                        }
                    case 'H': Marker("HidingSpot", markers, p, new Color(0.5f, 0.5f, 0.55f)); break;
                    case 'C': Marker("Camera", markers, p, new Color(0.5f, 0.35f, 0.9f), cameraPrefab); break;
                    case 'G': Marker("Guard", markers, p, new Color(0.9f, 0.25f, 0.25f), guardPrefab); break;
                    case 'D': dogSpawnCells.Add(new Vector2Int(r, c)); break;
                    case 'K':
                        {
                            GameObject key = Marker("Keycard1", markers, p,
                                new Color(0.25f, 0.75f, 0.75f));
                            ApplyPickupSprite(key, keySprite1);
                            AddPickup(key, Pickup.Kind.Keycard1);
                            break;
                        }
                    case 'Q':
                        {
                            GameObject key = Marker("Keycard2", markers, p,
                                new Color(0.25f, 0.75f, 0.75f));
                            ApplyPickupSprite(key, keySprite2);
                            AddPickup(key, Pickup.Kind.Keycard2);
                            break;
                        }
                }
                c++;
            }
        }

        if (spawnDogsFromMap && FindObjectsOfType<DogAI>().Length == 0)
        {
            EnsureDogScentManager();

            bool hasConfiguredRoutes = dogSpawnPositions != null &&
                dogPatrolPointA != null && dogPatrolPointB != null &&
                dogSpawnPositions.Length > 0 &&
                dogSpawnPositions.Length == dogPatrolPointA.Length &&
                dogSpawnPositions.Length == dogPatrolPointB.Length;

            if (hasConfiguredRoutes)
            {
                for (int i = 0; i < dogSpawnPositions.Length; i++)
                    SpawnDog(dogSpawnPositions[i], i, dogs, dogPatrolPointA[i], dogPatrolPointB[i]);
            }
            else
            {
                for (int i = 0; i < dogSpawnCells.Count; i++)
                    SpawnDog(CellToWorld(dogSpawnCells[i].x, dogSpawnCells[i].y), i, dogs);
            }
        }

        if (setupCamera && Camera.main != null)
        {
            Camera cam = Camera.main;
            cam.orthographic = true;
            cam.orthographicSize = Height / 2f + 1f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }
    }

    void SpawnDog(Vector2 spawnPosition, int index, Transform parent, Vector2? configuredPointA = null, Vector2? configuredPointB = null)
    {
        GameObject dog;

        if (dogPrefab != null)
        {
            dog = Instantiate(dogPrefab, parent);
            dog.transform.localPosition = spawnPosition;
        }
        else
        {
            dog = new GameObject("GuardDog_" + (index + 1));
            dog.transform.SetParent(parent, false);
            dog.transform.localPosition = spawnPosition;
            dog.transform.localScale = Vector3.one;

            SpriteRenderer spriteRenderer = dog.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = dogSprite;
            spriteRenderer.sortingOrder = 10;

            Animator animator = dog.AddComponent<Animator>();
            animator.runtimeAnimatorController = dogAnimatorController;

            dog.AddComponent<DogPathfinding>();
            dog.AddComponent<DogPerception>().obstacleLayers = LayerMask.GetMask("Default");
            dog.AddComponent<DogScentTracker>();

            GameObject coneObject = new GameObject("VisionCone");
            coneObject.transform.SetParent(dog.transform, false);
            coneObject.AddComponent<MeshFilter>();
            coneObject.AddComponent<MeshRenderer>();
            DogVisionCone cone = coneObject.AddComponent<DogVisionCone>();
            cone.obstacleLayers = LayerMask.GetMask("Default");

            dog.AddComponent<DogAI>();
        }
        dog.transform.localScale = Vector3.one * dogScale;
        DogAI dogAI = dog.GetComponent<DogAI>();
        DogPathfinding pathfinding = dog.GetComponent<DogPathfinding>();
        if (dogAI == null || pathfinding == null)
        {
            Debug.LogError("MuseumBuilder: dog prefab requires DogAI and DogPathfinding components.", dog);
            return;
        }

        Vector2 pointA;
        Vector2 pointB;
        if (configuredPointA.HasValue && configuredPointB.HasValue)
        {
            pointA = configuredPointA.Value;
            pointB = configuredPointB.Value;
            if (pathfinding.FindPath(spawnPosition, pointA).Count == 0 ||
                pathfinding.FindPath(spawnPosition, pointB).Count == 0)
            {
                Debug.LogWarning("MuseumBuilder: configured dog patrol route is not walkable for " + dog.name + ".", dog);
                return;
            }
        }
        else
        {
            int row = Height - 1 - Mathf.FloorToInt(spawnPosition.y + Height / 2f);
            int col = Mathf.FloorToInt(spawnPosition.x + Width / 2f);
            if (!TryFindPatrolPoints(new Vector2Int(row, col), pathfinding, out pointA, out pointB))
            {
                Debug.LogWarning("MuseumBuilder: no reachable patrol points found for " + dog.name + ".", dog);
                return;
            }
        }

        dogAI.pointA = CreatePatrolPoint(parent, dog.name + "_PatrolA", pointA);
        dogAI.pointB = CreatePatrolPoint(parent, dog.name + "_PatrolB", pointB);
    }

    void EnsureDogScentManager()
    {
        if (FindObjectOfType<ScentManager>() != null)
            return;

        GameObject managerObject = new GameObject("ScentManager");
        managerObject.transform.SetParent(transform, false);
        ScentManager manager = managerObject.AddComponent<ScentManager>();
        manager.scentInterval = 0.5f;
        manager.scentLifetime = 8f;
        manager.runningScentStrength = 0.7f;
        manager.walkingScentStrength = 0.3f;
        manager.crouchingScentStrength = 0.05f;
        manager.scentMarkerPrefab = scentMarkerPrefab;
    }

    bool TryFindPatrolPoints(Vector2Int spawnCell, DogPathfinding pathfinding, out Vector2 pointA, out Vector2 pointB)
    {
        Vector2 origin = CellToWorld(spawnCell.x, spawnCell.y);
        pointA = origin;
        pointB = origin;
        float bestA = float.MaxValue;
        float bestB = float.MaxValue;
        bool foundA = false;
        bool foundB = false;

        for (int row = 0; row < Height; row++)
        {
            for (int col = 0; col < Width; col++)
            {
                if (IsBlocked(row, col)) continue;
                Vector2 candidate = CellToWorld(row, col);
                float distance = Vector2.Distance(origin, candidate);
                if (distance < 1.5f || pathfinding.FindPath(origin, candidate).Count == 0) continue;

                if (distance < bestA)
                {
                    pointA = candidate;
                    bestA = distance;
                    foundA = true;
                }
                if (distance >= 3f && distance < bestB)
                {
                    pointB = candidate;
                    bestB = distance;
                    foundB = true;
                }
            }
        }

        return foundA && foundB;
    }

    Transform CreatePatrolPoint(Transform parent, string pointName, Vector2 position)
    {
        GameObject point = new GameObject(pointName);
        point.transform.SetParent(parent, false);
        point.transform.localPosition = position;
        return point.transform;
    }

    
    static void ApplyTiled(GameObject go, Sprite sprite, Vector2 size)
    {
        if (sprite == null) return;
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = Color.white;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = size;
        go.transform.localScale = Vector3.one;

        BoxCollider2D box = go.GetComponent<BoxCollider2D>();
        if (box != null) box.size = size;
    }

    static bool IsLaser(string[] lines, int r, int c)
    {
        if (r < 0 || r >= lines.Length || c < 0 || c >= lines[r].Length) return false;
        return lines[r][c] == 'L';
    }

    Transform NewGroup(string groupName)
    {
        GameObject g = new GameObject(groupName);
        g.transform.SetParent(transform, false);
        return g.transform;
    }

    GameObject Marker(string baseName, Transform parent, Vector3 pos, Color color, GameObject prefab = null)
    {
        int n;
        counters.TryGetValue(baseName, out n);
        n++;
        counters[baseName] = n;
        string objName = baseName + "_" + n;

        if (prefab != null)
        {
            GameObject go = Instantiate(prefab, parent);
            go.name = objName;
            go.transform.localPosition = pos;
            return go;
        }
        return Make(objName, parent, pos, new Vector2(0.6f, 0.6f), color, 3, false, false);
    }

    
    void AddPickup(GameObject go, Pickup.Kind kind)
    {
        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.8f;   
        Pickup pickup = go.AddComponent<Pickup>();
        pickup.kind = kind;
        pickup.value = 100;
    }

    static void ApplyPickupSprite(GameObject pickup, Sprite sprite)
    {
        if (pickup == null || sprite == null)
            return;

        SpriteRenderer renderer = pickup.GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.sortingOrder = 4;
    }

    GameObject Make(string objName, Transform parent, Vector3 pos, Vector2 size, Color color, int order, bool collider, bool trigger)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = color;
        sr.sortingOrder = order;

        if (collider)
        {
            BoxCollider2D box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = trigger;
        }
        return go;
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
