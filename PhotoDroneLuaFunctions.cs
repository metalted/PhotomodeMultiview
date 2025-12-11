using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ZeepSDK.Scripting;
using ZeepSDK.Scripting.ZUA;

namespace PhotomodeMultiview
{
    public class OnPhotoDroneCommand : ILuaEvent
    {
        public string Name => "PhotoDrone_OnCommand";
        private Action<string> _photoDroneAction;
        public void Subscribe()
        {
            _photoDroneAction = cmd =>
            {                
                ScriptingApi.CallFunction(Name, cmd);                
            };

            DroneCommand.OnCommand += _photoDroneAction;
        }

        public void Unsubscribe()
        {
            if(_photoDroneAction != null)
            {
                DroneCommand.OnCommand -= _photoDroneAction;
                _photoDroneAction = null;
            }
        }
    }

    public class OnPhotoDroneUpdate : ILuaEvent
    {
        public string Name => "PhotoDrone_OnUpdate";
        private Action<float> _photoDroneAction;

        public void Subscribe()
        {
            _photoDroneAction = dt =>
            {
                ScriptingApi.CallFunction(Name, dt);
            };

            Plugin.Instance.OnUpdate += _photoDroneAction;
        }

        public void Unsubscribe()
        {
            if(_photoDroneAction != null)
            {
                Plugin.Instance.OnUpdate -= _photoDroneAction;
                _photoDroneAction = null;
            }
        }
    }

    public class CreateDrone : ILuaFunction
   {
        public string Namespace => "PhotoDrone";
        public string Name => "CreateDrone";
        public Delegate CreateFunction()
        {
            return new Action<string, string>(Implementation);
        }
        private void Implementation(string droneID, string presetString)
        {
            if (!Plugin.Instance.inPhotoMode)
            {
                return;
            }

            if(presetString == "cinematic")
            {
                DroneCommand.CreateDrone(droneID, null, true);
                //return;
            }


            DronePreset preset = new DronePreset(presetString);
            if(!preset.Valid)
            {
                return;
            }

            DroneCommand.CreateDrone(droneID, preset);
        }
    }

    public class CloseDrone : ILuaFunction
    {
        public string Namespace => "PhotoDrone";
        public string Name => "CloseDrone";
        public Delegate CreateFunction()
        {
            return new Action<string>(Implementation);
        }

        private void Implementation(string droneID)
        {
            PhotoDrone drone = DroneCommand.GetDrone(droneID);
            if(drone != null)
            {
                drone.ShutDown(false);
            }
        }
    }

    public class CloseAllDrones : ILuaFunction
    {
        public string Namespace => "PhotoDrone";
        public string Name => "CloseAllDrones";
        public Delegate CreateFunction()
        {
            return new Action(Implementation);
        }

        private void Implementation()
        {
            DroneCommand.ShutDown();
        }
    }

    public class SetDroneTarget : ILuaFunction
    {
        public string Namespace => "PhotoDrone";
        public string Name => "SetTarget";
        public Delegate CreateFunction()
        {
            return new Action<string, string>(Implementation);
        }

        private void Implementation(string droneID, string target)
        {
            PhotoDrone drone = DroneCommand.GetDrone(droneID);
            if (drone != null)
            {
                PlayerData playerData = DroneCommand.GetPlayer(target);
                if(playerData != null)
                {
                    drone.SetTarget(playerData);
                }
            }
        }
    }

    public class SetDroneFollowMode : ILuaFunction
    {
        public string Namespace => "PhotoDrone";
        public string Name => "SetFollowMode";
        public Delegate CreateFunction()
        {
            return new Action<string, string>(Implementation);
        }

        private void Implementation(string droneID, string followMode)
        {
            PhotoDrone drone = DroneCommand.GetDrone(droneID);
            if (drone != null)
            {
                if (!System.Enum.TryParse(followMode, true, out FollowMode parsedMode))
                    parsedMode = FollowMode.Smooth;
                drone.followMode = parsedMode;
            }
        }
    }

    public class SetDronePosition : ILuaFunction
    {
        public string Namespace => "PhotoDrone";
        public string Name => "SetPosition";
        public Delegate CreateFunction()
        {
            return new Action<string, string, float, float>(Implementation);
        }
        public void Implementation(string droneID, string unit, float x, float y)
        {
            PhotoDrone drone = DroneCommand.GetDrone(droneID);
            if (drone != null)
            {
                bool usePixels = unit != "%";
                DronePreset preset = new DronePreset(drone, usePixels);
                preset.X = x;
                preset.Y = y;
                drone.ApplyPreset(preset);
            }
        }
    }

