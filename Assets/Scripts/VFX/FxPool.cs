using UnityEngine;

namespace PixelArena
{
    /// <summary>方块碎片 / 曳光弹 / 枪口火光 的对象池。</summary>
    public class FxPool : MonoBehaviour
    {
        public static FxPool Instance;
        public VoxelWorld World;

        private class Debris
        {
            public Transform t;
            public Renderer r;
            public Vector3 vel;
            public float life;
            public float maxLife;
            public float size;
            public bool active;
        }

        // 注意：此类名不能叫 Tracer，否则会与下面的 public void Tracer(...) 方法重名（CS0102）
        private class TracerSlot
        {
            public Transform t;
            public Renderer r;
            public float life;
            public bool active;
        }

        private const int DebrisCount = 180;
        private const int TracerCount = 28;
        private Debris[] debris;
        private TracerSlot[] tracers;
        private int debrisCursor, tracerCursor;

        public static FxPool Create(VoxelWorld world)
        {
            var go = new GameObject("FxPool");
            var fx = go.AddComponent<FxPool>();
            fx.World = world;
            fx.InitPools();
            Instance = fx;
            return fx;
        }

        private void InitPools()
        {
            debris = new Debris[DebrisCount];
            for (int i = 0; i < DebrisCount; i++)
            {
                var go = VoxelAssets.MakeBox("debris", transform, Vector3.zero, Vector3.one * 0.16f, Color.white);
                go.SetActive(false);
                debris[i] = new Debris { t = go.transform, r = go.GetComponent<Renderer>(), active = false };
            }
            tracers = new TracerSlot[TracerCount];
            for (int i = 0; i < TracerCount; i++)
            {
                var go = VoxelAssets.MakeBox("tracer", transform, Vector3.zero, new Vector3(0.05f, 0.05f, 1f), new Color(1f, 0.92f, 0.55f), VoxelAssets.Unlit);
                go.SetActive(false);
                tracers[i] = new TracerSlot { t = go.transform, r = go.GetComponent<Renderer>(), active = false };
            }
        }

        public void Burst(Vector3 pos, Color color, int count, float power = 1f)
        {
            for (int i = 0; i < count; i++)
            {
                var d = debris[debrisCursor];
                debrisCursor = (debrisCursor + 1) % DebrisCount;
                d.t.position = pos + Random.insideUnitSphere * 0.25f;
                d.t.rotation = Random.rotation;
                d.size = Random.Range(0.09f, 0.20f);
                d.t.localScale = Vector3.one * d.size;
                d.vel = new Vector3(Random.Range(-1f, 1f), Random.Range(0.6f, 1.6f), Random.Range(-1f, 1f)).normalized
                        * Random.Range(3f, 8f) * power;
                d.life = d.maxLife = Random.Range(0.7f, 1.4f);
                d.active = true;
                d.t.gameObject.SetActive(true);
                VoxelAssets.SetColor(d.r, color * Random.Range(0.85f, 1.15f));
            }
        }

        public void Tracer(Vector3 from, Vector3 to, Color color, float width = 0.05f)
        {
            var tr = tracers[tracerCursor];
            tracerCursor = (tracerCursor + 1) % TracerCount;
            Vector3 dir = to - from;
            float len = dir.magnitude;
            if (len < 0.01f) return;
            tr.t.position = from + dir * 0.5f;
            tr.t.rotation = Quaternion.LookRotation(dir.normalized);
            tr.t.localScale = new Vector3(width, width, len);
            tr.life = 0.05f;
            tr.active = true;
            tr.t.gameObject.SetActive(true);
            VoxelAssets.SetColor(tr.r, color);
        }

        public void Flash(Vector3 pos, Color color, float size = 0.22f)
        {
            var d = debris[debrisCursor];
            debrisCursor = (debrisCursor + 1) % DebrisCount;
            d.t.position = pos;
            d.t.rotation = Random.rotation;
            d.size = size;
            d.t.localScale = Vector3.one * size;
            d.vel = Vector3.zero;
            d.life = d.maxLife = 0.06f;
            d.active = true;
            d.t.gameObject.SetActive(true);
            VoxelAssets.SetColor(d.r, color);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = 0; i < DebrisCount; i++)
            {
                var d = debris[i];
                if (!d.active) continue;
                d.life -= dt;
                if (d.life <= 0f)
                {
                    d.active = false;
                    d.t.gameObject.SetActive(false);
                    continue;
                }
                d.vel.y -= 26f * dt;
                Vector3 next = d.t.position + d.vel * dt;
                if (World != null && World.IsSolidAt(Mathf.FloorToInt(next.x), Mathf.FloorToInt(next.y), Mathf.FloorToInt(next.z)))
                {
                    d.vel.Set(0f, 0f, 0f);
                }
                else
                {
                    d.t.position = next;
                }
                d.t.Rotate(d.vel.magnitude * dt * 60f, d.vel.magnitude * dt * 40f, 0f);
                float k = Mathf.Clamp01(d.life / d.maxLife);
                d.t.localScale = Vector3.one * d.size * (0.35f + 0.65f * k);
            }

            for (int i = 0; i < TracerCount; i++)
            {
                var tr = tracers[i];
                if (!tr.active) continue;
                tr.life -= dt;
                if (tr.life <= 0f)
                {
                    tr.active = false;
                    tr.t.gameObject.SetActive(false);
                    continue;
                }
                float k = tr.life / 0.05f;
                tr.t.localScale = new Vector3(tr.t.localScale.x * (0.6f + 0.4f * k), tr.t.localScale.y * (0.6f + 0.4f * k), tr.t.localScale.z);
            }
        }
    }
}
