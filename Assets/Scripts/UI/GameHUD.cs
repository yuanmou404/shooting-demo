using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PixelArena
{
    /// <summary>全部 UI 均由代码创建：HUD（含小地图）、主菜单、暂停、死亡、操作说明、设置。</summary>
    public class GameHUD : MonoBehaviour
    {
        public Canvas Canvas;
        public GameManager Gm;

        // ---------------- 配色 ----------------
        private static readonly Color cBackdrop = new Color(0.045f, 0.065f, 0.10f, 0.95f);
        private static readonly Color cScrim = new Color(0.02f, 0.03f, 0.05f, 0.62f);
        private static readonly Color cPanel = new Color(0.09f, 0.12f, 0.18f, 0.62f);
        private static readonly Color cSlot = new Color(0.04f, 0.06f, 0.09f, 0.78f);
        private static readonly Color cGold = new Color(1.00f, 0.83f, 0.36f);
        private static readonly Color cCyan = new Color(0.45f, 0.88f, 0.95f);
        private static readonly Color cText = new Color(0.93f, 0.95f, 0.99f);
        private static readonly Color cMuted = new Color(0.64f, 0.70f, 0.80f);
        private static readonly Color cGreen = new Color(0.20f, 0.62f, 0.36f);
        private static readonly Color cBlue = new Color(0.22f, 0.40f, 0.62f);
        private static readonly Color cRed = new Color(0.62f, 0.24f, 0.24f);
        private static readonly Color cDanger = new Color(0.92f, 0.28f, 0.24f);

        private Font fontXs, fontSm, fontMd, fontLg, fontXl;

        // HUD
        private Text hpValue, ammoValue, weaponName, scoreValue, waveValue, enemyValue, fpsText, hintText, huntText;
        private Text mapText;
        private Image hpFill, hpGhost, damageOverlay, hitmarker;
        private float hpGhost01 = 1f;        // 血条残影（受伤后缓慢下滑，看得见掉了多少）
        private const float HpBarW = 420f;
        private Image scopeOverlay;      // 狙击开镜时的镜筒遮罩
        private GameObject crosshair;
        private Text announceText, killFeedText, deathScoreText;
        private GameObject hudPanel, menuPanel, pausePanel, deathPanel, settingsPanel, helpPanel;
        private Button deathRestartButton, deathMenuButton;

        // 设置项文本
        private Text resValue, fsValue, fpsValue, pixelValue, pxScaleValue, sensValue, volumeValue, invertValue, autoFireValue, showFpsValue;
        // 暂停界面统计
        private Text pauseScore, pauseKills, pauseWave, pauseLeft;

        // 右侧武器栏：上一把 / 当前 / 下一把（滚轮切换时直接看得见会切到哪）
        private const int WeaponRows = 3;
        private const float WeaponBarX = 800f;
        private readonly Image[] weaponRowBg = new Image[WeaponRows];
        private readonly Text[] weaponRowKey = new Text[WeaponRows];
        private readonly Text[] weaponRowName = new Text[WeaponRows];
        private int weaponBarSlot = -1;

        // ---------------- 小队：积分 / 队友 / 召唤面板 / 坦克 ----------------
        private Text squadValue, callInPoints, tempWeaponText;
        private readonly Text[] allyRows = new Text[3];
        private GameObject squadCard, squadTip;
        private GameObject callInPanel, tankPanel;
        private readonly Text[] callInCost = new Text[4];
        private readonly Button[] callInBtn = new Button[4];
        private Image tankFill;
        private Text tankText;
        private GameObject modePanel;      // 模式选择：小队模式 / 单人突击模式
        private int tankBarSlot = -1;      // 坦克武器栏缓存（和人物武器栏分开，免得互相覆盖）

        // ---------------- 坦克舱内视角（第一人称）专用 UI ----------------
        private GameObject cockpitPanel;        // 观察窗遮罩（舱内金属框）
        private Image tankReticle;              // 炮镜准星（舱内专用，替换普通十字准星）
        private Text cockpitWeapon, cockpitHint, cockpitArmor;

        // ---------------- 小地图 / 找敌人 ----------------
        private const float MapSize = 208f;
        private const int MaxMapDots = 24;
        private RawImage mapImage;
        private RectTransform mapDotRoot;
        private Image[] mapDots;
        private TriangleGraphic[] mapAllies;   // 小地图上队友的青色箭头
        private TriangleGraphic[] mapAllyBacks;
        private TriangleGraphic mapPlayer;
        private Texture2D mapTex;
        private int mapSX, mapSZ;
        private bool mapReady;

        private RectTransform[] arrowRts;
        private TriangleGraphic[] arrows;
        private bool huntMode;
        private const float ArrowRx = 400f;
        private const float ArrowRy = 230f;

        private float hitmarkerTimer;
        private float damageAlpha;
        private bool damageIsHull;      // true = 车体挨打（橙），false = 人物掉血（红）
        private float announceTimer;
        private float killTimer;
        private float hintTimer;
        private float fpsTimer, fpsAccum;
        private int fpsFrames;
        private readonly List<string> killQueue = new List<string>();

        // ---------------------------------------------------------- 创建

        public static GameHUD Create(GameManager gm)
        {
            var go = new GameObject("GameHUD");
            var hud = go.AddComponent<GameHUD>();
            hud.Gm = gm;
            hud.Build();
            return hud;
        }

        private void Build()
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            Canvas = canvasGo.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            fontXs = MakeFont(22);
            fontSm = MakeFont(28);
            fontMd = MakeFont(36);
            fontLg = MakeFont(56);
            fontXl = MakeFont(88);

            BuildHud(canvasGo.transform);
            BuildMenu(canvasGo.transform);
            BuildPause(canvasGo.transform);
            BuildDeath(canvasGo.transform);
            BuildHelp(canvasGo.transform);
            BuildSettings(canvasGo.transform);
            BuildCallIn(canvasGo.transform);
            BuildModeSelect(canvasGo.transform);

            if (Gm != null && Gm.World != null) SetMapName(Gm.World.MapName);
            SetPlaying(false);
        }

        private Font MakeFont(int size)
        {
            string[] names = { "Microsoft YaHei", "Microsoft YaHei UI", "PingFang SC", "Noto Sans CJK SC", "Noto Sans SC", "Source Han Sans SC", "Droid Sans Fallback", "Roboto", "Arial", "Helvetica", "sans-serif" };
            Font f = Font.CreateDynamicFontFromOSFont(names, size);
            if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f;
        }

        // ---------------------------------------------------------- 基础控件

        private static void Anchor(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>
        /// 按容器宽度自动缩小字号，保证文字不会超出方框
        /// （CJK 按 1 个字宽、西文按 0.56 个字宽估算，够用且不依赖字体是否已加载）。
        /// </summary>
        private static void FitText(Text t, float maxWidth, int maxSize = 200)
        {
            if (t == null) return;
            string s = t.text;
            if (string.IsNullOrEmpty(s)) return;

            float worst = 0f, line = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\n') { if (line > worst) worst = line; line = 0f; continue; }
                line += (c >= '\u2E80') ? 1.0f : 0.56f;
            }
            if (line > worst) worst = line;
            if (worst < 0.01f) return;

            int size = Mathf.FloorToInt(maxWidth / worst);
            if (size < 9) size = 9;
            if (size > maxSize) size = maxSize;
            t.fontSize = size;

            var ol = t.GetComponent<Outline>();
            if (ol != null) ol.effectDistance = new Vector2(size * 0.07f, -size * 0.07f);
        }

        private Text Label(Transform parent, string name, string content, int size, Color color, TextAnchor anchor, Font font, float outlineDist)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font != null ? font : fontSm;
            t.fontSize = size;
            t.color = color;
            t.text = content;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(outlineDist, -outlineDist);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1000f, size * 2f);
            return t;
        }

        private Text Label(Transform parent, string name, string content, int size, Color color, TextAnchor anchor, Font font)
        {
            return Label(parent, name, content, size, color, anchor, font, 2f);
        }

        private RectTransform Card(Transform parent, string name, Vector2 size, Vector2 pos, Color border)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = cPanel;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = border;
            outline.effectDistance = new Vector2(3f, -3f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        private RectTransform Slot(Transform parent, string name, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = cSlot;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
            outline.effectDistance = new Vector2(2f, -2f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        private Image NewImage(Transform parent, string name, Color color, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return img;
        }

        /// <summary>把血条（/残影）设成从左端起、宽度占 t 的比例。</summary>
        private static void SetBarFill(Image img, float t)
        {
            if (img == null) return;
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(HpBarW * Mathf.Clamp01(t), 0f);
        }

        /// <summary>血条颜色：满血绿 → 半血橙 → 残血红（连续渐变，不再是硬跳三档）。</summary>
        private static Color HpColor(float t)
        {
            Color low = new Color(1.00f, 0.24f, 0.18f);
            Color mid = new Color(0.95f, 0.68f, 0.20f);
            Color high = new Color(0.36f, 0.80f, 0.44f);
            return t > 0.5f ? Color.Lerp(mid, high, (t - 0.5f) / 0.5f) : Color.Lerp(low, mid, t / 0.5f);
        }

        private TriangleGraphic MakeArrow(Transform parent, string name, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<TriangleGraphic>();
            g.color = color;
            g.raycastTarget = false;
            Anchor(g.rectTransform, 0f, 0f, size, size);
            return g;
        }

        private RectTransform FullPanel(Transform parent, string name, Color color, bool active)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.SetActive(active);
            return rt;
        }

        private Button MakeButton(Transform parent, string name, string text, Vector2 pos, Vector2 size, Color bg,
            UnityEngine.Events.UnityAction onClick, int fontSize, Font font)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = bg;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            outline.effectDistance = new Vector2(2f, -2f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var cb = btn.colors;
            cb.normalColor = bg;
            cb.highlightedColor = new Color(Mathf.Min(1f, bg.r * 1.45f + 0.08f), Mathf.Min(1f, bg.g * 1.45f + 0.08f), Mathf.Min(1f, bg.b * 1.45f + 0.08f), 1f);
            cb.pressedColor = new Color(bg.r * 0.7f, bg.g * 0.7f, bg.b * 0.7f, 1f);
            cb.selectedColor = bg;
            cb.disabledColor = new Color(0.28f, 0.28f, 0.3f, 0.7f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            var label = Label(go.transform, "Label", text, fontSize, Color.white, TextAnchor.MiddleCenter, font);
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            FitText(label, size.x - 28f, fontSize);

            btn.onClick.AddListener(() => AudioKit.Play2D("ui", 0.6f));
            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }

        private Button MakeButton(Transform parent, string name, string text, Vector2 pos, Vector2 size, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            return MakeButton(parent, name, text, pos, size, bg, onClick, 34, fontMd);
        }

        /// <summary>设置项：[-] [值] [+]（colX = 列中心相对卡片中心的偏移）</summary>
        private Text MakeStepRow(Transform parent, float colX, float y, string label, out Button minus, out Button plus)
        {
            Text l = Label(parent, "Row_" + label, label, 28, cMuted, TextAnchor.MiddleLeft, fontSm);
            Anchor(l.rectTransform, colX - 250f, y, 250f, 60f);
            FitText(l, 240f, 28);

            var box = Slot(parent, "Box_" + label, new Vector2(250f, 56f), new Vector2(colX + 30f, y));
            Text val = Label(box, "V", "", 24, cText, TextAnchor.MiddleCenter, fontMd);
            Anchor(val.rectTransform, 0f, 0f, 250f, 56f);

            minus = MakeButton(parent, "Minus_" + label, "-", new Vector2(colX + 195f, y), new Vector2(56f, 56f), new Color(0.28f, 0.33f, 0.42f), null, 32, fontMd);
            plus = MakeButton(parent, "Plus_" + label, "+", new Vector2(colX + 261f, y), new Vector2(56f, 56f), new Color(0.28f, 0.33f, 0.42f), null, 32, fontMd);
            return val;
        }

        private Text MakeToggleRow(Transform parent, float colX, float y, string label, out Button toggle)
        {
            Text l = Label(parent, "Row_" + label, label, 28, cMuted, TextAnchor.MiddleLeft, fontSm);
            Anchor(l.rectTransform, colX - 250f, y, 250f, 60f);
            FitText(l, 240f, 28);

            var box = Slot(parent, "Box_" + label, new Vector2(250f, 56f), new Vector2(colX + 30f, y));
            Text val = Label(box, "V", "", 24, cText, TextAnchor.MiddleCenter, fontMd);
            Anchor(val.rectTransform, 0f, 0f, 250f, 56f);

            toggle = MakeButton(parent, "Toggle_" + label, "切换 CHANGE", new Vector2(colX + 215f, y), new Vector2(210f, 56f), cBlue, null, 26, fontSm);
            return val;
        }

        private void SectionTitle(Transform parent, float colX, float y, string text, Color color)
        {
            var t = Label(parent, "Sec_" + text, text, 26, color, TextAnchor.MiddleLeft, fontSm);
            Anchor(t.rectTransform, colX - 250f, y, 460f, 44f);
            FitText(t, 450f, 26);
            var line = NewImage(parent, "Line_" + text, new Color(color.r, color.g, color.b, 0.30f), new Vector2(560f, 3f), new Vector2(colX + 30f, y - 30f));
            line.rectTransform.SetAsFirstSibling();
        }

        /// <summary>小统计块（暂停界面用）</summary>
        private Text Stat(Transform parent, string label, float x, float y, out Text value)
        {
            var t = Label(parent, "StatL_" + label, label, 24, cMuted, TextAnchor.MiddleCenter, fontXs);
            Anchor(t.rectTransform, x, y, 340f, 40f);
            FitText(t, 330f, 24);
            value = Label(parent, "StatV_" + label, "0", 46, cText, TextAnchor.MiddleCenter, fontLg);
            Anchor(value.rectTransform, x, y - 46f, 340f, 64f);
            return value;
        }

        // ---------------------------------------------------------- HUD

        private void BuildHud(Transform root)
        {
            hudPanel = new GameObject("HudPanel");
            hudPanel.transform.SetParent(root, false);
            var rt = hudPanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            // ---- 受伤红屏（最底层）
            damageOverlay = NewImage(hudPanel.transform, "DamageOverlay", new Color(0.8f, 0.05f, 0.05f, 0f), Vector2.zero, Vector2.zero);
            var drt = damageOverlay.rectTransform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            drt.SetAsFirstSibling();

            // ---- 准星（加黑描边保证任何背景下都看得见）
            var ch = new GameObject("Crosshair");
            crosshair = ch;
            ch.transform.SetParent(hudPanel.transform, false);
            var chRt = ch.AddComponent<RectTransform>();
            chRt.anchorMin = new Vector2(0.5f, 0.5f); chRt.anchorMax = new Vector2(0.5f, 0.5f);
            chRt.sizeDelta = new Vector2(60f, 60f);
            for (int i = 0; i < 4; i++)
            {
                var bar = new GameObject("bar" + i);
                bar.transform.SetParent(ch.transform, false);
                var img = bar.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0.92f);
                img.raycastTarget = false;
                var ol = bar.AddComponent<Outline>();
                ol.effectColor = new Color(0f, 0f, 0f, 0.9f);
                ol.effectDistance = new Vector2(2f, -2f);
                var brt = bar.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.5f, 0.5f); brt.anchorMax = new Vector2(0.5f, 0.5f);
                bool vertical = i < 2;
                brt.sizeDelta = vertical ? new Vector2(4f, 11f) : new Vector2(11f, 4f);
                float d = 11f;
                brt.anchoredPosition = vertical ? new Vector2(0f, i == 0 ? d : -d) : new Vector2(i == 2 ? -d : d, 0f);
            }

            // ---- 狙击镜遮罩：圆外全黑、圆内带十字分划。只有狙击开镜时显示。
            scopeOverlay = NewImage(hudPanel.transform, "ScopeOverlay", Color.white, Vector2.zero, Vector2.zero);
            var scrt = scopeOverlay.rectTransform;
            scrt.anchorMin = Vector2.zero; scrt.anchorMax = Vector2.one;
            scrt.offsetMin = Vector2.zero; scrt.offsetMax = Vector2.zero;
            scopeOverlay.raycastTarget = false;
            scopeOverlay.sprite = MakeScopeSprite(256);
            scopeOverlay.gameObject.SetActive(false);

            // ---- 命中标记
            hitmarker = NewImage(hudPanel.transform, "Hitmarker", new Color(1f, 1f, 1f, 0f), new Vector2(34f, 34f), Vector2.zero);
            hitmarker.raycastTarget = false;
            hitmarker.gameObject.SetActive(false);
            for (int i = 0; i < 2; i++)
            {
                var hm = new GameObject("hm" + i);
                hm.transform.SetParent(hitmarker.transform, false);
                var img = hm.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0.95f);
                img.raycastTarget = false;
                img.rectTransform.sizeDelta = new Vector2(30f, 5f);
                img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
            }

            // ---- 左下：生命（“HP + 细血条 + 数字”）
            // 空槽只用一层很淡的底色 + 黑描边，血量掉一半时不会再糊出一块灰板；
            // 后面那条半透明残影会延迟下滑，能直观看出这一下掉了多少血。
            var hpTag = Label(hudPanel.transform, "HpTag", "HP", 26, cMuted, TextAnchor.MiddleLeft, fontXs);
            Anchor(hpTag.rectTransform, -925f, -478f, 70f, 40f);

            const float barH = 26f;
            var hpBarBg = NewImage(hudPanel.transform, "HpBarBg", new Color(0.02f, 0.04f, 0.07f, 0.42f),
                new Vector2(HpBarW, barH), new Vector2(-712f, -478f));
            hpBarBg.raycastTarget = false;
            var hpOutline = hpBarBg.gameObject.AddComponent<Outline>();
            hpOutline.effectColor = new Color(0f, 0f, 0f, 0.65f);
            hpOutline.effectDistance = new Vector2(2f, -2f);

            hpGhost = NewImage(hpBarBg.transform, "HpGhost", new Color(1f, 0.45f, 0.32f, 0.5f), Vector2.zero, Vector2.zero);
            hpGhost.raycastTarget = false;
            SetBarFill(hpGhost, 1f);

            hpFill = NewImage(hpBarBg.transform, "HpFill", new Color(0.35f, 0.78f, 0.42f, 1f), Vector2.zero, Vector2.zero);
            hpFill.raycastTarget = false;
            SetBarFill(hpFill, 1f);

            // 25 / 50 / 75% 刻度
            for (int i = 1; i <= 3; i++)
            {
                var tick = NewImage(hpBarBg.transform, "HpTick" + i, new Color(0f, 0f, 0f, 0.40f),
                    new Vector2(2f, barH), new Vector2(-HpBarW * 0.5f + HpBarW * i * 0.25f, 0f));
                tick.raycastTarget = false;
            }

            hpValue = Label(hudPanel.transform, "HpValue", "100", 40, Color.white, TextAnchor.MiddleCenter, fontMd, 3f);
            Anchor(hpValue.rectTransform, -450f, -478f, 130f, 60f);
            hpValue.raycastTarget = false;

            // ---- 右下：弹药（纯文字 + 描边，不再用大卡片）
            ammoValue = Label(hudPanel.transform, "AmmoValue", "30 / 210", 52, new Color(1f, 0.90f, 0.45f), TextAnchor.MiddleRight, fontLg, 3f);
            Anchor(ammoValue.rectTransform, 700f, -470f, 420f, 74f);
            ammoValue.raycastTarget = false;
            weaponName = Label(hudPanel.transform, "WeaponName", "步枪 RIFLE", 26, cText, TextAnchor.MiddleRight, fontSm);
            Anchor(weaponName.rectTransform, 700f, -410f, 420f, 44f);
            weaponName.raycastTarget = false;

            // ---- 召唤武器倒计时（只在拿到喷火器 / 加特林时出现）
            tempWeaponText = Label(hudPanel.transform, "TempWeapon", "", 26, new Color(1f, 0.72f, 0.35f), TextAnchor.MiddleRight, fontSm, 2f);
            Anchor(tempWeaponText.rectTransform, 700f, -352f, 420f, 44f);
            tempWeaponText.raycastTarget = false;
            tempWeaponText.gameObject.SetActive(false);

            // ---- 顶部：分数 / 波次 / 剩余敌人
            var topCard = Card(hudPanel.transform, "TopCard", new Vector2(660f, 110f), new Vector2(0f, 470f), new Color(0.6f, 0.8f, 1f, 0.5f));
            var scoreLabel = Label(topCard, "ScoreL", "得分 SCORE", 22, cMuted, TextAnchor.MiddleCenter, fontXs);
            Anchor(scoreLabel.rectTransform, -215f, 28f, 230f, 32f);
            FitText(scoreLabel, 225f, 22);
            scoreValue = Label(topCard, "Score", "0", 40, cGold, TextAnchor.MiddleCenter, fontMd);
            Anchor(scoreValue.rectTransform, -215f, -16f, 230f, 54f);

            var waveLabel = Label(topCard, "WaveL", "波次 WAVE", 22, cMuted, TextAnchor.MiddleCenter, fontXs);
            Anchor(waveLabel.rectTransform, 0f, 28f, 230f, 32f);
            FitText(waveLabel, 225f, 22);
            waveValue = Label(topCard, "Wave", "第 1 波", 32, cText, TextAnchor.MiddleCenter, fontMd);
            Anchor(waveValue.rectTransform, 0f, -16f, 230f, 50f);

            var enemyLabel = Label(topCard, "EnemyL", "剩余敌人 LEFT", 22, cMuted, TextAnchor.MiddleCenter, fontXs);
            Anchor(enemyLabel.rectTransform, 215f, 28f, 230f, 32f);
            FitText(enemyLabel, 225f, 22);
            enemyValue = Label(topCard, "Enemies", "0", 32, new Color(1f, 0.62f, 0.55f), TextAnchor.MiddleCenter, fontMd);
            Anchor(enemyValue.rectTransform, 215f, -16f, 230f, 50f);

            // ---- 地图名（顶部卡片下方一行小字）
            mapText = Label(hudPanel.transform, "MapName", "地图 · 草原林场", 24, cMuted, TextAnchor.MiddleCenter, fontXs);
            Anchor(mapText.rectTransform, 0f, 392f, 700f, 38f);
            mapText.raycastTarget = false;
            FitText(mapText, 680f, 24);

            // ---- “最后几名敌人”提示
            huntText = Label(hudPanel.transform, "HuntText", "", 30, new Color(1f, 0.72f, 0.35f), TextAnchor.MiddleCenter, fontSm, 3f);
            Anchor(huntText.rectTransform, 0f, 330f, 900f, 50f);
            huntText.gameObject.SetActive(false);
            huntText.raycastTarget = false;

            // ---- 中央提示
            announceText = Label(hudPanel.transform, "Announce", "", 64, cGold, TextAnchor.MiddleCenter, fontXl, 3f);
            Anchor(announceText.rectTransform, 0f, 130f, 1500f, 140f);
            announceText.raycastTarget = false;

            // ---- 击杀提示（右侧中部，避开小地图）
            killFeedText = Label(hudPanel.transform, "KillFeed", "", 30, new Color(1f, 0.9f, 0.6f), TextAnchor.UpperRight, fontSm);
            killFeedText.rectTransform.anchorMin = new Vector2(1f, 1f);
            killFeedText.rectTransform.anchorMax = new Vector2(1f, 1f);
            killFeedText.rectTransform.anchoredPosition = new Vector2(-26f, -420f);
            killFeedText.rectTransform.sizeDelta = new Vector2(620f, 200f);
            killFeedText.raycastTarget = false;

            // ---- 帧率（放在小地图正下方）
            fpsText = Label(hudPanel.transform, "Fps", "", 26, cCyan, TextAnchor.MiddleCenter, fontXs);
            Anchor(fpsText.rectTransform, 790f, 145f, 300f, 40f);
            fpsText.raycastTarget = false;

            // ---- 开局操作提示
            hintText = Label(hudPanel.transform, "Hint", "", 26, cMuted, TextAnchor.MiddleCenter, fontXs);
            Anchor(hintText.rectTransform, 0f, -300f, 1600f, 50f);
            hintText.raycastTarget = false;

            // ---- 右上角暂停按钮（移动端没有 Esc）
            MakeButton(hudPanel.transform, "BtnPause", "II", new Vector2(890f, 490f), new Vector2(96f, 96f), new Color(0.16f, 0.20f, 0.28f), () =>
            {
                if (Gm != null && Gm.State == GameState.Playing) Gm.Pause();
            }, 40, fontMd);

            // ---- 右侧武器栏（滚轮会上/下切到哪一把，一眼可见）
            BuildWeaponBar();

            // ---- 左侧：小队积分 + 三个队友的状态
            BuildSquad(hudPanel.transform);

            // ---- 坦克 HUD（只在车里显示）
            BuildTankHud(hudPanel.transform);

            // ---- 小地图
            BuildMinimap();

            // ---- 屏外敌人指示箭头
            arrowRts = new RectTransform[6];
            arrows = new TriangleGraphic[6];
            for (int i = 0; i < arrows.Length; i++)
            {
                var a = MakeArrow(hudPanel.transform, "HuntArrow" + i, 28f, new Color(1f, 0.38f, 0.26f));
                a.gameObject.SetActive(false);
                arrowRts[i] = a.rectTransform;
                arrows[i] = a;
            }

            // ---- 坦克舱内视角 UI（最后建 = 在最上层，能盖住其它 HUD）
            BuildCockpit(hudPanel.transform);
        }

        // ---------------------------------------------------------- 右侧武器栏

        /// <summary>
        /// 竖排三行：上一把（暗）/ 当前（金框高亮）/ 下一把（暗）。
        /// 每格左边是数字键编号，右边是武器名 —— 滚轮往哪个方向切、会切到哪把，不用再记。
        /// </summary>
        private void BuildWeaponBar()
        {
            float[] ys = { -78f, -148f, -218f };
            for (int i = 0; i < WeaponRows; i++)
            {
                bool cur = i == 1;
                var row = Slot(hudPanel.transform, "WeaponRow" + i, new Vector2(300f, 62f), new Vector2(WeaponBarX, ys[i]));
                var img = row.GetComponent<Image>();
                img.color = cur ? new Color(0.08f, 0.11f, 0.16f, 0.80f) : new Color(0.05f, 0.07f, 0.11f, 0.42f);
                img.raycastTarget = false;
                var ol = row.GetComponent<Outline>();
                ol.effectColor = cur ? new Color(1f, 0.83f, 0.36f, 0.85f) : new Color(0f, 0f, 0f, 0.40f);
                ol.effectDistance = new Vector2(cur ? 3f : 2f, cur ? -3f : -2f);
                weaponRowBg[i] = img;

                // 左侧：数字键编号
                var keyBg = NewImage(row, "SlotKey", cur ? new Color(0.98f, 0.80f, 0.36f, 0.92f) : new Color(0.26f, 0.30f, 0.38f, 0.65f),
                    new Vector2(42f, 42f), new Vector2(-122f, 0f));
                keyBg.raycastTarget = false;
                var keyText = Label(keyBg.transform, "SlotKeyText", "1", 26,
                    cur ? new Color(0.06f, 0.06f, 0.09f) : new Color(0.62f, 0.68f, 0.78f),
                    TextAnchor.MiddleCenter, fontXs, 1.5f);
                Anchor(keyText.rectTransform, 0f, 0f, 46f, 46f);
                keyText.raycastTarget = false;
                weaponRowKey[i] = keyText;

                // 右侧：武器名
                var name = Label(row, "SlotName", "", 26, cur ? cText : new Color(0.58f, 0.64f, 0.74f),
                    TextAnchor.MiddleLeft, fontSm);
                Anchor(name.rectTransform, 25f, 0f, 220f, 52f);
                name.raycastTarget = false;
                weaponRowName[i] = name;
            }

            // 底部小字：怎么切
            var tip = Label(hudPanel.transform, "WeaponBarTip", "滚轮 / Q 切换", 22, cMuted, TextAnchor.MiddleCenter, fontXs);
            Anchor(tip.rectTransform, WeaponBarX, -258f, 300f, 34f);
            tip.raycastTarget = false;
            FitText(tip, 260f, 22);
        }

        /// <summary>当前槽位变了才重写三行文本（武器名是静态的，没必要每帧刷）。</summary>
        private void UpdateWeaponBar()
        {
            if (Gm == null) return;
            if (weaponRowBg[1] == null) return;
            if (weaponRowName[0] == null || weaponRowName[1] == null || weaponRowName[2] == null) return;

            // 人在坦克里：这一栏换成车裁的两件家伙（主炮 / 同轴机枪）
            if (Gm.TankMounted && Gm.Tank != null)
            {
                int tn = TankController.WeaponCount;
                int tcur = Gm.Tank.WeaponIndex;
                if (tcur == tankBarSlot) return;
                tankBarSlot = tcur;
                weaponBarSlot = -1;         // 下车后强制重刷人物武器栏
                int[] ts = { (tcur - 1 + tn) % tn, tcur, (tcur + 1) % tn };
                for (int i = 0; i < WeaponRows; i++)
                {
                    weaponRowKey[i].text = (ts[i] + 1).ToString();
                    string tn2 = TankController.SlotName(ts[i]);
                    if (weaponRowName[i].text != tn2)
                    {
                        weaponRowName[i].text = tn2;
                        FitText(weaponRowName[i], 208f, 26);
                    }
                }
                return;
            }

            if (Gm.Weapons == null) return;
            int n = Gm.Weapons.WeaponCount;
            if (n <= 0) return;

            int cur = Gm.Weapons.DisplaySlot;
            if (cur == weaponBarSlot) return;
            weaponBarSlot = cur;

            int[] slots = { (cur - 1 + n) % n, cur, (cur + 1) % n };
            for (int i = 0; i < WeaponRows; i++)
            {
                int s = slots[i];
                weaponRowKey[i].text = (s + 1).ToString();
                string t = Gm.Weapons.SlotName(s);
                if (weaponRowName[i].text != t)
                {
                    weaponRowName[i].text = t;
                    FitText(weaponRowName[i], 208f, 26);
                }
            }
        }

        // ---------------------------------------------------------- 坦克舱内视角 UI

        /// <summary>
        /// 舱内第一人称专用 UI：观察窗金属框（中间挖空）+ 炮镜准星 + 舱内状态字。
        /// 只在 V 切进舱内视角时显示，车外视角完全不出现。
        /// </summary>
        private void BuildCockpit(Transform root)
        {
            var shell = new GameObject("Cockpit");
            shell.transform.SetParent(root, false);
            var srt = shell.AddComponent<RectTransform>();
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            cockpitPanel = shell;

            // 观察窗：中间这块是透明的，四周用四块钢板围起来
            float winW = 1360f, winH = 620f, winY = 20f;
            float topY = winY + winH * 0.5f;      // 窗上沿
            float botY = winY - winH * 0.5f;      // 窗下沿
            Color steel = new Color(0.12f, 0.14f, 0.11f, 0.96f);
            Color steelLit = new Color(0.20f, 0.22f, 0.17f, 0.96f);

            // 上块覆盖 [topY, 540]，下块覆盖 [-540, botY]，左右两块补上两侧的窗框
            var top = NewImage(shell.transform, "CkTop", steel, new Vector2(1920f, 540f - topY), new Vector2(0f, (540f + topY) * 0.5f));
            top.raycastTarget = false;
            var bot = NewImage(shell.transform, "CkBot", steel, new Vector2(1920f, botY + 540f), new Vector2(0f, (-540f + botY) * 0.5f));
            bot.raycastTarget = false;
            var lf = NewImage(shell.transform, "CkLeft", steel, new Vector2(960f - winW * 0.5f, winH), new Vector2(-(960f + winW * 0.5f) * 0.5f, winY));
            lf.raycastTarget = false;
            var rt2 = NewImage(shell.transform, "CkRight", steel, new Vector2(960f - winW * 0.5f, winH), new Vector2((960f + winW * 0.5f) * 0.5f, winY));
            rt2.raycastTarget = false;

            // 窗口内圈的一圈亮边 + 铆钉（纯装饰，让"这是从舱里往外看"这件事一眼看出来）
            float edge = 6f;
            var e1 = NewImage(shell.transform, "CkEdgeT", steelLit, new Vector2(winW + edge * 2f, edge), new Vector2(0f, topY + edge * 0.5f));
            var e2 = NewImage(shell.transform, "CkEdgeB", steelLit, new Vector2(winW + edge * 2f, edge), new Vector2(0f, botY - edge * 0.5f));
            var e3 = NewImage(shell.transform, "CkEdgeL", steelLit, new Vector2(edge, winH + edge * 2f), new Vector2(-winW * 0.5f - edge * 0.5f, winY));
            var e4 = NewImage(shell.transform, "CkEdgeR", steelLit, new Vector2(edge, winH + edge * 2f), new Vector2(winW * 0.5f + edge * 0.5f, winY));
            e1.raycastTarget = e2.raycastTarget = e3.raycastTarget = e4.raycastTarget = false;

            Color rivet = new Color(0.30f, 0.31f, 0.26f, 0.95f);
            for (int i = 0; i < 9; i++)
            {
                float x = -winW * 0.5f + (winW / 8f) * i;
                var ra = NewImage(shell.transform, "RivetT" + i, rivet, new Vector2(10f, 10f), new Vector2(x, topY + 22f));
                var rb = NewImage(shell.transform, "RivetB" + i, rivet, new Vector2(10f, 10f), new Vector2(x, botY - 22f));
                ra.raycastTarget = false; rb.raycastTarget = false;
            }

            // 炮镜准星（舱内专用，和普通十字准星二选一）
            var ret = NewImage(shell.transform, "TankReticle", new Color(1f, 1f, 1f, 0.95f), new Vector2(300f, 300f), new Vector2(0f, winY));
            ret.raycastTarget = false;
            ret.sprite = MakeTankReticleSprite(192);
            tankReticle = ret;

            // 舱内状态字：左下装甲、下沿武器状态、左上操作提示
            cockpitArmor = Label(shell.transform, "CkArmor", "装甲 ARMOR", 22, new Color(0.95f, 0.78f, 0.42f),
                TextAnchor.MiddleLeft, fontXs);
            Anchor(cockpitArmor.rectTransform, -620f, -430f, 560f, 36f);
            cockpitArmor.raycastTarget = false;

            cockpitWeapon = Label(shell.transform, "CkWeapon", "主炮 CANNON", 26, new Color(1f, 0.86f, 0.52f),
                TextAnchor.MiddleCenter, fontSm);
            Anchor(cockpitWeapon.rectTransform, 0f, botY - 70f, 720f, 44f);
            cockpitWeapon.raycastTarget = false;

            cockpitHint = Label(shell.transform, "CkHint", "V 切车外视角 · E 下车 · 1 / 2 或滚轮 换武器",
                22, new Color(0.70f, 0.74f, 0.62f), TextAnchor.MiddleLeft, fontXs);
            Anchor(cockpitHint.rectTransform, -620f, 430f, 700f, 36f);
            cockpitHint.raycastTarget = false;
            FitText(cockpitHint, 680f, 22);

            shell.SetActive(false);
        }

        /// <summary>坦克炮镜：橙色圆环 + 十字分划 + 下方密位刻度（程序生成，不依赖图片）。</summary>
        private static Sprite MakeTankReticleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color amber = new Color(1f, 0.74f, 0.26f, 0.95f);
            float ringR = 0.40f, ringW = 0.016f;
            float lineW = 0.0055f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size - 0.5f;
                    float v = (y + 0.5f) / size - 0.5f;
                    float r = Mathf.Sqrt(u * u + v * v) * 2f;
                    bool on = Mathf.Abs(r - ringR) < ringW;
                    if (!on && r < ringR)
                    {
                        bool cross = (Mathf.Abs(u) < lineW && Mathf.Abs(v) > 0.05f)
                            || (Mathf.Abs(v) < lineW && Mathf.Abs(u) > 0.05f);
                        bool dot = r < 0.014f;
                        bool tick = false;
                        if (Mathf.Abs(u) < lineW * 2.2f)
                        {
                            for (int k = 1; k <= 3; k++)
                            {
                                if (Mathf.Abs(v - 0.10f * k) < lineW * 1.5f) { tick = true; break; }
                            }
                        }
                        on = cross || dot || tick;
                    }
                    tex.SetPixel(x, y, on ? amber : clear);
                }
            }
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>每帧：舱内 UI 的显示与文字（装甲、武器状态、装填）。</summary>
        private void UpdateCockpit()
        {
            if (cockpitPanel == null || Gm == null) return;
            bool on = Gm.TankMounted && Gm.Tank != null && Gm.Rig != null && Gm.Rig.TankFirstPerson;
            if (cockpitPanel.activeSelf != on) cockpitPanel.SetActive(on);
            if (tankReticle != null && tankReticle.gameObject.activeSelf != on) tankReticle.gameObject.SetActive(on);
            // 舱内用炮镜准星，普通十字准星收起来
            if (crosshair != null && on && crosshair.activeSelf) crosshair.SetActive(false);
            if (!on) { tankBarSlot = -1; return; }

            var tk = Gm.Tank;
            if (cockpitArmor != null)
            {
                string s = "装甲 ARMOR  " + Mathf.CeilToInt(tk.Hp) + " / " + Mathf.CeilToInt(tk.MaxHp);
                if (cockpitArmor.text != s) { cockpitArmor.text = s; FitText(cockpitArmor, 540f, 22); }
                cockpitArmor.color = tk.Doomed ? new Color(1f, 0.42f, 0.30f) : new Color(0.95f, 0.78f, 0.42f);
            }
            if (cockpitWeapon != null)
            {
                string s;
                if (tk.Doomed) s = "装甲损毁 · " + tk.DoomTimer.ToString("0.0") + " 秒后自爆 · 按 E 跳车";
                else if (tk.WeaponIndex == 0) s = tk.CannonReady ? "主炮 CANNON · 就绪" : "主炮装填中…";
                else s = "同轴机枪 MG · 按住左键扫射";
                if (cockpitWeapon.text != s) { cockpitWeapon.text = s; FitText(cockpitWeapon, 700f, 26); }
                cockpitWeapon.color = tk.Doomed ? new Color(1f, 0.42f, 0.30f) : new Color(1f, 0.86f, 0.52f);
            }
        }

        // ---------------------------------------------------------- 小队 / 坦克 HUD

        /// <summary>左上角：小队积分（召唤要花这个）+ 三个队友的血量/重生状态。</summary>
        private void BuildSquad(Transform root)
        {
            var card = Card(root, "SquadCard", new Vector2(360f, 120f), new Vector2(-640f, 400f), new Color(0.5f, 0.85f, 0.95f, 0.5f));
            squadCard = card.gameObject;
            var title = Label(card, "SquadTitle", "小队积分 SQUAD", 22, cMuted, TextAnchor.MiddleCenter, fontXs);
            Anchor(title.rectTransform, 0f, 32f, 340f, 32f);
            FitText(title, 320f, 22);
            squadValue = Label(card, "SquadValue", "0", 44, cCyan, TextAnchor.MiddleCenter, fontMd);
            Anchor(squadValue.rectTransform, 0f, -14f, 340f, 56f);
            squadValue.raycastTarget = false;

            var tip = Label(root, "SquadTip", "G 键 打开小队召唤", 24, cMuted, TextAnchor.MiddleCenter, fontXs);
            Anchor(tip.rectTransform, -640f, 328f, 360f, 38f);
            FitText(tip, 340f, 24);
            tip.raycastTarget = false;
            squadTip = tip.gameObject;

            for (int i = 0; i < allyRows.Length; i++)
            {
                var row = Slot(root, "AllyRow" + i, new Vector2(360f, 46f), new Vector2(-640f, 276f - i * 54f));
                row.gameObject.SetActive(true);
                var img = row.GetComponent<Image>();
                img.raycastTarget = false;
                var t = Label(row, "AllyText", "", 24, new Color(0.62f, 0.88f, 0.92f), TextAnchor.MiddleLeft, fontSm);
                Anchor(t.rectTransform, 16f, 0f, 328f, 44f);
                t.raycastTarget = false;
                allyRows[i] = t;
            }
        }

        private void BuildTankHud(Transform root)
        {
            var shell = new GameObject("TankHud");
            shell.transform.SetParent(root, false);
            var srt = shell.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.5f, 0.5f);
            srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = new Vector2(0f, -408f);
            srt.sizeDelta = new Vector2(560f, 120f);
            tankPanel = shell;

            var card = Card(shell.transform, "TankCard", new Vector2(560f, 120f), Vector2.zero, new Color(0.95f, 0.78f, 0.42f, 0.55f));
            var title = Label(card, "TankTitle", "坦克装甲 ARMOR（独立耐久 · 不掉人物血量）", 22, cMuted, TextAnchor.MiddleCenter, fontXs);
            Anchor(title.rectTransform, 0f, 36f, 540f, 34f);
            FitText(title, 520f, 22);

            var barBg = NewImage(card, "TankBarBg", new Color(0.02f, 0.04f, 0.07f, 0.45f), new Vector2(460f, 22f), Vector2.zero);
            barBg.raycastTarget = false;
            tankFill = NewImage(barBg.transform, "TankFill", new Color(0.95f, 0.72f, 0.30f, 1f), Vector2.zero, Vector2.zero);
            tankFill.raycastTarget = false;
            SetBarFill(tankFill, 1f);

            tankText = Label(card, "TankHint", "WASD 驾驶 · 鼠标转炮塔 · 左键开炮 · 碾过步兵直接碾死 · E 下车", 22, cMuted, TextAnchor.MiddleCenter, fontXs);
            Anchor(tankText.rectTransform, 0f, -34f, 540f, 32f);
            FitText(tankText, 520f, 22);
            shell.SetActive(false);
            srt.anchoredPosition = new Vector2(0f, -380f);
        }

        // ---------------------------------------------------------- 召唤面板（G）

        private void BuildCallIn(Transform root)
        {
            var panel = FullPanel(root, "CallInPanel", cScrim, false);
            callInPanel = panel.gameObject;

            var card = Card(panel, "CallInCard", new Vector2(900f, 620f), new Vector2(0f, 10f), new Color(0.6f, 0.8f, 1f, 0.55f));
            var title = Label(card, "CallInTitle", "小队召唤 SQUAD CALL-IN", 40, cGold, TextAnchor.MiddleCenter, fontMd, 3f);
            Anchor(title.rectTransform, 0f, 250f, 860f, 60f);
            FitText(title, 840f, 40);

            callInPoints = Label(card, "CallInPoints", "小队积分 0", 30, cCyan, TextAnchor.MiddleCenter, fontSm);
            Anchor(callInPoints.rectTransform, 0f, 194f, 860f, 44f);
            callInPoints.raycastTarget = false;

            string[] names = { "区域轰炸 ARTILLERY", "喷火器 FLAMETHROWER", "加特林 MINIGUN", "坦克 TANK" };
            string[] descs =
            {
                "标定准星落点 · 14 发炮弹覆盖约 9 米",
                "下发 60 秒 · 近距扇形火焰，持续灼烧",
                "下发 60 秒 · 枪管预热后高射速，移动变慢",
                "空投一辆坦克 · 走到车边按 E 登乘（触屏 RIDE）"
            };
            for (int i = 0; i < 4; i++)
            {
                var rowGo = new GameObject("CallInRow" + i);
                rowGo.transform.SetParent(card, false);
                var rrt = rowGo.AddComponent<RectTransform>();
                Anchor(rrt, 0f, 96f - i * 92f, 830f, 78f);
                var rimg = rowGo.AddComponent<Image>();
                rimg.color = new Color(0.12f, 0.16f, 0.22f, 0.90f);
                var ol = rowGo.AddComponent<Outline>();
                ol.effectColor = new Color(0f, 0f, 0f, 0.6f);
                ol.effectDistance = new Vector2(2f, -2f);
                var btn = rowGo.AddComponent<Button>();
                btn.targetGraphic = rimg;
                var cb = btn.colors;
                cb.highlightedColor = new Color(0.20f, 0.26f, 0.34f, 0.95f);
                cb.pressedColor = new Color(0.08f, 0.11f, 0.15f, 0.95f);
                cb.fadeDuration = 0.08f;
                btn.colors = cb;
                int idx = i;
                btn.onClick.AddListener(() => AudioKit.Play2D("ui", 0.6f));
                btn.onClick.AddListener(() => { if (Gm != null) Gm.RequestCallIn(idx); });
                callInBtn[i] = btn;

                var keyBg = NewImage(rowGo.transform, "Key", new Color(0.98f, 0.80f, 0.36f, 0.92f),
                    new Vector2(46f, 46f), new Vector2(-376f, 0f));
                keyBg.raycastTarget = false;
                var kt = Label(keyBg.transform, "K", (i + 1).ToString(), 26, new Color(0.06f, 0.06f, 0.09f),
                    TextAnchor.MiddleCenter, fontXs, 1.5f);
                Anchor(kt.rectTransform, 0f, 0f, 50f, 50f);
                kt.raycastTarget = false;

                var nm = Label(rowGo.transform, "Name", names[i], 28, cText, TextAnchor.MiddleLeft, fontSm);
                Anchor(nm.rectTransform, -108f, 16f, 470f, 38f);
                nm.raycastTarget = false;
                FitText(nm, 460f, 28);

                var ds = Label(rowGo.transform, "Desc", descs[i], 22, cMuted, TextAnchor.MiddleLeft, fontXs);
                Anchor(ds.rectTransform, -108f, -18f, 600f, 32f);
                ds.raycastTarget = false;
                FitText(ds, 590f, 22);

                var cost = Label(rowGo.transform, "Cost", "", 30, cGold, TextAnchor.MiddleRight, fontSm);
                Anchor(cost.rectTransform, 322f, 0f, 180f, 46f);
                cost.raycastTarget = false;
                callInCost[i] = cost;
            }

            var hint = Label(card, "CallInHint", "数字键 1~4 或点击选择 · G / Esc 关闭", 24, cMuted, TextAnchor.MiddleCenter, fontXs);
            Anchor(hint.rectTransform, -120f, -252f, 620f, 36f);
            FitText(hint, 600f, 24);
            hint.raycastTarget = false;
            // 关闭按钮：触屏端没有 G / Esc 键，必须有一个可点的关闭入口
            MakeButton(card, "BtnCallInClose", "关闭 CLOSE", new Vector2(288f, -252f), new Vector2(220f, 52f), cRed,
                () => { if (Gm != null) Gm.CloseCallIn(); });
        }

        public void ShowCallIn(bool show)
        {
            if (callInPanel == null) return;
            callInPanel.SetActive(show);
            if (show) RefreshCallIn();
        }

        /// <summary>面板打开时刷新一次：积分够不够用颜色区分，不够的直接点不动。</summary>
        private void RefreshCallIn()
        {
            if (Gm == null) return;
            if (callInPoints != null)
            {
                callInPoints.text = "小队积分 " + Gm.SquadPoints;
                FitText(callInPoints, 820f, 30);
            }
            for (int i = 0; i < 4; i++)
            {
                int cost = GameManager.CallInCost(i);
                bool ok = Gm.SquadPoints >= cost;
                if (i == 3 && Gm.Tank != null && Gm.Tank.IsAlive) ok = false;   // 场上已经有车了
                if (callInCost[i] != null)
                {
                    callInCost[i].text = cost.ToString();
                    callInCost[i].color = ok ? cGold : new Color(0.72f, 0.34f, 0.30f);
                }
                if (callInBtn[i] != null) callInBtn[i].interactable = ok;
            }
        }

        /// <summary>每帧刷新：队友状态行、坦克血条、召唤武器倒计时。</summary>
        private void UpdateSquad()
        {
            if (Gm == null) return;

            for (int i = 0; i < allyRows.Length; i++)
            {
                var t = allyRows[i];
                if (t == null) continue;
                string s = "";
                bool alive = false;
                if (i < Gm.Allies.Count && Gm.Allies[i] != null)
                {
                    var a = Gm.Allies[i];
                    s = a.StatusText;
                    alive = a.Alive;
                }
                if (t.text != s)
                {
                    t.text = s;
                    FitText(t, 316f, 24);
                }
                t.color = alive ? new Color(0.62f, 0.90f, 0.94f) : new Color(0.85f, 0.55f, 0.45f);
            }

            // 场上只要有车就显示装甲条（它是独立的第二血条，不在车里也能看）
            bool hasTank = Gm.Tank != null;
            if (tankPanel != null && tankPanel.activeSelf != hasTank) tankPanel.SetActive(hasTank);
            if (hasTank && tankFill != null)
            {
                var tk = Gm.Tank;
                float hp01 = tk.Hp01;
                SetBarFill(tankFill, hp01);
                tankFill.color = hp01 > 0.5f ? new Color(0.95f, 0.72f, 0.30f)
                    : (hp01 > 0.22f ? new Color(0.95f, 0.50f, 0.24f) : new Color(0.92f, 0.28f, 0.24f));

                string s;
                if (tk.Doomed)
                    s = "装甲损毁！" + tk.DoomTimer.ToString("0.0") + " 秒后自爆 · 按 E 跳车";
                else if (Gm.TankMounted)
                {
                    if (tk.WeaponIndex == 0)
                        s = tk.CannonReady ? "主炮就绪 · 左键开炮 · 直接碾死步兵 · E 下车" : "主炮装填中…";
                    else
                        s = "同轴机枪 · 按住左键扫射 · 直接碾死步兵 · E 下车";
                }
                else
                    s = "坦克待命 · 走到车边按 E 登乘";
                if (tankText != null)
                {
                    if (tankText.text != s)
                    {
                        tankText.text = s;
                        FitText(tankText, 520f, 22);
                    }
                    tankText.color = tk.Doomed ? new Color(1f, 0.42f, 0.30f) : cMuted;
                }
            }

            if (tempWeaponText != null)
            {
                float tt = Gm.Weapons != null ? Gm.Weapons.TempTimer : 0f;
                bool on = tt > 0f;
                if (tempWeaponText.gameObject.activeSelf != on) tempWeaponText.gameObject.SetActive(on);
                if (on)
                {
                    string s = "召唤武器 " + Mathf.CeilToInt(tt) + "s";
                    if (tempWeaponText.text != s)
                    {
                        tempWeaponText.text = s;
                        FitText(tempWeaponText, 400f, 26);
                    }
                }
            }
        }

        // ---------------------------------------------------------- 小地图

        private void BuildMinimap()
        {
            var card = Card(hudPanel.transform, "MapCard", new Vector2(244f, 244f), new Vector2(790f, 290f), new Color(0.45f, 0.85f, 0.95f, 0.5f));

            var title = Label(card, "MapTitle", "地图 MAP", 24, cMuted, TextAnchor.MiddleCenter, fontXs);
            Anchor(title.rectTransform, 0f, -104f, 240f, 30f);

            var mapGo = new GameObject("MapImage");
            mapGo.transform.SetParent(card, false);
            mapImage = mapGo.AddComponent<RawImage>();
            mapImage.color = Color.white;
            mapImage.raycastTarget = false;
            var mrt = mapGo.GetComponent<RectTransform>();
            Anchor(mrt, 0f, 14f, MapSize, MapSize);
            var ol = mapGo.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.85f);
            ol.effectDistance = new Vector2(2f, -2f);

            // 标记层：与地图同尺寸，坐标原点在地图中心（+x 向东，+y 对应世界 +z）
            var dotRootGo = new GameObject("Markers");
            dotRootGo.transform.SetParent(mapGo.transform, false);
            mapDotRoot = dotRootGo.AddComponent<RectTransform>();
            mapDotRoot.anchorMin = new Vector2(0.5f, 0.5f);
            mapDotRoot.anchorMax = new Vector2(0.5f, 0.5f);
            mapDotRoot.sizeDelta = new Vector2(MapSize, MapSize);
            mapDotRoot.anchoredPosition = Vector2.zero;

            mapDots = new Image[MaxMapDots];
            for (int i = 0; i < MaxMapDots; i++)
            {
                var dot = NewImage(mapDotRoot, "Dot" + i, new Color(1f, 0.5f, 0.28f), new Vector2(11f, 11f), Vector2.zero);
                dot.raycastTarget = false;
                dot.gameObject.SetActive(false);
                mapDots[i] = dot;
            }

            // 队友箭头（青色，比敌人点大一号，和红色的敌人一眼分开）
            mapAllies = new TriangleGraphic[3];
            mapAllyBacks = new TriangleGraphic[3];
            for (int i = 0; i < mapAllies.Length; i++)
            {
                var ab = MakeArrow(mapDotRoot, "AllyBack" + i, 19f, new Color(0f, 0f, 0f, 0.85f));
                ab.gameObject.SetActive(false);
                mapAllyBacks[i] = ab;
                var a = MakeArrow(mapDotRoot, "AllyArrow" + i, 12f, new Color(0.40f, 0.92f, 0.98f));
                a.gameObject.SetActive(false);
                mapAllies[i] = a;
            }

            // 玩家箭头（深色底 + 亮色，保证在任何地形上都看得见）
            var back = MakeArrow(mapDotRoot, "PlayerBack", 22f, new Color(0f, 0f, 0f, 0.85f));
            back.transform.SetAsFirstSibling();
            mapPlayer = MakeArrow(mapDotRoot, "PlayerArrow", 15f, new Color(0.65f, 0.98f, 1f));
        }

        private void EnsureMapTexture()
        {
            var w = Gm != null ? Gm.World : null;
            if (w == null || mapImage == null) return;

            if (mapTex == null || mapTex.width != w.SX || mapTex.height != w.SZ)
            {
                mapTex = new Texture2D(w.SX, w.SZ, TextureFormat.RGBA32, false, true);
                mapTex.filterMode = FilterMode.Point;
                mapTex.wrapMode = TextureWrapMode.Clamp;
            }
            mapSX = w.SX; mapSZ = w.SZ;

            var px = new Color32[mapSX * mapSZ];
            for (int z = 0; z < mapSZ; z++)
            {
                for (int x = 0; x < mapSX; x++)
                {
                    int top = w.GroundHeight(x, z, w.SY - 1);
                    byte id = top > 0 ? w.Get(x, top - 1, z) : (byte)BlockId.Air;
                    Color c = id == (byte)BlockId.Air ? new Color(0.05f, 0.07f, 0.10f) : BlockDef.DebrisColor(id);
                    float shade = 0.60f + Mathf.Clamp01((top - 4) / 12f) * 0.55f;
                    px[x + z * mapSX] = new Color(c.r * shade, c.g * shade, c.b * shade, 1f);
                }
            }
            mapTex.SetPixels32(px);
            mapTex.Apply(false, false);
            mapImage.texture = mapTex;
            mapReady = true;
        }

        private void UpdateMinimap()
        {
            if (!mapReady) { EnsureMapTexture(); if (!mapReady) return; }
            var w = Gm.World;
            if (w == null) return;

            // 玩家
            var pp = Gm.Player != null ? Gm.Player.transform.position : Vector3.zero;
            mapPlayer.rectTransform.anchoredPosition = new Vector2(
                (pp.x / mapSX - 0.5f) * MapSize,
                (pp.z / mapSZ - 0.5f) * MapSize);
            float yaw = Gm.Rig != null ? Gm.Rig.Yaw : 0f;
            mapPlayer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -yaw);

            // 敌人
            var list = Gm.Enemies;
            int n = 0;
            float pulse = 0.75f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f));
            for (int i = 0; i < list.Count && n < mapDots.Length; i++)
            {
                var e = list[i];
                if (e == null || !e.Alive) continue;
                var d = mapDots[n++];
                d.gameObject.SetActive(true);
                d.rectTransform.anchoredPosition = new Vector2(
                    Mathf.Clamp((e.transform.position.x / mapSX - 0.5f) * MapSize, -MapSize * 0.5f, MapSize * 0.5f),
                    Mathf.Clamp((e.transform.position.z / mapSZ - 0.5f) * MapSize, -MapSize * 0.5f, MapSize * 0.5f));
                d.rectTransform.localScale = huntMode ? Vector3.one * pulse : Vector3.one * 0.85f;
                d.color = huntMode ? new Color(1f, 0.34f, 0.24f) : new Color(1f, 0.62f, 0.30f);
                d.transform.SetAsLastSibling();
            }
            for (; n < mapDots.Length; n++) mapDots[n].gameObject.SetActive(false);

            // 队友（青色箭头，带朝向；阵亡时不画）
            if (mapAllies != null)
            {
                var allies = Gm.Allies;
                for (int i = 0; i < mapAllies.Length; i++)
                {
                    bool on = i < allies.Count && allies[i] != null && allies[i].Alive;
                    if (mapAllies[i] != null && mapAllies[i].gameObject.activeSelf != on) mapAllies[i].gameObject.SetActive(on);
                    if (mapAllyBacks[i] != null && mapAllyBacks[i].gameObject.activeSelf != on) mapAllyBacks[i].gameObject.SetActive(on);
                    if (!on) continue;

                    var a = allies[i];
                    Vector2 p = new Vector2(
                        Mathf.Clamp((a.transform.position.x / mapSX - 0.5f) * MapSize, -MapSize * 0.5f, MapSize * 0.5f),
                        Mathf.Clamp((a.transform.position.z / mapSZ - 0.5f) * MapSize, -MapSize * 0.5f, MapSize * 0.5f));
                    float ay = a.FacingYaw;
                    // 先摆深色底、再摆亮色箭头（后设置的才在上面）
                    mapAllyBacks[i].rectTransform.anchoredPosition = p;
                    mapAllyBacks[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, -ay);
                    mapAllyBacks[i].transform.SetAsLastSibling();
                    mapAllies[i].rectTransform.anchoredPosition = p;
                    mapAllies[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, -ay);
                    mapAllies[i].transform.SetAsLastSibling();
                }
            }

            // 玩家始终画在最上层
            mapPlayer.transform.SetAsLastSibling();
        }

        /// <summary>剩余敌人很少时，在屏幕边缘用箭头指出他们所在的方向。</summary>
        private void UpdateHuntArrows()
        {
            bool show = huntMode && Gm.State == GameState.Playing;
            var cam = Gm.Rig != null ? Gm.Rig.Cam : null;
            if (!show || cam == null)
            {
                for (int i = 0; i < arrows.Length; i++) arrows[i].gameObject.SetActive(false);
                return;
            }

            Vector3 fwd = cam.transform.forward; fwd.y = 0f;
            Vector3 right = cam.transform.right; right.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f || right.sqrMagnitude < 0.0001f) return;
            fwd.Normalize(); right.Normalize();

            int n = 0;
            var list = Gm.Enemies;
            float margin = 70f;
            for (int i = 0; i < list.Count && n < arrows.Length; i++)
            {
                var e = list[i];
                if (e == null || !e.Alive) continue;

                Vector3 sp = cam.WorldToScreenPoint(e.transform.position + Vector3.up * 1.0f);
                if (sp.z > 0f && sp.x > margin && sp.x < Screen.width - margin && sp.y > margin && sp.y < Screen.height - margin)
                    continue; // 已经在画面里，不用指方向

                Vector3 flat = e.transform.position - cam.transform.position;
                flat.y = 0f;
                if (flat.sqrMagnitude < 0.001f) continue;

                float a = Vector3.Dot(flat, right);
                float b = Vector3.Dot(flat, fwd);
                float ang = Mathf.Atan2(a, b);

                var rt = arrowRts[n];
                rt.gameObject.SetActive(true);
                rt.anchoredPosition = new Vector2(Mathf.Sin(ang) * ArrowRx, Mathf.Cos(ang) * ArrowRy);
                rt.localRotation = Quaternion.Euler(0f, 0f, -ang * Mathf.Rad2Deg);
                arrows[n].color = new Color(1f, 0.38f, 0.26f, 0.75f + 0.25f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)));
                n++;
            }
            for (; n < arrows.Length; n++) arrows[n].gameObject.SetActive(false);
        }

        // ---------------------------------------------------------- 主菜单

        /// <summary>
        /// 主菜单（《最后生还者 重制版》风格）：
        /// 背景就是真实的游戏画面（由 CameraRig 的电影运镜驱动），
        /// UI 只占左侧一列，右侧保持可见的实机场景。
        /// </summary>
        private void BuildMenu(Transform root)
        {
            menuPanel = FullPanel(root, "MenuPanel", new Color(0f, 0f, 0f, 0f), true).gameObject;

            // 左侧渐变遮罩：左边压暗保证文字可读，右边自然过渡到透明
            var scrim = NewImage(menuPanel.transform, "MenuScrim", Color.white, Vector2.zero, Vector2.zero);
            scrim.type = Image.Type.Simple;
            scrim.sprite = MakeFadeSprite(256, new Color(0.03f, 0.05f, 0.07f, 0.92f), new Color(0.03f, 0.05f, 0.07f, 0f));
            scrim.raycastTarget = false;
            var srt = scrim.rectTransform;
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(0.52f, 1f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            srt.SetAsFirstSibling();

            // 左侧一列内容
            var colGo = new GameObject("MenuColumn");
            colGo.transform.SetParent(menuPanel.transform, false);
            var colRt = colGo.AddComponent<RectTransform>();
            colRt.anchorMin = new Vector2(0f, 0f);
            colRt.anchorMax = new Vector2(0f, 1f);
            colRt.offsetMin = new Vector2(78f, 60f);
            colRt.offsetMax = new Vector2(860f, -90f);
            Transform col = colGo.transform;

            float x0 = -351f;        // 内容左边缘（列坐标系，列半宽 391）
            float w = 660f;          // 内容宽度
            float cx = x0 + w * 0.5f;

            // ---- 标题
            var title = Label(col, "Title", "方块竞技场", 86, cGold, TextAnchor.MiddleLeft, fontXl, 4f);
            Anchor(title.rectTransform, cx, 268f, w, 130f);
            FitText(title, w - 10f, 86);
            var sub = Label(col, "Sub", "PIXEL ARENA", 30, new Color(0.86f, 0.88f, 0.93f), TextAnchor.MiddleLeft, fontSm);
            Anchor(sub.rectTransform, cx, 194f, w, 52f);
            var tag = Label(col, "Tag", "体素射击 · 波次生存 DEMO", 24, cMuted, TextAnchor.MiddleLeft, fontXs);
            Anchor(tag.rectTransform, cx, 156f, w, 44f);
            FitText(tag, w - 10f, 24);

            var line = NewImage(col, "TitleLine", new Color(1f, 0.83f, 0.36f, 0.55f), new Vector2(w, 3f), new Vector2(cx, 128f));
            line.rectTransform.SetAsFirstSibling();
            line.raycastTarget = false;

            // ---- 菜单项（左对齐 + 左侧强调条）
            float bw = w - 60f;                       // 按钮比标题窄一点，右边留白
            float bx = x0 + bw * 0.5f;

            MenuButton(col, "BtnStart", "开始游戏  NEW GAME", new Vector2(bx, 58f), new Vector2(bw, 94f),
                new Color(0.20f, 0.44f, 0.26f, 0.72f), cGold, () =>
                {
                    // 先选模式（小队模式 / 单人突击模式），再真正开局
                    if (Gm != null) Gm.OpenModeSelect();
                }, 36);

            MenuButton(col, "BtnHelp", "操作说明  HOW TO PLAY", new Vector2(bx, -56f), new Vector2(bw, 82f),
                new Color(0.10f, 0.13f, 0.19f, 0.62f), cCyan, () =>
                {
                    menuPanel.SetActive(false);
                    helpPanel.SetActive(true);
                }, 30);

            MenuButton(col, "BtnSettings", "设置  OPTIONS", new Vector2(bx, -156f), new Vector2(bw, 82f),
                new Color(0.10f, 0.13f, 0.19f, 0.62f), cCyan, () =>
                {
                    menuPanel.SetActive(false);
                    settingsPanel.SetActive(true);
                    RefreshSettings();
                }, 30);

            if (!Application.isMobilePlatform)
            {
                MenuButton(col, "BtnQuit", "退出游戏  EXIT", new Vector2(bx, -256f), new Vector2(bw, 78f),
                    new Color(0.34f, 0.14f, 0.14f, 0.62f), cDanger, () =>
                    {
                        GameSettings.Save();
                        Application.Quit();
                    }, 28);
            }

        }

        /// <summary>单人突击模式没有队友也没有小队积分，整块小队面板要藏起来。</summary>
        public void SetSquadVisible(bool show)
        {
            if (squadCard != null) squadCard.SetActive(show);
            if (squadTip != null) squadTip.SetActive(show);
            for (int i = 0; i < allyRows.Length; i++)
            {
                if (allyRows[i] == null) continue;
                var row = allyRows[i].transform.parent;
                if (row != null) row.gameObject.SetActive(show);
            }
        }

        // ---------------------------------------------------------- 模式选择

        /// <summary>点"开始游戏"后先选模式：小队模式（有队友 / 积分 / 召唤）或单人突击模式。</summary>
        private void BuildModeSelect(Transform root)
        {
            modePanel = FullPanel(root, "ModePanel", cScrim, false).gameObject;

            var card = Card(modePanel.transform, "ModeCard", new Vector2(1080f, 620f), new Vector2(0f, 0f),
                new Color(0.6f, 0.8f, 1f, 0.55f));

            var title = Label(card, "ModeTitle", "选择模式  SELECT MODE", 44, cGold, TextAnchor.MiddleCenter, fontMd, 3f);
            Anchor(title.rectTransform, 0f, 240f, 1000f, 66f);
            FitText(title, 980f, 44);

            var sub = Label(card, "ModeSub", "模式决定了你有没有队友、能不能花小队积分召唤支援", 24, cMuted,
                TextAnchor.MiddleCenter, fontXs);
            Anchor(sub.rectTransform, 0f, 190f, 1000f, 36f);
            FitText(sub, 980f, 24);

            AddModeRow(card.transform, 0, "小队模式  SQUAD",
                "3 名 AI 队友随行推进 · 击杀攒小队积分 · G 键召唤轰炸 / 喷火器 / 加特林 / 坦克",
                new Color(0.16f, 0.38f, 0.30f, 0.95f), new Color(0.45f, 0.90f, 0.95f),
                () => { if (Gm != null) { ShowModeSelect(false); Gm.StartGame(GameMode.Squad); } });

            AddModeRow(card.transform, 1, "单人突击模式  SOLO ASSAULT",
                "孤狼作战 · 没有队友、没有小队积分与召唤 · 纯枪法和地形",
                new Color(0.20f, 0.16f, 0.14f, 0.95f), new Color(1.00f, 0.72f, 0.34f),
                () => { if (Gm != null) { ShowModeSelect(false); Gm.StartGame(GameMode.Solo); } });

            MakeButton(card, "BtnModeBack", "返回  BACK", new Vector2(0f, -244f), new Vector2(420f, 66f),
                new Color(0.10f, 0.13f, 0.19f, 0.85f), () => ShowModeSelect(false), 28, fontSm);
        }

        private void AddModeRow(Transform parent, int index, string name, string desc, Color bg, Color accent,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("ModeRow" + index);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            Anchor(rt, 0f, 80f - index * 156f, 990f, 132f);
            var img = go.AddComponent<Image>();
            img.color = bg;
            var ol = go.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.6f);
            ol.effectDistance = new Vector2(3f, -3f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.highlightedColor = new Color(Mathf.Min(1f, bg.r * 1.6f + 0.10f), Mathf.Min(1f, bg.g * 1.6f + 0.10f), Mathf.Min(1f, bg.b * 1.6f + 0.10f), 1f);
            cb.pressedColor = new Color(bg.r * 0.7f, bg.g * 0.7f, bg.b * 0.7f, 1f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            btn.onClick.AddListener(() => AudioKit.Play2D("ui", 0.6f));
            if (onClick != null) btn.onClick.AddListener(onClick);

            // 左侧强调条（小队=青，突击=橙）
            var bar = NewImage(go.transform, "AccentBar", accent, new Vector2(10f, 112f), new Vector2(-482f, 0f));
            bar.raycastTarget = false;

            var nm = Label(go.transform, "Name", name, 34, accent, TextAnchor.MiddleLeft, fontSm);
            Anchor(nm.rectTransform, -80f, 26f, 780f, 46f);
            nm.raycastTarget = false;
            FitText(nm, 760f, 34);

            var ds = Label(go.transform, "Desc", desc, 22, cMuted, TextAnchor.MiddleLeft, fontXs);
            Anchor(ds.rectTransform, -80f, -26f, 830f, 40f);
            ds.raycastTarget = false;
            FitText(ds, 810f, 22);
        }

        public void ShowModeSelect(bool show)
        {
            if (modePanel == null) return;
            modePanel.SetActive(show);
            if (menuPanel != null) menuPanel.SetActive(!show);
        }

        /// <summary>换图时更新 HUD 上的地图名。</summary>
        public void SetMapName(string name)
        {
            if (mapText == null) return;
            mapText.text = "地图 · " + name;
            FitText(mapText, 680f, 24);
        }

        /// <summary>横向渐变贴图：用于左侧遮罩，左边不透明、右边全透明。</summary>
        private static Sprite MakeFadeSprite(int width, Color left, Color right)
        {
            int h = 8;
            var tex = new Texture2D(width, h, TextureFormat.RGBA32, false);
            for (int x = 0; x < width; x++)
            {
                float t = x / (float)(width - 1);
                Color c = Color.Lerp(left, right, t);
                for (int y = 0; y < h; y++) tex.SetPixel(x, y, c);
            }
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0f, 0f, width, h), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 狙击镜贴图：圆孔外面全黑（挡住外围视野），圆孔里面透明，
        /// 再画上十字分划和几个密位刻度点。运行时生成，不需要任何美术资源。
        /// </summary>
        private static Sprite MakeScopeSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float inner = 0.385f;    // 通光孔径
            float feather = 0.030f;  // 边缘羽化
            float lineW = 0.0045f;   // 分划线宽度
            Color black = new Color(0f, 0f, 0f, 1f);
            Color reticle = new Color(0.05f, 0.05f, 0.06f, 0.9f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size - 0.5f;
                    float v = (y + 0.5f) / size - 0.5f;
                    float r = Mathf.Sqrt(u * u + v * v) * 2f;   // 0 中心 → 1 画布边缘
                    float a = Mathf.Clamp01((r - inner) / feather);
                    Color c = new Color(black.r, black.g, black.b, a);
                    if (a < 0.98f)
                    {
                        // 圆孔里面：画分划线（十字）+ 刻度点
                        bool thinV = Mathf.Abs(u) < lineW && Mathf.Abs(v) > 0.035f;
                        bool thinH = Mathf.Abs(v) < lineW && Mathf.Abs(u) > 0.035f;
                        bool dot = Mathf.Abs(u) < lineW * 2.6f && Mathf.Abs(v) < lineW * 2.6f;
                        // 密位刻度：竖线下方的三个粗短线
                        bool tick = false;
                        if (Mathf.Abs(u) < 0.035f)
                        {
                            for (int k = 1; k <= 3; k++)
                            {
                                float tv = -0.10f * k - 0.02f;
                                if (Mathf.Abs(v - tv) < lineW * 1.6f) { tick = true; break; }
                            }
                        }
                        if (dot || thinV || thinH || tick)
                            c = Color.Lerp(c, reticle, (1f - a) * 0.92f);
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>主菜单条目：半透明底 + 左侧强调条 + 文字左对齐。</summary>
        private Button MenuButton(Transform parent, string name, string text, Vector2 pos, Vector2 size,
            Color bg, Color accent, UnityEngine.Events.UnityAction onClick, int fontSize)
        {
            var btn = MakeButton(parent, name, text, pos, size, bg, onClick, fontSize, fontMd);
            // 左侧竖条
            var bar = NewImage(btn.transform, "Bar", accent, new Vector2(6f, size.y - 22f), new Vector2(-size.x * 0.5f + 3f, 0f));
            bar.rectTransform.SetAsFirstSibling();
            bar.raycastTarget = false;
            // 文字左对齐
            var lab = btn.transform.Find("Label");
            if (lab != null)
            {
                var t = lab.GetComponent<Text>();
                if (t != null)
                {
                    t.alignment = TextAnchor.MiddleLeft;
                    FitText(t, size.x - 64f, fontSize);
                }
                var rt = lab.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.offsetMin = new Vector2(34f, 0f);
                    rt.offsetMax = new Vector2(-18f, 0f);
                }
            }
            return btn;
        }

        // ---------------------------------------------------------- 暂停（独立界面）

        private void BuildPause(Transform root)
        {
            pausePanel = FullPanel(root, "PausePanel", cScrim, false).gameObject;

            var card = Card(pausePanel.transform, "PauseCard", new Vector2(900f, 900f), Vector2.zero, new Color(1f, 0.83f, 0.36f, 0.55f));

            var title = Label(card, "Title", "暂停 PAUSED", 68, cGold, TextAnchor.MiddleCenter, fontXl, 3f);
            Anchor(title.rectTransform, 0f, 360f, 900f, 110f);
            var line = NewImage(card, "Line", new Color(1f, 0.83f, 0.36f, 0.35f), new Vector2(760f, 3f), new Vector2(0f, 296f));
            line.rectTransform.SetAsFirstSibling();

            var statTitle = Label(card, "StatTitle", "本局战况 CURRENT RUN", 24, cMuted, TextAnchor.MiddleCenter, fontXs);
            Anchor(statTitle.rectTransform, 0f, 250f, 900f, 38f);
            FitText(statTitle, 880f, 24);

            Stat(card, "得分 SCORE", -220f, 190f, out pauseScore);
            Stat(card, "击杀 KILLS", 220f, 190f, out pauseKills);
            Stat(card, "波次 WAVE", -220f, 84f, out pauseWave);
            Stat(card, "剩余敌人 LEFT", 220f, 84f, out pauseLeft);

            MakeButton(card, "BtnResume", "继续游戏 RESUME", new Vector2(0f, -70f), new Vector2(520f, 104f), cGreen, () =>
            {
                if (Gm != null) Gm.Resume();
            }, 38, fontMd);

            MakeButton(card, "BtnSettings", "设置 SETTINGS", new Vector2(0f, -180f), new Vector2(520f, 88f), cBlue, () =>
            {
                pausePanel.SetActive(false);
                settingsPanel.SetActive(true);
                RefreshSettings();
            }, 32, fontMd);

            MakeButton(card, "BtnRestart", "重新开始 RESTART", new Vector2(0f, -280f), new Vector2(520f, 88f), new Color(0.32f, 0.42f, 0.58f), () =>
            {
                if (Gm != null) Gm.Restart();
            }, 32, fontMd);

            MakeButton(card, "BtnMenu", "返回主菜单 MAIN MENU", new Vector2(0f, -380f), new Vector2(520f, 88f), cRed, () =>
            {
                if (Gm != null) Gm.ReturnToMenu();
            }, 32, fontMd);

            var hint = Label(card, "Hint", "按 Esc 继续游戏 · 设置里可调整分辨率与帧率", 24, cMuted, TextAnchor.MiddleCenter, fontXs);
            Anchor(hint.rectTransform, 0f, -425f, 880f, 40f);
            FitText(hint, 860f, 24);
        }

        // ---------------------------------------------------------- 死亡

        private void BuildDeath(Transform root)
        {
            deathPanel = FullPanel(root, "DeathPanel", new Color(0.35f, 0.05f, 0.06f, 0.60f), false).gameObject;

            var card = Card(deathPanel.transform, "DeathCard", new Vector2(980f, 620f), Vector2.zero, new Color(0.95f, 0.35f, 0.32f, 0.6f));

            var title = Label(card, "Title", "你被击倒了", 80, cDanger, TextAnchor.MiddleCenter, fontXl, 4f);
            Anchor(title.rectTransform, 0f, 190f, 1000f, 130f);
            var sub = Label(card, "Sub", "YOU DIED", 32, new Color(0.85f, 0.55f, 0.5f), TextAnchor.MiddleCenter, fontSm);
            Anchor(sub.rectTransform, 0f, 108f, 1000f, 56f);

            deathScoreText = Label(card, "Score", "", 40, cText, TextAnchor.MiddleCenter, fontMd);
            Anchor(deathScoreText.rectTransform, 0f, 28f, 960f, 70f);

            deathRestartButton = MakeButton(card, "BtnRestart", "重新开始 RESTART", new Vector2(0f, -110f), new Vector2(440f, 100f), cGreen, () =>
            {
                if (Gm != null) Gm.Restart();
            }, 34, fontMd);
            deathRestartButton.gameObject.SetActive(false);

            deathMenuButton = MakeButton(card, "BtnMenu", "返回主菜单 MAIN MENU", new Vector2(0f, -215f), new Vector2(440f, 92f), cRed, () =>
            {
                if (Gm != null) Gm.ReturnToMenu();
            }, 30, fontMd);
            deathMenuButton.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------- 操作说明

        private void BuildHelp(Transform root)
        {
            helpPanel = FullPanel(root, "HelpPanel", cBackdrop, false).gameObject;
            var card = Card(helpPanel.transform, "HelpCard", new Vector2(1300f, 1010f), Vector2.zero, new Color(0.45f, 0.85f, 0.95f, 0.5f));

            var title = Label(card, "Title", "操作说明 HOW TO PLAY", 52, cCyan, TextAnchor.MiddleCenter, fontLg, 3f);
            Anchor(title.rectTransform, 0f, 445f, 1200f, 84f);

            var body = Label(card, "Body", HelpText(), 24, cText, TextAnchor.UpperLeft, fontSm);
            var brt = body.rectTransform;
            brt.anchorMin = new Vector2(0.5f, 0.5f);
            brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(0f, -25f);
            brt.sizeDelta = new Vector2(1180f, 800f);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;

            MakeButton(card, "BtnBack", "返回 BACK", new Vector2(0f, -450f), new Vector2(380f, 90f), cGreen, () =>
            {
                helpPanel.SetActive(false);
                menuPanel.SetActive(true);
            }, 34, fontMd);
        }

        private static string HelpText()
        {
            return
                "【目标】\n" +
                "在方块竞技场里击退一波波敌人。方块可以打碎，也能就地搭掩体。\n" +
                "每局都会随机换一张地图：草原林场 / 沙漠村庄 / 热带雨林 / 办公楼 / 沙2。\n" +
                "地形、掩体、配色都不一样，空地上还会刷出带绿色光环的医疗包（走近 +35 HP）。\n\n" +
                "【敌人怎么发现你】（参考《最后生还者》）\n" +
                "敌人有视野锥与视距上限：只有你进入它的视野、没被方块挡住、距离够近，它才会开始察觉。\n" +
                "警觉度是慢慢涨起来的 —— 它会先停下脚步盯着你看（起疑），抬枪瞄准，然后才进入交火。\n" +
                "· 它的转身是有速度的：背后的动静它要转过身才能看见，绕后有明显的偷袭窗口。\n" +
                "· 蹲下（Ctrl/C）更难被发现，冲刺更容易暴露，绕到侧面比正面硬闯安全得多。\n" +
                "· 一旦被某人发现，他会喊话，附近的同伙都会赶过来。\n" +
                "· 开枪会发出声音，附近即使看不见你的敌人也会过来查看（但位置有误差）。\n" +
                "· 敌人的枪法随距离急剧下降：近身几乎弹弹到肉，超出它的有效射程就基本打不中，\n" +
                "  很远时它根本不会开火。所以远距离对枪是有利的。\n" +
                "· 你躲起来之后，他们会跑到最后看到你的地方，逐个可疑点搜索、左右环视，\n" +
                "  搜不到才会回去巡逻（这时正是绕后偷袭的机会）。\n\n" +
                "【精英敌人】\n" +
                "第 3 波起开始出现。黑金重甲、戴红色目镜，血量是普通步兵的 4 倍以上，\n" +
                "枪法更好、射程更远、转身也更快。击杀给 300 分 —— 值得优先用小刀背刺处理。\n\n" +
                "【找敌人】\n" +
                "右上角小地图会实时显示所有敌人的位置（红点）与你的朝向（蓝色箭头）。\n" +
                "每波只剩最后 5 名敌人时，屏幕边缘会出现红色箭头指出他们的方向。\n\n" +
                "【PC 键位】\n" +
                "WASD / 方向键  移动         鼠标      转视角\n" +
                "左键  射击                  右键      开镜瞄准（狙击可开倍镜）\n" +
                "B / 中键  放置方块掩体      Space     跳跃\n" +
                "Shift 冲刺                  Ctrl / C  蹲下\n" +
                "冲刺中按 Ctrl / C           滑铲（压低身形快速通过，可跳跃取消）\n" +
                "R     换弹                  1~5       直选武器\n" +
                "Q / 滚轮上  下一把武器      滚轮下    上一把武器\n" +
                "V         切换第一 / 第三人称\n" +
                "G     小队召唤面板           E        登乘 / 离开坦克\n" +
                "F     检视武器（CS 风格动作）          Esc      暂停（再按一次继续）\n\n" +
                "【两种模式】\n" +
                "点“开始游戏”后先选模式：\n" +
                "· 小队模式 SQUAD        3 名 AI 队友随行、有头顶名字牌、位置显示在小地图上；\n" +
                "                        击杀攒小队积分，按 G 花分召唤支援。\n" +
                "· 单人突击模式 SOLO     孤狼作战，没有队友、没有小队积分和召唤，纯枪法与地形。\n\n" +
                "【小队系统】（仅小队模式）\n" +
                "3 名队友会跟着你推进并自主交火，头顶有名字牌（看到名字的一定是自己人），\n" +
                "小地图上用青色箭头显示他们的位置和朝向；阵亡 18 秒后在附近归队。\n" +
                "击杀攒小队积分（队友击杀打 6 折），按 G 花分召唤：\n" +
                "  1 区域轰炸 600 分   以准星落点为圆心，14 发炮弹覆盖约 9 米\n" +
                "  2 喷火器   350 分   下发 60 秒，近距离扇形火焰持续灼烧\n" +
                "  3 加特林   500 分   下发 60 秒，枪管预热后高射速，但移动变慢\n" +
                "  4 坦克    1200 分   空投一辆坦克，走到车边按 E 登乘\n" +
                "坦克：WASD 驾驶（A/D 转向）、鼠标转炮塔、左键开火，滚轮 / Q / 数字键 1、2 换武器。\n" +
                "  1 主炮 CANNON     爆破弹，碰到敌人（1.2 米内近炸）或方块就在那一点起爆，范围 7 米\n" +
                "  2 同轴机枪 MG     按住左键连发，只打敌人、不拆地形\n" +
                "主炮直击可以一炮带走步兵 / 突击兵 / 重装兵，精英兵吃满也还剩一口气，需要补一发。\n" +
                "车外视角地面上会画出炮弹落点（橙色=就绪、灰色=装填中）；V 键可切进舱内第一人称，\n" +
                "舱内有独立的观察窗 UI 与炮镜准星（再按 V 回到车外）。\n" +
                "坦克有自己的装甲条（和你的血量完全分开），人在车里挨打只掉装甲、人物一滴血不掉；\n" +
                "履带碾过任何敌人都是直接碾死（精英兵也不例外），碾死照样记分。\n" +
                "装甲归零后进入 3 秒自爆倒计时（滴滴声越来越急），趁这 3 秒按 E 跳车就能活，\n" +
                "留在车里会跟着一起炸。炮击有自伤风险，别站在弹幕里。\n\n" +
                "【安卓 / 触控】\n" +
                "左半屏拖动  虚拟摇杆移动（推到边缘自动冲刺）\n" +
                "右半屏滑动  转视角（带轻微辅助瞄准）\n" +
                "右下按钮    FIRE 开火 / JUMP 跳跃 / RELOAD 换弹 / BUILD 建块 / GUN 切枪\n" +
                "            AIM 开镜 / VIEW 视角 / LOOK 检视\n" +
                "右上角 II   暂停\n\n" +
                "【武器】\n" +
                "1 步枪 RIFLE   全自动，均衡，带红点瞄准镜\n" +
                "2 霰弹 SHOTGUN 双管 + 木质泵动，近距离 9 颗弹丸高爆发\n" +
                "3 狙击 SNIPER  远距离高伤害，爆头 2.2 倍，右键开镜后精度极高\n" +
                "4 手枪 PISTOL  半自动，单手持枪，换弹快，备用武器\n" +
                "5 小刀 KNIFE   近战，不吃弹药；从背后攻击 1.9 倍伤害\n\n" +
                "【提示】\n" +
                "换弹 / 切枪 / 检视都有独立动作，动作进行中无法开火，按键会打断检视。\n" +
                "开镜时灵敏度会自动降低；医疗包拾取后会在别处重新刷新。\n" +
                "打头伤害更高；石头要 3 枪、金属要 6 枪才打得碎；被击倒后自动重开新一局。";
        }

        // ---------------------------------------------------------- 设置

        private void BuildSettings(Transform root)
        {
            settingsPanel = FullPanel(root, "SettingsPanel", cBackdrop, false).gameObject;
            var card = Card(settingsPanel.transform, "SettingsCard", new Vector2(1500f, 760f), Vector2.zero, new Color(0.45f, 0.85f, 0.95f, 0.5f));

            var title = Label(card, "Title", "设置 SETTINGS", 56, cCyan, TextAnchor.MiddleCenter, fontLg, 3f);
            Anchor(title.rectTransform, 0f, 305f, 1000f, 86f);

            float L = -370f;   // 左列：画面
            float R = 370f;    // 右列：操作 / 音频

            // ================= 左列：画面
            SectionTitle(card, L, 210f, "画面 DISPLAY", cGold);

            resValue = MakeStepRow(card, L, 140f, "分辨率", out Button resMinus, out Button resPlus);
            resMinus.onClick.AddListener(() => { GameSettings.CycleResolution(-1); RefreshSettings(); });
            resPlus.onClick.AddListener(() => { GameSettings.CycleResolution(1); RefreshSettings(); });

            fsValue = MakeStepRow(card, L, 70f, "显示模式", out Button fsMinus, out Button fsPlus);
            fsMinus.onClick.AddListener(() => { GameSettings.CycleFullscreen(-1); RefreshSettings(); });
            fsPlus.onClick.AddListener(() => { GameSettings.CycleFullscreen(1); RefreshSettings(); });

            fpsValue = MakeStepRow(card, L, 0f, "帧率上限", out Button fpsMinus, out Button fpsPlus);
            fpsMinus.onClick.AddListener(() => { GameSettings.CycleFps(-1); RefreshSettings(); });
            fpsPlus.onClick.AddListener(() => { GameSettings.CycleFps(1); RefreshSettings(); });

            pixelValue = MakeToggleRow(card, L, -70f, "像素化画面", out Button pxToggle);
            pxToggle.onClick.AddListener(() =>
            {
                GameSettings.Pixelation = !GameSettings.Pixelation;
                GameSettings.Save();
                GameSettings.ApplyPixelation();
                RefreshSettings();
            });

            pxScaleValue = MakeStepRow(card, L, -140f, "像素倍率", out Button pxMinus, out Button pxPlus);
            pxMinus.onClick.AddListener(() =>
            {
                GameSettings.PixelScale = Mathf.Max(1, GameSettings.PixelScale - 1);
                GameSettings.Save(); GameSettings.ApplyPixelation(); RefreshSettings();
            });
            pxPlus.onClick.AddListener(() =>
            {
                GameSettings.PixelScale = Mathf.Min(6, GameSettings.PixelScale + 1);
                GameSettings.Save(); GameSettings.ApplyPixelation(); RefreshSettings();
            });

            showFpsValue = MakeToggleRow(card, L, -210f, "显示帧率", out Button fpsShowToggle);
            fpsShowToggle.onClick.AddListener(() =>
            {
                GameSettings.ShowFps = !GameSettings.ShowFps;
                GameSettings.Save(); RefreshSettings();
            });

            // ================= 右列：操作 / 音频
            SectionTitle(card, R, 210f, "操作 CONTROL", cGold);

            sensValue = MakeStepRow(card, R, 140f, "视角灵敏度", out Button sensMinus, out Button sensPlus);
            sensMinus.onClick.AddListener(() =>
            {
                GameSettings.Sensitivity = Mathf.Max(0.3f, GameSettings.Sensitivity - 0.3f);
                GameSettings.Save(); RefreshSettings();
            });
            sensPlus.onClick.AddListener(() =>
            {
                GameSettings.Sensitivity = Mathf.Min(8f, GameSettings.Sensitivity + 0.3f);
                GameSettings.Save(); RefreshSettings();
            });

            invertValue = MakeToggleRow(card, R, 70f, "反转纵向", out Button invToggle);
            invToggle.onClick.AddListener(() =>
            {
                GameSettings.InvertY = !GameSettings.InvertY;
                GameSettings.Save(); RefreshSettings();
            });

            if (Application.isMobilePlatform)
            {
                autoFireValue = MakeToggleRow(card, R, 0f, "自动开火", out Button afToggle);
                afToggle.onClick.AddListener(() =>
                {
                    GameSettings.AutoFireOnMobile = !GameSettings.AutoFireOnMobile;
                    GameSettings.Save(); RefreshSettings();
                });
            }

            SectionTitle(card, R, -70f, "音频 AUDIO", cGold);
            volumeValue = MakeStepRow(card, R, -140f, "音效音量", out Button volMinus, out Button volPlus);
            volMinus.onClick.AddListener(() =>
            {
                GameSettings.SfxVolume = Mathf.Max(0f, GameSettings.SfxVolume - 0.1f);
                GameSettings.Save(); AudioKit.Play2D("ui", GameSettings.SfxVolume); RefreshSettings();
            });
            volPlus.onClick.AddListener(() =>
            {
                GameSettings.SfxVolume = Mathf.Min(1f, GameSettings.SfxVolume + 0.1f);
                GameSettings.Save(); AudioKit.Play2D("ui", GameSettings.SfxVolume); RefreshSettings();
            });

            // ---- 底部按钮
            MakeButton(card, "BtnDefaults", "恢复默认 DEFAULTS", new Vector2(-260f, -290f), new Vector2(380f, 84f), new Color(0.34f, 0.30f, 0.42f), () =>
            {
                GameSettings.ResetDefaults();
                RefreshSettings();
            }, 30, fontMd);

            MakeButton(card, "BtnBack", "返回 BACK", new Vector2(260f, -290f), new Vector2(380f, 84f), cGreen, () =>
            {
                GameSettings.Save();
                settingsPanel.SetActive(false);
                if (Gm != null && Gm.State == GameState.Paused) pausePanel.SetActive(true);
                else menuPanel.SetActive(true);
            }, 34, fontMd);

            RefreshSettings();
        }

        private void RefreshSettings()
        {
            if (resValue != null) { resValue.text = GameSettings.ResolutionLabel(); FitText(resValue, 234f, 24); }
            if (fsValue != null) { fsValue.text = GameSettings.FullscreenLabel(); FitText(fsValue, 234f, 24); }
            if (fpsValue != null) { fpsValue.text = GameSettings.FpsLabel(); FitText(fpsValue, 234f, 24); }
            if (pixelValue != null) { pixelValue.text = GameSettings.Pixelation ? "开 ON" : "关 OFF"; FitText(pixelValue, 234f, 24); }
            if (pxScaleValue != null) { pxScaleValue.text = GameSettings.PixelScale + " ×"; FitText(pxScaleValue, 234f, 24); }
            if (showFpsValue != null) { showFpsValue.text = GameSettings.ShowFps ? "开 ON" : "关 OFF"; FitText(showFpsValue, 234f, 24); }
            if (sensValue != null) { sensValue.text = GameSettings.Sensitivity.ToString("0.0"); FitText(sensValue, 234f, 24); }
            if (invertValue != null) { invertValue.text = GameSettings.InvertY ? "开 ON" : "关 OFF"; FitText(invertValue, 234f, 24); }
            if (autoFireValue != null) { autoFireValue.text = GameSettings.AutoFireOnMobile ? "开 ON" : "关 OFF"; FitText(autoFireValue, 234f, 24); }
            if (volumeValue != null) { volumeValue.text = Mathf.RoundToInt(GameSettings.SfxVolume * 100f) + "%"; FitText(volumeValue, 234f, 24); }
        }

        // ---------------------------------------------------------- 状态切换

        public void SetPlaying(bool playing)
        {
            hudPanel.SetActive(playing);
            menuPanel.SetActive(!playing);
            pausePanel.SetActive(false);
            deathPanel.SetActive(false);
            settingsPanel.SetActive(false);
            helpPanel.SetActive(false);
            if (modePanel != null) modePanel.SetActive(false);
            SetHunt(0);
            if (playing)
            {
                hintTimer = 7f;
                EnsureMapTexture();
            }
        }

        public void ShowPause(bool show)
        {
            pausePanel.SetActive(show);
            settingsPanel.SetActive(false);
            if (show && Gm != null)
            {
                if (pauseScore != null) { pauseScore.text = Gm.Score.ToString(); FitText(pauseScore, 330f, 46); }
                if (pauseKills != null) { pauseKills.text = Gm.Kills.ToString(); FitText(pauseKills, 330f, 46); }
                if (pauseWave != null) { pauseWave.text = "第 " + Gm.Wave + " 波"; FitText(pauseWave, 330f, 46); }
                if (pauseLeft != null) { pauseLeft.text = Gm.AliveEnemies.ToString(); FitText(pauseLeft, 330f, 46); }
            }
        }

        public void ShowDeathPanel(bool canRestart)
        {
            deathPanel.SetActive(true);
            deathRestartButton.gameObject.SetActive(canRestart);
            deathMenuButton.gameObject.SetActive(canRestart);
            if (Gm != null)
            {
                deathScoreText.text = "得分 " + Gm.Score + "    击杀 " + Gm.Kills + "    波次 " + Gm.Wave;
                FitText(deathScoreText, 940f, 40);
            }
        }

        /// <summary>剩余敌人很少时开启“猎捕提示”：小地图红点闪烁 + 屏幕边缘方向箭头。</summary>
        public void SetHunt(int remaining)
        {
            huntMode = remaining > 0 && remaining <= 5;
            if (huntText != null)
            {
                huntText.gameObject.SetActive(huntMode);
                if (huntMode)
                {
                    huntText.text = "还剩 " + remaining + " 名敌人 · 方向见屏幕箭头 / 位置见小地图";
                    FitText(huntText, 880f, 30);
                }
            }
            if (!huntMode)
            {
                for (int i = 0; i < arrows.Length; i++) arrows[i].gameObject.SetActive(false);
            }
        }

        public void ShowHitmarker(bool head)
        {
            hitmarkerTimer = head ? 0.18f : 0.11f;
            hitmarker.gameObject.SetActive(true);
            Color c = head ? new Color(1f, 0.35f, 0.3f, 0.95f) : new Color(1f, 1f, 1f, 0.95f);
            var bars = hitmarker.GetComponentsInChildren<Image>();
            for (int i = 0; i < bars.Length; i++)
                if (bars[i].transform != hitmarker.transform) bars[i].color = c;
        }

        public void FlashDamage()
        {
            damageAlpha = 0.6f;
            damageIsHull = false;
        }

        /// <summary>车体挨打：橙色边框闪一下，和"自己掉血"的红色屏区分开。</summary>
        public void FlashHullHit()
        {
            damageAlpha = 0.42f;
            damageIsHull = true;
        }

        public void Announce(string text)
        {
            announceText.text = text;
            announceTimer = 2.2f;
        }

        public void PushKill(EnemyKind kind, int combo)
        {
            string who;
            if (kind == EnemyKind.Elite) who = "精英兵";
            else if (kind == EnemyKind.Heavy) who = "重装兵";
            else if (kind == EnemyKind.Rusher) who = "突击兵";
            else who = "步兵";
            killQueue.Add("击杀 " + who + (combo > 1 ? "  x" + combo : ""));
            if (killQueue.Count > 5) killQueue.RemoveAt(0);
            killFeedText.text = string.Join("\n", killQueue.ToArray());
            killTimer = 3.5f;
        }

        // ---------------------------------------------------------- 每帧

        private void Update()
        {
            if (Gm == null) return;
            float dt = Time.unscaledDeltaTime;

            // 武器 / 弹药 / 狙击镜
            if (Gm.Weapons != null)
            {
                var def = Gm.Weapons.Def;
                ammoValue.text = def.Melee ? "∞" : (Gm.Weapons.MagAmmo + " / " + Gm.Weapons.ReserveAmmo);
                string nameText = Gm.Weapons.IsReloading ? "换弹中 RELOADING..."
                    : (Gm.Weapons.IsAds ? "开镜 AIMING · " : "") + def.Name;
                if (weaponName.text != nameText)
                {
                    weaponName.text = nameText;
                    FitText(weaponName, 410f, 26);   // 文本会变长/变短，每次改完都要重新量一遍
                }

                // 狙击完全开镜时显示镜筒遮罩并隐藏普通准星
                bool scoped = def.Kind == WeaponKind.Sniper && Gm.Weapons.IsAds;
                if (scopeOverlay != null && scopeOverlay.gameObject.activeSelf != scoped)
                    scopeOverlay.gameObject.SetActive(scoped);
                if (crosshair != null && crosshair.activeSelf == scoped) crosshair.SetActive(!scoped);
                UpdateWeaponBar();
            }
            // 舱内视角（要在上面那段之后跑：它会按需要把普通准星收起来）
            UpdateCockpit();

            // 生命：主条立刻跟上，残影延迟下滑
            float hp01 = Mathf.Clamp01(Gm.PlayerHp / Gm.PlayerMaxHp);
            hpValue.text = Mathf.CeilToInt(Gm.PlayerHp).ToString();
            SetBarFill(hpFill, hp01);
            hpGhost01 = hpGhost01 > hp01 ? Mathf.MoveTowards(hpGhost01, hp01, dt * 0.45f) : hp01;
            SetBarFill(hpGhost, hpGhost01);
            hpFill.color = HpColor(hp01);

            // 顶部数据
            scoreValue.text = Gm.Score.ToString();
            waveValue.text = "第 " + Gm.Wave + " 波";
            enemyValue.text = (Gm.AliveEnemies + Gm.PendingSpawn).ToString();

            // 小队：积分 / 队友 / 召唤武器倒计时 / 坦克
            if (squadValue != null) squadValue.text = Gm.SquadPoints.ToString();
            UpdateSquad();

            // 命中标记
            if (hitmarkerTimer > 0f)
            {
                hitmarkerTimer -= dt;
                if (hitmarkerTimer <= 0f) hitmarker.gameObject.SetActive(false);
            }

            // 受伤红屏
            if (damageAlpha > 0f)
            {
                damageAlpha = Mathf.MoveTowards(damageAlpha, 0f, dt * 1.6f);
                damageOverlay.color = damageIsHull
                    ? new Color(1.0f, 0.55f, 0.15f, damageAlpha * 0.40f)
                    : new Color(0.8f, 0.05f, 0.05f, damageAlpha * 0.5f);
            }

            // 中央提示
            if (announceTimer > 0f)
            {
                announceTimer -= dt;
                announceText.color = new Color(1f, 0.83f, 0.36f, Mathf.Clamp01(announceTimer));
                if (announceTimer <= 0f) announceText.text = "";
            }

            // 击杀提示
            if (killTimer > 0f)
            {
                killTimer -= dt;
                if (killTimer <= 0f) { killQueue.Clear(); killFeedText.text = ""; }
            }

            // 开局操作提示（几秒后自动消失）
            if (hintTimer > 0f)
            {
                hintTimer -= dt;
                if (hintText != null)
                {
                    string squadPart = Gm != null && Gm.HasSquad ? " · G 小队召唤 · E 上下坦克" : "";
                    hintText.text = GameInput.TouchMode
                        ? "左半屏拖动移动 · 右半屏滑动转视角 · 右上角 II 暂停"
                        : "WASD 移动 · 左键射击 · 右键开镜 · B 建块 · R 换弹 · 滚轮切枪 · 1~5 选武器 · V 切视角 · "
                          + "冲刺+Ctrl 滑铲" + squadPart + " · Esc 暂停";
                    hintText.color = new Color(0.64f, 0.70f, 0.80f, Mathf.Clamp01(hintTimer / 2f));
                    if (hintTimer <= 0f) hintText.text = "";
                }
            }

            // 帧率显示
            fpsAccum += Time.unscaledDeltaTime;
            fpsFrames++;
            if (fpsAccum >= 0.5f)
            {
                fpsTimer = fpsFrames / fpsAccum;
                fpsAccum = 0f; fpsFrames = 0;
            }
            if (fpsText != null)
            {
                bool on = GameSettings.ShowFps && hudPanel.activeSelf;
                fpsText.gameObject.SetActive(on);
                if (on) fpsText.text = Mathf.RoundToInt(fpsTimer) + " FPS";
            }

            // 小地图 / 找敌人
            if (hudPanel.activeSelf)
            {
                UpdateMinimap();
                UpdateHuntArrows();
            }
        }

        /// <summary>实心三角形：小地图玩家朝向、屏幕边缘的敌人方向箭头。</summary>
        private class TriangleGraphic : Graphic
        {
            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                Rect r = rectTransform.rect;
                float hw = r.width * 0.5f;
                float hh = r.height * 0.5f;
                Color32 c = color;
                vh.AddVert(new Vector3(0f, hh), c, Vector2.zero);
                vh.AddVert(new Vector3(-hw, -hh), c, Vector2.zero);
                vh.AddVert(new Vector3(hw, -hh), c, Vector2.zero);
                vh.AddTriangle(0, 1, 2);
            }
        }
    }
}