    public class SetDroneSize : ILuaFunction
    {
        public string Namespace => "PhotoDrone";
        public string Name => "SetSize";
        public Delegate CreateFunction()
        {
            return new Action<string, string, float, float>(Implementation);
        }
        public void Implementation(string droneID, string unit, float x, float y)
        {
            PhotoDrone drone = DroneCommand.GetDrone(droneID);
            if (drone != null)
            {
                bool usePixels = unit != "%";
                DronePreset preset = new DronePreset(drone, usePixels);
                preset.Width = x;
                preset.Height = y;
                drone.ApplyPreset(preset);
            }
        }
    }

    public class SetDroneRect : ILuaFunction
    {
        public string Namespace => "PhotoDrone";
        public string Name => "SetRect";
        public Delegate CreateFunction()
        {
            return new Action<string, string, float, float, float, float>(Implementation);
        }
        public void Implementation(string droneID, string unit, float x, float y, float width, float height)
        {
            PhotoDrone drone = DroneCommand.GetDrone(droneID);
            if (drone != null)
            {
                bool usePixels = unit != "%";
                DronePreset preset = new DronePreset(drone, usePixels);
                preset.X = x;
                preset.Y = y;
                preset.Width = width;
                preset.Height = height;
                drone.ApplyPreset(preset);
            }
        }
    }

    public class SetDroneUI : ILuaFunction
    {
        public string Namespace => "PhotoDrone";
        public string Name => "SetUI";
        public Delegate CreateFunction()
        {
            return new Action<string, bool>(Implementation);
        }

        public void Implementation(string droneID, bool state)
        {
            PhotoDrone drone = DroneCommand.GetDrone(droneID);
            if (drone != null)
            {
                if(drone.isCinematic)
                {
                    return;
                }

                drone.droneUI.SetVisibility(state);
            }
        }
    }

    public class SetDroneLocked : ILuaFunction
    {
        public string Namespace => "PhotoDrone";
        public string Name => "SetLocked";
        public Delegate CreateFunction()
        {
            return new Action<string, bool>(Implementation);
        }

        public void Implementation(string droneID, bool state)
        {
            PhotoDrone drone = DroneCommand.GetDrone(droneID);
            if (drone != null)
            {
                drone.droneUI.SetLocked(state);
            }
        }
    }

    public class SetDroneFOV : ILuaFunction
    {
        public string Namespace => "PhotoDrone";
        public string Name => "SetFOV";
        public Delegate CreateFunction()
        {
            return new Action<string, float>(Implementation);
        }

        public void Implementation(string droneID, float fov)
        {
            PhotoDrone drone = DroneCommand.GetDrone(droneID);
            Debug.Log(drone);
            if (drone != null)
            {
                drone.SetFOV(fov);
            }
        }
    }

    public class GetPlayerNames : ILuaFunction
    {
        public string Namespace => "PhotoDrone";
        public string Name => "GetPlayerNames";
        public Delegate CreateFunction()
        {
            return new Func<List<string>>(Implementation);
        }
        private List<string> Implementation()
        {
            return DroneCommand.playerNames;
        }
    }
    
    public class SetDroneCinematic : ILuaFunction
    {
        public string Namespace => "PhotoDrone";
        public string Name => "SetCinematic";
        public Delegate CreateFunction()
        {
            return new Action<string, bool, bool, string>(Implementation);
        }

        public void Implementation(string droneID, bool state, bool uiVisible = false, string script = "")
        {
            PhotoDrone drone = DroneCommand.GetDrone(droneID);
            if (drone != null)
            {
                drone.SetCinematic(state, uiVisible, script);
            }
        }
    }

    public class GetPlayerTime : ILuaFunction
    {
        public string Namespace => "PhotoDrone";
        public string Name => "GetPlayerTime";
        public Delegate CreateFunction()
        {
            return new Func<string, float>(Implementation);
        }
        private float Implementation(string username)
        {
            return DroneCommand.GetPlayerTime(username);
        }
    }

