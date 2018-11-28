using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Drok.DualContouring.Octrees;

namespace Drok.DualContouring
{
    /// <summary>
    /// Holds all relevant code and data to generate a mesh with DC.
    /// </summary>
    /// <remarks>
    /// Needs to be rewritten and better documented.
    /// </remarks>
    public static class ADCMeshGenerator
    {

        #region Data provided by the original DC impl.  Used for the contouring process.

        private readonly static int[,] edgevmap =
        {
            {0,4},{1,5},{2,6},{3,7},	// x-axis 
	        {0,2},{1,3},{4,6},{5,7},	// y-axis
	        {0,1},{2,3},{4,5},{6,7}		// z-axis
            };

        private readonly static int[] edgemask = { 5, 3, 6 };

        private readonly static int[,] vertMap =
        {
            {0,0,0},
            {0,0,1},
            {0,1,0},
            {0,1,1},
            {1,0,0},
            {1,0,1},
            {1,1,0},
            {1,1,1}
        };

        private readonly static int[,] faceMap = { { 4, 8, 5, 9 }, { 6, 10, 7, 11 }, { 0, 8, 1, 10 }, { 2, 9, 3, 11 }, { 0, 4, 2, 6 }, { 1, 5, 3, 7 } };
        private readonly static int[,] cellProcFaceMask = { { 0, 4, 0 }, { 1, 5, 0 }, { 2, 6, 0 }, { 3, 7, 0 }, { 0, 2, 1 }, { 4, 6, 1 }, { 1, 3, 1 }, { 5, 7, 1 }, { 0, 1, 2 }, { 2, 3, 2 }, { 4, 5, 2 }, { 6, 7, 2 } };
        private readonly static int[,] cellProcEdgeMask = { { 0, 1, 2, 3, 0 }, { 4, 5, 6, 7, 0 }, { 0, 4, 1, 5, 1 }, { 2, 6, 3, 7, 1 }, { 0, 2, 4, 6, 2 }, { 1, 3, 5, 7, 2 } };

        private readonly static int[,,] faceProcFaceMask = {
            {{4,0,0},{5,1,0},{6,2,0},{7,3,0}},
            {{2,0,1},{6,4,1},{3,1,1},{7,5,1}},
            {{1,0,2},{3,2,2},{5,4,2},{7,6,2}}
        };

        private readonly static int[,,] faceProcEdgeMask = {
            {{1,4,0,5,1,1},{1,6,2,7,3,1},{0,4,6,0,2,2},{0,5,7,1,3,2}},
            {{0,2,3,0,1,0},{0,6,7,4,5,0},{1,2,0,6,4,2},{1,3,1,7,5,2}},
            {{1,1,0,3,2,0},{1,5,4,7,6,0},{0,1,5,0,4,1},{0,3,7,2,6,1}}
        };

        private readonly static int[,,] edgeProcEdgeMask = {
            {{3,2,1,0,0},{7,6,5,4,0}},
            {{5,1,4,0,1},{7,3,6,2,1}},
            {{6,4,2,0,2},{7,5,3,1,2}},
        };

        private readonly static int[,] processEdgeMask = { { 3, 2, 1, 0 }, { 7, 5, 6, 4 }, { 11, 10, 9, 8 } };

        #endregion

        /// <summary>
        /// Generates a mesh from the octree using DC.
        /// </summary>
        /// <param name="octree"></param>
        /// <param name="mesh"></param>
        public static void GenerateMeshFromOctree(Octree octree, Mesh mesh)
        {
            if (octree == null)
            {
                throw new NullReferenceException("Octree cannot be null.");
            }
            else if (!octree.completed)
            {
                throw new ArgumentException("Octree must be completed before a mesh can be generated.");
            }
            else if (octree.baseNode == null)
            {
                Debug.LogWarning("Octree base node is null.  Is this a mistake?");
                return;
            }

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<int> indices = new List<int>();

            // Generate Indices for every vertex.
            GenerateVertexIndices(octree.baseNode, vertices, normals);

            // Begin processing all nodes in the octree.
            ContourCellProc(octree.baseNode, indices);

            // Assign all relevant mesh data.
            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.triangles = indices.ToArray();
        }

