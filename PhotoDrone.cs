using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PhotomodeMultiview
{
    public enum FollowMode
    {
        Smooth,
        Strict,
        Locked,
        Bumper,
        First
    }

    public class PhotoDrone : MonoBehaviour
    {
        public string droneID;

        //UI
        public DroneWindowUI droneUI;       

        //Cameras
        private Camera mainCamera;
        private Camera skyboxCamera;

        //Camera target texture
        private RenderTexture camTexture;
        int prevWidth = 0;
        int prevHeight = 0;

        //Following
        private bool following = false;
        public FollowMode followMode = FollowMode.Smooth;
        public PlayerData targetPlayer;
        private Transform targetTransform;
        public Vector3 lookOffset = new Vector3(6f, 0, 0);

        public Vector3 followOffset = new Vector3(0, 2, -3);        
        public Vector3 firstPersonOffset = new Vector3(0, 0.2f, 2.2f);
        public Vector3 bumperOffset = new Vector3(0, 0.5f, 3.5f);
        public Vector3 lockedOffset = new Vector3(0, 1.7f, -3f);

        private float lookAheadDistance = 5f;
        private float smoothFollowSpeed = 5f;

        public bool isCinematic = false;
        public TransformAnimator anim;

        //Reconnection
        private Coroutine recoveryRoutine;

        public void Setup(GameObject dronePrefab, bool isCinematic = false)
        {
            this.isCinematic = isCinematic;

            // Apply tuning from config
            followOffset = new Vector3(
                Plugin.Instance.followOffsetX.Value,
                Plugin.Instance.followOffsetY.Value,
                Plugin.Instance.followOffsetZ.Value
            );

            firstPersonOffset = new Vector3(
                Plugin.Instance.firstPersonOffsetX.Value,
                Plugin.Instance.firstPersonOffsetY.Value,
                Plugin.Instance.firstPersonOffsetZ.Value
            );

            bumperOffset = new Vector3(
                Plugin.Instance.bumperOffsetX.Value,
                Plugin.Instance.bumperOffsetY.Value,
                Plugin.Instance.bumperOffsetZ.Value
            );

            lockedOffset = new Vector3(
                Plugin.Instance.lockedOffsetX.Value,
                Plugin.Instance.lockedOffsetY.Value,
                Plugin.Instance.lockedOffsetZ.Value
            );

            // These two are not vectors:
            lookAheadDistance = Plugin.Instance.lookAheadDistance.Value;
            smoothFollowSpeed = Plugin.Instance.smoothFollowSpeed.Value;


            //Add the drone prefab to the transfrom.
            GameObject cameraRig = Instantiate(dronePrefab, transform);
            cameraRig.name = "DroneCameraRig";
            cameraRig.gameObject.SetActive(true);

            //Get all the references
            camTexture = new RenderTexture(320, 240, 16);
            // Assign cameras
            Camera[] cams = cameraRig.GetComponentsInChildren<Camera>();
            foreach (Camera cam in cams)
            {
                if (cam.clearFlags == CameraClearFlags.Skybox)
                {
                    skyboxCamera = cam;
                }
                else
                {
                    mainCamera = cam;
                }
                cam.transform.localPosition = Vector3.zero;
                cam.transform.localRotation = Quaternion.identity;
            }

            mainCamera.targetTexture = camTexture;
            skyboxCamera.targetTexture = camTexture;
            mainCamera.depth = 1;
            skyboxCamera.depth = 0;

            ApplyFOVForMode();

            // Spawn the UI
            GameObject uiGO = DroneWindowUIFactory.CreateDroneWindowUI(DroneCommand.canvas.transform);
            droneUI = uiGO.GetComponent<DroneWindowUI>();

            // Assign RenderTexture to the RawImage
            droneUI.feedImage.texture = camTexture;
            
            // Hook up buttons
            droneUI.OnClosed = () => ShutDown(false);
            droneUI.OnLogPressed = () =>
            {
                string presetString = $"----------\nScreenSpace: {GetCurrentPreset(false)}\nPixels    : {GetCurrentPreset(true)}";
                Plugin.Instance.WriteStringToFile(presetString);
            };

            droneUI.SetFollowModes(new List<string>(Enum.GetNames(typeof(FollowMode))));

            // Assign dropdown logic
            droneUI.OnPlayerSelected = (name) =>
            {
                var player = DroneCommand.GetPlayer(name);
                if (player != null)
                    SetTarget(player);
            };

            droneUI.OnFollowModeSelected = (modeName) =>
            {
                followMode = (FollowMode)Enum.Parse(typeof(FollowMode), modeName);
                ApplyFOVForMode();
            };

            if (isCinematic)
            {
                anim = transform.gameObject.AddComponent<TransformAnimator>();
                anim.drone = this;
                anim.targetCamera1 = mainCamera;
                anim.targetCamera2 = skyboxCamera;

                droneUI.SetVisibility(false);
                droneUI.nameUI.gameObject.SetActive(false);
                droneUI.velocityUI.gameObject.SetActive(false);
                droneUI.speedDisplay.gameObject.SetActive(false);
            }
        }

        private void ApplyFOVForMode()
        {
            float fov = Plugin.Instance.baseFOV.Value;

            switch (followMode)
            {
                case FollowMode.Smooth:
                    fov = Plugin.Instance.smoothFOV.Value;
                    break;
                case FollowMode.Strict:
                    fov = Plugin.Instance.strictFOV.Value;
                    break;
                case FollowMode.Locked:
                    fov = Plugin.Instance.lockedFOV.Value;
                    break;
                case FollowMode.Bumper:
                    fov = Plugin.Instance.bumperFOV.Value;
                    break;
                case FollowMode.First:
                    fov = Plugin.Instance.firstFOV.Value;
                    break;
            }

            if (mainCamera != null)
                mainCamera.fieldOfView = fov;

            if (skyboxCamera != null)
                skyboxCamera.fieldOfView = fov;
        }


        void Update()
        {
            if(isCinematic)
            {
                return;
            }
            Debug.Log($"PhotoDrone Update {isCinematic} | {followMode}");

            if (targetPlayer == null || targetTransform == null)
            {
                if (following)
                {
                    ShutDown();
                    return;
                }
            }

            if (!following)
            {
                return;
            }

            try
            {
                Vector3 lookPoint = targetTransform.position + targetTransform.forward * lookAheadDistance;
                transform.LookAt(lookPoint);

                switch (followMode)
                {
                    case FollowMode.Smooth:
                        // Smooth follow logic
                        Vector3 desiredPosition = targetTransform.position + targetTransform.rotation * followOffset;
                        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * smoothFollowSpeed);
                        Quaternion targetRotation = Quaternion.LookRotation(lookPoint - transform.position);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * smoothFollowSpeed);
                        break;
                    case FollowMode.Strict:
                        transform.position = targetTransform.position + targetTransform.rotation * followOffset;
                        transform.LookAt(lookPoint);
                        break;
                    case FollowMode.Locked:
                        transform.position = targetTransform.TransformPoint(lockedOffset);
                        Quaternion baseRotation = targetTransform.rotation;
                        Quaternion localTilt = Quaternion.Euler(lookOffset);
                        transform.rotation = baseRotation * localTilt;
                        break;
                    case FollowMode.First:
                        transform.position = targetTransform.TransformPoint(firstPersonOffset);
                        transform.rotation = targetTransform.rotation;
                        break;
                    case FollowMode.Bumper:
                        transform.position = targetTransform.TransformPoint(bumperOffset);
                        transform.rotation = targetTransform.rotation;
                        break;


                }
            }
            catch (Exception e)
            {
                Debug.LogWarning(e.Message);
                ShutDown();
            }

            if (targetPlayer.zeepkistNetworkPlayer.Zeepkist != null)
            {
                NetworkedZeepkistGhost ghost = targetPlayer.zeepkistNetworkPlayer.Zeepkist;
                droneUI.velocityUI.text = ghost.displayVelocity.ToString();
                droneUI.speedDisplay.DrawControlDisplay(ghost.armsUp, ghost.brake, ghost.isUsingActionKey, ghost.GetInputScalar(), ghost.GetSteeringScalar(), ghost.GetMaxSteerAngle(), ghost.zeepkistPowerUp, ghost.GetPitchScalar());
            }

            Vector2 currentSize = droneUI.feedImage.rectTransform.rect.size;
            int texWidth = Mathf.RoundToInt(currentSize.x);
            int texHeight = Mathf.RoundToInt(currentSize.y);

            if (texWidth != prevWidth || texHeight != prevHeight)
            {
                ResizeRenderTexture(texWidth, texHeight);
                prevWidth = texWidth;
                prevHeight = texHeight;
            }
        }

        public void ApplyPreset(DronePreset preset)
        {
            if (preset == null)
                return;

            followMode = preset.Mode;

            if (string.IsNullOrEmpty(preset.Target))
            {
                SetInitialTarget();
            }
            else
            {
                PlayerData p = DroneCommand.GetPlayer(preset.Target);
                if (p == null)
                    SetInitialTarget();
                else
                    SetTarget(p);
            }

            float x = preset.X;
            float y = preset.Y;
            float width = preset.Width;
            float height = preset.Height;

            if (!preset.UsePixels)
            {
                x *= Screen.width;
                y *= Screen.height;
                width *= Screen.width;
                height *= Screen.height;
            }

            x = Mathf.RoundToInt(x);
            y = Mathf.RoundToInt(y);
            width = Mathf.Max(100, Mathf.RoundToInt(width));
            height = Mathf.Max(100, Mathf.RoundToInt(height));

            // Apply to RectTransform
            RectTransform rect = droneUI.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(x, -y); // Top-left anchor = y is inverted
            rect.sizeDelta = new Vector2(width, height);

            // Resize RenderTexture immediately
            ResizeRenderTexture((int)width, (int)height);
            prevWidth = (int)width;
            prevHeight = (int)height;
        }
        public string GetCurrentPreset(bool usePixels = true)
        {
            string mode = followMode.ToString().ToLower();
            string target = targetPlayer != null ? targetPlayer.username : "";
            string unit = usePixels ? "px" : "%";

            RectTransform rect = droneUI.GetComponent<RectTransform>();
            Vector2 anchoredPos = rect.anchoredPosition;
            Vector2 size = rect.sizeDelta;

            float x = usePixels ? Mathf.RoundToInt(anchoredPos.x) : anchoredPos.x / Screen.width;
            float y = usePixels ? Mathf.RoundToInt(-anchoredPos.y) : -anchoredPos.y / Screen.height;
            float width = usePixels ? Mathf.RoundToInt(size.x) : size.x / Screen.width;
            float height = usePixels ? Mathf.RoundToInt(size.y) : size.y / Screen.height;

            string presetString = $"{mode};{target};{unit};{x.ToString(System.Globalization.CultureInfo.InvariantCulture)};" +
                                  $"{y.ToString(System.Globalization.CultureInfo.InvariantCulture)};" +
                                  $"{width.ToString(System.Globalization.CultureInfo.InvariantCulture)};" +
                                  $"{height.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

            return presetString;
        }

        public void SetFOV(float fov)
        {
            float f = Mathf.Clamp(fov, 1f, 135f);
            if(mainCamera != null)
            {
                mainCamera.fieldOfView = f;
            }

            if(skyboxCamera != null)
            {
                skyboxCamera.fieldOfView = f;                
            }
        }
        
        public void SetCinematic(bool state, bool uiVisible = false, string script = "")
        {
            isCinematic = state;
            if (isCinematic)
            {
                //anim = transform.gameObject.AddComponent<TransformAnimator>();
                //anim.targetCamera1 = mainCamera;
                //anim.targetCamera2 = skyboxCamera;
                if (script != null && script != "")
                    anim.Run(script);
                else
                    anim.StartAnimation();
                
                droneUI.SetVisibility(uiVisible);
                droneUI.nameUI.gameObject.SetActive(false);
                droneUI.velocityUI.gameObject.SetActive(false);
                droneUI.speedDisplay.gameObject.SetActive(false);
            }
            else
            {
                //anim = transform.gameObject.AddComponent<TransformAnimator>();
                //anim.targetCamera1 = mainCamera;
                //anim.targetCamera2 = skyboxCamera;
                anim.StopAnimation();

                droneUI.SetVisibility(uiVisible);
                droneUI.nameUI.gameObject.SetActive(true);
                droneUI.velocityUI.gameObject.SetActive(true);
                droneUI.speedDisplay.gameObject.SetActive(true);
            }
        }
       
        public void SetTarget(PlayerData player)
        {
            try
            {
                targetPlayer = player;

                if (targetPlayer.isLocalPlayer)
                {
                    var local = GameObject.FindObjectOfType<ReadyToReset>(true);
                    targetTransform = local.transform;

                    droneUI.speedDisplay.gameObject.SetActive(false);
                    droneUI.velocityUI.gameObject.SetActive(false);
                }
                else
                {
                    var ghost = targetPlayer.zeepkistNetworkPlayer;
                    targetTransform = ghost.Zeepkist.ghostModel.transform;

                    droneUI.speedDisplay.gameObject.SetActive(true);
                    droneUI.velocityUI.gameObject.SetActive(true);
                }

                droneUI.nameUI.text = targetPlayer.username;

                mainCamera.enabled = true;
                skyboxCamera.enabled = true;
                following = true;
                ApplyFOVForMode();

                transform.position = targetTransform.position + targetTransform.rotation * followOffset;
                Vector3 lookPoint = targetTransform.position + targetTransform.forward * lookAheadDistance;
                transform.rotation = Quaternion.LookRotation(lookPoint - transform.position);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Exception in SetTarget: " + e.Message);
                ShutDown(false);
            }
        }
        public void SetInitialTarget()
        {
            List<string> names = new List<string>(DroneCommand.playerNames);
            foreach (string n in names)
            {
                PlayerData player = DroneCommand.GetPlayer(n);
                if (player != null)
                {
                    SetTarget(player);
                    return;
                }
            }

            ShutDown(false);
        }

        void ResizeRenderTexture(int width, int height)
        {
            if (camTexture != null)
            {
                camTexture.Release();
                Destroy(camTexture);
            }

            var newTex = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);

            if (mainCamera != null)
                mainCamera.targetTexture = newTex;

            if (skyboxCamera != null)
                skyboxCamera.targetTexture = newTex;

            camTexture = newTex;

            // Reassign to UI
            if (droneUI != null && droneUI.feedImage != null)
            {
                droneUI.feedImage.texture = camTexture;
            }
        }

        private IEnumerator AttemptRecovery()
        {
            float timeout = 3f;
            float elapsed = 0f;
            float interval = 0.25f;

            while (elapsed < timeout)
            {
                yield return new WaitForSeconds(interval);
                elapsed += interval;

                PlayerData retry = DroneCommand.GetPlayer(targetPlayer.username);
                if (retry != null)
                {
                    SetTarget(retry);
                    yield break;
                }
            }

            DroneCommand.DestroyDrone(this);
        }

        public void ShutDown(bool cooldown = true)
        {
            mainCamera.enabled = false;
            skyboxCamera.enabled = false;
            following = false;

            if(cooldown)
            {
                Debug.LogWarning("Trying reattempt");
                if (recoveryRoutine != null)
                {
                    StopCoroutine(recoveryRoutine);
                }

                recoveryRoutine = StartCoroutine(AttemptRecovery());
            }
            else
            {
                DroneCommand.DestroyDrone(this);
            }
        }

        public void CleanUp()
        {
            if (droneUI != null)
            {
                GameObject.Destroy(droneUI.gameObject);
                droneUI = null;
            }

            if (camTexture != null)
            {
                camTexture.Release();
                GameObject.Destroy(camTexture);
                camTexture = null;
            }
        }

        void OnDestroy()
        {
            if (camTexture != null)
            {
                camTexture.Release();
            }
        }
    }
}