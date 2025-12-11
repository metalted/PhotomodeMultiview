using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using ZeepkistClient;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;
using ZeepSDK.PhotoMode;
using ZeepSDK.ChatCommands;
using ZeepSDK.Chat;
using System;
using ZeepSDK.Level;

namespace PhotomodeMultiview
{
    public class PlayerData
    {
        public string username;
        public bool isLocalPlayer;
        public ZeepkistNetworkPlayer zeepkistNetworkPlayer;

        public PlayerData(ZeepkistNetworkPlayer zeepkistNetworkPlayer)
        {
            this.zeepkistNetworkPlayer = zeepkistNetworkPlayer;
            username = zeepkistNetworkPlayer.GetUserNameNoTag();
            isLocalPlayer = zeepkistNetworkPlayer.IsLocal;
        }
    }

    public static class DroneCommand
    {
        private static GameObject dronePrefab;
        private static Dictionary<string, PhotoDrone> drones = new Dictionary<string, PhotoDrone>();
        public static List<PlayerData> players = new List<PlayerData>();
        public static List<string> playerNames = new List<string>();
        public static Canvas canvas;
        public static Action<string> OnCommand;
        public static int DroneCount => drones.Count;

        public static void Initialize()
        {
            MultiplayerApi.PlayerJoined += (ZeepkistNetworkPlayer player) =>
            {
                RefreshPlayers();
            };

            MultiplayerApi.PlayerLeft += (ZeepkistNetworkPlayer player) =>
            {
                RefreshPlayers();
            };

            RacingApi.LevelLoaded += () =>
            {
                if (ZeepkistNetwork.IsConnected)
                {
                    if (dronePrefab == null)
                    {
                        CreateDronePrefab();
                    }

                    RefreshPlayers();                    
                }
            };

            PhotoModeApi.PhotoModeEntered += () =>
            {
                Plugin.Instance.shouldShowGUI = true;
                Plugin.Instance.inPhotoMode = true;
            };

            PhotoModeApi.PhotoModeExited += () =>
            {
                ShutDown();
                Plugin.Instance.shouldShowGUI = false;
                Plugin.Instance.inPhotoMode = false;
            };

            MultiplayerApi.DisconnectedFromGame += () =>
            {
                ShutDown();
                Plugin.Instance.shouldShowGUI = false;
                Plugin.Instance.inPhotoMode = false;
            };
            RacingApi.RoundEnded += () =>
            {
                ShutDown();
                Plugin.Instance.shouldShowGUI = false;
                Plugin.Instance.inPhotoMode = false;
            };

            ChatCommandApi.RegisterLocalChatCommand(
                "/",
                "pd",
                "Photodrone lua command",
                arguments => {

                    if (arguments == "log")
                    {
                        FlyingCameraScript fcs = GameObject.FindObjectOfType<FlyingCameraScript>(true);
                        if(fcs != null)
                        {
                            ChatApi.AddLocalMessage($"pos[x:{fcs.nikon.transform.position.x} | y:{fcs.nikon.transform.position.y} | z:{fcs.nikon.transform.position.z}] rot[x: {fcs.nikon.transform.eulerAngles.x}| y: {fcs.nikon.transform.eulerAngles.y}| z:{fcs.nikon.transform.eulerAngles.z}]");
                        }
                    }
                    else
                    {
                        OnCommand?.Invoke(arguments);
                    }
                }
            );

            CreateCanvas();
        }

