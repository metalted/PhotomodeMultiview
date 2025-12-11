using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System;

namespace PhotomodeMultiview
{

    public class TransformAnimator : MonoBehaviour
    {
        public struct Frame
        {
            public Vector3 position;
            public Vector3 rotation;
            public float time;
            public bool isAbsolute;
            public bool useLocalSpace;
            public float? fov;
            public bool setProjection; // true if projection mode changes
            public bool ortho;         // true for orthographic, false for perspective
            public bool useLookAt;
            public bool smoothLookAt;
            public Vector3 lookAtTarget; //static look-at
            public string lookAtPlayerName; //dynamic look-at
            public FollowMode followMode;
            public bool cinematicMode;
        }

        private List<Frame> frames = new List<Frame>();
        private int currentFrameIndex = 0;
        private float frameTimer = 0f;
        private bool isPlaying = false;

        public PhotoDrone drone;
        private bool cinMode = true;
        private FollowMode folMode;

        public bool useLookAt;
        private bool smoothLookAt = false;
        public Vector3 lookAtTarget;

        private Transform dynamicLookAtTransform;

        private Vector3 startPos;
        private Quaternion startRot;

        private Vector3 targetPos;
        private Quaternion targetRot;

        public Camera targetCamera1;
        public Camera targetCamera2;

        private float startFov;
        private float targetFov;
        private bool animateFov;

        private bool loop = false;

        public void Run(string script)
        {
            SetupAnimation(Parse(script));
        }

        private void Update()
        {
            if (!isPlaying || currentFrameIndex >= frames.Count || frames.Count == 0)
                return;

            var frame = frames[currentFrameIndex];

            if (frame.time <= 0f)
            {
                transform.position = frame.isAbsolute ? frame.position : transform.position + frame.position;
                transform.rotation = frame.isAbsolute ? Quaternion.Euler(frame.rotation) : transform.rotation * Quaternion.Euler(frame.rotation);
                NextFrame();
                return;
            }

            frameTimer += Time.deltaTime;
            float t = Mathf.Clamp01(frameTimer / frame.time);
            
            if (!frame.cinematicMode)
            {
                drone.isCinematic = false;
                drone.followMode = frame.followMode;
                drone.droneUI.nameUI.gameObject.SetActive(true);
                drone.droneUI.velocityUI.gameObject.SetActive(true);
                drone.droneUI.speedDisplay.gameObject.SetActive(true);
                if (t >= 1f)
                {
                    NextFrame();
                }
                return;
            }
            else
            {
                drone.isCinematic = true;
                drone.droneUI.nameUI.gameObject.SetActive(false);
                drone.droneUI.velocityUI.gameObject.SetActive(false);
                drone.droneUI.speedDisplay.gameObject.SetActive(false);
            }
            
            // Movement
            if (frame.useLookAt && frame.useLocalSpace)
            {
                Vector3 localMovePerSec = transform.TransformDirection(frame.position) / frame.time;
                transform.position += localMovePerSec * Time.deltaTime;
            }
            else
            {
                transform.position = Vector3.Lerp(startPos, targetPos, t);
            }

            if (useLookAt)
            {
                Vector3 targetPosLook;

                if (dynamicLookAtTransform != null)
                {
                    targetPosLook = dynamicLookAtTransform.position;
                }
                else
                {
                    targetPosLook = lookAtTarget;
                }

                Vector3 dir = targetPosLook - transform.position;

                if (dir.sqrMagnitude > 0.001f)
                {
                    Quaternion lookRot = Quaternion.LookRotation(dir.normalized, Vector3.up);

                    if (smoothLookAt)
                    {
                        transform.rotation = Quaternion.Slerp(startRot, lookRot, t);
                    }
                    else
                    {
                        transform.rotation = lookRot;
                    }
                }
            }
            else
            {
                transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            }

            if (animateFov)
            {
                targetCamera1.fieldOfView = Mathf.Lerp(startFov, targetFov, t);
                targetCamera2.fieldOfView = Mathf.Lerp(startFov, targetFov, t);
            }

            if (t >= 1f)
            {
                NextFrame();
            }
        }

