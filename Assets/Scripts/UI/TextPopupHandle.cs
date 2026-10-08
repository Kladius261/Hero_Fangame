using UnityEngine;

namespace HeroFangame.UI
{
    /// <summary>
    /// Lightweight handle returned by TextPopupManager.BeginSustained,
    /// owned by the calling ability script for as long as it's held/active.
    /// Drives the procedural "comic lettering" growth of a sustained popup
    /// (e.g. Heat Vision's F! -> FZ! -> FZZ! -> ... -> FZZZZZZZZZZT!) and its
    /// eventual pop-out.
    ///
    /// The ending/suffix character is revealed at whichever happens first:
    /// the repeat count hitting its cap (10), or Release() being called —
    /// once revealed it stays until the popup is destroyed, so the word
    /// always reads as "complete" right before it disappears.
    /// </summary>
    public class TextPopupHandle
    {
        private const int MaxRepeatCount = 10;

        private readonly TextPopup popup;
        private readonly TextPopupManager.ProceduralWord word;
        private readonly bool fixedPosition;
        private readonly Vector3 positionOffset;

        private int repeatCount;
        private bool suffixRevealed;
        private float growthTimer;

        public TextPopupHandle(TextPopup popup, TextPopupManager.ProceduralWord word, bool fixedPosition, Vector3 positionOffset = default)
        {
            this.popup = popup;
            this.word = word;
            this.fixedPosition = fixedPosition;
            this.positionOffset = positionOffset;
        }

        /// <summary>
        /// No-op for fixed-position popups (e.g. Flight Charge, which stays
        /// put at the charge's starting point).
        /// </summary>
        public void UpdatePosition(Vector3 worldPosition)
        {
            if (fixedPosition || popup == null)
            {
                return;
            }
            popup.transform.position = worldPosition + positionOffset;
        }

        public void Tick(float deltaTime, float growthInterval)
        {
            if (popup == null || suffixRevealed || growthInterval <= 0f)
            {
                return;
            }

            growthTimer += deltaTime;
            while (growthTimer >= growthInterval && repeatCount < MaxRepeatCount)
            {
                growthTimer -= growthInterval;
                repeatCount++;
                if (repeatCount >= MaxRepeatCount)
                {
                    RevealSuffix();
                    return;
                }
                popup.SetText(BuildText(includeSuffix: false));
            }
        }

        public void Release(float popOutDuration = 0.15f)
        {
            if (popup == null)
            {
                return;
            }
            if (!suffixRevealed)
            {
                RevealSuffix();
            }
            popup.PopOutAndDestroy(popOutDuration);
        }

        private void RevealSuffix()
        {
            suffixRevealed = true;
            popup.SetText(BuildText(includeSuffix: true));
        }

        private string BuildText(bool includeSuffix)
        {
            string repeated = repeatCount > 0 ? new string(word.repeatChar, repeatCount) : string.Empty;
            return includeSuffix
                ? word.prefix + repeated + word.suffix + "!"
                : word.prefix + repeated + "!";
        }
    }
}
