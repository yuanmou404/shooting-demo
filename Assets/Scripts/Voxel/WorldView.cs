using System.Collections.Generic;
using UnityEngine;

namespace PixelArena
{
    /// <summary>
    /// 体素世界的渲染层：管理区块 GameObject、破坏/放置方块、瞄准高亮框。
    /// </summary>
    public class WorldView : MonoBehaviour
    {
        public VoxelWorld World { get; private set; }
        public Material BlockMaterial { get; private set; }

        private readonly Dictionary<long, GameObject> chunks = new Dictionary<long, GameObject>();
        private readonly List<long> dirty = new List<long>();
        private Transform chunkRoot;
        private LineRenderer highlight;
        private int rebuildBudgetPerFrame = 3;

        public void Build(VoxelWorld world, Texture2D atlas)
        {
            World = world;
            BlockMaterial = CreateBlockMaterial(atlas);

            chunkRoot = new GameObject("Chunks").transform;
            chunkRoot.SetParent(transform, false);

            int cs = ChunkMesher.ChunkSize;
            int nx = Mathf.CeilToInt(world.SX / (float)cs);
            int ny = Mathf.CeilToInt(world.SY / (float)cs);
            int nz = Mathf.CeilToInt(world.SZ / (float)cs);
            for (int cy = 0; cy < ny; cy++)
                for (int cz = 0; cz < nz; cz++)
                    for (int cx = 0; cx < nx; cx++)
                        BuildChunk(cx, cy, cz);

            // 瞄准高亮框
            var hlGO = new GameObject("BlockHighlight");
            hlGO.transform.SetParent(transform, false);
            highlight = hlGO.AddComponent<LineRenderer>();
            highlight.useWorldSpace = true;
            highlight.material = VoxelAssets.NewMaterial("Shaders/VoxelUnlit", "PixelArena/VoxelUnlit");
            highlight.startColor = new Color(0, 0, 0, 0.9f);
            highlight.endColor = new Color(0, 0, 0, 0.9f);
            highlight.startWidth = 0.035f;
            highlight.endWidth = 0.035f;
            highlight.positionCount = 0;
            highlight.enabled = false;
        }

