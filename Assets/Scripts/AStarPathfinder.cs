using UnityEngine;
using System.Collections.Generic;

public static class AStarPathfinder
{
    public class Node
    {
        public Vector2Int pos;
        public int gCost;
        public int hCost;
        public int fCost { get { return gCost + hCost; } }
        public Node parent;
    }

    // 获取从 start 到 end 的路径，假设地图范围为 (0,0) 到 (gridWidth-1, gridHeight-1)
    public static List<Vector2Int> GetPath(Vector2Int start, Vector2Int end, int gridWidth, int gridHeight)
    {
        List<Node> openList = new List<Node>();
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();

        Node startNode = new Node { pos = start, gCost = 0, hCost = GetManhattanDistance(start, end) };
        openList.Add(startNode);

        while (openList.Count > 0)
        {
            // 获取 fCost 最小的节点
            Node currentNode = openList[0];
            for (int i = 1; i < openList.Count; i++)
            {
                if (openList[i].fCost < currentNode.fCost || (openList[i].fCost == currentNode.fCost && openList[i].hCost < currentNode.hCost))
                {
                    currentNode = openList[i];
                }
            }

            openList.Remove(currentNode);
            closedSet.Add(currentNode.pos);

            // 如果到达目标
            if (currentNode.pos == end)
            {
                return RetracePath(startNode, currentNode);
            }

            // 遍历4个邻居（上下左右）
            foreach (Vector2Int neighborPos in GetNeighbors(currentNode.pos, gridWidth, gridHeight))
            {
                if (closedSet.Contains(neighborPos))
                    continue;

                int tentativeGCost = currentNode.gCost + 1; // 每步代价为1

                Node neighborNode = openList.Find(n => n.pos == neighborPos);
                if (neighborNode == null)
                {
                    neighborNode = new Node();
                    neighborNode.pos = neighborPos;
                    neighborNode.gCost = tentativeGCost;
                    neighborNode.hCost = GetManhattanDistance(neighborPos, end);
                    neighborNode.parent = currentNode;
                    openList.Add(neighborNode);
                }
                else if (tentativeGCost < neighborNode.gCost)
                {
                    neighborNode.gCost = tentativeGCost;
                    neighborNode.parent = currentNode;
                }
            }
        }

        // 没有找到路径则返回只包含起点的路径
        return new List<Vector2Int>() { start };
    }

    private static List<Vector2Int> GetNeighbors(Vector2Int pos, int gridWidth, int gridHeight)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();

        // 上
        if (pos.y - 1 >= 0)
            neighbors.Add(new Vector2Int(pos.x, pos.y - 1));
        // 下
        if (pos.y + 1 < gridHeight)
            neighbors.Add(new Vector2Int(pos.x, pos.y + 1));
        // 左
        if (pos.x - 1 >= 0)
            neighbors.Add(new Vector2Int(pos.x - 1, pos.y));
        // 右
        if (pos.x + 1 < gridWidth)
            neighbors.Add(new Vector2Int(pos.x + 1, pos.y));

        return neighbors;
    }

    private static int GetManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static List<Vector2Int> RetracePath(Node startNode, Node endNode)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        Node currentNode = endNode;
        while (currentNode != startNode)
        {
            path.Add(currentNode.pos);
            currentNode = currentNode.parent;
        }
        path.Reverse();
        // 在路径中加上起点
        path.Insert(0, startNode.pos);
        return path;
    }
}
