using UnityEngine;

namespace Drok.DualContouring.Octrees
{
    /// <summary>
    /// Holds info on how this node should be drawn.
    /// </summary>
    public class OctreeDrawInfo
    {
        /// <summary>
        /// The position of the vertex associated with this node.
        /// </summary>
        public Vector3 position;
        /// <summary>
        /// The normal of the vertex associated with this node.
        /// </summary>
        public Vector3 normal;
        /// <summary>
        /// The index of the vertex associated with this node.
        /// </summary>
        public int index;
        /// <summary>
        /// A number representing what corners are within the shape.
        /// </summary>
        public int corners;

        public OctreeDrawInfo Clone() {
            OctreeDrawInfo clone = new OctreeDrawInfo();

            clone.position = position;
            clone.normal = normal;
            clone.index = index;
            clone.corners = corners;

            return clone;
        }
    }
}
