using MARDEK.Progress;
using MARDEK.CharacterSystem;
using MARDEK.Stats;
using UnityEngine;
using TMPro;

namespace MARDEK.UI
{
    public class CharacterEquipmentUI : InventoryUI
    {
        [SerializeField] ElemStrip elemStrip;

        [SerializeField] TextMeshProUGUI levelText;
        [SerializeField] ConditionBar hpBar;
        [SerializeField] ConditionBar mpBar;

        [SerializeField] TextMeshProUGUI attackValue;
        [SerializeField] TextMeshProUGUI defValue;
        [SerializeField] TextMeshProUGUI mdefValue;

        [SerializeField] TextMeshProUGUI strValue;
        [SerializeField] TextMeshProUGUI vitValue;
        [SerializeField] TextMeshProUGUI sprValue;
        [SerializeField] TextMeshProUGUI aglValue;

        [SerializeField] IntegerStat levelStat;
        [SerializeField] IntegerStat currentHpStat;
        [SerializeField] IntegerStat maxHpStat;
        [SerializeField] IntegerStat currentMpStat;
        [SerializeField] IntegerStat maxMpStat;

        [SerializeField] IntegerStat attackStat;
        [SerializeField] IntegerStat defStat;
        [SerializeField] IntegerStat mdefStat;

        [SerializeField] IntegerStat strStat;
        [SerializeField] IntegerStat vitStat;
        [SerializeField] IntegerStat sprStat;
        [SerializeField] IntegerStat aglStat;

        public Character Character
        {
            get
            {
                var index = transform.GetSiblingIndex();
                if (Party.Instance.Characters.Count <= index)
                    return null;
                return Party.Instance.Characters[index];
            }
        }
        private void OnEnable()
        {
            var index = transform.GetSiblingIndex();
            if (Character != null)
            {
                AssignInventoryToUI(Character.EquippedItems);

                elemStrip.SetElement(Character.Profile.element);

                hpBar.SetValues(Character.GetStat(currentHpStat), Character.GetStat(maxHpStat));
                mpBar.SetValues(Character.GetStat(currentMpStat), Character.GetStat(maxMpStat));

                levelText.text = "Lv " + Character.GetStat(levelStat);

                attackValue.text = Character.GetStat(attackStat).ToString();
                defValue.text = Character.GetStat(defStat).ToString();
                mdefValue.text = Character.GetStat(mdefStat).ToString();

                strValue.text = Character.GetStat(strStat).ToString();
                vitValue.text = Character.GetStat(vitStat).ToString();
                sprValue.text = Character.GetStat(sprStat).ToString();
                aglValue.text = Character.GetStat(aglStat).ToString();
            }
        }
    }
}