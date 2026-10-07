using UnityEngine;

namespace PixelArena
{
    /// <summary>共享材质 / 基础几何体工具（全部运行时创建，无需预制体）。</summary>
    public static class VoxelAssets
    {
        private static Material solidMat;
        private static Material unlitMat;
        private static Mesh cubeMesh;

        // ------------------------------------------------------------------
        // 着色器解析
        // 打包后 Shader.Find 对于"没有任何场景/材质资产引用"的自定义着色器会返回 null，
        // 直接 new Material(null) 会抛异常并中断整个初始化（表现为进游戏只有天空、什么都没有）。
        // 因此统一从 Resources 目录读取 —— Resources 里的资源一定会被打进包。
        // ------------------------------------------------------------------

        public static Shader ResolveShader(string resourcePath, string shaderName)
        {
            Shader sh = Resources.Load<Shader>(resourcePath);
            if (sh == null) sh = Shader.Find(shaderName);
            if (sh == null) sh = Shader.Find("Legacy Shaders/Diffuse");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null)
            {
                Material builtin = Resources.GetBuiltinResource<Material>("Default-Diffuse.mat");
                if (builtin != null) sh = builtin.shader;
            }
            if (sh == null) Debug.LogError("[PixelArena] 找不到着色器：" + shaderName + " (" + resourcePath + ")");
            return sh;
        }

        /// <summary>创建材质，保证不返回 null，打包后也不会因空着色器抛异常。</summary>
        public static Material NewMaterial(string resourcePath, string shaderName)
        {
            Shader sh = ResolveShader(resourcePath, shaderName);
            if (sh != null) return new Material(sh);
            Material builtin = Resources.GetBuiltinResource<Material>("Default-Diffuse.mat");
            return builtin != null ? new Material(builtin) : null;
        }

        // 安全设参：着色器缺失或退化为内置着色器时不会刷错误日志
        public static void SetVec(Material m, string n, Vector4 v) { if (m != null && m.HasProperty(n)) m.SetVector(n, v); }
        public static void SetCol(Material m, string n, Color c) { if (m != null && m.HasProperty(n)) m.SetColor(n, c); }
        public static void SetF(Material m, string n, float f) { if (m != null && m.HasProperty(n)) m.SetFloat(n, f); }

        public static Material Solid
        {
            get
            {
                if (solidMat == null)
                {
                    solidMat = NewMaterial("Shaders/VoxelSolid", "PixelArena/VoxelSolid");
                    SetVec(solidMat, "_SunDir", new Vector4(0.45f, 0.82f, 0.35f, 0f).normalized);
                    SetCol(solidMat, "_SunColor", new Color(1.05f, 1.0f, 0.92f, 1f));
                    SetCol(solidMat, "_Ambient", new Color(0.45f, 0.48f, 0.55f, 1f));
                    SetCol(solidMat, "_FogColor", new Color(0.66f, 0.78f, 0.92f, 1f));
                    SetVec(solidMat, "_FogParams", new Vector4(45f, 165f, 0f, 0f));
                    SetF(solidMat, "_FogStrength", 0.85f);
                }
                return solidMat;
            }
        }

        public static Material Unlit
        {
            get
            {
                if (unlitMat == null)
                {
                    unlitMat = NewMaterial("Shaders/VoxelUnlit", "PixelArena/VoxelUnlit");
                    SetVec(unlitMat, "_FogParams", new Vector4(45f, 165f, 0f, 0f));
                    SetF(unlitMat, "_FogStrength", 0f);
                }
                return unlitMat;
            }
        }

        public static Mesh Cube
        {
            get
            {
                if (cubeMesh == null)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cubeMesh = go.GetComponent<MeshFilter>().sharedMesh;
                    Object.Destroy(go);
                }
                return cubeMesh;
            }
        }

        /// <summary>创建一个纯色方块（不生成碰撞体）。</summary>
        public static GameObject MakeBox(string name, Transform parent, Vector3 localPos, Vector3 size, Color color, Material mat = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = Cube;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat != null ? mat : Solid;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            SetColor(mr, color);
            return go;
        }

        private static readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

        public static void SetColor(Renderer r, Color c)
        {
            if (r == null) return;
            r.GetPropertyBlock(block);
            block.SetColor("_Color", c);
            r.SetPropertyBlock(block);
        }

        /// <summary>带枢轴点的肢体（枢轴在顶端，便于摆动）。</summary>
        public static Transform MakeLimb(Transform parent, Vector3 pivot, Vector3 size, Vector3 offsetFromPivot, Color color, Material mat = null)
        {
            var pivotGo = new GameObject("pivot");
            pivotGo.transform.SetParent(parent, false);
            pivotGo.transform.localPosition = pivot;
            var box = MakeBox("limb", pivotGo.transform, offsetFromPivot, size, color, mat);
            // 缩放会继承父级，这里通过局部 scale 直接给出绝对尺寸
            box.transform.localScale = size;
            return pivotGo.transform;
        }
    }
}
