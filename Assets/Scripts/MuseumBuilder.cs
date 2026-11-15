using System;
using System.Collections.Generic;
using UnityEngine;

// Builds a blockout museum from a text map (museum_map.txt) at runtime.
// Legend:  # wall   . floor   1 keycard door 1   2 keycard door 2   L laser
//          E exit   S thief start   $ loot   H hiding spot   C camera
//          G guard spawn   D dog spawn   K keycard 1 pickup   Q keycard 2 pickup
// Row 0 is the top of the map. 1 grid cell = 1 Unity unit.
public class MuseumBuilder : MonoBehaviour
{
    public TextAsset mapFile;
    public bool setupCamera = true;

    // Optional art. If empty, plain coloured blocks are used.
    // Use a seamless tile (for example 16x16 or 32x32 px) with Pixels Per Unit set so one tile = 1 unit.
    public Sprite floorSprite;
    public Sprite wallSprite;

    // Optional. If empty, a plain orange square is drawn instead.
    public GameObject doorPrefab1;
    public GameObject doorPrefab2;

    // Optional. If empty, a coloured square marker is drawn instead.
    public GameObject thiefPrefab;
    public GameObject guardPrefab;
    public GameObject dogPrefab;
    public GameObject cameraPrefab;

    public int Width { get; private set; }
    public int Height { get; private set; }

    // true = wall or locked door. Use this for the A* grid.
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

        // Floor
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
                    // merge a horizontal run of walls into one object
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
                    case '$': AddPickup(Marker("Loot", markers, p, new Color(0.98f, 0.82f, 0.25f)), Pickup.Kind.Loot); break;
                    case 'H': Marker("HidingSpot", markers, p, new Color(0.5f, 0.5f, 0.55f)); break;
                    case 'C': Marker("Camera", markers, p, new Color(0.5f, 0.35f, 0.9f), cameraPrefab); break;
                    case 'G': Marker("Guard", markers, p, new Color(0.9f, 0.25f, 0.25f), guardPrefab); break;
                    case 'D': Marker("Dog", markers, p, new Color(0.9f, 0.5f, 0.2f), dogPrefab); break;
                    case 'K': AddPickup(Marker("Keycard1", markers, p, new Color(0.25f, 0.75f, 0.75f)), Pickup.Kind.Keycard1); break;
                    case 'Q': AddPickup(Marker("Keycard2", markers, p, new Color(0.25f, 0.75f, 0.75f)), Pickup.Kind.Keycard2); break;
                }
                c++;
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

    // Repeats a tile sprite across the object instead of stretching it.
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

    // Makes a marker collectable: loot, keycards and exits react when the thief touches them.
    void AddPickup(GameObject go, Pickup.Kind kind)
    {
        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.8f;   // marker is scaled 0.6, so this is about half a cell in the world
        Pickup pickup = go.AddComponent<Pickup>();
        pickup.kind = kind;
        pickup.value = 100;
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
