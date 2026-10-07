using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelArena
{
    /// <summary>
    /// 程序化生成的 8-bit 音效（不依赖任何音频文件）+ 播放池。
    /// </summary>
    public static class AudioKit
    {
        private const int PoolSize = 14;
        private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private static AudioSource[] pool;
        private static int cursor;
        private static bool ready;

        public static void Init()
        {
            if (ready) return;
            ready = true;
            clips.Clear();

            clips["rifle"] = Gen("rifle", 0.16f, t =>
            {
                float nz = Mathf.Sin(t * 2400f) * Mathf.Sin(t * 331f);
                float body = Mathf.Sin(t * 780f) * 0.5f;
                return (nz * 0.5f + body) * Mathf.Exp(-t * 17f) * 0.55f;
            });

            clips["shotgun"] = Gen("shotgun", 0.34f, t =>
            {
                float nz = Mathf.Sin(t * 1500f) * Mathf.Sin(t * 197f);
                float boom = Mathf.Sin(t * 260f) * 0.6f;
                return (nz * 0.55f + boom) * Mathf.Exp(-t * 8f) * 0.6f;
            });

            clips["sniper"] = Gen("sniper", 0.55f, t =>
            {
                float crack = Mathf.Sin(t * 3200f) * Mathf.Exp(-t * 22f);
                float tail = Mathf.Sin(t * 420f) * Mathf.Exp(-t * 4f) * 0.35f;
                return (crack + tail) * 0.55f;
            });

            clips["hit"] = Gen("hit", 0.07f, t => Mathf.Sin(t * 2600f) * Mathf.Exp(-t * 26f) * 0.35f);
            clips["headshot"] = Gen("headshot", 0.16f, t => Mathf.Sin(t * 1800f + Mathf.Sin(t * 40f) * 6f) * Mathf.Exp(-t * 12f) * 0.4f);

            clips["block"] = Gen("block", 0.26f, t =>
            {
                float nz = Mathf.Sin(t * 900f) * Mathf.Sin(t * 137f);
                return nz * Mathf.Exp(-t * 9f) * 0.4f;
            });

            clips["place"] = Gen("place", 0.09f, t => Mathf.Sin(t * 1200f) * Mathf.Exp(-t * 20f) * 0.3f);

            clips["reload"] = Gen("reload", 0.5f, t =>
            {
                if (t < 0.42f) return Mathf.Sin(t * 3000f) * Mathf.Exp(-t * 40f) * 0.3f;
                return Mathf.Sin((t - 0.42f) * 4000f) * Mathf.Exp(-(t - 0.42f) * 45f) * 0.32f;
            });

            clips["die"] = Gen("die", 0.5f, t =>
            {
                float f = 900f - t * 700f;
                return Mathf.Sin(t * f * 6f) * Mathf.Exp(-t * 5f) * 0.35f;
            });

            clips["hurt"] = Gen("hurt", 0.28f, t => Mathf.Sin(t * 320f) * Mathf.Exp(-t * 8f) * 0.4f);
            clips["jump"] = Gen("jump", 0.12f, t => Mathf.Sin(t * 700f + t * t * 900f) * Mathf.Exp(-t * 12f) * 0.22f);
            clips["land"] = Gen("land", 0.12f, t => Mathf.Sin(t * 300f) * Mathf.Exp(-t * 16f) * 0.25f);
            clips["empty"] = Gen("empty", 0.06f, t => Mathf.Sin(t * 2200f) * Mathf.Exp(-t * 40f) * 0.22f);
            clips["wave"] = Gen("wave", 0.7f, t =>
            {
                float f = 300f + t * 500f;
                return Mathf.Sin(t * f * 6f) * Mathf.Exp(-t * 3f) * 0.3f;
            });
            clips["pistol"] = Gen("pistol", 0.12f, t =>
                (Mathf.Sin(t * 900f) * 0.6f + Mathf.Sin(t * 7300f) * 0.4f) * Mathf.Exp(-t * 26f) * 0.6f);
            clips["knife"] = Gen("knife", 0.16f, t =>
                Mathf.Sin(t * 320f + Mathf.Sin(t * 90f) * 4f) * Mathf.Exp(-t * 18f) * 0.3f);
            clips["heal"] = Gen("heal", 0.34f, t =>
                Mathf.Sin(t * (700f + t * 900f)) * Mathf.Exp(-t * 7f) * 0.35f);
            clips["ui"] = Gen("ui", 0.09f, t => Mathf.Sin(t * 1800f) * Mathf.Exp(-t * 22f) * 0.25f);
            // 爆炸（轰炸 / 坦克炮 / 油罐）：低频轰 + 噪声尾
            clips["explode"] = Gen("explode", 0.8f, t =>
            {
                float boom = Mathf.Sin(t * 140f) * Mathf.Exp(-t * 5f);
                float nz = Mathf.Sin(t * 1300f) * Mathf.Sin(t * 211f) * Mathf.Exp(-t * 9f);
                return (boom * 0.7f + nz * 0.35f) * 0.85f;
            });
            // 喷火器的"呼"声（低频气流）
            clips["flame"] = Gen("flame", 0.3f, t =>
                Mathf.Sin(t * 480f + Mathf.Sin(t * 130f) * 5f) * Mathf.Sin(t * 0.6f * Mathf.PI) * 0.3f);
            // 敌人发现玩家时的提示音（两声上扬的短促哨音）
            clips["spot"] = Gen("spot", 0.34f, t =>
            {
                float f = 1200f + t * 900f;
                return Mathf.Sin(t * f * 6f) * Mathf.Exp(-t * 6f) * 0.26f;
            });

            var host = new GameObject("AudioPool");
            GameObject.DontDestroyOnLoad(host);
            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var src = host.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f;
                src.dopplerLevel = 0f;
                src.minDistance = 4f;
                src.maxDistance = 70f;
                src.rolloffMode = AudioRolloffMode.Linear;
                pool[i] = src;
            }
        }

        private static AudioClip Gen(string name, float dur, Func<float, float> wave)
        {
            const int rate = 44100;
            int n = Mathf.Max(16, (int)(rate * dur));
            var clip = AudioClip.Create(name, n, 1, rate, false);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                data[i] = Mathf.Clamp(wave(t), -1f, 1f);
            }
            clip.SetData(data, 0);
            return clip;
        }

        public static void Play(string name, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            if (!ready) return;
            AudioClip clip;
            if (!clips.TryGetValue(name, out clip) || clip == null) return;
            var src = pool[cursor];
            cursor = (cursor + 1) % PoolSize;
            src.transform.position = pos;
            src.pitch = pitch;
            src.volume = Mathf.Clamp01(volume * GameSettings.SfxVolume);
            src.spatialBlend = 1f;
            src.PlayOneShot(clip);
        }

        public static void Play2D(string name, float volume = 1f, float pitch = 1f)
        {
            if (!ready) return;
            AudioClip clip;
            if (!clips.TryGetValue(name, out clip) || clip == null) return;
            var src = pool[cursor];
            cursor = (cursor + 1) % PoolSize;
            src.pitch = pitch;
            src.volume = Mathf.Clamp01(volume * GameSettings.SfxVolume);
            src.spatialBlend = 0f;
            src.PlayOneShot(clip);
        }
    }
}