        private static void GenerateVertexIndices(OctreeNode node, List<Vector3> verts, List<Vector3> norms)
        {
            if (node == null)
            {
                return;
            }

            if (node.type != OctreeNodeType.NODE_LEAF)
            {
                for (int i = 0; i < 8; i++)
                {
                    GenerateVertexIndices(node.children[i], verts, norms);
                }
            }

            if (node.type != OctreeNodeType.NODE_INTERNAL) {
                OctreeDrawInfo drawInfo = node.drawInfo;
                //if (drawInfo == null) {
                    //throw new NullReferenceException("Could not add vertex!  DrawInfo was null!");
                //}

                drawInfo.index = verts.Count;
                verts.Add(drawInfo.position);
                norms.Add(drawInfo.normal);

            }
        }

        /// <summary>
        /// Process all nodes depth first.
        /// </summary>
        /// <param name="node">The starting node.</param>
        /// <param name="ind">A list of indices.</param>
        private static void ContourCellProc(OctreeNode node, List<int> ind)
        {
            //Test if the node exists.
            if (node == null)
            {
                return;
            }

            // Test if the node is NODE_INTERNAL.
            if (node.type == OctreeNodeType.NODE_INTERNAL)
            {
                // Loop through and process all child nodes.
                for (int i = 0; i < 8; i++)
                {
                    ContourCellProc(node.children[i], ind);
                }

                for (int i = 0; i < 12; i++)
                {
                    OctreeNode[] faceNodes = new OctreeNode[2];
                    int[] c = {
                        cellProcFaceMask[i, 0],
                        cellProcFaceMask[i, 1]
                    };

                    faceNodes[0] = node.children[c[0]];
                    faceNodes[1] = node.children[c[1]];

                    ContourFaceProc(faceNodes, cellProcFaceMask[i, 2], ind);
                }

                for (int i = 0; i < 6; i++)
                {
                    OctreeNode[] edgeNodes = new OctreeNode[4];
                    int[] c = {
                        cellProcEdgeMask[i, 0],
                        cellProcEdgeMask[i, 1],
                        cellProcEdgeMask[i, 2],
                        cellProcEdgeMask[i, 3]
                    };

                    for (int j = 0; j < 4; j++)
                    {
                        edgeNodes[j] = node.children[c[j]];
                    }

                    ContourEdgeProc(edgeNodes, cellProcEdgeMask[i, 4], ind);
                }
            }
        }

        private static void ContourFaceProc(OctreeNode[] nodes, int dir, List<int> ind)
        {
            // Test if both nodes exist.
            if (nodes[0] == null || nodes[1] == null)
            {
                return;
            }

            // Test if either node is NODE_INTERNAL.
            if (nodes[0].type == OctreeNodeType.NODE_INTERNAL || nodes[1].type == OctreeNodeType.NODE_INTERNAL)
            {
                for (int i = 0; i < 4; i++)
                {
                    OctreeNode[] faceNodes = new OctreeNode[2];
                    int[] c = {
                        faceProcFaceMask[dir, i, 0],
                        faceProcFaceMask[dir, i, 1]
                    };

                    for (int j = 0; j < 2; j++)
                    {
                        if (nodes[j].type != OctreeNodeType.NODE_INTERNAL)
                        {
                            faceNodes[j] = nodes[j];
                        }
                        else
                        {
                            faceNodes[j] = nodes[j].children[c[j]];
                        }
                    }

                    ContourFaceProc(faceNodes, faceProcFaceMask[dir, i, 2], ind);
                }

                int[,] orders = {
                    { 0, 0, 1, 1 },
                    { 0, 1, 0, 1 }
                };

                for (int i = 0; i < 4; i++)
                {
                    OctreeNode[] edgeNodes = new OctreeNode[4];
                    int[] c = {
                        faceProcEdgeMask[dir, i, 1],
                        faceProcEdgeMask[dir, i, 2],
                        faceProcEdgeMask[dir, i, 3],
                        faceProcEdgeMask[dir, i, 4],
                    };

                    int index = faceProcEdgeMask[dir, i, 0];
                    for (int j = 0; j < 4; j++)
                    {
                        if (nodes[orders[index, j]].type == OctreeNodeType.NODE_LEAF || nodes[orders[index, j]].type == OctreeNodeType.NODE_PSUEDO)
                        {
                            edgeNodes[j] = nodes[orders[index, j]];
                        }
                        else
                        {
                            edgeNodes[j] = nodes[orders[index, j]].children[c[j]];
                        }
                    }

                    ContourEdgeProc(edgeNodes, faceProcEdgeMask[dir, i, 5], ind);
                }
            }
        }

