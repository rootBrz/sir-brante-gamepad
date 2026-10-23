using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Brante.Gamepad
{
    // On-screen keyboard for text fields. The layout follows the game's language.
    internal sealed class ControllerKeyboard
    {
        private TMP_InputField textField;
        private InputField legacyField;
        private EventSystem system;
        private KeyboardLayout layout;
        private KeyboardText text;
        private string prompt;
        private int index, direction;
        private float nextRepeat;
        private bool lowerCase;
        private GUIStyle style;

        internal bool IsOpen { get; private set; }

        internal void Open(Probe field, Action<string> trace)
        {
            textField = field.textField;
            legacyField = field.legacyField;
            string current = textField != null ? textField.text : legacyField.text;
            int limit = textField != null ? textField.characterLimit : legacyField.characterLimit;
            Graphic placeholder = textField != null ? textField.placeholder : legacyField.placeholder;
            TMP_Text tmpPlaceholder = placeholder as TMP_Text;
            Text legacyPlaceholder = placeholder as Text;
            string hint = tmpPlaceholder != null ? tmpPlaceholder.text : legacyPlaceholder != null ? legacyPlaceholder.text : "";
            prompt = string.IsNullOrEmpty(hint) ? "Text" : hint;
            layout = KeyboardLayout.ForLanguage(GameLanguage.Code());
            text = new KeyboardText(current, limit);
            index = 0;
            direction = 0;
            system = EventSystem.current;
            if (system != null) system.enabled = false;
            IsOpen = true;
            if (trace != null) trace("Controller keyboard opened (" + layout.Characters.Length + " characters)");
        }

        internal void Update(Buttons buttons, Buttons pressed, Bindings bindings, float now, Action<string> trace)
        {
            if (textField == null && legacyField == null) { Close(false, trace); return; }
            int wanted = (buttons & Buttons.DPadUp) != 0 ? 1 : (buttons & Buttons.DPadDown) != 0 ? 2 :
                (buttons & Buttons.DPadLeft) != 0 ? 3 : (buttons & Buttons.DPadRight) != 0 ? 4 : 0;
            if (wanted != 0 && (wanted != direction || now >= nextRepeat))
            {
                index = layout.Move(index, wanted == 3 ? -1 : wanted == 4 ? 1 : 0, wanted == 1 ? -1 : wanted == 2 ? 1 : 0);
                nextRepeat = now + (wanted != direction ? 0.35f : 0.14f);
            }
            direction = wanted;
            if ((pressed & bindings.Back) != 0) Close(false, trace);
            else if ((pressed & bindings.ModMenu) != 0) Close(true, trace);
            else if ((pressed & bindings.Skip) != 0) Press(layout.ActionIndex(KeyboardAction.Delete), trace);
            else if ((pressed & bindings.Keyboard) != 0) lowerCase = !lowerCase;
            else if ((pressed & bindings.Click) != 0) Press(index, trace);
        }

        private void Press(int key, Action<string> trace)
        {
            if (!layout.IsAction(key)) { text.Append(layout.Character(key, lowerCase)); return; }
            switch (layout.ActionAt(key))
            {
                case KeyboardAction.Delete: text.Delete(); break;
                case KeyboardAction.Case: lowerCase = !lowerCase; break;
                case KeyboardAction.Done: Close(true, trace); break;
                default: Close(false, trace); break;
            }
        }

        internal void Close(bool apply, Action<string> trace)
        {
            if (apply)
            {
                if (textField != null && textField.IsInteractable() && !textField.readOnly) textField.text = text.Value;
                if (legacyField != null && legacyField.IsInteractable() && !legacyField.readOnly) legacyField.text = text.Value;
            }
            IsOpen = false;
            textField = null;
            legacyField = null;
            if (system != null) system.enabled = true;
            system = null;
            direction = 0;
            if (trace != null) trace(apply ? "Controller keyboard accepted" : "Controller keyboard cancelled");
        }

        internal void Draw(Bindings bindings)
        {
            if (!IsOpen) return;
            if (style == null) style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter };
            style.fontSize = Mathf.Max(18, Screen.height / 40);
            float width = Mathf.Min(Screen.width - 40, 960), height = Mathf.Min(Screen.height - 40, 680);
            Rect box = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);
            Color color = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.08f, 1);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.Box(box, "");
            GUI.Label(new Rect(box.x + 20, box.y + 15, width - 40, 40), prompt + ": " + text.Value, style);
            int rows = layout.Rows + 1;
            float gridWidth = width - 40, gridTop = box.y + 70;
            float cellWidth = gridWidth / layout.Columns, cellHeight = (height - 140) / rows;
            for (int i = 0; i < layout.KeyCount; i++)
            {
                Rect cell;
                if (layout.IsAction(i))
                {
                    float slot = gridWidth / KeyboardLayout.ActionCount;
                    cell = new Rect(box.x + 20 + (i - layout.Characters.Length) * slot, gridTop + layout.Rows * cellHeight, slot - 6, cellHeight - 6);
                }
                else cell = new Rect(box.x + 20 + layout.ColumnOf(i) * cellWidth, gridTop + layout.RowOf(i) * cellHeight, cellWidth - 6, cellHeight - 6);
                GUI.color = i == index ? new Color(1f, 0.8f, 0.3f) : color;
                if (GUI.Button(cell, layout.Label(i, lowerCase), style)) Press(i, null);
            }
            GUI.color = color;
            GUI.Label(new Rect(box.x + 20, box.yMax - 55, width - 40, 40), HintText.KeyboardHelp(bindings));
        }
    }

    internal static class GameLanguage
    {
        // I2 Localization's code for the active language, e.g. "ru". Empty when it cannot be read.
        internal static string Code()
        {
            try
            {
                Type manager = AccessTools.TypeByName("I2.Loc.LocalizationManager");
                System.Reflection.PropertyInfo property = manager == null ? null : AccessTools.Property(manager, "CurrentLanguageCode");
                string code = property == null ? null : property.GetValue(null, null) as string;
                return code ?? "";
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}
