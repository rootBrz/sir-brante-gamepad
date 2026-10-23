using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Brante.Gamepad
{
    // What lies under the pointer.
    internal struct Probe
    {
        internal GameObject target;
        internal Box box;          // screen-space bounds of target; valid when target != null
        internal TMP_InputField textField;
        internal InputField legacyField;
        internal HintContext context;
        internal bool HasField { get { return textField != null || legacyField != null; } }
    }

    // Reads the live UI: finds clickable controls, ranks them for D-pad navigation, and describes the hovered one.
    internal sealed class UiScanner
    {
        private const int MaxAttempts = 32;
        private static readonly float[,] Samples =
        {
            { 0f, 0f }, { -0.3f, 0f }, { 0.3f, 0f }, { 0f, -0.3f }, { 0f, 0.3f },
            { -0.3f, -0.3f }, { 0.3f, -0.3f }, { -0.3f, 0.3f }, { 0.3f, 0.3f }
        };

        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private readonly Vector3[] corners = new Vector3[4];
        private readonly Dictionary<GameObject, int> index = new Dictionary<GameObject, int>();
        private readonly List<GameObject> targets = new List<GameObject>();
        private readonly List<Box> boxes = new List<Box>();
        private readonly List<int> order = new List<int>();
        private PointerEventData pointer;
        private EventSystem pointerSystem;

        internal bool Raycast(Vector2 position)
        {
            if (EventSystem.current == null) return false;
            if (pointerSystem != EventSystem.current)
            {
                pointerSystem = EventSystem.current;
                pointer = new PointerEventData(pointerSystem);
            }
            pointer.position = position;
            hits.Clear();
            pointerSystem.RaycastAll(pointer, hits);
            return hits.Count > 0;
        }

        // The object that would receive a click, hover or press for this graphic.
        private static GameObject Target(GameObject graphic)
        {
            GameObject target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(graphic);
            if (target == null) target = ExecuteEvents.GetEventHandler<IPointerDownHandler>(graphic);
            if (target == null) target = ExecuteEvents.GetEventHandler<IPointerEnterHandler>(graphic);
            return target;
        }

        private Box ScreenBox(RectTransform rect, Canvas canvas)
        {
            rect.GetWorldCorners(corners);
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 first = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Box box = Box.Point(first.x, first.y);
            for (int i = 1; i < 4; i++)
            {
                Vector2 corner = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                box = box.Union(Box.Point(corner.x, corner.y));
            }
            return box;
        }

        internal Probe Inspect(Vector2 position)
        {
            Probe result = new Probe();
            if (!Raycast(position)) return result;
            GameObject top = hits[0].gameObject;
            GameObject target = Target(top);
            TMP_InputField tmp = top.GetComponentInParent<TMP_InputField>();
            InputField legacy = top.GetComponentInParent<InputField>();
            if (tmp != null && (!tmp.IsInteractable() || tmp.readOnly)) tmp = null;
            if (legacy != null && (!legacy.IsInteractable() || legacy.readOnly)) legacy = null;
            result.textField = tmp;
            result.legacyField = legacy;
            result.context.OverTextField = tmp != null || legacy != null;
            result.context.OverScrollable = top.GetComponentInParent<ScrollRect>() != null;
            result.context.OverDraggable = top.GetComponentInParent<Slider>() != null || top.GetComponentInParent<Scrollbar>() != null;
            RectTransform rect = target != null ? target.GetComponent<RectTransform>() : null;
            if (rect != null)
            {
                result.target = target;
                result.box = ScreenBox(rect, target.GetComponentInParent<Canvas>());
            }
            return result;
        }

        private void Collect()
        {
            index.Clear();
            targets.Clear();
            boxes.Clear();
            Box screen = new Box(0, 0, Screen.width, Screen.height);
            foreach (Graphic graphic in Object.FindObjectsOfType<Graphic>())
            {
                if (!graphic.isActiveAndEnabled || !graphic.raycastTarget || graphic.canvas == null) continue;
                GameObject target = Target(graphic.gameObject);
                if (target == null) continue;
                Box box = ScreenBox(graphic.rectTransform, graphic.canvas).Intersect(screen);
                if (box.Width <= 0f || box.Height <= 0f) continue;
                int slot;
                if (index.TryGetValue(target, out slot)) boxes[slot] = boxes[slot].Union(box);
                else
                {
                    index[target] = targets.Count;
                    targets.Add(target);
                    boxes.Add(box);
                }
            }
        }

        // Moves toward the best control in the direction (dx right/left, dy up/down). Succeeds with a screen
        // position that really hits that control; a covered or modal-blocked control is skipped.
        internal bool Move(Vector2 pointerPosition, int dx, int dy, bool wrap, out Vector2 destination, out string name)
        {
            destination = pointerPosition;
            name = null;
            Collect();
            float screenArea = Screen.width * (float)Screen.height;
            Box from = Box.Point(pointerPosition.x, pointerPosition.y);
            GameObject current = null;
            if (Raycast(pointerPosition)) current = Target(hits[0].gameObject);
            int currentSlot;
            if (current != null && index.TryGetValue(current, out currentSlot) && boxes[currentSlot].Area <= screenArea * 0.25f)
                from = boxes[currentSlot];
            else
                current = null;

            // Hide oversized controls (full-screen click catchers) and the current one from the candidates.
            List<int> slots = new List<int>();
            List<Box> candidates = new List<Box>();
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] == current || boxes[i].Area > screenArea * 0.5f) continue;
                slots.Add(i);
                candidates.Add(boxes[i]);
            }
            Navigator.Rank(from, candidates, dx, dy, wrap, order);
            for (int n = 0; n < order.Count && n < MaxAttempts; n++)
            {
                int slot = slots[order[n]];
                Box box = boxes[slot];
                for (int s = 0; s < Samples.GetLength(0); s++)
                {
                    Vector2 point = new Vector2(box.CenterX + Samples[s, 0] * box.Width, box.CenterY + Samples[s, 1] * box.Height);
                    if (!Raycast(point) || Target(hits[0].gameObject) != targets[slot]) continue;
                    destination = point;
                    name = targets[slot].name;
                    return true;
                }
            }
            return false;
        }
    }
}
