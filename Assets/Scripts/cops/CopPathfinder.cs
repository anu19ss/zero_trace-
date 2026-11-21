using System.Collections.Generic;
using UnityEngine;

// 8-way A* on MuseumBuilder's grid. Door cells remain navigable for cops:
// the collider makes a cop bump and open a closed door before passing through.
public class CopPathfinder
{
    readonly MuseumBuilder map;
    readonly int[,] directions = {
        {-1, 0}, {1, 0}, {0, -1}, {0, 1},
        {-1, -1}, {-1, 1}, {1, -1}, {1, 1}
    };
    readonly Dictionary<int, Door> doorsByCell = new Dictionary<int, Door>();

    sealed class Node
    {
        public int row, col, g, h;
        public Node parent;
        public int F { get { return g + h; } }
        public Node(int r, int c, int cost, int heuristic, Node p)
        { row = r; col = c; g = cost; h = heuristic; parent = p; }
    }

    public CopPathfinder(MuseumBuilder builder)
    {
        map = builder;
        CacheDoors();
    }

    void CacheDoors()
    {
        doorsByCell.Clear();
        if (map == null || map.Width <= 0 || map.Height <= 0)
            return;

        Door[] doors = Object.FindObjectsOfType<Door>();
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] == null)
                continue;
            int row, col;
            if (WorldToCell(doors[i].transform.position, out row, out col))
                doorsByCell[row * map.Width + col] = doors[i];
        }
    }

    public bool IsBlockedCell(int row, int col)
    {
        if (map == null || row < 0 || col < 0 || row >= map.Height || col >= map.Width)
            return true;
        if (!map.IsBlocked(row, col))
            return false;

        Door door;
        if (doorsByCell.TryGetValue(row * map.Width + col, out door) && door != null)
            return false;

        return true;
    }

    public bool IsWalkable(Vector2 position)
    {
        int row, col;
        return WorldToCell(position, out row, out col) && !IsBlockedCell(row, col);
    }

    public List<Vector2> FindPath(Vector2 from, Vector2 to)
    {
        List<Vector2> empty = new List<Vector2>();
        if (!HasMap()) return empty;

        int sr, sc, tr, tc;
        if (!WorldToCell(from, out sr, out sc) || !WorldToCell(to, out tr, out tc) ||
            IsBlockedCell(sr, sc) || IsBlockedCell(tr, tc))
            return empty;

        if (sr == tr && sc == tc)
        {
            empty.Add(to);
            return empty;
        }

        List<Node> open = new List<Node>();
        bool[,] closed = new bool[map.Height, map.Width];
        Node[,] best = new Node[map.Height, map.Width];
        Node start = new Node(sr, sc, 0, Heuristic(sr, sc, tr, tc), null);
        open.Add(start);
        best[sr, sc] = start;
        Node found = null;
        Node nearestReachable = start;

        while (open.Count > 0)
        {
            int bestIndex = 0;
            for (int i = 1; i < open.Count; i++)
                if (open[i].F < open[bestIndex].F ||
                    (open[i].F == open[bestIndex].F && open[i].h < open[bestIndex].h))
                    bestIndex = i;

            Node current = open[bestIndex];
            open.RemoveAt(bestIndex);
            if (closed[current.row, current.col]) continue;
            closed[current.row, current.col] = true;

            if (current.h < nearestReachable.h ||
                (current.h == nearestReachable.h && current.g < nearestReachable.g))
                nearestReachable = current;

            if (current.row == tr && current.col == tc)
            {
                found = current;
                break;
            }

            for (int d = 0; d < 8; d++)
            {
                int nr = current.row + directions[d, 0];
                int nc = current.col + directions[d, 1];
                if (IsBlockedCell(nr, nc) || closed[nr, nc]) continue;

                bool diagonal = directions[d, 0] != 0 && directions[d, 1] != 0;
                if (diagonal &&
                    (IsBlockedCell(current.row + directions[d, 0], current.col) ||
                     IsBlockedCell(current.row, current.col + directions[d, 1])))
                    continue;

                int ng = current.g + (diagonal ? 14 : 10);
                Node old = best[nr, nc];
                if (old != null && old.g <= ng) continue;
                Node next = new Node(nr, nc, ng, Heuristic(nr, nc, tr, tc), current);
                best[nr, nc] = next;
                open.Add(next);
            }
        }

        // An inaccessible search target still produces a safe route to the
        // nearest reachable grid cell instead of a direct wall-crossing fallback.
        if (found == null)
            found = nearestReachable;

        List<Vector2> raw = new List<Vector2>();
        for (Node node = found; node != null; node = node.parent)
            raw.Add(map.CellToWorld(node.row, node.col));
        raw.Reverse();
        return Smooth(raw);
    }

    // Returns distinct reachable cells near the last known position. A flood
    // fill verifies reachability once, then farthest-point sampling spreads the
    // sweep across the room instead of repeatedly selecting the nearest tile.
    public List<Vector2> GetSearchPoints(Vector2 from, Vector2 center,
        float radius, int pointCount, int phase)
    {
        List<Vector2> points = new List<Vector2>();
        if (!HasMap() || pointCount <= 0) return points;

        int sr, sc;
        if (!WorldToCell(from, out sr, out sc) || IsBlockedCell(sr, sc))
            return points;

        bool[,] visited = new bool[map.Height, map.Width];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        List<Vector2> candidates = new List<Vector2>();
        visited[sr, sc] = true;
        queue.Enqueue(new Vector2Int(sr, sc));
        float radiusSqr = Mathf.Max(1f, radius) * Mathf.Max(1f, radius);

        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            Vector2 world = map.CellToWorld(cell.x, cell.y);
            if ((world - center).sqrMagnitude <= radiusSqr)
                candidates.Add(world);

            for (int d = 0; d < 8; d++)
            {
                int nr = cell.x + directions[d, 0];
                int nc = cell.y + directions[d, 1];
                if (nr < 0 || nc < 0 || nr >= map.Height || nc >= map.Width ||
                    visited[nr, nc] || IsBlockedCell(nr, nc))
                    continue;

                bool diagonal = directions[d, 0] != 0 && directions[d, 1] != 0;
                if (diagonal &&
                    (IsBlockedCell(cell.x + directions[d, 0], cell.y) ||
                     IsBlockedCell(cell.x, cell.y + directions[d, 1])))
                    continue;

                visited[nr, nc] = true;
                queue.Enqueue(new Vector2Int(nr, nc));
            }
        }

        if (candidates.Count == 0)
        {
            points.Add(map.CellToWorld(sr, sc));
            return points;
        }

        Vector2 seed = center + DirectionForPhase(phase) * Mathf.Min(radius * 0.45f, 2f);
        int first = 0;
        float firstDistance = float.MaxValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            float distance = (candidates[i] - seed).sqrMagnitude;
            if (distance < firstDistance)
            {
                firstDistance = distance;
                first = i;
            }
        }

        points.Add(candidates[first]);
        while (points.Count < pointCount && points.Count < candidates.Count)
        {
            int next = -1;
            float greatestDistance = -1f;
            for (int i = 0; i < candidates.Count; i++)
            {
                bool alreadySelected = false;
                float nearestSelected = float.MaxValue;
                for (int j = 0; j < points.Count; j++)
                {
                    float distance = (candidates[i] - points[j]).sqrMagnitude;
                    if (distance < 0.01f)
                    {
                        alreadySelected = true;
                        break;
                    }
                    nearestSelected = Mathf.Min(nearestSelected, distance);
                }

                if (!alreadySelected && nearestSelected > greatestDistance)
                {
                    greatestDistance = nearestSelected;
                    next = i;
                }
            }

            if (next < 0) break;
            points.Add(candidates[next]);
        }

        return points;
    }

    public bool AreInSameWalkableCell(Vector2 a, Vector2 b)
    {
        int ar, ac, br, bc;
        return WorldToCell(a, out ar, out ac) && WorldToCell(b, out br, out bc) &&
            ar == br && ac == bc && !IsBlockedCell(ar, ac);
    }

    bool HasMap()
    {
        return map != null && map.Blocked != null && map.Width > 0 && map.Height > 0;
    }

    bool WorldToCell(Vector2 p, out int row, out int col)
    {
        if (!HasMap())
        {
            row = col = -1;
            return false;
        }
        col = Mathf.FloorToInt(p.x + map.Width * 0.5f);
        row = map.Height - 1 - Mathf.FloorToInt(p.y + map.Height * 0.5f);
        if (row < 0 || col < 0 || row >= map.Height || col >= map.Width)
        { row = col = -1; return false; }
        return true;
    }

    int Heuristic(int r, int c, int tr, int tc)
    {
        int dx = Mathf.Abs(c - tc), dy = Mathf.Abs(r - tr);
        return 14 * Mathf.Min(dx, dy) + 10 * (Mathf.Max(dx, dy) - Mathf.Min(dx, dy));
    }

    static Vector2 DirectionForPhase(int phase)
    {
        float angle = ((phase % 3) + 3) % 3 * 120f;
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    List<Vector2> Smooth(List<Vector2> raw)
    {
        List<Vector2> result = new List<Vector2>();
        if (raw.Count == 0) return result;
        int anchor = 0;
        result.Add(raw[0]);
        while (anchor < raw.Count - 1)
        {
            int furthest = raw.Count - 1;
            while (furthest > anchor + 1 && !GridLineClear(raw[anchor], raw[furthest])) furthest--;
            result.Add(raw[furthest]);
            anchor = furthest;
        }
        return result;
    }

    bool GridLineClear(Vector2 a, Vector2 b)
    {
        int ar, ac, br, bc;
        if (!WorldToCell(a, out ar, out ac) || !WorldToCell(b, out br, out bc)) return false;
        int dx = Mathf.Abs(bc - ac), dy = Mathf.Abs(br - ar);
        int sx = ac < bc ? 1 : -1, sy = ar < br ? 1 : -1;
        int err = dx - dy, x = ac, y = ar;
        while (true)
        {
            if (IsBlockedCell(y, x)) return false;
            if (x == bc && y == br) return true;
            int e2 = 2 * err, oldX = x, oldY = y;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
            if (x != oldX && y != oldY &&
                (IsBlockedCell(oldY, x) || IsBlockedCell(y, oldX))) return false;
        }
    }
}
