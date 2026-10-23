using UnityEngine.UI;

namespace TMPro
{
    public class TMP_Text : MaskableGraphic
    {
        public virtual string text { get { return null; } set { } }
    }

    public class TMP_InputField : Selectable
    {
        public string text { get { return null; } set { } }
        public int characterLimit { get { return 0; } set { } }
        public bool readOnly { get { return false; } set { } }
        public Graphic placeholder { get { return null; } set { } }
    }
}
