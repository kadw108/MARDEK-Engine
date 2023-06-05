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

                foreach(ComponentJSON compo in model.components)
                {
                    anim.listOfCurves.Add(new ComponentCurves(
                        compo.gameObjectName, compo.depth));
                }

                foreach (AnimationFrameJSON frame in anim.listOfFrames)
                {
                    foreach(AnimationFrameComponentJSON compFrameInfo in frame.listOfComponents)
                    {
                        ComponentCurves componentCurves = anim.listOfCurves.Find(
                            curves => (curves.depth == compFrameInfo.componentDepth)
                        );

                        // Sometimes it's null, I'm guessing for special components that weren't included in model.components
                        // Alternatively for components that get added mid-animation
                        // TODO add support for this stuff
                        if (componentCurves != null)
                        {
                            Vector2 framePosition = Utility.Position(compFrameInfo.transform);
                            Vector3 frameRotation = Utility.Rotation(compFrameInfo.transform).eulerAngles;
                            Vector3 frameScale = Utility.Scale(compFrameInfo.transform);

                            // MARDEK's animations are 30FPS by default, so (1.0 / 30.0)
                            float frameTime = frame.relativeFrameNumber * (float) (1.0 / 30.0);

                            // Make InTangent and OutTangent both 0 to disable interpolation,
                            // after https://stackoverflow.com/questions/57566668/unity-how-to-disable-animation-interpolation-animation-curves 
                            // Interpolation makes angles spin when they go from 0-360 (not ideal)
                            componentCurves.translateX.AddKey(
                                new Keyframe(frameTime, framePosition.x)
                            );
                            componentCurves.translateY.AddKey(
                                new Keyframe(frameTime, framePosition.y)
                            );

                            // For the Unity 360-0 or 0-360 rotation interpolation error
                            if (componentCurves.prevRotZ != -1)
                            {
                                if (componentCurves.prevRotZ > 340 && frameRotation.z < 10)
                                {
                                    componentCurves.circleNum++;
                                }
                                else if (componentCurves.prevRotZ < 10 && frameRotation.z > 340)
                                {
                                    componentCurves.circleNum--;
                                }
                            }
                            componentCurves.prevRotZ = frameRotation.z;
                            float actualRotZ = frameRotation.z + componentCurves.circleNum * 360;
                            componentCurves.rotZ.AddKey(
                                new Keyframe(frameTime, actualRotZ)
                            );


                            // Sometimes scale goes to 0 for one frame when it shouldn't, no clue why,
                            // probably an artifact of the process used to get scale from the 2x3 transform matrix,
                            // just keep track of when it happens and undo it
                            if (Mathf.Abs(componentCurves.prevScaleX) > 0.9 &&
                                Mathf.Abs(frameScale.x) < 0.1)
                            {
                                frameScale.x = componentCurves.prevScaleX;
                            }
                            componentCurves.scaleX.AddKey(
                                new Keyframe(frameTime, frameScale.x)
                            );
                            componentCurves.prevScaleX = frameScale.x;

                            if (Mathf.Abs(componentCurves.prevScaleY) > 0.9 &&
                                Mathf.Abs(frameScale.y) < 0.1)
                            {
                                frameScale.y = componentCurves.prevScaleY;
                            }
                            componentCurves.scaleY.AddKey(
                                new Keyframe(frameTime, frameScale.y)
                            );
                            componentCurves.prevScaleY = frameScale.y;
                        }
                    }
                }

                foreach (ComponentCurves c in anim.listOfCurves)
                {
                    string componentName = c.gameObjectName;

                    // Property names from https://forum.unity.com/threads/list-of-all-property-names.192974/
                    clip.SetCurve(componentName, typeof(Transform), "localPosition.x", c.translateX);
                    clip.SetCurve(componentName, typeof(Transform), "localPosition.y", c.translateY);
                    // clip.SetCurve(componentName, typeof(Transform), "localEulerAnglesRaw.x", c.rotX);
                    // clip.SetCurve(componentName, typeof(Transform), "localEulerAnglesRaw.y", c.rotY);
                    clip.SetCurve(componentName, typeof(Transform), "localEulerAnglesRaw.z", c.rotZ);
                    clip.SetCurve(componentName, typeof(Transform), "localScale.x", c.scaleX);
                    clip.SetCurve(componentName, typeof(Transform), "localScale.y", c.scaleY);
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

            // Not part of the JSON
            // Used to conveniently store animation info once it's read separately
            public List<ComponentCurves> listOfCurves = new();
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
            // gameObjectName and depth of component these curves are attached to
            // Used to identify it when creating the animation
            public string gameObjectName;
            public string depth;

            public AnimationCurve translateX;
            public AnimationCurve translateY;
            // public AnimationCurve rotX;
            // public AnimationCurve rotY;
            public AnimationCurve rotZ;
            public AnimationCurve scaleX;
            public AnimationCurve scaleY;

            // For fixing the "interpolation between 360 and 0 degrees" Unity issue
            public float prevRotZ;
            public float circleNum;

            public float prevScaleX;
            public float prevScaleY;

            public ComponentCurves(string gameObjectName, string depth)
            {
                this.gameObjectName = gameObjectName;
                this.depth = depth;

                translateX = new();
                translateY = new();
                // rotX = new();
                // rotY = new();
                rotZ = new();
                scaleX = new();
                scaleY = new();

                prevRotZ = -1;
                circleNum = 0;
            }
        }

        // Utility functions
        // They get position/rotation/scale from the 2x3 affine matrix (TransformJSON)
        private static class Utility
        {
            /*
             * The SWF 2x3 transform matrix is:
             * scaleX rotateSkew1 translateX
             * rotateSkew0 scaleY translateY
             */

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

                float tr = t.scaleX + t.scaleY + 1;
                double qw, qx, qy, qz;

                if (tr > 0)
                {
                    float S = Mathf.Sqrt((float)(tr + 1.0)) * 2; // S=4*qw 
                    qw = 0.25 * S;
                    qx = 0;
                    qy = 0;
                    qz = (t.rotateSkew0 - t.rotateSkew1) / S;
                }
                else if ((t.scaleX > t.scaleY) & (t.scaleX > 1))
                {
                    float S = Mathf.Sqrt((float)(t.scaleX - t.scaleY)) * 2; // S=4*qx 
                    qw = 0;
                    qx = 0.25 * S;
                    qy = (t.rotateSkew1 + t.rotateSkew0) / S;
                    qz = 0;
                }
                else if (t.scaleY > 1)
                {
                    float S = Mathf.Sqrt((float)(t.scaleY - t.scaleX)) * 2; // S=4*qy
                    qw = 0;
                    qx = (t.rotateSkew1 + t.rotateSkew0) / S;
                    qy = 0.25 * S;
                    qz = 0;
                }
                else
                {
                    float S = Mathf.Sqrt((float)(1.0 + 1 - t.scaleX - t.scaleY)) * 2; // S=4*qz
                    qw = (t.rotateSkew0 - t.rotateSkew1) / S;
                    qx = 0;
                    qy = 0;
                    qz = 0.25 * S; 
                }

                Quaternion rot = new Quaternion(
                    (float) qx, (float) qy, (float) qz, (float) qw);
                return rot;
            }
            public static Vector3 Scale(TransformJSON t)
            {
                // Giving the scales the sign of scaleX, scaleY allows for negative scales
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
