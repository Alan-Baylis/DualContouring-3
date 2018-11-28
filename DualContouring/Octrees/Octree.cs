using System;
using System.Collections.Generic;
using UnityEngine;

namespace Drok.DualContouring.Octrees
{
    public class Octree
    {

        public const int MATERIAL_SOLID = 1;
        public const int MATERIAL_AIR = 0;

        private const int steps = 8;

        /// <summary>
        /// Offset values when creating children.
        /// </summary>
        private static readonly Vector3[] CHILD_MIN_OFFSETS =
        {
            // needs to match the vertMap from Dual Contouring impl.
            new Vector3( 0, 0, 0 ),
            new Vector3( 0, 0, 1 ),
            new Vector3( 0, 1, 0 ),
            new Vector3( 0, 1, 1 ),

            new Vector3( 1, 0, 0 ),
            new Vector3( 1, 0, 1 ),
            new Vector3( 1, 1, 0 ),
            new Vector3( 1, 1, 1 )

        };

        /// <summary>
        /// A reversed version of CHILD_MIN_OFFSETS
        /// </summary>
        private static readonly int[,,] CHILD_MIN_OFFSETS_REVERSE =
        {
            // Needs to be the reverse of CHILD_MIN_OFFSETS.

            // X == 0
            {
                // Y == 0
                {
                    0,
                    1
                },

                // Y == 1
                {
                    2,
                    3
                }
            },

            // X == 1
            {
                // Y == 0
                {
                    4,
                    5
                },

                // Y == 1
                {
                    6,
                    7
                }
            }
        };

        /// <summary>
        /// Contains vertices for specific edges.
        /// </summary>
        private static readonly int[,] edgevmap =
        {
            {0,4},{1,5},{2,6},{3,7},	// x-axis 
	        {0,2},{1,3},{4,6},{5,7},	// y-axis
	        {0,1},{2,3},{4,5},{6,7}		// z-axis
        };

        /// <summary>
        /// A function that returns a density. Positive numbers are solid and negative are empty.
        /// </summary>
        private Func<float, float, float, float> function;

        /// <summary>
        /// The base node of which this octree stems.
        /// </summary>
        public OctreeNode baseNode;
        public bool completed = false;

        public Octree(int size)
        {
            baseNode = new OctreeNode();
            baseNode.size = size;
            baseNode.type = OctreeNodeType.NODE_INTERNAL;
        }

        /// <summary>
        /// Dynamically constructs an Octree from a given set of nodes.
        /// </summary>
        public void ConstructOctreeFromNodes(OctreeNode[] nodes)
        {
            List<OctreeNode> parents = new List<OctreeNode>();
            foreach (OctreeNode node in nodes)
            {
                ConstructOctreeUpwards(node, parents);
            }

            completed = true;
        }

        private void ConstructOctreeUpwards(OctreeNode node, List<OctreeNode> parents)
        {
            int parentSize = node.size * 2;
            Vector3 parentPos = node.position - VectorUtils.Mod(node.position, parentSize);
            Vector3 childPos = (node.position - parentPos) / node.size;
            int childIndex = CHILD_MIN_OFFSETS_REVERSE[(int)childPos.x, (int)childPos.y, (int)childPos.z];

            if (node.size < (baseNode.size / 2))
            {

                foreach (OctreeNode p in parents)
                {
                    if (p.size == parentSize && p.position == parentPos)
                    {
                        p.children[childIndex] = node;
                        ConstructOctreeUpwards(p, parents);
                        return;
                    }
                }

                OctreeNode parent = new OctreeNode();
                parents.Add(parent);
                parent.type = OctreeNodeType.NODE_INTERNAL;
                parent.size = parentSize;
                parent.position = parentPos;
                parent.children[childIndex] = node;
                ConstructOctreeUpwards(parent, parents);
            }
            else
            {
                baseNode.children[childIndex] = node;
            }
        }

        /// <summary>
        /// Recursively constructs OctreeNodes depth first.
        /// </summary>
        public void ConstructOctreeFromDensity(Func<float, float, float, float> func)
        {
            function = func;

            completed = false;
            baseNode = ConstructOctreeNodes(baseNode);
            completed = true;
        }

        /// <summary>
        /// Recursively constructs OctreeNodes depth first.
        /// </summary>
        /// <param name="node">The base node to split from.</param>
        /// <returns></returns>
        private OctreeNode ConstructOctreeNodes(OctreeNode node)
        {
            // If the node doesn't exist return.
            if (node == null)
            {
                return null;
            }

            // If size equals 1 then the node is a possible leaf.
            if (node.size == 1)
            {
                return ConstructLeafNode(node);
            }

            int childSize = node.size / 2;
            bool hasChildren = false;

            for (int i = 0; i < 8; i++)
            {
                OctreeNode child = new OctreeNode();
                child.size = childSize;
                child.position = node.position + (CHILD_MIN_OFFSETS[i] * childSize);
                child.type = OctreeNodeType.NODE_INTERNAL;

                node.children[i] = ConstructOctreeNodes(child);
                hasChildren |= (node.children[i] != null);
            }

            if (!hasChildren)
            {
                node = null;
                return null;
            }

            return node;
        }

