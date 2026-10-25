using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// A fighter's face in the duel (the design's §2.1): plays a <see cref="FaceMoment"/> — the lids and brows move to it
    /// in under a tenth of a second, the drawn mouth and eyes are laid over the painted ones (through the head's face
    /// map), the face tinted (blue when frozen), the hair up (shocked). A stronger moment is never cut short by a weaker
    /// one; when a moment's time is up the face goes back to the one under it (worried at low health, else set).
    /// </summary>
    public class FaceExpressionController : MonoBehaviour
    {
        private SkinnedHero skin;
        private FaceExpression current, resting;
        private float until;
        private float lids, brows;
        private MaterialPropertyBlock block;
        private Texture2D shownTex;
        private bool drawnOn;
        private float chatterT;

        private static readonly int FaceOnId = Shader.PropertyToID("_FaceOn"), FaceTexId = Shader.PropertyToID("_FaceTex"), FaceMapId = Shader.PropertyToID("_FaceMap");
        private static readonly int SkinToneId = Shader.PropertyToID("_SkinTone"), FaceTintId = Shader.PropertyToID("_FaceTint");

        public FaceMoment Resting => resting != null ? resting.moment : FaceMoment.Idle;

        public FaceMoment Current => current != null && Time.time < until ? current.moment : resting != null ? resting.moment : FaceMoment.Idle;

        public static FaceExpressionController On(Transform model)
        {
            if (model == null) return null;
            var s = model.GetComponentInChildren<SkinnedHero>();
            if (s == null) return null;
            var c = s.GetComponent<FaceExpressionController>();
            return c != null ? c : s.gameObject.AddComponent<FaceExpressionController>();
        }

        private void Awake()
        {
            skin = GetComponent<SkinnedHero>();
            resting = FaceExpressionData.Current.Of(FaceMoment.Idle);
        }

        /// <summary>The face answers <paramref name="m"/> (unless a stronger moment is still showing).</summary>
        public void Play(FaceMoment m)
        {
            var f = FaceExpressionData.Current.Of(m);
            if (current != null && Time.time < until && current.priority > f.priority) return;
            current = f;
            until = Time.time + f.seconds;
        }

        /// <summary>The face under the moments: worried (low health) or set (the duel), or none (outside a fight).</summary>
        public void Rest(FaceMoment m) => resting = FaceExpressionData.Current.Of(m);

        /// <summary>Back to the plain face at once (a new round, the menu).</summary>
        public void Clear()
        {
            current = null;
            until = 0f;
            resting = FaceExpressionData.Current.Of(FaceMoment.Idle);
        }

        /// <summary>Shows the face as it is now at once (before a stand-in copies the body this frame).</summary>
        public void ApplyNow() => LateUpdate();

        private void LateUpdate()
        {
            if (skin == null) return;
            var f = current != null && Time.time < until ? current : resting;
            if (f == null) return;
            float k = Mathf.Min(1f, Time.deltaTime / Mathf.Max(0.02f, FaceExpressionData.Current.blend));
            lids = Mathf.Lerp(lids, f.lids, k);
            brows = Mathf.Lerp(brows, f.brows, k);
            skin.FaceLid = lids;
            skin.FaceBrows = brows;
            skin.FaceWink = f.wink;
            skin.HairUp = f.hairUp;
            DrawFace(f);
        }

        /// <summary>The drawn mouth and eyes (and the tint) on the head's paint, through a property block (shared paint untouched).</summary>
        private void DrawFace(FaceExpression f)
        {
            var r = skin.FaceRenderer;
            if (r == null) return;
            var map = FaceArt.Map(skin.HeadFileName);
            bool on = map != null && (f.mouth != MouthShape.None || f.eyes != EyeShape.None || f.sweat);
            int frame = 0;
            if (f.mouth == MouthShape.Chatter) { chatterT += Time.deltaTime * 14f; frame = (int)chatterT % 2; }
            var tex = on ? FaceArt.Get(skin.HeadFileName, f.mouth, f.eyes, f.sweat, frame) : null;
            float tint = f.tintAmount;
            if (on == drawnOn && tex == shownTex && Mathf.Approximately(tint, lastTint)) return;
            drawnOn = on;
            shownTex = tex;
            lastTint = tint;
            if (block == null) block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetFloat(FaceOnId, on ? 1f : 0f);
            if (map != null) block.SetTexture(FaceMapId, map);
            if (tex != null) block.SetTexture(FaceTexId, tex);
            block.SetColor(SkinToneId, skin.SkinColor);
            var t = f.tint; t.a = tint;
            block.SetColor(FaceTintId, t);
            r.SetPropertyBlock(block);
        }

        private float lastTint = -1f;

        private void OnDisable()
        {
            if (skin == null) return;
            skin.FaceLid = 0f; skin.FaceBrows = 0f; skin.FaceWink = false; skin.HairUp = false;
            var r = skin.FaceRenderer;
            if (r != null && block != null) { r.GetPropertyBlock(block); block.SetFloat(FaceOnId, 0f); r.SetPropertyBlock(block); }
            drawnOn = false;
            shownTex = null;
        }
    }
}
