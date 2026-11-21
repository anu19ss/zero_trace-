using System.Collections.Generic;
using UnityEngine;

// Adds non-blocking room furnishings, wall art and one collectible gallery painting.
// Crouched hiding remains governed by the existing H cells and ThiefController.
public class MuseumEnvironmentDecorator : MonoBehaviour
{
    public MuseumBuilder museum;
    public Sprite plantSprite;
    public Sprite tableSprite;
    public Sprite bookshelfSprite;

    const int GalleryLootValue = 100;
    readonly List<Vector2> occupiedFloorCells = new List<Vector2>();

    void Start()
    {
        if (museum == null)
            museum = FindObjectOfType<MuseumBuilder>();
        if (museum == null || museum.mapFile == null || museum.Blocked == null)
            return;

        string[] lines = museum.mapFile.text.Replace("\r", "").Split(
            new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length != museum.Height || lines[0].Length != museum.Width)
            return;

        Transform root = new GameObject("MuseumEnvironmentDecorations").transform;
        root.SetParent(museum.transform, false);
        Sprite[] paintings = {
            CreatePaintingSprite(0),
            CreatePaintingSprite(1),
            CreatePaintingSprite(2)
        };

        AddHidingCover(lines, root);
        AddRoomFurniture(lines, root);
        AddGalleryLoot(lines, root, paintings[2]);
        AddCorridorPaintings(lines, root, paintings);
        AddCollidersToExistingFurniture();
    }

    void AddHidingCover(string[] lines, Transform root)
    {
        // H cells already receive the existing crouch concealment bonus.
        // Furniture is visual only, so it never blocks a doorway or path.
        int shelfCount = 0;
        for (int row = 0; row < museum.Height; row++)
        {
            for (int col = 0; col < museum.Width; col++)
            {
                if (lines[row][col] != 'H')
                    continue;

                bool useShelf = bookshelfSprite != null && shelfCount < 2;
                Sprite sprite = useShelf ? bookshelfSprite : plantSprite;
                if (sprite == null)
                    continue;
                Vector2 position = museum.CellToWorld(row, col);
                if (SceneSpriteOccupies(position, 0.28f, 0.28f))
                    continue;

                AddFurniture(root, useShelf ? "HidingBookshelf" : "HidingPlant",
                    sprite, position, useShelf ? new Vector2(0.82f, 0.92f) :
                    new Vector2(0.74f, 0.78f));
                occupiedFloorCells.Add(position);
                if (useShelf)
                    shelfCount++;
            }
        }
    }

    void AddRoomFurniture(string[] lines, Transform root)
    {
        PlaceFurniture(lines, root, tableSprite, "MuseumTable",
            new Vector2Int(24, 16), new Vector2(0.90f, 0.76f));
        PlaceFurniture(lines, root, tableSprite, "MuseumTable",
            new Vector2Int(24, 30), new Vector2(0.90f, 0.76f));

        PlaceFurniture(lines, root, plantSprite, "MuseumPlant",
            new Vector2Int(4, 4), new Vector2(0.70f, 0.76f));
        PlaceFurniture(lines, root, plantSprite, "MuseumPlant",
            new Vector2Int(5, 15), new Vector2(0.70f, 0.76f));
        PlaceFurniture(lines, root, plantSprite, "MuseumPlant",
            new Vector2Int(23, 20), new Vector2(0.70f, 0.76f));
    }

    void PlaceFurniture(string[] lines, Transform root, Sprite sprite, string objectName,
        Vector2Int preferredCell, Vector2 size)
    {
        if (sprite == null)
            return;

        int bestRow = -1;
        int bestCol = -1;
        float bestScore = float.MaxValue;
        for (int row = 1; row < museum.Height - 1; row++)
        {
            for (int col = 1; col < museum.Width - 1; col++)
            {
                if (!IsOpenFurnitureCell(lines, row, col))
                    continue;
                Vector2 position = museum.CellToWorld(row, col);
                if (TooCloseToPlaced(position, 1.5f) ||
                    SceneSpriteOccupies(position, size.x * 0.5f, size.y * 0.5f))
                    continue;

                float score = (row - preferredCell.x) * (row - preferredCell.x) +
                    (col - preferredCell.y) * (col - preferredCell.y);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestRow = row;
                    bestCol = col;
                }
            }
        }

