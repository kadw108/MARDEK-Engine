using UnityEngine;
using MARDEK.Core;
using TMPro;

namespace MARDEK.CharacterSystem
{
    [CreateAssetMenu(menuName ="MARDEK/Character/CharacterVoice")]
    public class CharacterVoice : AddressableScriptableObject
    {
        [field: SerializeField] public Color Color { get; private set; }
        [field: SerializeField] public TMP_FontAsset Font { get; private set; }
        [field: SerializeField] public Material Material { get; private set; }
        [field: SerializeField] public int FontSize { get; private set; }
    }
}
