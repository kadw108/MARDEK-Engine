using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

using System.IO;
using System;
using Newtonsoft.Json;
using System.Linq;

namespace MARDEK.Battle
{
    // To make this work:
    // 1. Comment the denoted line (currently line 10) in AddressableScriptableObject.cs
    // 2. Uncomment the setter for listed attributes and setters in Item.cs
    // 3. Attach to empty gameObject in a scene and run the scene.
    public class ModelImporter : MonoBehaviour
    {
        // The JSON should be formatted as follows:
        // The JSON file must start with [  and end with ], with an array of BattleFrameModel info between them.
        public TextAsset jsonFile;

        [MenuItem("Examples/Create Prefab")]
        static void CreatePrefab()
        {
            // Keep track of the currently selected GameObject(s)
            GameObject[] objectArray = Selection.gameObjects;

            // Loop through every GameObject in the array above
            foreach (GameObject gameObject in objectArray)
            {
                // Create folder Prefabs and set the path as within the Prefabs folder,
                // and name it as the GameObject's name with the .Prefab format
                if (!Directory.Exists("Assets/Prefabs"))
                    AssetDatabase.CreateFolder("Assets", "Prefabs");
                string localPath = "Assets/Prefabs/" + gameObject.name + ".prefab";

                // Make sure the file name is unique, in case an existing Prefab has the same name.
                localPath = AssetDatabase.GenerateUniqueAssetPath(localPath);

                // Create the new Prefab and log whether Prefab was saved successfully.
                bool prefabSuccess;
                PrefabUtility.SaveAsPrefabAssetAndConnect(gameObject, localPath, InteractionMode.UserAction, out prefabSuccess);
                if (prefabSuccess == true)
                    Debug.Log("Prefab was saved successfully");
                else
                    Debug.Log("Prefab failed to save" + prefabSuccess);
            }
        }


        void Start()
        {
            DeserializeItems();
        }

        void DeserializeItems()
        {
            Debug.Log("START DESERIALIZING");

            List<BattleModelFrameJSON> list =
                JsonConvert.DeserializeObject<List<BattleModelFrameJSON>>(jsonFile.text);

            foreach (BattleModelFrameJSON model in list)
            {
                // Filter out these 2 components because they're special and I haven't figured out
                // how to handle them yet.
                model.components = model.components.FindAll(c => (
                    c.spriteNumber != "2234" && c.spriteNumber != "2312")
                );

                // Make parent battle model prefab
                GameObject modelPrefab = new GameObject();
                modelPrefab.name = model.spriteNumber;
                for (int i = 0; i < model.components.Count; i++)
                {
                    ComponentJSON cJSON = model.components[i];

                    // try to convert JPEXS 2x3 affine transform matrix to Unity's Matrix4x4
                    // using formula from https://forum.unity.com/threads/convert-matrix-2x3-to-4x4.1153091/
                    Matrix4x4 newMatrix = new Matrix4x4();
                    newMatrix.SetRow(0, new Vector4(
                        cJSON.transformMatrix.scaleX, cJSON.transformMatrix.rotateSkew1,
                        0, cJSON.transformMatrix.translateX)
                    );
                    newMatrix.SetRow(1, new Vector4(
                        cJSON.transformMatrix.rotateSkew0, cJSON.transformMatrix.scaleY,
                        0, cJSON.transformMatrix.translateY)
                    );
                    newMatrix.SetRow(2, new Vector4(0, 0, 1, 0));
                    newMatrix.SetRow(3, new Vector4(0, 0, 0, 1));
                    Debug.Log(cJSON.spriteNumber + " matrix: " + newMatrix.ToString());

                    GameObject childComponent = new GameObject();
                    childComponent.name = cJSON.spriteNumber;
                    // childComponent.transform.SetParent(modelPrefab.transform, true);
                    childComponent.transform.parent = modelPrefab.transform;
                    childComponent.transform.localPosition = Utility.ExtractPosition(newMatrix);
                    childComponent.transform.localRotation = Utility.ExtractRotation(newMatrix);
                    childComponent.transform.localScale = Utility.ExtractScale(newMatrix);

                    SpriteRenderer renderer = childComponent.AddComponent<SpriteRenderer>();
                    renderer.sortingLayerName = "UI Sprites";
                    renderer.sortingOrder = i;
                }

                string modelPrefabName = AssetDatabase.GenerateUniqueAssetPath(
                    "Assets/Prefabs/BattleModels/" + model.spriteNumber + ".prefab"
                    );

                // Create the new Prefab and log whether Prefab was saved successfully.
                bool prefabSuccess;
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(
                    modelPrefab, modelPrefabName, out prefabSuccess);
                if (prefabSuccess == true)
                    Debug.Log("Prefab was saved successfully");
                else
                    Debug.Log("Prefab failed to save" + prefabSuccess);

                // now create the variants that actually have the sprite info
                // can't do this in the first loop because the basic model doesn't exist yet
                foreach(ShapeJSON sJSON in model.components[0].shapes)
                {
                    GameObject variant = (GameObject)PrefabUtility.InstantiatePrefab(savedPrefab);
                    variant.name = sJSON.label;

                    // Assign the correct sprites to this variant
                    foreach (Transform component in variant.transform)
                    {
                        ComponentJSON compInfo = model.components.Find(c => (c.spriteNumber == component.name));
                        ShapeJSON shapeInfo = compInfo.shapes.Find(s => (s.label == variant.name));

                        SpriteRenderer renderer = component.GetComponent<SpriteRenderer>();
                        if (renderer == null)
                        {
                            Debug.Log("Component has null sprite renderer!");
                        }
                        else
                        {
                            renderer.sprite = SearchModelSprite(shapeInfo.shapeNumber);
                        }
                    }

                    string variantName = AssetDatabase.GenerateUniqueAssetPath(
                        "Assets/Prefabs/BattleModels/" + model.spriteNumber + "_" + sJSON.label + ".prefab"
                    );
                    PrefabUtility.SaveAsPrefabAsset(variant, variantName);
                }

                DestroyImmediate(modelPrefab);
            }
        }

