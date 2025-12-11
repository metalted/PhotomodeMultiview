using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using ZeepSDK.Scripting;

namespace PhotomodeMultiview
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string pluginGuid = "com.metalted.zeepkist.photodrone";
        public const string pluginName = "PhotoDrone";
        public const string pluginVersion = "1.9.1";

        public static Plugin Instance;

        public ConfigEntry<KeyCode> createDroneKey;
        public ConfigEntry<bool> clearDronesOnPreset;
        public ConfigEntry<string> presets;

        public ConfigEntry<KeyCode> showPresetUIKey;
        public ConfigEntry<bool> showPresetUI;

        public ConfigEntry<KeyCode> activeKey;
        public ConfigEntry<bool> active;

        public ConfigEntry<bool> showDroneUI;
        public ConfigEntry<KeyCode> showDroneUIKey;
        public ConfigEntry<KeyCode> closeAllDrones;

        public ConfigEntry<KeyCode> toggleCursor;

        public ConfigEntry<KeyCode> luaKey0;
        public ConfigEntry<KeyCode> luaKey1;
        public ConfigEntry<KeyCode> luaKey2;
        public ConfigEntry<KeyCode> luaKey3;
        public ConfigEntry<KeyCode> luaKey4;
        public ConfigEntry<KeyCode> luaKey5;
        public ConfigEntry<KeyCode> luaKey6;
        public ConfigEntry<KeyCode> luaKey7;
        public ConfigEntry<KeyCode> luaKey8;
        public ConfigEntry<KeyCode> luaKey9;

        // Follow tuning settings
        public ConfigEntry<float> followOffsetX;
        public ConfigEntry<float> followOffsetY;
        public ConfigEntry<float> followOffsetZ;

        public ConfigEntry<float> lookAheadDistance;

        public ConfigEntry<float> smoothFollowSpeed;

        // Mode offsets
        public ConfigEntry<float> firstPersonOffsetX;
        public ConfigEntry<float> firstPersonOffsetY;
        public ConfigEntry<float> firstPersonOffsetZ;

        public ConfigEntry<float> bumperOffsetX;
        public ConfigEntry<float> bumperOffsetY;
        public ConfigEntry<float> bumperOffsetZ;

        public ConfigEntry<float> lockedOffsetX;
        public ConfigEntry<float> lockedOffsetY;
        public ConfigEntry<float> lockedOffsetZ;

        // Base FOV
        public ConfigEntry<float> baseFOV;

        // Per-mode FOVs
        public ConfigEntry<float> smoothFOV;
        public ConfigEntry<float> strictFOV;
        public ConfigEntry<float> lockedFOV;
        public ConfigEntry<float> bumperFOV;
        public ConfigEntry<float> firstFOV;


        public bool shouldShowGUI = false;
        public bool inPhotoMode = false;
        public List<DronePresetGroup> presetGroups = new List<DronePresetGroup>();
        public List<string> groupNames = new List<string>();

        public Action<float> OnUpdate;

        private void Awake()
        {
            Instance = this;

            Harmony harmony = new Harmony(pluginGuid);
            harmony.PatchAll();

            InitializeConfig();
            RegisterLua();
            DroneCommand.Initialize();

            // Plugin startup logic
            Logger.LogInfo($"Plugin PhotoDrone is loaded!");
        }

        private void InitializeConfig()
        {
            createDroneKey = Config.Bind("Settings", "Create Drone", KeyCode.None, "");
            clearDronesOnPreset = Config.Bind("Settings", "Clear Drones On Preset", true, "");
            presets = Config.Bind("Settings", "Presets", "", "");

            showPresetUIKey = Config.Bind("Settings", "Show Preset UI Key", KeyCode.None, "");
            showPresetUI = Config.Bind("Settings", "Show Preset UI", true, "");

            activeKey = Config.Bind("Settings", "Active Key", KeyCode.None, "");
            active = Config.Bind("Settings", "Active", true, "");

            showDroneUI = Config.Bind("Settings", "Show Drone UI", true, "");
            showDroneUIKey = Config.Bind("Settings", "Show Drone UI Key", KeyCode.None, "");

            closeAllDrones = Config.Bind("Settings", "Close All Drones", KeyCode.None, "");
            toggleCursor = Config.Bind("Settings", "Toggle Cursor", KeyCode.None, "");

            luaKey0 = Config.Bind("Key Commands", "Lua Key 0", KeyCode.None, "");
            luaKey1 = Config.Bind("Key Commands", "Lua Key 1", KeyCode.None, "");
            luaKey2 = Config.Bind("Key Commands", "Lua Key 2", KeyCode.None, "");
            luaKey3 = Config.Bind("Key Commands", "Lua Key 3", KeyCode.None, "");
            luaKey4 = Config.Bind("Key Commands", "Lua Key 4", KeyCode.None, "");
            luaKey5 = Config.Bind("Key Commands", "Lua Key 5", KeyCode.None, "");
            luaKey6 = Config.Bind("Key Commands", "Lua Key 6", KeyCode.None, "");
            luaKey7 = Config.Bind("Key Commands", "Lua Key 7", KeyCode.None, "");
            luaKey8 = Config.Bind("Key Commands", "Lua Key 8", KeyCode.None, "");
            luaKey9 = Config.Bind("Key Commands", "Lua Key 9", KeyCode.None, "");

            // === Follow Offsets ===
            followOffsetX = Config.Bind("Camera Tuning", "Follow Offset X", 0f, "Default follow offset X");
            followOffsetY = Config.Bind("Camera Tuning", "Follow Offset Y", 2f, "Default follow offset Y");
            followOffsetZ = Config.Bind("Camera Tuning", "Follow Offset Z", -3f, "Default follow offset Z");

            // === Look Ahead ===
            lookAheadDistance = Config.Bind("Camera Tuning", "Look Ahead Distance", 5f, "How far ahead the drone looks");

            // === Smooth Follow Speed ===
            smoothFollowSpeed = Config.Bind("Camera Tuning", "Smooth Follow Speed", 5f, "How fast the camera lerps");

            // === First Person Offset ===
            firstPersonOffsetX = Config.Bind("Camera Tuning", "First Person Offset X", 0f, "");
            firstPersonOffsetY = Config.Bind("Camera Tuning", "First Person Offset Y", 0.2f, "");
            firstPersonOffsetZ = Config.Bind("Camera Tuning", "First Person Offset Z", 2.2f, "");

            // === Bumper Offset ===
            bumperOffsetX = Config.Bind("Camera Tuning", "Bumper Offset X", 0f, "");
            bumperOffsetY = Config.Bind("Camera Tuning", "Bumper Offset Y", 0.5f, "");
            bumperOffsetZ = Config.Bind("Camera Tuning", "Bumper Offset Z", 3.5f, "");

            // === Locked Offset ===
            lockedOffsetX = Config.Bind("Camera Tuning", "Locked Offset X", 0f, "");
            lockedOffsetY = Config.Bind("Camera Tuning", "Locked Offset Y", 1.7f, "");
            lockedOffsetZ = Config.Bind("Camera Tuning", "Locked Offset Z", -3f, "");

            // Base camera FOV
            baseFOV = Config.Bind("Camera Tuning", "Base FOV", 60f, "Default camera field of view");

            // Per-mode FOV settings
            smoothFOV = Config.Bind("Camera Tuning", "Smooth Mode FOV", 60f, "FOV for Smooth follow mode");
            strictFOV = Config.Bind("Camera Tuning", "Strict Mode FOV", 60f, "FOV for Strict follow mode");
            lockedFOV = Config.Bind("Camera Tuning", "Locked Mode FOV", 50f, "FOV for Locked mode");
            bumperFOV = Config.Bind("Camera Tuning", "Bumper Mode FOV", 70f, "FOV for Bumper mode");
            firstFOV = Config.Bind("Camera Tuning", "First Person Mode FOV", 90f, "FOV for First-person camera");
        }

        private void RegisterLua()
        {
            ScriptingApi.RegisterType<UnityEngine.Vector3>();
            ScriptingApi.RegisterEvent<OnPhotoDroneCommand>();
            ScriptingApi.RegisterEvent<OnPhotoDroneUpdate>();
            ScriptingApi.RegisterFunction<CreateDrone>();
            ScriptingApi.RegisterFunction<CloseDrone>();
            ScriptingApi.RegisterFunction<CloseAllDrones>();
            ScriptingApi.RegisterFunction<SetDroneTarget>();
            ScriptingApi.RegisterFunction<SetDroneFollowMode>();
            ScriptingApi.RegisterFunction<SetDronePosition>();
            ScriptingApi.RegisterFunction<SetDroneSize>();
            ScriptingApi.RegisterFunction<SetDroneRect>();
            ScriptingApi.RegisterFunction<SetDroneUI>();
            ScriptingApi.RegisterFunction<SetDroneFOV>();
            ScriptingApi.RegisterFunction<SetDroneLocked>();
            ScriptingApi.RegisterFunction<SetDroneCinematic>();
            ScriptingApi.RegisterFunction<GetPlayerNames>();
            ScriptingApi.RegisterFunction<GetPlayerTime>();
            ScriptingApi.RegisterFunction<GetPlayerDistance>();
            ScriptingApi.RegisterFunction<GetPlayerPosition>();
            ScriptingApi.RegisterFunction<GetDroneNames>();
            ScriptingApi.RegisterFunction<TogglePhotomode>();
            ScriptingApi.RegisterFunction<ShowPlayer>();
            ScriptingApi.RegisterFunction<RunAnimation>();
            ScriptingApi.RegisterFunction<SetGameChat>();
            ScriptingApi.RegisterFunction<SetPhotomodeUI>();
            ScriptingApi.RegisterFunction<InPhotomode>();
        }

        private void Start()
        {
            presetGroups = DronePresetParser.ParseAllGroups(presets.Value);
            groupNames = presetGroups.Select(g => g.Name).ToList();
            Config.SettingChanged += Config_SettingChanged;
        }

        private void Config_SettingChanged(object sender, SettingChangedEventArgs e)
        {
            presetGroups = DronePresetParser.ParseAllGroups(presets.Value);
            groupNames = presetGroups.Select(g => g.Name).ToList();
        }

        public void Update()
        {
            if (Input.GetKeyDown(createDroneKey.Value) && active.Value && inPhotoMode)
            {
                DroneCommand.CreateDrone(System.Guid.NewGuid().ToString());
            }

            if(Input.GetKeyDown(showPresetUIKey.Value))
            {
                showPresetUI.Value = !showPresetUI.Value;
            }

            if(Input.GetKeyDown(activeKey.Value))
            {
                active.Value = !active.Value;
            }

            if(Input.GetKeyDown(showDroneUIKey.Value))
            {
                showDroneUI.Value = !showDroneUI.Value;
                DroneCommand.SetUI(showDroneUI.Value);
            }

            if(Input.GetKeyDown(closeAllDrones.Value))
            {
                DroneCommand.ShutDown();
            }

            if(Input.GetKeyDown(toggleCursor.Value) && active.Value && inPhotoMode)
            {
                PlayerManager.Instance.cursorManager.SetCursorEnabled(!Cursor.visible);
            }

            if(Input.GetKeyDown(luaKey0.Value) && active.Value)
            {
                DroneCommand.OnCommand?.Invoke("luakey0");
            }

            if (Input.GetKeyDown(luaKey1.Value) && active.Value)
            {
                DroneCommand.OnCommand?.Invoke("luakey1");
            }

            if (Input.GetKeyDown(luaKey2.Value) && active.Value)
            {
                DroneCommand.OnCommand?.Invoke("luakey2");
            }

            if (Input.GetKeyDown(luaKey3.Value) && active.Value)
            {
                DroneCommand.OnCommand?.Invoke("luakey3");
            }

            if (Input.GetKeyDown(luaKey4.Value) && active.Value)
            {
                DroneCommand.OnCommand?.Invoke("luakey4");
            }

            if (Input.GetKeyDown(luaKey5.Value) && active.Value)
            {
                DroneCommand.OnCommand?.Invoke("luakey5");
            }

            if (Input.GetKeyDown(luaKey6.Value) && active.Value)
            {
                DroneCommand.OnCommand?.Invoke("luakey6");
            }

            if (Input.GetKeyDown(luaKey7.Value) && active.Value)
            {
                DroneCommand.OnCommand?.Invoke("luakey7");
            }

            if (Input.GetKeyDown(luaKey8.Value) && active.Value)
            {
                DroneCommand.OnCommand?.Invoke("luakey8");
            }

            if (Input.GetKeyDown(luaKey9.Value) && active.Value)
            {
                DroneCommand.OnCommand?.Invoke("luakey9");
            }

            if (DroneCommand.DroneCount > 0)
            {
                OnUpdate?.Invoke(Time.deltaTime);
            }
        }

        public void ApplyGroup(DronePresetGroup group)
        {
            if(clearDronesOnPreset.Value)
            {
                DroneCommand.ShutDown();
            }

            foreach(DronePreset preset in group.Presets)
            {
                DroneCommand.CreateDrone(System.Guid.NewGuid().ToString(), preset);
            }
        }

        public void OnGUI()
        {
            if (!shouldShowGUI || !showPresetUI.Value || !active.Value || !inPhotoMode)
            {
                return;
            }     
            
            if(groupNames.Count == 0)
            {
                return;
            }

            try
            {
                for (int i = 0; i < groupNames.Count; i++)
                {
                    if(GUI.Button(new Rect(0 + i * 100f, Screen.height - 25f, 100f, 25f), groupNames[i]))
                    {
                        DronePresetGroup group = presetGroups.FirstOrDefault(p => p.Name == groupNames[i]);
                        if(group != null)
                        {
                            ApplyGroup(group);
                        }
                    }
                }
            }
            catch { }
        }

        public void WriteStringToFile(string content)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + @"\BepInEx\plugins\DronePresetLog.txt";

            try
            {
                if (!File.Exists(path))
                {
                    File.Create(path).Close();
                }

                File.AppendAllText(path, content + Environment.NewLine);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Failed to write to file: " + e.Message);
            }
        }

    }
}