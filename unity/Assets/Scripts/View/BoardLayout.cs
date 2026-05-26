using System.Collections.Generic;
using UnityEngine;
using Dejarik;

namespace Dejarik.View
{
    // Board geometry in board-local units, ported from the web game's boardGeometry.ts. The whole board
    // is placed/scaled in AR space by a parent "board root" transform, so these stay in the original units
    // and all proportions match the browser version. Cells lie flat in the XZ plane at y = BaseTop.
    public static class BoardLayout
    {
        public const float RCenter = 0.85f;
        public static readonly float[] Inner = { 1.0f, 2.5f };
        public static readonly float[] Outer = { 2.6f, 4.45f };
        public const float Rim = 4.85f;
        public const float InnerR = (1.0f + 2.5f) / 2f;
        public const float OuterR = (2.6f + 4.45f) / 2f;
        public const float CellHalf = 14f * Mathf.Deg2Rad;
        public const float BaseTop = 0.15f;
        public const float PieceY = 0.155f;
        public const float PieceScale = 0.9f;   // standing piece height in board units

        // Board-local position of a space (on the play plane), matching pos3D() in the web game.
        public static Vector3 Pos3D(int space)
        {
            if (space == Board.Center) return new Vector3(0f, PieceY, 0f);
            bool inner = space <= 12;
            int ray = (space <= 12 ? space - 1 : space - 13) % Board.Rays;
            float phi = ray * Mathf.PI * 2f / Board.Rays;
            float r = inner ? InnerR : OuterR;
            return new Vector3(r * Mathf.Sin(phi), PieceY, -r * Mathf.Cos(phi));
        }

        // ---- procedural cell meshes (built directly in the XZ plane, +Y up) ----

        public static Mesh SectorMesh(float rIn, float rOut, int ray)
        {
            const int segs = 10;
            float phiMid = ray * Mathf.PI * 2f / Board.Rays;
            float a0 = phiMid - CellHalf, a1 = phiMid + CellHalf;
            var verts = new List<Vector3>();
            for (int i = 0; i <= segs; i++)
            {
                float a = a0 + (a1 - a0) * i / segs;
                verts.Add(new Vector3(Mathf.Sin(a) * rOut, 0f, -Mathf.Cos(a) * rOut));   // outer arc
                verts.Add(new Vector3(Mathf.Sin(a) * rIn, 0f, -Mathf.Cos(a) * rIn));     // inner arc
            }
            var tris = new List<int>();
            for (int i = 0; i < segs; i++)
            {
                int o0 = i * 2, in0 = i * 2 + 1, o1 = (i + 1) * 2, in1 = (i + 1) * 2 + 1;
                tris.Add(o0); tris.Add(o1); tris.Add(in0);
                tris.Add(in0); tris.Add(o1); tris.Add(in1);
            }
            return Build(verts, tris);
        }

        public static Mesh CircleMesh(float r)
        {
            const int segs = 48;
            var verts = new List<Vector3> { Vector3.zero };
            for (int i = 0; i <= segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Sin(a) * r, 0f, -Mathf.Cos(a) * r));
            }
            var tris = new List<int>();
            for (int i = 1; i <= segs; i++) { tris.Add(0); tris.Add(i); tris.Add(i + 1); }
            return Build(verts, tris);
        }

        public static Mesh RingMesh(float rIn, float rOut)
        {
            const int segs = 64;
            var verts = new List<Vector3>();
            for (int i = 0; i <= segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                float s = Mathf.Sin(a), c = -Mathf.Cos(a);
                verts.Add(new Vector3(s * rOut, 0f, c * rOut));
                verts.Add(new Vector3(s * rIn, 0f, c * rIn));
            }
            var tris = new List<int>();
            for (int i = 0; i < segs; i++)
            {
                int o0 = i * 2, in0 = i * 2 + 1, o1 = (i + 1) * 2, in1 = (i + 1) * 2 + 1;
                tris.Add(o0); tris.Add(o1); tris.Add(in0);
                tris.Add(in0); tris.Add(o1); tris.Add(in1);
            }
            return Build(verts, tris);
        }

        static Mesh Build(List<Vector3> verts, List<int> tris)
        {
            var m = new Mesh();
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            var normals = new Vector3[verts.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            m.SetNormals(normals);
            m.RecalculateBounds();
            return m;
        }
    }

    public enum CellRole { Move, Attack, Push, Selected, Center, Light, Dark }
}