        private static void ContourEdgeProc(OctreeNode[] nodes, int dir, List<int> ind)
        {
            // Test if any nodes are null.
            if (nodes[0] == null || nodes[1] == null || nodes[2] == null || nodes[3] == null)
            {
                return;
            }
            // Test if all nodes are not NODE_INTERNAL.
            else if (nodes[0].type != OctreeNodeType.NODE_INTERNAL && nodes[1].type != OctreeNodeType.NODE_INTERNAL && nodes[2].type != OctreeNodeType.NODE_INTERNAL && nodes[3].type != OctreeNodeType.NODE_INTERNAL)
            {
                ContourProcessEdge(nodes, dir, ind);
            }
            else
            {
                for (int i = 0; i < 2; i++)
                {
                    OctreeNode[] edgeNodes = new OctreeNode[4];
                    int[] c =
                    {
                        edgeProcEdgeMask[dir, i, 0],
                        edgeProcEdgeMask[dir, i, 1],
                        edgeProcEdgeMask[dir, i, 2],
                        edgeProcEdgeMask[dir, i, 3],
                    };

                    for (int j = 0; j < 4; j++)
                    {
                        if (nodes[j].type == OctreeNodeType.NODE_LEAF || nodes[j].type == OctreeNodeType.NODE_PSUEDO)
                        {
                            edgeNodes[j] = nodes[j];
                        }
                        else
                        {
                            edgeNodes[j] = nodes[j].children[c[j]];
                        }
                    }

                    ContourEdgeProc(edgeNodes, edgeProcEdgeMask[dir, i, 4], ind);
                }
            }
        }

        private static void ContourProcessEdge(OctreeNode[] nodes, int dir, List<int> ind)
        {
            float minSize = int.MaxValue;
            int minIndex = 0;
            int[] indices = { -1, -1, -1, -1 };
            bool flip = false;
            bool[] signChange = { false, false, false, false };

            for (int i = 0; i < 4; i++)
            {
                int edge = processEdgeMask[dir, i];
                int c1 = edgevmap[edge, 0];
                int c2 = edgevmap[edge, 1];

                int m1 = (nodes[i].drawInfo.corners >> c1) & 1;
                int m2 = (nodes[i].drawInfo.corners >> c2) & 1;

                if (nodes[i].size < minSize)
                {
                    minSize = nodes[i].size;
                    minIndex = i;
                    flip = m1 != Octree.MATERIAL_AIR;
                }

                indices[i] = nodes[i].drawInfo.index;

                signChange[i] =
                    (m1 == Octree.MATERIAL_AIR && m2 != Octree.MATERIAL_AIR) ||
                    (m1 != Octree.MATERIAL_AIR && m2 == Octree.MATERIAL_AIR);
            }

            if (signChange[minIndex])
            {
                if (!flip)
                {
                    ind.Add(indices[0]);
                    ind.Add(indices[1]);
                    ind.Add(indices[3]);

                    ind.Add(indices[0]);
                    ind.Add(indices[3]);
                    ind.Add(indices[2]);
                }
                else
                {
                    ind.Add(indices[0]);
                    ind.Add(indices[3]);
                    ind.Add(indices[1]);

                    ind.Add(indices[0]);
                    ind.Add(indices[2]);
                    ind.Add(indices[3]);
                }
            }
        }
    }
}