        private Material CreateBlockMaterial(Texture2D atlas)
        {
            // 必须走 VoxelAssets（Resources 优先）——打包后 Shader.Find 拿不到未被引用的自定义着色器
            Material mat = VoxelAssets.NewMaterial("Shaders/VoxelBlocks", "PixelArena/VoxelBlocks");
            if (mat == null)
            {
                Debug.LogError("[PixelArena] 方块材质创建失败，世界将不可见。");
                return null;
            }
            if (atlas != null && mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", atlas);
            VoxelAssets.SetVec(mat, "_SunDir", new Vector4(0.45f, 0.82f, 0.35f, 0f).normalized);
            VoxelAssets.SetCol(mat, "_SunColor", new Color(1.05f, 1.0f, 0.92f, 1f));
            VoxelAssets.SetCol(mat, "_Ambient", new Color(0.42f, 0.45f, 0.52f, 1f));
            VoxelAssets.SetCol(mat, "_FogColor", new Color(0.66f, 0.78f, 0.92f, 1f));
            VoxelAssets.SetVec(mat, "_FogParams", new Vector4(45f, 165f, 0f, 0f));
            VoxelAssets.SetF(mat, "_FogStrength", 0.85f);
            return mat;
        }

        /// <summary>换地图时同步换一套环境光 / 雾 / 阳光配色。</summary>
        public void ApplyLook(Color ambient, Color sun, Color fog)
        {
            if (BlockMaterial == null) return;
            VoxelAssets.SetCol(BlockMaterial, "_Ambient", ambient);
            VoxelAssets.SetCol(BlockMaterial, "_SunColor", sun);
            VoxelAssets.SetCol(BlockMaterial, "_FogColor", fog);
        }

        private long Key(int cx, int cy, int cz) => ((long)cx << 42) | ((long)cy << 21) | (long)cz;

        /// <summary>整体重建（重开一局时恢复被破坏的地形）。</summary>
        public void RebuildAll()
        {
            dirty.Clear();
            int cs = ChunkMesher.ChunkSize;
            int nx = Mathf.CeilToInt(World.SX / (float)cs);
            int ny = Mathf.CeilToInt(World.SY / (float)cs);
            int nz = Mathf.CeilToInt(World.SZ / (float)cs);
            for (int cy = 0; cy < ny; cy++)
                for (int cz = 0; cz < nz; cz++)
                    for (int cx = 0; cx < nx; cx++)
                        BuildChunk(cx, cy, cz);
        }

        private void BuildChunk(int cx, int cy, int cz)
        {
            int cs = ChunkMesher.ChunkSize;
            Mesh mesh = ChunkMesher.Build(World, cx * cs, cy * cs, cz * cs, cs);
            long key = Key(cx, cy, cz);
            if (mesh == null)
            {
                if (chunks.TryGetValue(key, out GameObject old) && old != null) Destroy(old);
                chunks.Remove(key);
                return;
            }
            GameObject go;
            if (!chunks.TryGetValue(key, out go) || go == null)
            {
                go = new GameObject("Chunk_" + cx + "_" + cy + "_" + cz);
                go.transform.SetParent(chunkRoot, false);
                go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.material = BlockMaterial;
                chunks[key] = go;
            }
            var mf = go.GetComponent<MeshFilter>();
            if (mf.sharedMesh != null) Destroy(mf.sharedMesh);
            mf.sharedMesh = mesh;
        }

        private void LateUpdate()
        {
            int n = Mathf.Min(rebuildBudgetPerFrame, dirty.Count);
            for (int i = 0; i < n; i++)
            {
                long key = dirty[0];
                dirty.RemoveAt(0);
                int cx = (int)(key >> 42);
                int cy = (int)((key >> 21) & 0x1FFFFF);
                int cz = (int)(key & 0x1FFFFF);
                BuildChunk(cx, cy, cz);
            }
        }

        public void MarkDirtyCell(int x, int y, int z)
        {
            int cs = ChunkMesher.ChunkSize;
            int cx = Mathf.FloorToInt(x / (float)cs);
            int cy = Mathf.FloorToInt(y / (float)cs);
            int cz = Mathf.FloorToInt(z / (float)cs);
            long key = Key(cx, cy, cz);
            if (!dirty.Contains(key)) dirty.Add(key);
            // 边界方块会影响邻居区块
            int lx = x - cx * cs, ly = y - cy * cs, lz = z - cz * cs;
            if (lx == 0 && cx > 0) AddDirty(Key(cx - 1, cy, cz));
            if (lx == cs - 1) AddDirty(Key(cx + 1, cy, cz));
            if (ly == 0 && cy > 0) AddDirty(Key(cx, cy - 1, cz));
            if (ly == cs - 1) AddDirty(Key(cx, cy + 1, cz));
            if (lz == 0 && cz > 0) AddDirty(Key(cx, cy, cz - 1));
            if (lz == cs - 1) AddDirty(Key(cx, cy, cz + 1));
        }

        private void AddDirty(long key)
        {
            if (!dirty.Contains(key)) dirty.Add(key);
        }

        /// <summary>射击破坏方块。返回是否破坏成功。</summary>
        public bool DamageBlock(Vector3Int cell, float amount, out byte brokenId)
        {
            brokenId = 0;
            if (World == null) return false;
            bool broke = World.Damage(cell.x, cell.y, cell.z, amount, out brokenId);
            MarkDirtyCell(cell.x, cell.y, cell.z);
            if (broke) World.MarkNavDirty(cell.x, cell.z);   // 打通/挖塌会改变可走性
            return broke;
        }

        /// <summary>在命中面外侧放置方块。</summary>
        public bool PlaceBlock(Vector3Int cell, Vector3Int normal, byte id, Vector3 playerMin, Vector3 playerMax)
        {
            if (World == null) return false;
            Vector3Int target = cell + normal;
            if (!World.InBounds(target.x, target.y, target.z)) return false;
            if (World.Get(target.x, target.y, target.z) != (byte)BlockId.Air) return false;

            // 不允许把方块放进玩家身体里
            var min = new Vector3(target.x, target.y, target.z);
            var max = min + Vector3.one;
            if (max.x > playerMin.x && min.x < playerMax.x &&
                max.y > playerMin.y && min.y < playerMax.y &&
                max.z > playerMin.z && min.z < playerMax.z) return false;

            World.Set(target.x, target.y, target.z, id);
            MarkDirtyCell(target.x, target.y, target.z);
            World.MarkNavDirty(target.x, target.z);          // 放了一块砖，路可能就被堵死了
            return true;
        }

        public void SetHighlight(Vector3Int cell)
        {
            if (highlight == null) return;
            if (World == null || World.Get(cell.x, cell.y, cell.z) == (byte)BlockId.Air)
            {
                highlight.enabled = false;
                return;
            }
            Vector3 p = new Vector3(cell.x, cell.y, cell.z);
            float e = 0.002f;
            Vector3 a = p + new Vector3(-e, -e, -e);
            Vector3 b = p + new Vector3(1 + e, -e, -e);
            Vector3 c = p + new Vector3(1 + e, -e, 1 + e);
            Vector3 d = p + new Vector3(-e, -e, 1 + e);
            Vector3 a2 = p + new Vector3(-e, 1 + e, -e);
            Vector3 b2 = p + new Vector3(1 + e, 1 + e, -e);
            Vector3 c2 = p + new Vector3(1 + e, 1 + e, 1 + e);
            Vector3 d2 = p + new Vector3(-e, 1 + e, 1 + e);

            var pts = new Vector3[24];
            int i = 0;
            void Seg(Vector3 s, Vector3 t) { pts[i++] = s; pts[i++] = t; }
            Seg(a, b); Seg(b, c); Seg(c, d); Seg(d, a);
            Seg(a2, b2); Seg(b2, c2); Seg(c2, d2); Seg(d2, a2);
            Seg(a, a2); Seg(b, b2); Seg(c, c2); Seg(d, d2);

            highlight.positionCount = 24;
            highlight.SetPositions(pts);
            highlight.enabled = true;
        }

        public void HideHighlight()
        {
            if (highlight != null) highlight.enabled = false;
        }
    }
}
