using System;
using System.Collections.Generic;
using System.Linq;

namespace Dejarik
{
    public enum Ring { Center, Inner, Outer }

    public readonly struct SpacePoint
    {
        public readonly int Space;
        public readonly double X;       // -1..1 unit square, 0 = center
        public readonly double Y;
        public readonly double AngleDeg;
        public readonly double Radius;
        public SpacePoint(int space, double x, double y, double angleDeg, double radius)
        {
            Space = space; X = x; Y = y; AngleDeg = angleDeg; Radius = radius;
        }
    }

    // Board layout: 25 spaces. 0 = center hub; 1..12 = inner ring (ray i = id-1); 13..24 = outer ring
    // (ray i = id-13). Center is adjacent to all 12 inner spaces. A ring space connects to its two orbit
    // neighbours (same ring) and the same-ray space in the neighbouring ring. Center is NOT adjacent to
    // the outer ring; diagonals between rings are not allowed.
    public static class Board
    {
        public const int Center = 0;
        public const int Rays = 12;
        public const int SpaceCount = 25;

        const double InnerRadius = 0.42;
        const double OuterRadius = 0.78;

        public static readonly int[][] Adjacency = ComputeAdjacency();

        public static Ring RingOf(int space)
        {
            if (space == Center) return Ring.Center;
            return space <= 12 ? Ring.Inner : Ring.Outer;
        }

        // -1 for center (no ray), else 0..11.
        public static int RayOf(int space)
        {
            if (space == Center) return -1;
            return space <= 12 ? space - 1 : space - 13;
        }

        public static int InnerId(int ray) => 1 + ((ray % Rays) + Rays) % Rays;
        public static int OuterId(int ray) => 13 + ((ray % Rays) + Rays) % Rays;

        public static int[] Neighbors(int space) => Adjacency[space];

        public static bool AreAdjacent(int a, int b) => Adjacency[a].Contains(b);

        static int[][] ComputeAdjacency()
        {
            var adj = new List<int>[SpaceCount];
            for (int i = 0; i < SpaceCount; i++) adj[i] = new List<int>();

            // center <-> every inner space
            for (int i = 0; i < Rays; i++)
            {
                adj[Center].Add(InnerId(i));
                adj[InnerId(i)].Add(Center);
            }

            for (int i = 0; i < Rays; i++)
            {
                int inner = InnerId(i);
                int outer = OuterId(i);
                adj[inner].Add(InnerId(i + 1)); adj[inner].Add(InnerId(i - 1));   // orbit neighbours
                adj[outer].Add(OuterId(i + 1)); adj[outer].Add(OuterId(i - 1));
                adj[inner].Add(outer);                                            // same ray, neighbouring ring
                adj[outer].Add(inner);
            }

            return adj.Select(list => list.Distinct().OrderBy(x => x).ToArray()).ToArray();
        }

        // Polar geometry for rendering. Angles in degrees, 0 = top, clockwise.
        public static SpacePoint Point(int space)
        {
            if (space == Center) return new SpacePoint(space, 0, 0, 0, 0);
            int r = RayOf(space);
            double angleDeg = r * 360.0 / Rays;
            double radius = RingOf(space) == Ring.Inner ? InnerRadius : OuterRadius;
            double rad = (angleDeg - 90.0) * (Math.PI / 180.0);
            return new SpacePoint(space, radius * Math.Cos(rad), radius * Math.Sin(rad), angleDeg, radius);
        }
    }
}
