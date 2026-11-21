
using System.Collections.Generic;
using UnityEngine;

public class DogPathfinding : MonoBehaviour
{
    private MuseumBuilder museum;

    private void Awake()
    {
        museum = FindObjectOfType<MuseumBuilder>();

        if (museum == null)
        {
            Debug.LogError("DogPathfinding: MuseumBuilder not found!");
        }
    }

    public List<Vector2> FindPath(Vector2 startPosition, Vector2 targetPosition)
    {
        List<Vector2> path = new List<Vector2>();

        if (museum == null || museum.Blocked == null)
            return path;

        int startRow = WorldToRow(startPosition);
        int startCol = WorldToCol(startPosition);

        int targetRow = WorldToRow(targetPosition);
        int targetCol = WorldToCol(targetPosition);

        if (!IsInsideGrid(startRow, startCol) ||
            !IsInsideGrid(targetRow, targetCol))
            return path;

        if (museum.IsBlocked(targetRow, targetCol))
            return path;

        Vector2Int start = new Vector2Int(startRow, startCol);
        Vector2Int target = new Vector2Int(targetRow, targetCol);

        List<Node> openList = new List<Node>();
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();

        Node startNode = new Node(start, null, 0,
            GetDistance(start, target));

        openList.Add(startNode);

        while (openList.Count > 0)
        {
            Node current = GetLowestCostNode(openList);

            if (current.position == target)
            {
                path = ReconstructPath(current);
                return path;
            }

            openList.Remove(current);
            closedSet.Add(current.position);

            foreach (Vector2Int neighbour in GetNeighbours(current.position))
            {
                if (!IsInsideGrid(neighbour.x, neighbour.y))
                    continue;

                if (museum.IsBlocked(neighbour.x, neighbour.y))
                    continue;

                if (closedSet.Contains(neighbour))
                    continue;

                int newCost = current.gCost + 1;

                Node existing = openList.Find(
                    n => n.position == neighbour
                );

                if (existing == null)
                {
                    Node newNode = new Node(
                        neighbour,
                        current,
                        newCost,
                        GetDistance(neighbour, target)
                    );

                    openList.Add(newNode);
                }
                else if (newCost < existing.gCost)
                {
                    existing.gCost = newCost;
                    existing.parent = current;
                }
            }
        }

        return path;
    }

    private int WorldToRow(Vector2 position)
    {
        return museum.Height - 1 -
            Mathf.FloorToInt(position.y + museum.Height / 2f);
    }

    private int WorldToCol(Vector2 position)
    {
        return Mathf.FloorToInt(position.x + museum.Width / 2f);
    }

    private bool IsInsideGrid(int row, int col)
    {
        return row >= 0 && row < museum.Height &&
               col >= 0 && col < museum.Width;
    }

    private List<Vector2Int> GetNeighbours(Vector2Int position)
    {
        return new List<Vector2Int>
        {
            new Vector2Int(position.x - 1, position.y),
            new Vector2Int(position.x + 1, position.y),
            new Vector2Int(position.x, position.y - 1),
            new Vector2Int(position.x, position.y + 1)
        };
    }

    private int GetDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private Node GetLowestCostNode(List<Node> nodes)
    {
        Node best = nodes[0];

        foreach (Node node in nodes)
        {
            if (node.fCost < best.fCost ||
                (node.fCost == best.fCost && node.hCost < best.hCost))
            {
                best = node;
            }
        }

        return best;
    }

    private List<Vector2> ReconstructPath(Node node)
    {
        List<Vector2> result = new List<Vector2>();

        while (node != null)
        {
            Vector3 worldPosition = museum.CellToWorld(
                node.position.x,
                node.position.y
            );

            result.Add(worldPosition);
            node = node.parent;
        }

        result.Reverse();
        return result;
    }

    private class Node
    {
        public Vector2Int position;
        public Node parent;
        public int gCost;
        public int hCost;

        public int fCost => gCost + hCost;

        public Node(Vector2Int position, Node parent,
            int gCost, int hCost)
        {
            this.position = position;
            this.parent = parent;
            this.gCost = gCost;
            this.hCost = hCost;
        }
    }
}