    public class GetPlayerDistance : ILuaFunction
    {
        public string Namespace => "PhotoDrone";
        public string Name => "GetPlayerDistance";
        public Delegate CreateFunction()
        {
            return new Func<string, string, float>(Implementation);
        }
        private float Implementation(string droneID, string playerName)
        {
            PhotoDrone drone = DroneCommand.GetDrone(droneID);
            PlayerData player = DroneCommand.GetPlayer(playerName);
            if (drone != null && player != null)
            {
                return Vector3.Distance(drone.transform.position, player.zeepkistNetworkPlayer.Zeepkist.position);
            }
            else
            {
                return -1f;
            }
        }
    }
    
    public class GetPlayerPosition : ILuaFunction
    {
        public string Namespace => "PhotoDrone";
        public string Name => "GetPlayerPosition";
        public Delegate CreateFunction()
        {
            return new Func<string, Vector3>(Implementation);
        }
        private Vector3 Implementation(string playerName)
        {
            PlayerData player = DroneCommand.GetPlayer(playerName);
            if (player != null)
            {
                return player.zeepkistNetworkPlayer.Zeepkist.position;
            }
            else
            {
                return new Vector3();
            }
        }
    }

    public class GetDroneNames : ILuaFunction
    {
        public string Namespace => "PhotoDrone";
        public string Name => "GetDroneNames";
        public Delegate CreateFunction()
        {
            return new Func<List<string>>(Implementation);
        }
        private List<string> Implementation()
        {
            return DroneCommand.GetDroneNames();
        }
    }

    public class TogglePhotomode : ILuaFunction
    {
        public string Namespace => "PhotoDrone";
        public string Name => "TogglePhotomode";
        public Delegate CreateFunction()
        {
            return new Action(Implementation);
        }
        private void Implementation()
        {
            EnableFlyingCamera2 pm = GameObject.FindObjectOfType<EnableFlyingCamera2>(true);
            if(pm != null)
            {
                pm.ToggleFlyingCamera();
            }
        }
    }

    public class ShowPlayer : ILuaFunction
    {
        public string Namespace => "PhotoDrone";
        public string Name => "ShowPlayer";
        public Delegate CreateFunction()
        {
            return new Action<string>(Implementation);
        }
        private void Implementation(string playerName)
        {
            //Try to get the player
            PlayerData player = DroneCommand.GetPlayer(playerName);
            if(player != null)
            {
                if(player.zeepkistNetworkPlayer != null)
                {
                    if(player.zeepkistNetworkPlayer.Zeepkist != null)
                    {
                        player.zeepkistNetworkPlayer.Zeepkist.isSpectateInvisible = false;
                        player.zeepkistNetworkPlayer.Zeepkist.DoVisibility();
                    }
                }
            }
        }
    }

    public class RunAnimation : ILuaFunction
    {
        public string Namespace => "PhotoDrone";
        public string Name => "RunAnimation";
        public Delegate CreateFunction()
        {
            return new Action<string, string>(Implementation);
        }
        private void Implementation(string droneID, string script)
        {
            PhotoDrone drone = DroneCommand.GetDrone(droneID);
            if (drone != null)
            {
                if(drone.isCinematic)
                {
                    if(drone.anim != null)
                    {
                        drone.anim.Run(script);
                        drone.anim.StartAnimation();
                    }
                }
            }
        }
    }

    public class SetGameChat : ILuaFunction
    {
        public string Namespace => "PhotoDrone";
        public string Name => "SetChat";

        public Delegate CreateFunction()
        {
            return new Action<bool>(Implementation);
        }
        public void Implementation(bool state)
        {
            PlayerManager.Instance.instellingen.Settings.online_drawChat = state;
            PlayerManager.Instance.instellingen.SaveWaarden();
        }
    }

    public class SetPhotomodeUI : ILuaFunction
    {
        public string Namespace => "PhotoDrone";
        public string Name => "SetPhotomodeUI";

        public Delegate CreateFunction()
        {
            return new Action<bool>(Implementation);
        }
        public void Implementation(bool state)
        {
            SpectatorCameraUI ui = GameObject.FindObjectOfType<SpectatorCameraUI>(true);
            if(ui != null)
            {
                ui.ShowUI = state;
                ui.UpdateUI();
            }
        }
    }

    public class InPhotomode : ILuaFunction
    {
        public string Namespace => "PhotoDrone";
        public string Name => "InPhotomode";
        public Delegate CreateFunction()
        {
            return new Func<bool>(Implementation);
        }

        private bool Implementation()
        {
            return Plugin.Instance.inPhotoMode;
        }
    }
}
