using System.Collections.Generic;
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
    /// 安卓 / iOS 触控操作：左下常驻浮动摇杆移动，右侧滑动转视角，
    /// 右下角战斗键（FIRE / 正上方 RELOAD / 左侧 JUMP / 左上 AIM），
    /// 右上角暂停键旁放切视角，左上放小队召唤与上下坦克。全部图标化，切枪走右侧武器栏点击。
    /// UI 以 1920x1080 参考分辨率设计、随屏幕等比缩放。
    /// </summary>
    public class MobileControls : MonoBehaviour
    {
        public Canvas Canvas;
        public Image JoystickBase;
        public Image JoystickKnob;
        public HoldButton FireButton;

        private HoldButton jumpBtn, reloadBtn, viewBtn, aimBtn;
        private HoldButton squadBtn, mountBtn;   // 小队召唤 / 上下坦克
        private RectTransform[] buttonRects;
        private int moveFinger = -1;
        private Vector2 joystickCenter;                    // 摇杆当前中心（Canvas 坐标）
        private const float JoyRadius = 120f;
        private static readonly Vector2 JoyHome = new Vector2(230f, 230f);   // 摇杆待机位（Canvas 坐标，左下原点）

        /// <summary>由 HUD 注册的可点击区域（如武器栏）：这些区域内不触发转视角。</summary>
        private static readonly List<RectTransform> ExtraBlockers = new List<RectTransform>();

        public static void RegisterBlocker(RectTransform rt)
        {
            if (rt == null) return;
            if (ExtraBlockers.Contains(rt)) return;
            ExtraBlockers.Add(rt);
        }

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
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // 摇杆：常驻在左下待机位；按住左半屏任意位置时底盘跟到拇指下
            var baseGo = new GameObject("JoystickBase");
            baseGo.transform.SetParent(canvasGo.transform, false);
            JoystickBase = baseGo.AddComponent<Image>();
            JoystickBase.color = new Color(1f, 1f, 1f, 0.16f);
            var brt = baseGo.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.zero;
            brt.sizeDelta = new Vector2(JoyRadius * 2f, JoyRadius * 2f);
            brt.anchoredPosition = JoyHome;

            var knobGo = new GameObject("Knob");
            knobGo.transform.SetParent(baseGo.transform, false);
            JoystickKnob = knobGo.AddComponent<Image>();
            JoystickKnob.color = new Color(1f, 1f, 1f, 0.42f);
            knobGo.GetComponent<RectTransform>().sizeDelta = new Vector2(86f, 86f);

            // ---- 右下战斗键（避开 HUD：弹药 x≥1450 y≤152、武器栏 x≥1610 y≥265）----
            FireButton = MakeButton(canvasGo.transform, "BtnFire", new Vector2(1310f, 210f), new Vector2(205f, 205f),
                new Color(0.85f, 0.25f, 0.22f, 0.55f), IconSprites.Fire());
            reloadBtn = MakeButton(canvasGo.transform, "BtnReload", new Vector2(1310f, 500f), new Vector2(150f, 150f),
                new Color(0.35f, 0.45f, 0.35f, 0.5f), IconSprites.Reload());      // 换弹在开火键正上方
            jumpBtn = MakeButton(canvasGo.transform, "BtnJump", new Vector2(1090f, 200f), new Vector2(150f, 150f),
                new Color(0.25f, 0.55f, 0.85f, 0.5f), IconSprites.Jump());
            aimBtn = MakeButton(canvasGo.transform, "BtnAim", new Vector2(1090f, 470f), new Vector2(140f, 140f),
                new Color(0.30f, 0.52f, 0.52f, 0.5f), IconSprites.Aim());

            // ---- 右上角：切视角放在暂停键（HUD 的 II，x 1802~1898 y 982~1078）左边 ----
            viewBtn = MakeButton(canvasGo.transform, "BtnView", new Vector2(1700f, 1030f), new Vector2(96f, 96f),
                new Color(0.40f, 0.50f, 0.40f, 0.5f), IconSprites.View());

            // ---- 左上战术键（避开积分卡 x≤500 与队友血量行）----
            squadBtn = MakeButton(canvasGo.transform, "BtnSquad", new Vector2(620f, 915f), new Vector2(130f, 130f),
                new Color(0.20f, 0.55f, 0.70f, 0.5f), IconSprites.Squad());
            mountBtn = MakeButton(canvasGo.transform, "BtnMount", new Vector2(620f, 755f), new Vector2(120f, 120f),
                new Color(0.80f, 0.50f, 0.20f, 0.5f), IconSprites.Ride());

            buttonRects = new[]
            {
                FireButton.GetComponent<RectTransform>(),
                reloadBtn.GetComponent<RectTransform>(),
                jumpBtn.GetComponent<RectTransform>(),
                aimBtn.GetComponent<RectTransform>(),
                viewBtn.GetComponent<RectTransform>(),
                squadBtn.GetComponent<RectTransform>(),
                mountBtn.GetComponent<RectTransform>()
            };
        }

        private HoldButton MakeButton(Transform parent, string name, Vector2 pos, Vector2 size, Color color, Sprite icon)
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

            if (icon != null)
            {
                // 图标：按钮内居中，占 58%，白色由父级 Image 之外的自身 color 决定
                var icoGo = new GameObject("Icon");
                icoGo.transform.SetParent(go.transform, false);
                var ico = icoGo.AddComponent<Image>();
                ico.sprite = icon;
                ico.color = new Color(1f, 1f, 1f, 0.95f);
                ico.raycastTarget = false;
                var irt = ico.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.5f, 0.5f); irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = Vector2.zero;
                float s = Mathf.Min(size.x, size.y) * 0.58f;
                irt.sizeDelta = new Vector2(s, s);
            }
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
                    GameInput.TouchMoveAxis = d / JoyRadius;
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

                // 右侧拖动 = 转视角（排除按钮、排除 HUD 注册的可点区域；delta 用屏幕像素，灵敏度不变）
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
            GameInput.TouchAim = aimBtn.Held;
            GameInput.TouchToggleView = TakeTap(viewBtn);
            GameInput.TouchCallIn = TakeTap(squadBtn);
            GameInput.TouchInteract = TakeTap(mountBtn);

            // 这几个动作触屏上不再给按钮（建块 / 滑铲 / 检视 / 切枪），切枪改为点右侧武器栏
            GameInput.TouchNextWeapon = false;
            GameInput.TouchPlace = false;
            GameInput.TouchSlide = false;
            GameInput.TouchInspect = false;
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
            Camera cam = Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Canvas.worldCamera;
            if (buttonRects != null)
            {
                for (int i = 0; i < buttonRects.Length; i++)
                {
                    if (buttonRects[i] != null && RectTransformUtility.RectangleContainsScreenPoint(buttonRects[i], screenPos, cam))
                        return true;
                }
            }
            for (int i = 0; i < ExtraBlockers.Count; i++)
            {
                if (ExtraBlockers[i] != null && RectTransformUtility.RectangleContainsScreenPoint(ExtraBlockers[i], screenPos, cam))
                    return true;
            }
            return false;
        }
    }
}
