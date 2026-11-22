using System;
using UnityEngine;

/* Spiral ramp geometry.
 *
 * A helix in the XZ plane: constant radius, rising by "Height" over "Turns"
 * full turns. Angle 0 is the start of the ramp and the angle grows up to
 * TotalAngle. Unlike a roundabout this curve is open on purpose - its two ends
 * sit at different heights, which is exactly what a ramp needs. The player
 * connects both ends to the road network afterwards.
 */
namespace RoundaboutBuilder.Tools
{
    public class HelixPath
    {
        public Vector3 Center;
        public float Radius;
        public float Height;
        public float Turns;
        public bool Reverse;
        public float StartAngle;

        public HelixPath(Vector3 center, float radius, float height, float turns, bool reverse, float startAngle)
        {
            Center = center;
            Radius = Math.Max(radius, 1f);
            Height = height;
            Turns = Math.Max(turns, 0.1f);
            Reverse = reverse;
            StartAngle = startAngle;
        }

        public float TotalAngle
        {
            get { return 2f * (float)Math.PI * Turns; }
        }

        /* +1 goes counter-clockwise in game coordinates, -1 the other way. */
        private float Direction
        {
            get { return Reverse ? -1f : 1f; }
        }

        public float RisePerRadian
        {
            get { return Height / TotalAngle; }
        }

        /* Length of the ramp measured flat (the 3D length is slightly longer). */
        public float ArcLengthOnPlan
        {
            get { return 2f * (float)Math.PI * Radius * Turns; }
        }

        /* Grade as a fraction: 0.05 means 5 %. */
        public float Grade
        {
            get { return ArcLengthOnPlan <= 0.01f ? 0f : Height / ArcLengthOnPlan; }
        }

        public Vector3 PositionAt(float angle)
        {
            float theta = StartAngle + Direction * angle;
            float rise = RisePerRadian * angle;
            return new Vector3(
                Center.x + Radius * (float)Math.Cos(theta),
                Center.y + rise,
                Center.z + Radius * (float)Math.Sin(theta));
        }

        /* Normalized direction of travel at the given angle. */
        public Vector3 TangentAt(float angle)
        {
            float theta = StartAngle + Direction * angle;
            Vector3 tangent = new Vector3(
                -Radius * (float)Math.Sin(theta) * Direction,
                RisePerRadian,
                Radius * (float)Math.Cos(theta) * Direction);
            return tangent.normalized;
        }

        /* How many segments we need so that the chord between two neighbouring
         * nodes stays around targetChord. The count is the angle divided by the
         * step, so it grows and shrinks together with the radius - radius 30 at one
         * turn gives six segments. A segment never bends more than 90 degrees, which
         * is where the "at least four per turn" floor comes from. */
        public static int SegmentCount(float radius, float totalAngle, float targetChord)
        {
            if (radius <= 0.01f)
                return 4;

            float ratio = targetChord / (2f * radius);
            if (ratio > 1f) ratio = 1f;

            float stepAngle = 2f * (float)Math.Asin(ratio);
            if (stepAngle > Mathf.PI / 2f) stepAngle = Mathf.PI / 2f;

            return Mathf.Clamp(Mathf.RoundToInt(totalAngle / stepAngle), 2, 256);
        }

        public static float ChordLength(float radius, float stepAngle)
        {
            return 2f * radius * (float)Math.Sin(stepAngle / 2f);
        }
    }
}
