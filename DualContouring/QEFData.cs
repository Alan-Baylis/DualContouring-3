using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct QEFData
{
    /// <summary>
    /// An array of positions of zero crossing points.
    /// </summary>
    public Vector3[] positions;
    /// <summary>
    /// And array of normals for the points held in positions.
    /// </summary>
    public Vector3[] normals;
    /// <summary>
    /// The point calculated when this data is solved.
    /// </summary>
    public Vector3 point;
    /// <summary>
    /// The error squared of the solution to this data.
    /// </summary>
    public float error;
}
