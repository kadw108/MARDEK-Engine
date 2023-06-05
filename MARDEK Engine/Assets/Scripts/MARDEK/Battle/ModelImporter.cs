using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace MARDEK.Battle
{
    // To make this work:
    // Attach to empty gameObject in a scene and run the scene.
    public class ModelImporter : MonoBehaviour
    {
        // The JSON should be formatted as follows:
        // The JSON file must start with [  and end with ], with an array of BattleFrameModel info between them.
        public TextAsset jsonFile;

        private static readonly string animationFolder = "Assets/Animations/BattleModelAnimations/";
        private static readonly string modelPrefabFolder = "Assets/Prefabs/BattleModels/";

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
                Directory.CreateDirectory(animationFolder + model.name);
                Directory.CreateDirectory(modelPrefabFolder + model.name);

                foreach (ComponentJSON component in model.components)
                {
                    model.depthDict[component.depth] = component;
                }

                // Filter out these 2 components because they're special and I haven't figured out
                // how to handle them yet
                model.components = model.components.FindAll(c => (
                    c.spriteNumber != "2234" && c.spriteNumber != "2312")
                );

                // Make parent battle model object
                GameObject modelObject = MakeModelPrefab(model);
                string modelPrefabPath = modelPrefabFolder + model.name + "/" + model.name + ".prefab";

                // Create the new model prefab from the model object
                bool prefabSuccess;
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(
                    modelObject, modelPrefabPath, out prefabSuccess);
                if (prefabSuccess != true)
                {
                    Debug.Log("WARNING: Model " + model.spriteNumber + "'s prefab failed to save!");
                    return;
                }

                MakeModelPrefabVariants(model, savedPrefab);
                DestroyImmediate(modelObject);

                // Now create the animations
                // GameObject modelObject = MakeModelPrefab(model);
                MakeModelAnimations(model);
            }
        }

        private GameObject MakeModelPrefab(BattleModelFrameJSON model)
        {
            GameObject modelPrefab = new()
            {
                name = model.name
            };

            for (int i = 0; i < model.components.Count; i++)
            {
                ComponentJSON cJSON = model.components[i];

                GameObject childComponent = new()
                {
                    // Components must have a unique name because animations don't work
                    // if 2 components have the same name
                    name = GameObjectUtility.GetUniqueNameForSibling(
                    modelPrefab.transform, cJSON.spriteNumber)
                };
                cJSON.gameObjectName = childComponent.name;
                childComponent.transform.parent = modelPrefab.transform;

                /*
                Matrix4x4 newMatrix = Utility.UnityMatrixFromFlash(cJSON.transformMatrix);
                childComponent.transform.localPosition = Utility.ExtractPosition(newMatrix);
                childComponent.transform.localRotation = Utility.ExtractRotation(newMatrix);
                childComponent.transform.localScale = Utility.ExtractScale(newMatrix);
                */
                childComponent.transform.localPosition = Utility.Position(cJSON.transformMatrix);
                childComponent.transform.localRotation = Utility.Rotation(cJSON.transformMatrix);
                childComponent.transform.localScale = Utility.Scale(cJSON.transformMatrix);

                SpriteRenderer renderer = childComponent.AddComponent<SpriteRenderer>();
                renderer.sortingLayerName = "UI Sprites";
                renderer.sortingOrder = i;
            }

            // Attach animation controller to the model prefab
            Animator animator = modelPrefab.AddComponent<Animator>();
            // Create runtime animation controller for the model prefab's animator
            RuntimeAnimatorController controller =
                UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(
                animationFolder + model.name + "/" + model.name + ".controller"
            );
            animator.runtimeAnimatorController = controller;


            return modelPrefab;
        }

        private void MakeModelPrefabVariants(BattleModelFrameJSON model, GameObject savedPrefab)
        {
            // Create model prefab variants that actually have the sprite info
            // Can't do this in the first loop because the basic model doesn't exist yet
            foreach(ShapeJSON sJSON in model.components[0].shapes)
            {
                GameObject variant = (GameObject)PrefabUtility.InstantiatePrefab(savedPrefab);
                variant.name = sJSON.label;

                // Assign the correct sprites to this variant
                foreach (Transform component in variant.transform)
                {
                    ComponentJSON compInfo = model.components.Find(c => (c.gameObjectName == component.name));
                    ShapeJSON shapeInfo = compInfo.shapes.Find(s => (s.label == variant.name));

                    SpriteRenderer renderer = component.GetComponent<SpriteRenderer>();
                    if (renderer == null)
                    {
                        Debug.Log("Component " + component.name + " has null sprite renderer!");
                    }
                    else if (shapeInfo == null) {
                        Debug.Log("Component " + component.name + " has null shapeInfo!");
                    }
                    else
                    {
                        renderer.sprite = SearchModelSprite(shapeInfo.shapeNumber);
                    }
                }

                string variantPath = modelPrefabFolder + model.name + "/" +
                    sJSON.label + "_" + model.name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(variant, variantPath);
            }
        }

        // NOTE: This will NOT WORK PROPERLY if you don't run MakeModelPrefab beforehand, because
        // that's what sets the unique names for each ComponentJSON in model.components
        private void MakeModelAnimations(BattleModelFrameJSON model)
        {
            foreach (AnimationJSON anim in model.animations) {
                AnimationClip clip = new();

                foreach (AnimationFrameJSON frame in anim.listOfFrames)
                {
                    foreach(AnimationFrameComponentJSON compFrameInfo in frame.listOfComponents)
                    {
                        ComponentJSON componentRef = model.depthDict[compFrameInfo.componentDepth];

                        /*
                        Matrix4x4 frameTransform = Utility.UnityMatrixFromFlash(compFrameInfo.transform);
                        Vector3 framePosition = Utility.ExtractPosition(frameTransform);
                        Vector3 frameRotation = Utility.ExtractRotation(frameTransform).eulerAngles;
                        Vector3 frameScale = Utility.ExtractScale(frameTransform);
                        */

                        Vector2 framePosition = Utility.Position(compFrameInfo.transform);
                        Vector3 frameRotation = Utility.Rotation(compFrameInfo.transform).eulerAngles;
                        Vector3 frameScale = Utility.Scale(compFrameInfo.transform);

                        // MARDEK's animations are 30FPS by default
                        float frameTime = frame.relativeFrameNumber * (float) (1.0 / 30.0);

                        componentRef.curves.translateX.AddKey(
                            new Keyframe(
                                frameTime,
                                framePosition.x
                            )
                        );

                        componentRef.curves.translateY.AddKey(
                            new Keyframe(
                                frameTime,
                                framePosition.y
                            )
                        );

                        componentRef.curves.rotX.AddKey(
                            new Keyframe(
                                frameTime,
                                frameRotation.x
                            )
                        );

                        componentRef.curves.rotY.AddKey(
                            new Keyframe(
                                frameTime,
                                frameRotation.y
                            )
                        );

                        componentRef.curves.rotZ.AddKey(
                            new Keyframe(
                                frameTime,
                                frameRotation.z
                            )
                        );

                        componentRef.curves.scaleX.AddKey(
                            new Keyframe(
                                frameTime,
                                frameScale.x
                            )
                        );

                        componentRef.curves.scaleY.AddKey(
                            new Keyframe(
                                frameTime,
                                frameScale.y
                            )
                        );
                    }
                }

                foreach (ComponentJSON c in model.components)
                {
                    string componentName = c.gameObjectName;

                    // Property names from https://forum.unity.com/threads/list-of-all-property-names.192974/
                    clip.SetCurve(componentName, typeof(Transform), "localPosition.x", c.curves.translateX);
                    clip.SetCurve(componentName, typeof(Transform), "localPosition.y", c.curves.translateY);
                    clip.SetCurve(componentName, typeof(Transform), "localEulerAnglesRaw.x", c.curves.rotX);
                    clip.SetCurve(componentName, typeof(Transform), "localEulerAnglesRaw.y", c.curves.rotY);
                    clip.SetCurve(componentName, typeof(Transform), "localEulerAnglesRaw.z", c.curves.rotZ);
                    clip.SetCurve(componentName, typeof(Transform), "localScale.x", c.curves.scaleX);
                    clip.SetCurve(componentName, typeof(Transform), "localScale.y", c.curves.scaleY);
                }

                // According to the documentation, this should be called after curves are set
                clip.EnsureQuaternionContinuity();

                // Seems there is no way to set a clip to loop automatically with code
                // TODO add workaround: make all the clips transition back to each other in the animator
                // https://stackoverflow.com/questions/57913717/how-to-set-looping-for-animation-clip-in-script

                string anim_name = anim.name + "_" + model.name + ".anim";
                AssetDatabase.CreateAsset(clip,
                    animationFolder + model.name + "/" + anim_name);
            }
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
        }

        // Type that represents how the data is formatted in portraits.json
        // Used to create the actual CharacterPortrait ScriptableObject.
        private class BattleModelFrameJSON
        {
            [SerializeField] public string name;
            [SerializeField] public string spriteNumber;
            [SerializeField] public string frameNumber;
            [SerializeField] public List<ComponentJSON> components;
            [SerializeField] public List<AnimationJSON> animations;

            // Not part of the JSON - used to store a depth-component map for animation stuff
            // Has to be reconstructed in C# on read becauses JSON doesn't support object references sadly
            public Dictionary<string, ComponentJSON> depthDict = new();
        }

        private class ComponentJSON
        {
            [SerializeField] public TransformJSON transformMatrix;
            [SerializeField] public string spriteNumber;
            [SerializeField] public string depth;
            [SerializeField] public List<ShapeJSON> shapes;

            // Not part of the JSON
            // Used to conveniently store animation info once it's read separately
            public ComponentCurves curves = new();
            // Used to store unique name for each component, since animations are based on
            // gameObject names meaning all components must have different names
            public string gameObjectName = "";
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

        private class AnimationJSON
        {
            [SerializeField] public string name;
            [SerializeField] public int startFrame;
            [SerializeField] public List<AnimationFrameJSON> listOfFrames;
        }

        private class AnimationFrameJSON
        {
            [SerializeField] public int frameNumber;
            [SerializeField] public int relativeFrameNumber;
            [SerializeField] public List<AnimationFrameComponentJSON> listOfComponents;
        }

        private class AnimationFrameComponentJSON
        {
            [SerializeField] public string componentDepth;
            [SerializeField] public TransformJSON transform;
        }

        private class ComponentCurves
        {
            public AnimationCurve translateX;
            public AnimationCurve translateY;
            public AnimationCurve rotX;
            public AnimationCurve rotY;
            public AnimationCurve rotZ;
            public AnimationCurve scaleX;
            public AnimationCurve scaleY;

            public ComponentCurves()
            {
                translateX = new();
                translateY = new();
                rotX = new();
                rotY = new();
                rotZ = new();
                scaleX = new();
                scaleY = new();
            }
        }

        // Utility functions
        private static class Utility
        {
            /*
            // Old matrix utility functions
            // From https://forum.unity.com/threads/how-to-assign-matrix4x4-to-transform.121966/
            // No longer using these because they don't work perfectly for some reason
            // (In particular, the ExtractRotation formula doesn't take one of the rotateSkew values into account
            // and therefore gives incorrect rotation values)
            public static Quaternion ExtractRotation(Matrix4x4 matrix)
            {
                Vector3 forward;
                forward.x = matrix.m02;
                forward.y = matrix.m12;
                forward.z = matrix.scaleZ;

                Vector3 upwards;
                upwards.x = matrix.m01;
                upwards.y = matrix.scaleY;
                upwards.z = matrix.m21;

                return Quaternion.LookRotation(forward, upwards);
            }

            public static Vector3 ExtractPosition(Matrix4x4 matrix)
            {
                Vector2 position;
                position.x = matrix.m03;
                position.y = matrix.m13;
                return position;
            }

            public static Vector3 ExtractScale(Matrix4x4 matrix)
            {
                Vector3 scale;
                scale.x = new Vector4(matrix.scaleX, matrix.m10, matrix.m20, matrix.m30).magnitude;
                scale.y = new Vector4(matrix.m01, matrix.scaleY, matrix.m21, matrix.m31).magnitude;
                scale.z = 1;
                return scale;
            }

            public static Matrix4x4 UnityMatrixFromFlash(TransformJSON transformMatrix)
            {
                // Try to convert JPEXS 2x3 affine transform matrix to Unity's Matrix4x4
                // Using formula from https://forum.unity.com/threads/convert-matrix-2x3-to-4x4.1153091/
                Matrix4x4 newMatrix = new Matrix4x4();
                newMatrix.SetRow(0, new Vector4(
                    transformMatrix.scaleX, transformMatrix.rotateSkew1,
                    0, transformMatrix.translateX)
                );
                newMatrix.SetRow(1, new Vector4(
                    transformMatrix.rotateSkew0, transformMatrix.scaleY,
                    0, transformMatrix.translateY)
                );
                newMatrix.SetRow(2, new Vector4(0, 0, 1, 0));
                newMatrix.SetRow(3, new Vector4(0, 0, 0, 1));

                return newMatrix;
            }
            */

            // New functions - the old ones had some issues and the scale/rotation was off
            // Also converting to 4x4 matrix was unnecessary

            /*
             * The SWF 2x3 transform matrix is:
             * scaleX rotateSkew1 translateX
             * rotateSkew0 scaleY translateY
             */

            // Get position/rotation/scale from the 2x3 affine matrix
            // Based on https://math.stackexchange.com/questions/13150/extracting-rotation-scale-values-from-2d-transformation-matrix?noredirect=1&lq=1 
            // NOTE: THIS IS IMPERFECT BECAUSE IT DOES NOT WORK FOR SKEW
            // The Unity Transform component does not support skew
            public static Quaternion Rotation(TransformJSON t)
            {
                // The Math StackExchange link above provides rotation as a single angle
                // So we use a different formula instead, taken from this link
                // https://euclideanspace.com/maths/geometry/rotations/conversions/matrixToQuaternion/index.htm

                // (That might not be necessary since I think the 2x3 -> 4x4 matrix conversion means
                // that only the z-euler angle rotation gets used anyway, so it is just a single angle after all,
                // but this formula is supposed to be more robust and I want to keep it just in case
                // there are rotations that use the x and y euler angles after all?

                /*
                 * From https://forum.unity.com/threads/convert-matrix-2x3-to-4x4.1153091/
                 * The Unity 4x4 transform matrix is:
                 * scaleX      rotateSkew1 0 translateX
                 * rotateSkew0 scaleY      0 translateY
                 * 0           0           1 0
                 * 0           0           0 1
                 */

                float scaleX = t.scaleX; // m00
                float scaleY = t.scaleY; // m11
                float scaleZ = 1; // m22

                float m21 = 0;
                float m12 = 0;
                float m02 = 0;
                float m20 = 0;
                float m10 = t.rotateSkew0;
                float m01 = t.rotateSkew1;

                float tr = scaleX + scaleY + scaleZ;
                double qw, qx, qy, qz;

                if (tr > 0)
                {
                    float S = Mathf.Sqrt((float)(tr + 1.0)) * 2; // S=4*qw 
                    qw = 0.25 * S;
                    qx = (m21 - m12) / S; // always 0
                    qy = (m02 - m20) / S; // always 0
                    qz = (m10 - m01) / S;
                }
                else if ((scaleX > scaleY) & (scaleX > scaleZ))
                {
                    float S = Mathf.Sqrt((float)(1.0 + scaleX - scaleY - scaleZ)) * 2; // S=4*qx 
                    qw = (m21 - m12) / S; // always 0
                    qx = 0.25 * S;
                    qy = (m01 + m10) / S;
                    qz = (m02 + m20) / S; // always 0
                }
                else if (scaleY > scaleZ)
                {
                    float S = Mathf.Sqrt((float)(1.0 + scaleY - scaleX - scaleZ)) * 2; // S=4*qy
                    qw = (m02 - m20) / S; // always 0
                    qx = (m01 + m10) / S;
                    qy = 0.25 * S;
                    qz = (m12 + m21) / S; // always 0
                }
                else
                {
                    float S = Mathf.Sqrt((float)(1.0 + scaleZ - scaleX - scaleY)) * 2; // S=4*qz
                    qw = (m10 - m01) / S;
                    qx = (m02 + m20) / S; // always 0
                    qy = (m12 + m21) / S; // always 0
                    qz = 0.25 * S; 
                }

                Quaternion rot = new Quaternion(
                    (float) qx, (float) qy, (float) qz, (float) qw);
                return rot;
            }
            public static Vector3 Scale(TransformJSON t)
            {
                Vector3 scale;
                scale.x = new Vector2(t.scaleX, t.rotateSkew1).magnitude;
                scale.x = t.scaleX > 0 ? scale.x : -scale.x; // Give scale.x sign of scaleX
                scale.y = new Vector2(t.scaleY, t.rotateSkew0).magnitude;
                scale.y = t.scaleY > 0 ? scale.y : -scale.y; // Give scale.y sign of scaleY

                scale.z = 1;
                return scale;
            }

            public static Vector2 Position(TransformJSON t)
            {
                return new Vector2(t.translateX, t.translateY);
            }
        }
    }
}