        if (bestRow < 0)
            return;
        Vector2 selected = museum.CellToWorld(bestRow, bestCol);
        AddFurniture(root, objectName, sprite, selected, size);
        occupiedFloorCells.Add(selected);
    }

    bool IsOpenFurnitureCell(string[] lines, int row, int col)
    {
        if (lines[row][col] != '.')
            return false;
        // Tables and plants are placed only in open room interiors, with a
        // one-cell walkable buffer around them.
        for (int r = row - 1; r <= row + 1; r++)
            for (int c = col - 1; c <= col + 1; c++)
                if (lines[r][c] != '.')
                    return false;
        return true;
    }

    bool TooCloseToPlaced(Vector2 position, float distance)
    {
        for (int i = 0; i < occupiedFloorCells.Count; i++)
            if (Vector2.Distance(position, occupiedFloorCells[i]) < distance)
                return true;
        return false;
    }

    bool SceneSpriteOccupies(Vector2 position, float halfWidth, float halfHeight)
    {
        SpriteRenderer[] renderers = FindObjectsOfType<SpriteRenderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null || sr.transform.IsChildOf(museum.transform))
                continue;
            Bounds bounds = sr.bounds;
            if (Mathf.Abs(position.x - bounds.center.x) < halfWidth + bounds.extents.x + 0.15f &&
                Mathf.Abs(position.y - bounds.center.y) < halfHeight + bounds.extents.y + 0.15f)
                return true;
        }
        return false;
    }

    static void AddFurniture(Transform root, string objectName, Sprite sprite,
        Vector2 position, Vector2 worldSize)
    {
        GameObject item = new GameObject(objectName);
        item.transform.SetParent(root, false);
        item.transform.position = position;
        Vector2 spriteSize = sprite.bounds.size;
        item.transform.localScale = new Vector3(
            worldSize.x / Mathf.Max(0.01f, spriteSize.x),
            worldSize.y / Mathf.Max(0.01f, spriteSize.y), 1f);
        SpriteRenderer renderer = item.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 3;

        // Keep hiding cells reachable while blocking contact with the base of
        // their plant. Room tables and plants use their full visible footprint.
        if (objectName == "HidingPlant")
        {
            BoxCollider2D baseCollider = item.AddComponent<BoxCollider2D>();
            baseCollider.size = new Vector2(spriteSize.x * 0.55f, spriteSize.y * 0.16f);
            baseCollider.offset = new Vector2(0f, -spriteSize.y * 0.40f);
        }
        else if (objectName != "HidingBookshelf")
        {
            BoxCollider2D collider = item.AddComponent<BoxCollider2D>();
            collider.size = spriteSize;
        }
    }

    void AddCollidersToExistingFurniture()
    {
        AddColliderIfMissing("wooden_table");
        AddColliderIfMissing("potted_plant");
        AddColliderIfMissing("bookshelf");
    }

    static void AddColliderIfMissing(string objectName)
    {
        GameObject item = GameObject.Find(objectName);
        if (item == null || item.GetComponent<Collider2D>() != null)
            return;

        SpriteRenderer renderer = item.GetComponent<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null)
            return;

        BoxCollider2D collider = item.AddComponent<BoxCollider2D>();
        // Sprite bounds are in local units; the object's existing transform
        // scale then keeps the collider aligned with the artwork.
        collider.size = renderer.sprite.bounds.size;
    }

    void AddGalleryLoot(string[] lines, Transform root, Sprite painting)
    {
        // The existing bookshelf identifies the upper-left gallery. Select
        // the nearest free floor cell around its centre, avoiding map items.
        Vector2Int preferred = new Vector2Int(5, 7);
        int bestRow = -1;
        int bestCol = -1;
        float bestScore = float.MaxValue;
        for (int row = 1; row <= 8 && row < museum.Height - 1; row++)
        {
            for (int col = 1; col <= 10 && col < museum.Width - 1; col++)
            {
                if (lines[row][col] != '.')
                    continue;
                Vector2 position = museum.CellToWorld(row, col);
                if (TooCloseToPlaced(position, 1.1f) || SceneSpriteOccupies(position, 0.30f, 0.30f))
                    continue;
                float score = (row - preferred.x) * (row - preferred.x) +
                    (col - preferred.y) * (col - preferred.y);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestRow = row;
                    bestCol = col;
                }
            }
        }

        if (bestRow < 0)
        {
            Debug.LogWarning("MuseumEnvironmentDecorator: no free gallery cell for the painting loot.", this);
            return;
        }

        GameObject loot = new GameObject("Loot_Painting_Extra");
        loot.transform.SetParent(root, false);
        loot.transform.position = museum.CellToWorld(bestRow, bestCol);
        loot.transform.localScale = Vector3.one;
        SpriteRenderer renderer = loot.AddComponent<SpriteRenderer>();
        renderer.sprite = painting;
        renderer.sortingOrder = 4;
        CircleCollider2D trigger = loot.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 0.8f;
        Pickup pickup = loot.AddComponent<Pickup>();
        pickup.kind = Pickup.Kind.Loot;
        pickup.value = GalleryLootValue;
        occupiedFloorCells.Add(loot.transform.position);
    }

    void AddCorridorPaintings(string[] lines, Transform root, Sprite[] paintings)
    {
        List<Vector2Int> wallCells = new List<Vector2Int>();
        for (int row = 2; row < museum.Height - 2; row++)
        {
            for (int col = 2; col < museum.Width - 2; col++)
            {
                if (lines[row][col] != '#')
                    continue;
                // A wall cell along a corridor normally has floor on only one
                // side. Requiring floor on both opposing sides filtered out
                // nearly every usable wall face, so no paintings were placed.
                bool touchesFloor = IsOpen(lines[row - 1][col]) ||
                    IsOpen(lines[row + 1][col]) ||
                    IsOpen(lines[row][col - 1]) ||
                    IsOpen(lines[row][col + 1]);
                if (!touchesFloor || NearDoorOrMarker(lines, row, col))
                    continue;
                wallCells.Add(new Vector2Int(row, col));
            }
        }

        List<Vector2Int> selected = new List<Vector2Int>();
        for (int i = 0; i < wallCells.Count && selected.Count < 8; i++)
        {
            Vector2Int candidate = wallCells[i];
            bool spaced = true;
            for (int j = 0; j < selected.Count; j++)
                if (Mathf.Abs(candidate.x - selected[j].x) +
                    Mathf.Abs(candidate.y - selected[j].y) < 5)
                    spaced = false;
            if (spaced)
                selected.Add(candidate);
        }

        for (int i = 0; i < selected.Count; i++)
        {
            Vector2Int cell = selected[i];
            GameObject picture = new GameObject("CorridorPainting_" + (i + 1));
            picture.transform.SetParent(root, false);
            picture.transform.position = museum.CellToWorld(cell.x, cell.y) +
                new Vector3(0f, 0f, -0.02f);
            picture.transform.localScale = new Vector3(0.95f, 0.85f, 1f);
            SpriteRenderer renderer = picture.AddComponent<SpriteRenderer>();
            renderer.sprite = paintings[i % paintings.Length];
            renderer.sortingOrder = 2;
        }
    }

    static bool IsOpen(char cell)
    {
        return cell == '.' || cell == 'H';
    }

    static bool NearDoorOrMarker(string[] lines, int row, int col)
    {
        for (int r = row - 1; r <= row + 1; r++)
            for (int c = col - 1; c <= col + 1; c++)
            {
                char cell = lines[r][c];
                if (cell == '1' || cell == '2' || cell == 'E' || cell == 'L' ||
                    cell == 'C' || cell == 'K' || cell == 'Q' || cell == '$' ||
                    cell == 'P' || cell == 'G' || cell == 'D' || cell == 'S')
                    return true;
            }
        return false;
    }

    static Sprite CreatePaintingSprite(int style)
    {
        const int width = 64;
        const int height = 48;
        Color32[] pixels = new Color32[width * height];
        Color32 darkFrame = new Color32(55, 31, 18, 255);
        Color32 wood = new Color32(118, 70, 34, 255);
        Color32 gold = new Color32(224, 174, 74, 255);

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                bool outer = x < 3 || x >= width - 3 || y < 3 || y >= height - 3;
                bool inner = x == 3 || x == width - 4 || y == 3 || y == height - 4;
                pixels[y * width + x] = outer ? darkFrame : inner ? gold : wood;
            }

        for (int y = 5; y < height - 5; y++)
        {
            for (int x = 5; x < width - 5; x++)
            {
                Color32 color;
                int horizon = style == 1 ? 20 : 23;
                float skyBlend = Mathf.InverseLerp(5f, horizon, y);
                Color sky = Color.Lerp(new Color(0.22f, 0.43f, 0.62f),
                    new Color(0.78f, 0.62f, 0.36f), skyBlend);
                float ridgeA = 15f + Mathf.Sin((x + style * 11) * 0.15f) * 5f;
                float ridgeB = 11f + Mathf.Sin((x + style * 7) * 0.23f) * 3f;
                if (y > ridgeA)
                    color = style == 2 ? new Color(0.27f, 0.25f, 0.31f) :
                        new Color(0.25f, 0.35f, 0.30f);
                else if (y > ridgeB)
                    color = new Color(0.38f, 0.29f, 0.24f);
                else
                    color = sky;

                float sunX = style == 1 ? 22f : 43f;
                float sunY = 31f;
                if ((x - sunX) * (x - sunX) + (y - sunY) * (y - sunY) < 16f)
                    color = new Color(0.95f, 0.78f, 0.42f);
                pixels[y * width + x] = color;
            }
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "MuseumPaintingTexture_" + style;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f), 64f);
        sprite.name = "MuseumPainting_" + style;
        return sprite;
    }
}
