using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenBurial
{
    public enum EDisableMazeRocks
    {
        Never,
        [Description("When Tomb Forced Open")]
        WhenForcedOpen,
        Always
    }

    [BepInPlugin("valafi.openburial", "Open Burial", "1.1.0")]
    public class OpenBurialPlugin : BaseUnityPlugin
    {
        public static ManualLogSource logSource;

        public static ConfigEntry<EDisableMazeRocks> disableMazeRocks;

        private void Awake()
        {
            logSource = Logger;
            logSource.LogInfo($"Open Burial is loaded!");

            disableMazeRocks = Config.Bind("Tomb Maze Rocks", "DisableMazeRocks", EDisableMazeRocks.WhenForcedOpen, "Maze rocks can totally block the maze tunnels! This determines when to disable them.");

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

                // Determine if there is an entrance
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

                // Disable maze rocks if configured
                if (OpenBurialPlugin.disableMazeRocks.Value == EDisableMazeRocks.Always || OpenBurialPlugin.disableMazeRocks.Value == EDisableMazeRocks.WhenForcedOpen && !hasEntrance)
                {
                    HashSet<string> rockContainerNames = new HashSet<string> { "rocks", "floor", "roof" };

                    foreach (Transform templeChild in desertRockSpawner.gameObject.transform)
                    {
                        if (templeChild.name.ToLower() != "inside") continue;

                        foreach (Transform insideChild in templeChild)
                        {
                            if (insideChild.name.ToLower() != "maze") continue;

                            foreach (Transform mazeChild in insideChild)
                            {
                                if (!rockContainerNames.Contains(mazeChild.name.ToLower())) continue;
                                mazeChild.gameObject.SetActive(false);
                            }
                        }
                    }
                }

                // Force an entrance if there wasn't one
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
