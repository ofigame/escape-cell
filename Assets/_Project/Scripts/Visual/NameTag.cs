using TMPro;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>A player's name floating over its foi (or army), always turned to the camera.</summary>
    public class NameTag : MonoBehaviour
    {
        private Transform target;
        private float height;
        private TextMeshPro text;

        public static NameTag Create(Transform target, string name, Color colour, float height = 1.35f)
        {
            var go = new GameObject("NameTag");
            var tag = go.AddComponent<NameTag>();
            tag.target = target;
            tag.height = height;
            tag.text = go.AddComponent<TextMeshPro>();
            tag.text.font = SquashBot.UI.UiFactory.Font;
            tag.text.textPreprocessor = new SquashBot.UI.RtlTextPreprocessor(tag.text);
            tag.text.text = name;
            tag.text.fontSize = 3.2f;
            tag.text.fontStyle = FontStyles.Bold;
            tag.text.alignment = TextAlignmentOptions.Center;
            tag.text.color = colour;
            tag.text.outlineWidth = 0.25f;
            tag.text.outlineColor = new Color32(10, 10, 20, 255);
            tag.text.rectTransform.sizeDelta = new Vector2(8f, 1.5f);
            tag.LateUpdate();
            return tag;
        }

        /// <summary>A new name (the player renamed).</summary>
        public void SetText(string name) => text.text = name;

        /// <summary>Follows another object (the rival foi changes between knock-downs).</summary>
        public void Follow(Transform t) => target = t;

        private void LateUpdate()
        {
            if (target == null) { text.enabled = false; return; }
            text.enabled = target.gameObject.activeInHierarchy;
            transform.position = target.position + Vector3.up * height;
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
