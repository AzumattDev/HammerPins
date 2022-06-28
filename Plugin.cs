using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ServerSync;
using UnityEngine;

namespace HammerPins
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class HammerPinsPlugin : BaseUnityPlugin
    {
        internal const string ModName = "HammerPins";
        internal const string ModVersion = "1.0.0";
        internal const string Author = "Azumatt";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;

        internal static string ConnectionError = "";

        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource HammerPinsLogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        private static readonly ConfigSync ConfigSync = new(ModGUID)
            { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On,
                "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            DisplayPins = config("1 - General", "Display Pins", Toggle.On, "Show the pins on the map");

            IconsToPin = config("2 - Icons", "Icons to Pin",
                "guard_stone,VikingShip,Cart,Karve,portal_wood,bed,piece_bed02,Raft",
                new ConfigDescription("Prefab names of the pins from your hammer you wish to show on the map", null,
                    new ConfigurationManagerAttributes { CustomDrawer = MyDrawer }));

            IconsToPin.SettingChanged += (_, _) =>
            {
                Array.Clear(DisplayPinsOnMap.PrefabArray, 0, DisplayPinsOnMap.PrefabArray.Length);
                DisplayPinsOnMap.PrefabArray = IconsToPin.Value.Trim().Split(',').ToArray();
            };


            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                HammerPinsLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                HammerPinsLogger.LogError($"There was an issue loading your {ConfigFileName}");
                HammerPinsLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        internal static ConfigEntry<Toggle> DisplayPins = null!;
        internal static ConfigEntry<string>? IconsToPin;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private sealed class ConfigurationManagerAttributes
        {
            //public bool? Browsable = false;
            public Action<ConfigEntryBase> CustomDrawer = null!;
        }

        static void MyDrawer(ConfigEntryBase entry)
        {
            bool wasUpdated = false;
            GUILayout.BeginVertical();
            string? prefabs = entry.BoxedValue.ToString();
            string newItemName = GUILayout.TextArea(prefabs,
                new GUIStyle(GUI.skin.textArea) { stretchHeight = true });
            wasUpdated = wasUpdated || !ReferenceEquals(prefabs, newItemName);
            GUILayout.EndVertical();
            if (wasUpdated)
            {
                entry.BoxedValue = newItemName;
            }
        }

        class AcceptableShortcuts : AcceptableValueBase
        {
            public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
            {
            }

            public override object Clamp(object value) => value;
            public override bool IsValid(object value) => true;

            public override string ToDescriptionString() =>
                "# Acceptable values: " + string.Join(", ", KeyboardShortcut.AllKeyCodes);
        }

        #endregion
    }
}