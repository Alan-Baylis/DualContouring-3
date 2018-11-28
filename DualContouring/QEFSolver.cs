using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class QEFSolver
{
    public static int steps = 8;

    /// <summary>
    /// Aproximates the solution to the provided data.
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static QEFData Solve3D(QEFData data)
    {
        float increment = 1f / steps;

        data.error = float.MaxValue;

        for (float x = 0; x <= 1; x += increment)
        {
            for (float y = 0; y <= 1; y += increment)
            {
                for (float z = 0; z <= 1; z += increment)
                {
                    Vector3 point = new Vector3(x, y, z);

                    float error = GetError(data, point);

                    if (error < data.error) {
                        data.point = point;
                        data.error = error;
                    }
                }
            }
        }

        return data;
    }

    public static float GetError(QEFData data, Vector3 point) {
        float error = 0;

        for (int i = 0; i < data.positions.Length; i++)
        {
            Plane plane = new Plane(data.normals[i], data.positions[i]);
            error += Mathf.Pow(Mathf.Abs(plane.GetDistanceToPoint(point)), 2);
        }

        return error;
    }
}
