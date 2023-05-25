using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

namespace MARDEK.UI
{
    public class SlotCursorUI : MonoBehaviour
    {
        [SerializeField] Image itemImage;
        [SerializeField] TextMeshProUGUI amountText;
        [SerializeField] Sprite transparentSprite;

        void Update()
        {
            Vector2 screenPosition = new Vector2(Mouse.current.position.x.ReadValue(), Mouse.current.position.y.ReadValue());
            Vector2 worldPosition = Camera.main.ScreenToWorldPoint(screenPosition);
            this.transform.position = worldPosition;

            if (SlotCursor.instance.IsEmpty())
            {
                this.itemImage.sprite = transparentSprite;
                this.amountText.text = "";
            }
            else
            {
                this.itemImage.sprite = SlotCursor.instance.GetItem().sprite;
                this.amountText.text = SlotCursor.instance.GetAmount().ToString();
            }
        }
    }
}