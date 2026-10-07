using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PixelArena
{
    /// <summary>
    /// 区块网格生成器：只生成暴露面，烘焙面亮度 + 环境光遮蔽(AO) 到顶点色。
    /// </summary>
    public static class ChunkMesher
    {
        public const int ChunkSize = 16;

        // 六个面：顺序 +X, -X, +Y, -Y, +Z, -Z
        // 顶点顺序保证 cross(v1-v0, v2-v0) 等于外法线（Unity 正面绕序）
        private static readonly int[][] FaceVerts = new int[][]
        {
            new int[] { 1,0,1,  1,0,0,  1,1,0,  1,1,1 }, // +X
            new int[] { 0,0,0,  0,0,1,  0,1,1,  0,1,0 }, // -X
            new int[] { 0,1,1,  1,1,1,  1,1,0,  0,1,0 }, // +Y
            new int[] { 0,0,0,  1,0,0,  1,0,1,  0,0,1 }, // -Y
            new int[] { 0,0,1,  1,0,1,  1,1,1,  0,1,1 }, // +Z
            new int[] { 1,0,0,  0,0,0,  0,1,0,  1,1,0 }, // -Z
        };

        private static readonly Vector3Int[] FaceNormal = new Vector3Int[]
        {
            new Vector3Int( 1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int( 0, 1, 0),
            new Vector3Int( 0,-1, 0),
            new Vector3Int( 0, 0, 1),
            new Vector3Int( 0, 0,-1),
        };

        private static readonly int[] FaceUAxis = { 2, 2, 0, 0, 0, 0 };
        private static readonly int[] FaceVAxis = { 1, 1, 2, 2, 1, 1 };
        private static readonly float[] FaceLight = { 0.78f, 0.78f, 1.0f, 0.55f, 0.88f, 0.88f };

        private static readonly float[] AoTable = { 0.46f, 0.66f, 0.83f, 1.0f };

        public static Mesh Build(VoxelWorld w, int ox, int oy, int oz, int size)
        {
            var verts = new List<Vector3>(4096);
            var norms = new List<Vector3>(4096);
            var uvs = new List<Vector2>(4096);
            var cols = new List<Color32>(4096);
            var tris = new List<int>(8192);

            float pad = 0.5f / BlockDef.AtlasSize;

            for (int ly = 0; ly < size; ly++)
            {
                int y = oy + ly;
                if (y >= w.SY) break;
                for (int lz = 0; lz < size; lz++)
                {
                    int z = oz + lz;
                    for (int lx = 0; lx < size; lx++)
                    {
                        int x = ox + lx;
                        byte id = w.Get(x, y, z);
                        if (id == (byte)BlockId.Air) continue;

                        for (int f = 0; f < 6; f++)
                        {
                            Vector3Int n = FaceNormal[f];
                            if (w.IsOpaqueAt(x + n.x, y + n.y, z + n.z)) continue;

                            int tile = f == 2 ? BlockDef.TileTop(id) : (f == 3 ? BlockDef.TileBottom(id) : BlockDef.TileSide(id));
                            int col = tile % BlockDef.Cols;
                            int row = tile / BlockDef.Cols;
                            float u0 = (col * BlockDef.Tile) / (float)BlockDef.AtlasSize + pad;
                            float u1 = ((col + 1) * BlockDef.Tile) / (float)BlockDef.AtlasSize - pad;
                            float v0 = (row * BlockDef.Tile) / (float)BlockDef.AtlasSize + pad;
                            float v1 = ((row + 1) * BlockDef.Tile) / (float)BlockDef.AtlasSize - pad;

                            int[] fv = FaceVerts[f];
                            int ua = FaceUAxis[f], va = FaceVAxis[f];
                            float baseLight = FaceLight[f];

                            int baseIdx = verts.Count;
                            int ao0 = 0, ao1 = 0, ao2 = 0, ao3 = 0;

                            for (int k = 0; k < 4; k++)
                            {
                                float vx = x + fv[k * 3 + 0];
                                float vy = y + fv[k * 3 + 1];
                                float vz = z + fv[k * 3 + 2];
                                verts.Add(new Vector3(vx, vy, vz));
                                norms.Add(new Vector3(n.x, n.y, n.z));

                                float su = fv[k * 3 + ua] * 2 - 1;
                                float sv = fv[k * 3 + va] * 2 - 1;
                                Vector3Int s1 = n, s2 = n, cr = n;
                                s1[ua] += (int)su;
                                s2[va] += (int)sv;
                                cr[ua] += (int)su;
                                cr[va] += (int)sv;

                                int o1 = w.IsOpaqueAt(x + s1.x, y + s1.y, z + s1.z) ? 1 : 0;
                                int o2 = w.IsOpaqueAt(x + s2.x, y + s2.y, z + s2.z) ? 1 : 0;
                                int oc = w.IsOpaqueAt(x + cr.x, y + cr.y, z + cr.z) ? 1 : 0;
                                int ao = (o1 == 1 && o2 == 1) ? 0 : 3 - (o1 + o2 + oc);
                                if (k == 0) ao0 = ao; else if (k == 1) ao1 = ao; else if (k == 2) ao2 = ao; else ao3 = ao;

                                float lum = baseLight * AoTable[ao];
                                cols.Add(new Color32((byte)(lum * 255f), (byte)(lum * 255f), (byte)(lum * 255f), 255));
                            }

                            uvs.Add(new Vector2(u0, v0));
                            uvs.Add(new Vector2(u1, v0));
                            uvs.Add(new Vector2(u1, v1));
                            uvs.Add(new Vector2(u0, v1));

                            // AO 插值修正：选择更合理的对角线
                            if (ao0 + ao2 > ao1 + ao3)
                            {
                                tris.Add(baseIdx + 0); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
                                tris.Add(baseIdx + 0); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
                            }
                            else
                            {
                                tris.Add(baseIdx + 1); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
                                tris.Add(baseIdx + 1); tris.Add(baseIdx + 3); tris.Add(baseIdx + 0);
                            }
                        }
                    }
                }
            }

            if (tris.Count == 0) return null;

            var mesh = new Mesh();
            mesh.name = "ChunkMesh";
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
