using System;

namespace Brante.Gamepad
{
    internal enum KeyboardAction { Delete, Case, Done, Cancel }

    // Grid of characters followed by a row of action keys. Key index i < Characters.Length is a character;
    // Characters.Length + n is the nth action.
    internal sealed class KeyboardLayout
    {
        internal const int ActionCount = 4;
        internal readonly string Characters;
        internal readonly int Columns;

        internal KeyboardLayout(string characters, int columns)
        {
            Characters = characters;
            Columns = columns;
        }

        internal int KeyCount { get { return Characters.Length + ActionCount; } }
        internal int Rows { get { return (Characters.Length + Columns - 1) / Columns; } }

        internal bool IsAction(int index) { return index >= Characters.Length; }
        internal KeyboardAction ActionAt(int index) { return (KeyboardAction)(index - Characters.Length); }
        internal int ActionIndex(KeyboardAction action) { return Characters.Length + (int)action; }

        // Placement on screen: grid keys use (index % Columns, index / Columns); actions sit on one row below.
        internal int ColumnOf(int index)
        {
            return IsAction(index) ? (2 * (index - Characters.Length) + 1) * Columns / (2 * ActionCount) : index % Columns;
        }
        internal int RowOf(int index) { return IsAction(index) ? Rows : index / Columns; }

        internal string Label(int index, bool lowerCase)
        {
            if (IsAction(index))
            {
                switch (ActionAt(index))
                {
                    case KeyboardAction.Delete: return "Delete";
                    case KeyboardAction.Case: return "Aa";
                    case KeyboardAction.Done: return "Done";
                    default: return "Cancel";
                }
            }
            char letter = Characters[index];
            return letter == ' ' ? "Space" : (lowerCase ? char.ToLowerInvariant(letter) : letter).ToString();
        }

        internal char Character(int index, bool lowerCase)
        {
            char letter = Characters[index];
            return lowerCase ? char.ToLowerInvariant(letter) : letter;
        }

        // dx/dy: right is +1 x, down is +1 y (reading order). Stays put at an edge.
        internal int Move(int index, int dx, int dy)
        {
            int last = Characters.Length - 1;
            if (dx != 0)
            {
                if (IsAction(index)) return Math.Max(Characters.Length, Math.Min(KeyCount - 1, index + dx));
                return Math.Max(0, Math.Min(last, index + dx));
            }
            if (dy == 0) return index;
            if (IsAction(index))
            {
                if (dy > 0) return index;
                int column = ColumnOf(index);
                int lastRowStart = (Rows - 1) * Columns;
                return Math.Min(last, lastRowStart + column);
            }
            int row = index / Columns;
            if (dy < 0) return row == 0 ? index : index - Columns;
            if (index + Columns <= last) return index + Columns;
            if (row < Rows - 1) return last; // short last row: land on its final key
            int slot = (2 * (index % Columns) + 1) * ActionCount / (2 * Columns); // action keys span the grid width evenly
            return Characters.Length + Math.Min(ActionCount - 1, slot);
        }

        // Matches the game's Latin default; other languages add their own letters.
        internal static KeyboardLayout ForLanguage(string languageCode)
        {
            string code = (languageCode ?? "").ToLowerInvariant();
            int dash = code.IndexOfAny(new[] { '-', '_' });
            if (dash > 0) code = code.Substring(0, dash);
            const string Latin = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string Marks = "-.' ";
            switch (code)
            {
                case "ru": return new KeyboardLayout("АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ" + Marks, 9);
                case "de": return new KeyboardLayout(Latin + "ÄÖÜ" + Marks, 9);
                case "fr": return new KeyboardLayout(Latin + "ÀÂÇÉÈÊËÎÏÔÙÛŒ" + Marks, 9);
                case "es": return new KeyboardLayout(Latin + "ÁÉÍÑÓÚÜ" + Marks, 9);
                case "it": return new KeyboardLayout(Latin + "ÀÈÉÌÒÙ" + Marks, 9);
                case "pt": return new KeyboardLayout(Latin + "ÁÂÃÀÇÉÊÍÓÔÕÚ" + Marks, 9);
                case "pl": return new KeyboardLayout(Latin + "ĄĆĘŁŃÓŚŹŻ" + Marks, 9);
                case "tr": return new KeyboardLayout(Latin + "ÇĞİÖŞÜ" + Marks, 9);
                default: return new KeyboardLayout(Latin + Marks, 8);
            }
        }
    }

    // Text being edited by the controller keyboard; applies the field's character limit.
    internal sealed class KeyboardText
    {
        internal string Value;
        internal readonly int Limit;
        internal KeyboardText(string value, int limit) { Value = value ?? ""; Limit = limit; }

        internal bool Append(char letter)
        {
            if (Limit > 0 && Value.Length >= Limit) return false;
            Value += letter;
            return true;
        }

        internal bool Delete()
        {
            if (Value.Length == 0) return false;
            Value = Value.Substring(0, Value.Length - 1);
            return true;
        }
    }
}
