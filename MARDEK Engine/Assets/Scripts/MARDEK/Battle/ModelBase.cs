using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace MARDEK.Battle
{
    // To make this work:
    // Attach to empty gameObject in a scene,
    // attach the appropriate json file (currently test.json in Assets/Sprites/BattleModels/Imports)
    // and run the scene.
    public class ModelBase : MonoBehaviour
    {
        [SerializeField] SpriteRenderer weaponSprite;
        [SerializeField] SpriteRenderer shieldSprite;
    }
}
