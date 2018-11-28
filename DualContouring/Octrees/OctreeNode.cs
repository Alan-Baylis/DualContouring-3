using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Drok.DualContouring.Octrees
{
    public class OctreeNode
    {

        public OctreeNodeType type;
        public Vector3 position;
        public int size;

        public OctreeDrawInfo drawInfo;

        public OctreeNode[] children = new OctreeNode[8];

        public OctreeNode()
        {
            type = OctreeNodeType.NODE_NONE;
            position = Vector3.zero;
            size = 1;
        }

        public OctreeNode Clone()
        {
            OctreeNode clone = new OctreeNode();

            clone.type = type;
            clone.position = position;
            clone.size = size;
            clone.drawInfo = drawInfo.Clone();

            return clone;
        }
    }
}