        private string pickFirstNotNull(params string[] strings)
        {
            foreach (string s in strings)
            {
                if (s != null)
                {
                    return s;
                }
            }
            return null;
        }

        private Sprite SearchModelSprite(string spriteName)
        {
            Sprite returnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Sprites/BattleModels/Imports/battleModelShapes/" + spriteName + ".svg"
            );
            if (returnSprite == null)
            {
                Debug.Log("Could not find " + spriteName + " in battleModelShapes!");
            }
            return returnSprite;

            /*
            string[] guids = AssetDatabase.FindAssets(
                "t:Sprite " + spriteName,
                new string[] { "Assets/Sprites/BattleModels/Imports/battleModelShapes" + spriteName }
            );
            if (guids.Length > 1)
            {
                // take matching asset with shortest name
                // since AssetDatabase.FindAssets gets all assets that contain the search string

                string[] match_names = guids.Select(
                    id => AssetDatabase.GUIDToAssetPath(id)).ToArray();
                int minLength = match_names.Min(n => n.Length);
                string final_name = match_names.FirstOrDefault(x => x.Length == minLength);

                return AssetDatabase.LoadAssetAtPath<Sprite>(final_name);
            }
            else if (guids.Length == 0)
            {
                Debug.Log("Could not find " + spriteName + " in battleModelShapes!");
                return null;
            }
            else
            {
                return AssetDatabase.LoadAssetAtPath<Sprite>(
                    AssetDatabase.GUIDToAssetPath(guids[0])
                );
            }
            */
        }

        // Type that represents how the data is formatted in portraits.json
        // Used to create the actual CharacterPortrait ScriptableObject.
        private class BattleModelFrameJSON
        {
            [SerializeField] public List<string> labels;
            [SerializeField] public string spriteNumber;
            [SerializeField] public string frameNumber;
            [SerializeField] public List<ComponentJSON> components;
        }

        private class ComponentJSON
        {
            [SerializeField] public TransformJSON transformMatrix;
            [SerializeField] public string spriteNumber;
            [SerializeField] public List<ShapeJSON> shapes;
        }

        private class TransformJSON
        {
            [SerializeField] public float scaleX;
            [SerializeField] public float rotateSkew0;
            [SerializeField] public float translateX;
            [SerializeField] public float scaleY;
            [SerializeField] public float rotateSkew1;
            [SerializeField] public float translateY;
        }

        private class ShapeJSON
        {
            [SerializeField] public string shapeNumber;
            [SerializeField] public string label;
        }

        // Matrix utility functions
        // From https://forum.unity.com/threads/how-to-assign-matrix4x4-to-transform.121966/
        private static class Utility
        {
            public static Quaternion ExtractRotation(Matrix4x4 matrix)
            {
                Vector3 forward;
                forward.x = matrix.m02;
                forward.y = matrix.m12;
                forward.z = matrix.m22;

                Vector3 upwards;
                upwards.x = matrix.m01;
                upwards.y = matrix.m11;
                upwards.z = matrix.m21;

                return Quaternion.LookRotation(forward, upwards);
            }

            public static Vector3 ExtractPosition(Matrix4x4 matrix)
            {
                Vector3 position;
                position.x = matrix.m03;
                position.y = matrix.m13;
                position.z = matrix.m23;
                return position;
            }

            public static Vector3 ExtractScale(Matrix4x4 matrix)
            {
                Vector3 scale;
                scale.x = new Vector4(matrix.m00, matrix.m10, matrix.m20, matrix.m30).magnitude;
                scale.y = new Vector4(matrix.m01, matrix.m11, matrix.m21, matrix.m31).magnitude;
                scale.z = new Vector4(matrix.m02, matrix.m12, matrix.m22, matrix.m32).magnitude;
                return scale;
            }
        }
    }
}