        public void SetupAnimation(List<Frame> newFrames)
        {
            frames = newFrames;
            currentFrameIndex = 0;
            frameTimer = 0f;
        }
        public void StartAnimation()
        {
            if(frames.Count == 0)
                return;
            currentFrameIndex = 0;
            frameTimer = 0f;
            isPlaying = true;
            SetupCurrentFrame();
        }
        
        public void StopAnimation()
        {
            currentFrameIndex = 0;
            frameTimer = 0f;
            isPlaying = false;
        }

        private void NextFrame()
        {
            currentFrameIndex++;
            frameTimer = 0f;

            if (currentFrameIndex < frames.Count)
            {
                SetupCurrentFrame();
            }
            else if (loop)
            {
                currentFrameIndex = 0;
                SetupCurrentFrame();
            }
            else
            {
                isPlaying = false;
            }
        }

        private void SetupCurrentFrame()
        {
            var frame = frames[currentFrameIndex];

            startPos = transform.position;
            startRot = transform.rotation;

            if (frame.isAbsolute)
                targetPos = frame.position;
            else if (frame.useLocalSpace)
                targetPos = transform.position + transform.TransformDirection(frame.position);
            else
                targetPos = transform.position + frame.position;

            // Default rotation logic (only used if not using lookAt)
            targetRot = frame.isAbsolute ? Quaternion.Euler(frame.rotation) : transform.rotation * Quaternion.Euler(frame.rotation);

            animateFov = false;

            if (frame.setProjection)
            {
                targetCamera1.orthographic = frame.ortho;
                targetCamera2.orthographic = frame.ortho;
            }

            if (frame.fov.HasValue)
            {
                startFov = targetCamera1.fieldOfView;
                startFov = targetCamera2.fieldOfView;
                targetFov = frame.fov.Value;
                animateFov = frame.time > 0;
                if (!animateFov)
                {
                    targetCamera1.fieldOfView = targetFov;
                    targetCamera2.fieldOfView = targetFov;
                }
            }

            useLookAt = frame.useLookAt;
            smoothLookAt = frame.smoothLookAt;
            lookAtTarget = frame.lookAtTarget;

            if (!string.IsNullOrEmpty(frame.lookAtPlayerName))
            {
                var p = DroneCommand.players
                    .FirstOrDefault(pl =>
                        pl.username.Equals(frame.lookAtPlayerName, System.StringComparison.OrdinalIgnoreCase));

                if (p == null)
                {
                    Debug.LogWarning($"[Animator] Player '{frame.lookAtPlayerName}' not found.");
                    dynamicLookAtTransform = null;
                    return;
                }

                var np = p.zeepkistNetworkPlayer;

                if (np == null)
                {
                    Debug.LogError($"[Animator] NetworkPlayer is NULL for '{p.username}'");
                    dynamicLookAtTransform = null;
                    return;
                }

                // remote player → ghost model
                if (!p.isLocalPlayer)
                {
                    dynamicLookAtTransform = np.Zeepkist?.ghostModel?.hatParent.transform;

                    if (dynamicLookAtTransform == null)
                        Debug.LogError($"[Animator] ghostModel is NULL for remote player '{p.username}'");

                    return;
                }

                // local player → ReadyToReset
                var local = GameObject.FindObjectOfType<ReadyToReset>(true);

                if (local != null)
                    dynamicLookAtTransform = local.transform;
                else
                    Debug.LogError("[Animator] ReadyToReset not found for local player");
            }
            else
            {
                dynamicLookAtTransform = null;
            }


            /*// Resolve dynamic look-at target
            if (!string.IsNullOrEmpty(frame.lookAtPlayerName))
            {
                var p = DroneCommand.players
                    .FirstOrDefault(pl => pl.username.Equals(frame.lookAtPlayerName, System.StringComparison.OrdinalIgnoreCase));

                if (p != null && p.zeepkistNetworkPlayer != null)
                    dynamicLookAtTransform = p.zeepkistNetworkPlayer?.Zeepkist?.gameObject.transform;
                else
                    dynamicLookAtTransform = null;
            }
            else
            {
                dynamicLookAtTransform = null;
            }*/
        }

