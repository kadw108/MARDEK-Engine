using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MARDEK.UI
{
    public class ItemNameHeader : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI textField;
        [SerializeField] ElemStrip elemStrip;
        [SerializeField] Image elementBackground;

        void Update()
        {
            var slot = SlotUI.selectedSlot;
            if (slot != null && slot.item != null)
            {
                textField.text = $"{slot.item.displayName} x{slot.amount}";

                elemStrip.SetElement(slot.item.element);
                elemStrip.gameObject.SetActive(true);

                elementBackground.sprite = slot.item.element.thinSprite;
                elementBackground.gameObject.SetActive(true);
            }
            else
            {
                textField.text = string.Empty;
                elemStrip.gameObject.SetActive(false);
                elementBackground.gameObject.SetActive(false);
            }
        }
    }
}