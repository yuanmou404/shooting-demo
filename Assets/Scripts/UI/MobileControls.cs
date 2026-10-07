using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PixelArena
{
    /// <summary>可被按住 / 点击的触控按钮。</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public bool Held;
        public bool Tapped;

        public void OnPointerDown(PointerEventData eventData)
        {
            Held = true;
            Tapped = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Held = false;
        }
    }

    /// <summary>
    /// 安卓 / iOS 触控操作：左下常驻虚拟摇杆移动（浮动跟随拇指），右侧滑动转视角，
    /// 右下角战斗按钮群（FIRE/AIM/JUMP/RELOAD）+ 中部低频按钮 + 左上战术按钮。
    /// UI 以 1920x1080 参考分辨率设计、随屏幕等比缩放，高分屏不再挤成一团。
    /// </summary>
    public class MobileControls : MonoBehaviour
    {
        public Canvas Canvas;
        public Image JoystickBase;
        public Image JoystickKnob;
        public HoldButton FireButton;

        private HoldButton jumpBtn, reloadBtn, switchBtn, viewBtn, buildBtn, inspectBtn, aimBtn;
        private HoldButton squadBtn, mountBtn, slideBtn;   // SQUAD 召唤面板 / RIDE 上下坦克 / SLIDE 滑铲
        private RectTransform[] buttonRects;
        private int moveFinger = -1;
        private Vector2 joystickCenter;                    // 摇杆当前中心（Canvas 坐标）
        private const float JoyRadius = 120f;
        private static readonly Vector2 JoyHome = new Vector2(230f, 230f);   // 摇杆待机位（Canvas 坐标，左下原点）

        public static MobileControls Create()
        {
            var go = new GameObject("MobileControls");
            var mc = go.AddComponent<MobileControls>();
            mc.Build();
            mc.SetVisible(false);   // 主菜单 / 暂停时隐藏触控按钮
            return mc;
        }

        /// <summary>只在真正开打时显示触控 UI。</summary>
        public void SetVisible(bool v)
        {
            if (Canvas != null) Canvas.gameObject.SetActive(v);
        }

        private void Build()
        {
            var canvasGo = new GameObject("MobileCanvas");
            canvasGo.transform.SetParent(transform, false);
            Canvas = canvasGo.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 200;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            // 关键修复：此前用 ConstantPixelSize，在 2400x1080 手机上所有按钮按原始像素排布，
            // 全部挤在屏幕左侧 1/3（右边大片空白）。改为 1920x1080 参考分辨率等比缩放。
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // 摇杆：常驻显示在左下待机位；按住左半屏任意位置时，底盘跟到拇指下（浮动摇杆）
            var baseGo = new GameObject("JoystickBase");
            baseGo.transform.SetParent(canvasGo.transform, false);
            JoystickBase = baseGo.AddComponent<Image>();
            JoystickBase.color = new Color(1f, 1f, 1f, 0.18f);
            var brt = baseGo.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.zero;
            brt.sizeDelta = new Vector2(JoyRadius * 2f, JoyRadius * 2f);
            brt.anchoredPosition = JoyHome;

            var knobGo = new GameObject("Knob");
            knobGo.transform.SetParent(baseGo.transform, false);
            JoystickKnob = knobGo.AddComponent<Image>();
            JoystickKnob.color = new Color(1f, 1f, 1f, 0.45f);
            knobGo.GetComponent<RectTransform>().sizeDelta = new Vector2(86f, 86f);

            // ---- 右下战斗群（避开 HUD：弹药 x≥1450 y≤152、武器栏 x≥1610 y≥265，均为 1920x1080 系）----
            FireButton = MakeButton(canvasGo.transform, "BtnFire", "FIRE", new Vector2(1310f, 210f), new Vector2(205f, 205f), new Color(0.85f, 0.25f, 0.22f, 0.55f));
            aimBtn     = MakeButton(canvasGo.transform, "BtnAim", "AIM", new Vector2(1095f, 200f), new Vector2(145f, 145f), new Color(0.30f, 0.52f, 0.52f, 0.5f));
            jumpBtn    = MakeButton(canvasGo.transform, "BtnJump", "JUMP", new Vector2(1310f, 490f), new Vector2(145f, 145f), new Color(0.25f, 0.55f, 0.85f, 0.5f));
            reloadBtn  = MakeButton(canvasGo.transform, "BtnReload", "RELOAD", new Vector2(1095f, 480f), new Vector2(130f, 130f), new Color(0.35f, 0.45f, 0.35f, 0.5f));

            // ---- 中列：切枪 / 建块 / 视角（中低频，竖排往上看一眼再点）----
            switchBtn  = MakeButton(canvasGo.transform, "BtnSwitch", "GUN", new Vector2(890f, 200f), new Vector2(120f, 120f), new Color(0.45f, 0.40f, 0.55f, 0.5f));
            buildBtn   = MakeButton(canvasGo.transform, "BtnBuild", "BUILD", new Vector2(890f, 455f), new Vector2(115f, 115f), new Color(0.60f, 0.50f, 0.25f, 0.5f));
            viewBtn    = MakeButton(canvasGo.transform, "BtnView", "VIEW", new Vector2(890f, 665f), new Vector2(110f, 110f), new Color(0.40f, 0.50f, 0.40f, 0.5f));
            inspectBtn = MakeButton(canvasGo.transform, "BtnInspect", "LOOK", new Vector2(890f, 860f), new Vector2(110f, 110f), new Color(0.45f, 0.35f, 0.50f, 0.5f));
            slideBtn   = MakeButton(canvasGo.transform, "BtnSlide", "SLIDE", new Vector2(1095f, 680f), new Vector2(125f, 125f), new Color(0.55f, 0.45f, 0.28f, 0.5f));

            // ---- 左上战术按钮（避开积分卡 x≤500 与队友血量行）----
            squadBtn   = MakeButton(canvasGo.transform, "BtnSquad", "SQUAD", new Vector2(620f, 915f), new Vector2(130f, 130f), new Color(0.20f, 0.55f, 0.70f, 0.5f));
            mountBtn   = MakeButton(canvasGo.transform, "BtnMount", "RIDE", new Vector2(620f, 755f), new Vector2(120f, 120f), new Color(0.80f, 0.50f, 0.20f, 0.5f));

            buttonRects = new[]
            {
                FireButton.GetComponent<RectTransform>(),
                jumpBtn.GetComponent<RectTransform>(),
                reloadBtn.GetComponent<RectTransform>(),
                switchBtn.GetComponent<RectTransform>(),
                viewBtn.GetComponent<RectTransform>(),
                buildBtn.GetComponent<RectTransform>(),
                inspectBtn.GetComponent<RectTransform>(),
                aimBtn.GetComponent<RectTransform>(),
                squadBtn.GetComponent<RectTransform>(),
                mountBtn.GetComponent<RectTransform>(),
                slideBtn.GetComponent<RectTransform>()
            };
        }

        private HoldButton MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.zero;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var btn = go.AddComponent<HoldButton>();

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var t = textGo.AddComponent<Text>();
            t.text = label;
            // 字号随按钮尺寸缩放（1920x1080 参考系下自动适配高分屏）
            t.fontSize = Mathf.Max(16, Mathf.RoundToInt(size.x * 0.155f));
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            return btn;
        }

        /// <summary>由 GameManager 在读取输入之前调用，避免 Unity 脚本执行顺序导致的一帧延迟。</summary>
        public void Tick()
        {
            if (!GameInput.TouchMode) return;

            float w = Screen.width;
            // Canvas 用 ScaleWithScreenSize 后 Canvas 坐标 ≠ 屏幕像素，触点必须换算
            float sf = Canvas != null && Canvas.scaleFactor > 0f ? Canvas.scaleFactor : 1f;
            bool moveActive = false;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                Vector2 p = touch.position / sf;   // 屏幕像素 → Canvas 坐标

                if (touch.fingerId == moveFinger)
                {
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        moveFinger = -1;
                        GameInput.TouchMoveAxis = Vector2.zero;
                        GameInput.TouchSprint = false;
                        JoystickKnob.rectTransform.anchoredPosition = Vector2.zero;
                        JoystickBase.rectTransform.anchoredPosition = JoyHome;   // 回待机位（常驻显示）
                        continue;
                    }
                    Vector2 d = p - joystickCenter;
                    float len = d.magnitude;
                    if (len > JoyRadius) d = d / len * JoyRadius;
                    Vector2 axis = d / JoyRadius;
                    GameInput.TouchMoveAxis = axis;
                    GameInput.TouchSprint = len > JoyRadius * 0.92f;
                    JoystickKnob.rectTransform.anchoredPosition = d;
                    moveActive = true;
                    continue;
                }

                if (touch.phase == TouchPhase.Began && moveFinger == -1)
                {
                    if (touch.position.x < w * 0.45f && !IsOverButton(touch.position))
                    {
                        moveFinger = touch.fingerId;
                        joystickCenter = p;
                        JoystickBase.rectTransform.anchoredPosition = p;
                        JoystickKnob.rectTransform.anchoredPosition = Vector2.zero;
                        continue;
                    }
                }

                // 右侧拖动 = 转视角（不包含按钮区域与移动摇杆；delta 用屏幕像素，灵敏度与旧版一致）
                if (touch.fingerId != moveFinger && !IsOverButton(touch.position) && touch.position.x >= w * 0.35f)
                {
                    GameInput.TouchLookDelta += touch.deltaPosition;
                }
            }

            if (!moveActive && moveFinger == -1)
            {
                GameInput.TouchMoveAxis = Vector2.zero;
            }

            GameInput.TouchFire = FireButton.Held;
            GameInput.TouchJump = jumpBtn.Held;
            GameInput.TouchReload = TakeTap(reloadBtn);
            GameInput.TouchNextWeapon = TakeTap(switchBtn);
            GameInput.TouchToggleView = TakeTap(viewBtn);
            GameInput.TouchPlace = buildBtn.Held;
            GameInput.TouchInspect = TakeTap(inspectBtn);
            GameInput.TouchAim = aimBtn.Held;
            GameInput.TouchCallIn = TakeTap(squadBtn);
            GameInput.TouchInteract = TakeTap(mountBtn);
            GameInput.TouchSlide = TakeTap(slideBtn);
        }

        private bool TakeTap(HoldButton b)
        {
            if (b.Tapped)
            {
                b.Tapped = false;
                return true;
            }
            return false;
        }

        private bool IsOverButton(Vector2 screenPos)
        {
            if (buttonRects == null) return false;
            Camera cam = Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Canvas.worldCamera;
            for (int i = 0; i < buttonRects.Length; i++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(buttonRects[i], screenPos, cam)) return true;
            }
            return false;
        }
    }
}
