using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenBurial
{
    [BepInPlugin("valafi.openburial", "Open Burial", "1.0.0")]
    public class OpenBurialPlugin : BaseUnityPlugin
    {
        public static ManualLogSource logSource;

        private void Awake()
        {
            logSource = Logger;
            logSource.LogInfo($"Open Burial is loaded!");

            Harmony harmony = new Harmony("valafi.openburial");
            harmony.PatchAll();
        }
    }


    [HarmonyPatch(typeof(RunManager), nameof(RunManager.StartRun))]
    public static class RunManagerStartRunOpenBurialPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            MethodInfo GetRefsMethod = typeof(DesertRockSpawner).GetMethod("GetRefs", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo enterencesField = typeof(DesertRockSpawner).GetField("enterences", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo insideField = typeof(DesertRockSpawner).GetField("inside", BindingFlags.NonPublic | BindingFlags.Instance);

            MethodInfo InstantiatePrefabMethod = typeof(HelperFunctions).GetMethod("InstantiatePrefab", BindingFlags.NonPublic | BindingFlags.Static, null, new System.Type[] { typeof(GameObject), typeof(Vector3), typeof(Quaternion), typeof(Transform) }, null);
            
            int seed = SceneManager.GetActiveScene().path.GetHashCode();
            System.Random prng = new System.Random(seed);

            DesertRockSpawner[] desertRockSpawners = Object.FindObjectsByType<DesertRockSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (DesertRockSpawner desertRockSpawner in desertRockSpawners)
            {
                GetRefsMethod.Invoke(desertRockSpawner, null);
                Transform enterences = (Transform)enterencesField.GetValue(desertRockSpawner);
                Transform inside = (Transform)insideField.GetValue(desertRockSpawner);

                bool hasEntrance = false;
                foreach (Transform entranceContainer in enterences)
                {
                    foreach (Transform doorObject in entranceContainer)
                    {
                        // If the door object name does not match an entrance object then it's not an entrance
                        if (!desertRockSpawner.enterenceObjects.Any(e => e.name == doorObject.name)) continue;

                        // Door object name DID match an entrance object, if it does NOT match a blocker object then it must be an entrance
                        if (!desertRockSpawner.blockerObjects.Any(b => b.name == doorObject.name))
                        {
                            hasEntrance = true;
                            break;
                        }

                        // Door object name matched an entrance and a blocker object. Blockers have a child for LODs with the LODGroup component, so fallback is to check that
                        bool doorHasLODs = false;
                        foreach (Transform doorChild in doorObject)
                        {
                            if (doorChild.TryGetComponent<LODGroup>(out _))
                            {
                                doorHasLODs = true;
                                break;
                            }
                        }
                        if (!doorHasLODs)
                        {
                            hasEntrance = true;
                            break;
                        }
                    }
                    if (hasEntrance) break;
                }
                if (hasEntrance == true) continue;

                Transform targetEntranceContainer = enterences.GetChild(prng.Next(0, enterences.childCount));

                for (int targetBlockerObject = targetEntranceContainer.childCount - 1; targetBlockerObject >= 0; targetBlockerObject--)
                {
                    Object.DestroyImmediate(targetEntranceContainer.GetChild(targetBlockerObject).gameObject);
                }

                ((GameObject)InstantiatePrefabMethod.Invoke(null, new object[] { desertRockSpawner.enterenceObjects[prng.Next(0, desertRockSpawner.enterenceObjects.Length)], targetEntranceContainer.position, targetEntranceContainer.rotation, targetEntranceContainer })).transform.localScale = Vector3.one * 2f;
                inside.position = new Vector3(targetEntranceContainer.position.x, inside.position.y, targetEntranceContainer.position.z);
            }
        }
    }
}