        private List<Frame> Parse(string script)
        {
            try
            {
                var parsed = new List<Frame>();

                Vector3 pos = Vector3.zero;
                Vector3 rot = Vector3.zero;
                bool abs = false;
                float time = 0f;
                bool local = false;
                float? fov = null;
                bool setProj = false;
                bool useOrtho = false;
                Vector3 lookTarget = Vector3.zero;
                bool useLook = false;
                bool smoothLook = false;
                string lookAtPlayerName = null;

                loop = false;

                foreach (var line in script.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)))
                {
                    var parts = line.Split(' ');
                    var cmd = parts[0].ToLower();
                    var args = parts.Length > 1
                    ? parts.Skip(1)
                        .Select(s => float.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var v) ? v : 0f)
                        .ToArray()
                    : Array.Empty<float>();

                    switch (cmd)
                    {
                        case "setposition":
                            pos = new Vector3(args[0], args[1], args[2]);
                            abs = true;
                            break;
                        case "setrotation":
                            rot = new Vector3(args[0], args[1], args[2]);
                            abs = true;
                            break;
                        case "move":
                            pos = new Vector3(args[0], args[1], args[2]);
                            abs = false;
                            break;
                        case "rotate":
                            rot = new Vector3(args[0], args[1], args[2]);
                            abs = false;
                            break;
                        case "lmove":
                            pos = new Vector3(args[0], args[1], args[2]);
                            abs = false;
                            local = true;
                            break;
                        case "fov":
                            fov = args[0];
                            break;
                        case "setfov":
                            fov = args[0];
                            time = 0f;
                            break;
                        case "ortho":
                            setProj = true;
                            useOrtho = true;
                            break;
                        case "persp":
                            setProj = true;
                            useOrtho = false;
                            break;
                        case "loop":
                            loop = true;
                            break;                       
                        case "lookat":
                            lookTarget = new Vector3(args[0], args[1], args[2]);
                            useLook = true;
                            smoothLook = false;
                            break;
                        case "smoothlookat":
                            lookTarget = new Vector3(args[0], args[1], args[2]);
                            useLook = true;
                            smoothLook = true;
                            break;
                        case "lookatplayer":                            
                            useLook = true;
                            smoothLook = false;

                            // Rebuild the full name after the command
                            lookAtPlayerName = line.Substring(cmd.Length).Trim();

                            Debug.Log($"[Parser] lookatplayer: captured name '{lookAtPlayerName}'");

                            lookTarget = Vector3.zero;
                            break;
                        case "smoothlookatplayer":
                            useLook = true;
                            smoothLook = true;

                            lookAtPlayerName = line.Substring(cmd.Length).Trim();

                            Debug.Log($"[Parser] smoothlookatplayer: captured name '{lookAtPlayerName}'");

                            lookTarget = Vector3.zero;
                            break;

                        case "clearlookat":
                            useLook = false;
                            smoothLook = false;
                            lookAtPlayerName = null;
                            lookTarget = Vector3.zero;
                            break;
                        case "time":
                            time = args[0];
                            parsed.Add(new Frame
                            {
                                position = pos,
                                rotation = rot,
                                time = time,
                                isAbsolute = abs,
                                useLocalSpace = local,
                                fov = fov,
                                setProjection = setProj,
                                ortho = useOrtho,
                                useLookAt = useLook,
                                lookAtTarget = lookTarget,
                                smoothLookAt = smoothLook,
                                lookAtPlayerName = lookAtPlayerName,
                                cinematicMode = cinMode,
                                followMode = folMode
                            });

                            //Reset after frame
                            pos = Vector3.zero;
                            rot = Vector3.zero;
                            time = 0f;
                            abs = false;
                            local = false;
                            fov = null;
                            setProj = false;
                            useOrtho = false;
                            lookTarget = Vector3.zero;
                            useLook = false;
                            smoothLook = false;
                            lookAtPlayerName = null;
                            cinMode = true;
                            folMode = FollowMode.Smooth;
                            break;
                        case "followmode":
                            string rawMode = line.Substring(cmd.Length).Trim();
                            Debug.Log($"[Parser] followmode  '{rawMode}'");
                            if (!System.Enum.TryParse(rawMode, true, out FollowMode parsedMode))
                                parsedMode = FollowMode.Smooth;
                            cinMode = false;
                            folMode = parsedMode;
                            break;
                    }
                }

                return parsed;
            }
            catch
            {
                return new List<Frame>();
            }
        }
    }
}
