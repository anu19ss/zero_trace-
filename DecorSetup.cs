using System.Collections;
using UnityEngine;

// The decorations (tables, plants, bookshelves) are created when the game starts,
// so components added by hand in the editor disappear. This script adds them in code instead.
// It can also place EXTRA hiding spots and tables by copying ones that already exist.
// Put it on ONE empty GameObject in the scene.
public class DecorSetup : MonoBehaviour
{
    [Header("Which objects get which behaviour (by the START of their name)")]
    [Tooltip("These become hiding spots (press E).")]
    public string[] hidingNameStarts = { "Hiding", "MuseumPlant" };

    [Tooltip("These can be knocked over (press F).")]
    public string[] distractNameStarts = { "MuseumTable" };

    public float triggerSize = 1.5f;   // size of the interaction area, in cells

    [Header("Extra items placed by map cell (X = column, Y = row, row 0 is the top)")]
    public int mapWidth = 40;
    public int mapHeight = 28;

    public Vector2Int[] extraHidingCells =
    {
        new Vector2Int(1, 8),   new Vector2Int(25, 8),  new Vector2Int(37, 3),
        new Vector2Int(1, 10),  new Vector2Int(30, 14),
        new Vector2Int(1, 20),  new Vector2Int(1, 26),
        new Vector2Int(19, 26), new Vector2Int(19, 16),
        new Vector2Int(21, 26), new Vector2Int(29, 16), new Vector2Int(38, 16)
    };

    public Vector2Int[] extraTableCells =
    {
        new Vector2Int(9, 11),  new Vector2Int(29, 11),
        new Vector2Int(14, 21), new Vector2Int(24, 23), new Vector2Int(34, 25)
    };

    bool extrasSpawned;

    IEnumerator Start()
    {
        yield return null;                       // wait for the museum to be built
        Apply();
        yield return new WaitForSeconds(1f);     // in case some decorations appear a little later
        Apply();
    }

    void Apply()
    {
        SpawnExtras();

        foreach (SpriteRenderer sr in FindObjectsOfType<SpriteRenderer>())
        {
            GameObject go = sr.gameObject;

            if (StartsWithAny(go.name, hidingNameStarts) && go.GetComponent<HidingSpot>() == null)
            {
                AddTrigger(go);
                go.AddComponent<HidingSpot>();
            }
            else if (StartsWithAny(go.name, distractNameStarts) && go.GetComponent<Distractible>() == null)
            {
                AddTrigger(go);
                go.AddComponent<Distractible>();
            }
        }
    }

    // Copies an existing hiding plant and an existing table to the extra cells, once.
    void SpawnExtras()
    {
        if (extrasSpawned) return;

        GameObject hidingSource = FindByNameStart("HidingPlant");
        GameObject tableSource = FindByNameStart("MuseumTable");
        if (hidingSource == null && tableSource == null) return;   // decorations not built yet

        if (hidingSource != null)
            foreach (Vector2Int cell in extraHidingCells)
                Copy(hidingSource, "HidingPlant_Extra", cell);

        if (tableSource != null)
            foreach (Vector2Int cell in extraTableCells)
                Copy(tableSource, "MuseumTable_Extra", cell);

        extrasSpawned = true;
    }

    void Copy(GameObject source, string newName, Vector2Int cell)
    {
        GameObject copy = Instantiate(source, source.transform.parent);
        copy.name = newName;
        copy.transform.position = CellToWorld(cell);
    }

    // Same maths as MuseumBuilder (the museum is centred on the world origin).
    Vector3 CellToWorld(Vector2Int cell)
    {
        float x = cell.x - mapWidth / 2f + 0.5f;
        float y = (mapHeight - 1 - cell.y) - mapHeight / 2f + 0.5f;
        return new Vector3(x, y, 0f);
    }

    static GameObject FindByNameStart(string nameStart)
    {
        foreach (SpriteRenderer sr in FindObjectsOfType<SpriteRenderer>())
            if (sr.gameObject.name.StartsWith(nameStart)) return sr.gameObject;
        return null;
    }

    void AddTrigger(GameObject go)
    {
        BoxCollider2D box = go.GetComponent<BoxCollider2D>();
        if (box == null) box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;

        // Make the area about triggerSize cells wide in the world, whatever the object's scale is.
        Vector3 s = go.transform.lossyScale;
        float sx = Mathf.Max(0.01f, Mathf.Abs(s.x));
        float sy = Mathf.Max(0.01f, Mathf.Abs(s.y));
        box.size = new Vector2(triggerSize / sx, triggerSize / sy);
        box.offset = Vector2.zero;
    }

    static bool StartsWithAny(string objectName, string[] starts)
    {
        foreach (string s in starts)
            if (!string.IsNullOrEmpty(s) && objectName.StartsWith(s)) return true;
        return false;
    }
}