        /// <summary>
        /// Constructs a leaf node with the required info for dual contouring.
        /// </summary>
        /// <param name="node">The node to construct.</param>
        /// <returns></returns>
        private OctreeNode ConstructLeafNode(OctreeNode node)
        {
            int corners = 0;
            for (int i = 0; i < 8; i++)
            {
                Vector3 cornerPos = node.position + CHILD_MIN_OFFSETS[i];
                float density = function(cornerPos.x, cornerPos.y, cornerPos.z);
                int solid = density > 0f ? MATERIAL_SOLID : MATERIAL_AIR;
                corners |= (solid << i);
            }

            // Test if the node is entirely inside or outside of the volume.
            if (corners == 0 || corners == 255)
            {
                // Delete the nodes
                return null;
            }

            // Set the node type to leaf.
            node.type = OctreeNodeType.NODE_LEAF;

            node.drawInfo = new OctreeDrawInfo();

            // Set the vertex position to be in the middle of the node.
            Vector3 centerOffset = Vector3.one * ((float)node.size / 2);
            node.drawInfo.position = node.position + FindBestVertex(node.position.x, node.position.y, node.position.z, corners);
            node.drawInfo.corners = corners;
            node.drawInfo.normal = CalculateSurfaceNormal(node.position.x, node.position.y, node.position.z);

            return node;
        }

        /// <summary>
        /// Calculates the surface normal at position (<paramref name="x">, <paramref name="y">, <paramref name="z">).
        /// </summary>
        /// <param name="x">The x coordinate of the calculated normal.</param>
        /// <param name="y">The y coordinate of the calculated normal.</param>
        /// <param name="z">The z coordinate of the calculated normal.</param>
        /// <returns>A normalized Vector3.</returns>
        private Vector3 CalculateSurfaceNormal(float x, float y, float z)
        {
            float offset = 0.001f;

            float dx = function(x + offset, y, z) - function(x - offset, y, z);
            float dy = function(x, y + offset, z) - function(x, y - offset, z);
            float dz = function(x, y, z + offset) - function(x, y, z - offset);

            return new Vector3(-dx, -dy, -dz).normalized;
        }

        private Vector3 FindBestVertex(float x, float y, float z, int corners)
        {
            QEFData data = new QEFData();
            data.positions = GetCrossingPoints(x, y, z, corners);
            data.normals = new Vector3[data.positions.Length];

            for (int i = 0; i < data.normals.Length; i++) {
                Vector3 point = data.positions[i];
                data.normals[i] = CalculateSurfaceNormal(point.x + x, point.y + y, point.z + z);
            }

            data = QEFSolver.Solve3D(data);

            return data.point;
        }

        private Vector3[] GetCrossingPoints(float x, float y, float z, int corners)
        {
            List<Vector3> points = new List<Vector3>();
            int MAX_CROSSINGS = 8;
            int edgeCount = 0;

            for (int i = 0; i < 12 && edgeCount < MAX_CROSSINGS; i++) {
                int c1 = edgevmap[i, 0];
                int c2 = edgevmap[i, 1];

                int m1 = (corners >> c1) & 1;
                int m2 = (corners >> c2) & 1;

                if (m1 == m2) {
                    // There is no zero crossing point for this edge.
                    continue;
                }

                Vector3 from = new Vector3(x, y, z) + CHILD_MIN_OFFSETS[c1];
                Vector3 to = new Vector3(x, y, z) + CHILD_MIN_OFFSETS[c2];

                Vector3 point = ApproximateCrossingPosition(from, to) - new Vector3(x, y, z);
                points.Add(point);
            }

            return points.ToArray();
        }

        /// <summary>
        /// Approximates the zero crossing of the specified edge. 
        /// </summary>
        /// <param name="from">Starting Vector3.</param>
        /// <param name="to">Ending Vector3.</param>
        /// <returns></returns>
        private Vector3 ApproximateCrossingPosition(Vector3 from, Vector3 to)
        {
            float minValue = float.MaxValue;
            Vector3 result = new Vector3();
            float increment = 1.0f / steps;

            for (float t = 0; t < 1.0f; t += increment)
            {
                Vector3 pos = Vector3.Lerp(from, to, t);
                float value = function(pos.x, pos.y, pos.z);

                if (value < minValue)
                {
                    minValue = value;
                    result = pos;
                }
            }

            return result;
        }
    }
}