        public static void CreateCanvas()
        {
            GameObject canvasGO = new GameObject("DroneCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -1;

            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            GameObject.DontDestroyOnLoad(canvasGO);
        }
        
        public static void SetUI(bool state)
        {
            foreach(PhotoDrone pd in drones.Values)
            {
                if(pd.droneUI != null)
                {
                    if (!pd.isCinematic)
                    {
                        pd.droneUI.SetVisibility(state);
                    }
                }
            }
        }

        public static PhotoDrone GetDrone(string droneID)
        {
            if(drones.ContainsKey(droneID))
            {
                return drones[droneID];
            }

            return null;
        }

        public static List<string> GetDroneNames()
        {
            return drones.Keys.ToList();
        }

        public static List<string> GetPlayers()
        {
            return playerNames;
        }

        public static PhotoDrone CreateDrone(string droneID, DronePreset preset = null, bool isCinematic = false)
        {
            if (!ZeepkistNetwork.IsConnected)
            {
                Debug.Log("Can't create drone because we are not connected.");
                return null;
            }

            if(dronePrefab == null)
            {
                Debug.LogWarning("Can't create drone because drone prefab isn't ready yet.");
                return null;
            }

            if(players.Count == 0)
            {
                Debug.LogWarning("Can't create drone because there are no valid player targets.");
                return null;
            }

            if(string.IsNullOrEmpty(droneID))
            {
                Debug.LogWarning("CreateDrone: droneID is empty");
                return null;
            }

            if(drones.ContainsKey(droneID))
            {
                Debug.LogWarning("CreateDrone: droneID already exists.");
                return null;
            }

            GameObject drone = new GameObject("Drone");
            PhotoDrone d = drone.AddComponent<PhotoDrone>();
            d.droneID = droneID;
            drones.Add(droneID, d);
            d.Setup(dronePrefab, isCinematic);

            if(preset != null)
            {
                d.ApplyPreset(preset);
            }
            else if(!isCinematic)
            {
                d.SetInitialTarget();
            }
            
            GameObject.DontDestroyOnLoad(drone);

            return d;
        }

        public static void CreateDronePrefab()
        {
            try
            {
                // Find the camera to copy
                FlyingCameraScript flyingCameraScript = GameObject.FindObjectOfType<FlyingCameraScript>(true);
                if (flyingCameraScript == null)
                    return;

                // Clone the camera rig
                GameObject copy = GameObject.Instantiate(flyingCameraScript.gameObject);
                copy.name = "DroneCameraRig";

                // Strip unused components
                GameObject.Destroy(copy.GetComponent<AudioListener>());
                GameObject.Destroy(copy.GetComponent<FlyingCameraScript>());

                var ppLayer = copy.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>();
                if (ppLayer != null)
                    ppLayer.enabled = false;

                GameObject.DontDestroyOnLoad(copy);

                // Hide the prefab for later spawning
                copy.SetActive(false);

                // Assign to global drone prefab
                dronePrefab = copy;
            }
            catch
            {
                Debug.LogWarning("Wasn't able to create drone prefab.");
            }
        }        

        private static void RefreshPlayers()
        {
            players.Clear();
            playerNames.Clear();

            if(!ZeepkistNetwork.IsConnected)
            {
                return;
            }

            List<ZeepkistNetworkPlayer> pList = new List<ZeepkistNetworkPlayer>(ZeepkistNetwork.Players.Values);
            foreach(ZeepkistNetworkPlayer p in pList)
            {
                players.Add(new PlayerData(p));
            }

            playerNames = players.Select(p => p.username).ToList();
        }

        public static PlayerData GetPlayer(string name)
        {
            return players.FirstOrDefault(p => p.username == name);
        }

        public static float GetPlayerTime(string username)
        {
            PlayerData player = GetPlayer(username);
            if(player == null)
            {
                return -1f;
            }

            NetworkedZeepkistGhost g = player.zeepkistNetworkPlayer.Zeepkist;
            if (g != null)
            {
                return g.displayRuntime;
            }

            return -1f;
        }

        public static void ShutDown()
        {
            Dictionary<string, PhotoDrone> droneDict = new Dictionary<string, PhotoDrone>(drones);

            foreach(PhotoDrone d in droneDict.Values)
            {
                d.ShutDown(false);
            }

            drones.Clear();
        }

        public static void DestroyDrone(PhotoDrone drone)
        {
            drone.CleanUp();
            string droneID = drone.droneID;
            GameObject.Destroy(drone.gameObject);
            drones.Remove(droneID);
        }
    }
}
