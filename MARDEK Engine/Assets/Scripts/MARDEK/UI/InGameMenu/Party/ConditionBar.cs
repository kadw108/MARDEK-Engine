using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MARDEK.UI
{
    public class ConditionBar : MonoBehaviour
    {
        [SerializeField] List<Color> textColors;
        [SerializeField] List<Color> backgroundColors;

        [SerializeField] Image background;
        [SerializeField] TextMeshProUGUI currentValue;
        [SerializeField] TextMeshProUGUI maxValue;
        [SerializeField] TextMeshProUGUI currentOverMaxValue;

        public void SetValues(int currentValue, int maxValue)
        {
            if (this.backgroundColors.Count != this.textColors.Count) throw new System.ArgumentException("Must have same number of text colors as background colors");
            if (this.backgroundColors.Count < 1) throw new System.ArgumentException("Must have at least 1 background color");

            if (this.currentValue != null && this.maxValue != null)
            {
                this.currentValue.text = currentValue.ToString();
                this.maxValue.text = maxValue.ToString();
            }
            if (this.currentOverMaxValue != null)
            {
                this.currentOverMaxValue.text = currentValue.ToString() + "/" + maxValue.ToString();
            }
            
            float currentFraction = (float) currentValue / (float) maxValue;
            int colorIndex = (int) (currentFraction * (this.backgroundColors.Count - 1) + 0.5f);

            if (float.IsFinite(currentFraction) == false)
                return;

            this.background.transform.localScale = new Vector3(currentFraction, 1f, 1f);
            this.background.color = this.backgroundColors[colorIndex];

            if (this.currentValue != null && this.maxValue != null)
            {
                this.currentValue.color = this.textColors[colorIndex];
                this.maxValue.color = this.textColors[colorIndex];
            }
            if (this.currentOverMaxValue != null)
            {
                this.currentOverMaxValue.color = this.textColors[colorIndex];
            }
        }
    }
}
