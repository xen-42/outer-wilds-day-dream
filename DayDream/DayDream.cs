using OWML.ModHelper;
using OWML.Common;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using OWML.Utils;
using System.Reflection;

namespace DayDream
{
    class DayDream : ModBehaviour
    {
        public static DayDream SharedInstance { get; private set; }

        public static bool SeeSun { get; private set; }
        public static float SunAngle { get; private set; }
        public static bool SeeSolarSystem { get; private set; }
        public static bool DreamAtAnyFire { get; private set; }
        public static float SunIntensity { get; private set; }
        public static float ShadowIntensity { get; private set; }

        public Campfire CampfireSleptAt;
        public RelativeLocationData CampfireRelativeLocation;

        private float[] _previousCullLayerDistances = null;
        private bool _sceneLoaded = false;
        private Quaternion _baseRotation;
        private Vector3 _rotationAxis;

        private bool _initNextTick = false;
        //private bool _teleportNextTick = false;

        public List<OWCamera> Cameras { get; private set; }

        public override void Configure(IModConfig config)
        {
            base.Configure(config);

            var newSunIntensity = config.GetSettingsValue<float>("Sun intensity");
            var flag = newSunIntensity != SunIntensity;
            SunIntensity = newSunIntensity;

            var newShadowIntensity = config.GetSettingsValue<float>("Shadow intensity");
            flag |= newShadowIntensity != ShadowIntensity;
            ShadowIntensity = newShadowIntensity;

            var newSunAngle = config.GetSettingsValue<float>("Sun angle");
            var flag1 = newSunAngle != SunAngle;
            SunAngle = newSunAngle;

            var newSeeSun = config.GetSettingsValue<bool>("See sun");
            var flag2 = newSeeSun != SeeSun;
            SeeSun = newSeeSun;

            var newSeeSolarSystem = config.GetSettingsValue<bool>("See solar system");
            flag2 |= (newSeeSolarSystem != SeeSolarSystem);
            SeeSolarSystem = newSeeSolarSystem;

            DreamAtAnyFire = config.GetSettingsValue<bool>("Dream at any fire");

            if(_sceneLoaded)
            {
                WriteInfo("Settings changed");
                if(flag && Locator.GetDreamWorldController().IsInDream())
                {
                    Locator.GetDreamWorldController().ApplySunOverrides(Locator.GetPlayerCamera(), default);
                }
                if (flag1)
                {
                    SetDreamWorldAngle();
                }
                if (flag2) ResetSolarSystemVisibility();
            }
        }

        private void Start()
        {
            SharedInstance = this;

            ModHelper.HarmonyHelper.AddPrefix<DreamWorldController>("ApplySunOverrides", typeof(Patches), nameof(Patches.ApplySunOverrides));
            //ModHelper.HarmonyHelper.AddPrefix<DreamWorldController>("FixedUpdate", typeof(Patches), nameof(Patches.DreamWorldControllerFixedUpdate));
            ModHelper.HarmonyHelper.AddPrefix<Campfire>("OnStopSleeping", typeof(Patches), nameof(Patches.OnStopSleeping));
            ModHelper.HarmonyHelper.AddPrefix<PartyPathAction>("StartFollowPath", typeof(Patches), nameof(Patches.StartFollowPath));

            GlobalMessenger.AddListener("EnterDreamWorld", OnEnterDreamWorld);
            GlobalMessenger.AddListener("ExitDreamWorld", OnExitDreamWorld);

            ModHelper.Console.WriteLine($"Mod {nameof(DayDream)} is loaded!", MessageType.Success);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "SolarSystem")
            {
                _sceneLoaded = false;
                return;
            }

            _sceneLoaded = true;

            // We want to save this for later to keep the rotation consistent.
            var dreamWorld = Locator.GetAstroObject(AstroObject.Name.DreamWorld);
            var sunPosition = Locator.GetAstroObject(AstroObject.Name.Sun).transform.position;
            Vector3 toSun = dreamWorld.transform.position - sunPosition;
            _baseRotation = Quaternion.LookRotation(Vector3.forward, -toSun);
            _rotationAxis = dreamWorld.GetOWRigidbody().transform.TransformDirection(Vector3.right);

            SetDreamWorldAngle();

            // Compatibility with camera mods, they just have to add their cameras to this
            Cameras = new List<OWCamera>();

            _initNextTick = true;
        }

        private void SetDreamWorldAngle()
        {
            WriteInfo($"Setting sun angle to {SunAngle}");

            var dreamWorld = Locator.GetAstroObject(AstroObject.Name.DreamWorld);
            var rotation = _baseRotation * Quaternion.AngleAxis(SunAngle - 90, _rotationAxis);

            if(Locator.GetDreamWorldController().IsInDream())
            {
                var oldParent = Locator.GetPlayerBody().transform.parent;
                Locator.GetPlayerBody().transform.SetParent(dreamWorld.transform);
                dreamWorld.GetOWRigidbody().SetRotation(rotation);
                Locator.GetPlayerBody().transform.SetParent(oldParent);
            }
            else
            {
                dreamWorld.GetOWRigidbody().SetRotation(rotation);
            }

            float freqPerLoop = 0.1f; //Rotation per loop
            dreamWorld.GetOWRigidbody().SetAngularVelocity(dreamWorld.transform.TransformDirection(Vector3.left) * freqPerLoop * 2f*Mathf.PI / 1320f);
        }

        private void ResetSolarSystemVisibility()
        {
            if (Locator.GetDreamWorldController().IsInDream())
            {
                //Reset camera settings
                SetCameraSettings(false);
                SetCameraSettings(true);
            }
        }

        private void SetCameraSettings(bool entering)
        {
            foreach (var camera in Cameras)
            {
                if (entering)
                {
                    if (SeeSun)
                    {
                        // Make it so the sun layer is visible to the camera again
                        camera.mainCamera.cullingMask |= 1 << LayerMask.NameToLayer("Sun");
                    }
                    else
                    {
                        camera.mainCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Sun"));
                    }

                    if (SeeSolarSystem)
                    {
                        camera.mainCamera.layerCullDistances = new float[32];
                        camera.farClipPlane = Locator.GetDreamWorldController().GetValue<float>("_prevPlayerCameraFarPlaneDist");
                    }
                    else
                    {
                        camera.farClipPlane = 2000f;
                    }

                    if (SeeSun && !SeeSolarSystem)
                    {
                        // We reset the farClipPlane but put its value into layerCullDistances except for the sun layer.
                        _previousCullLayerDistances = camera.mainCamera.layerCullDistances;
                        var distances = new float[32];
                        for (int i = 0; i < 32; i++)
                        {
                            distances[i] = (i == LayerMask.NameToLayer("Sun")) ? 0 : 2000f;
                        }
                        camera.mainCamera.layerCullDistances = distances;
                        camera.mainCamera.farClipPlane = Locator.GetDreamWorldController().GetValue<float>("_prevPlayerCameraFarPlaneDist");
                    }
                }
                else
                {
                    // Make sure the layerCullDistances are back to normal
                    if (_previousCullLayerDistances != null)
                    {
                        camera.mainCamera.layerCullDistances = _previousCullLayerDistances;
                        _previousCullLayerDistances = null;
                    }
                    camera.mainCamera.farClipPlane = Locator.GetDreamWorldController().GetValue<float>("_prevPlayerCameraFarPlaneDist");
                }
            }
        }

        private void OnEnterDreamWorld()
        {
            SetCameraSettings(true);
        }

        private void OnExitDreamWorld()
        {
            SetCameraSettings(false);

            //if (CampfireSleptAt != null) _teleportNextTick = true;
        }

        private void Update()
        {
            if(_initNextTick)
            {
                _initNextTick = false;
                Cameras.Add(Locator.GetPlayerCamera());
            }

            /*
            if(_teleportNextTick)
            {
                // Teleport back to the campfire
                var playerObj = Locator.GetPlayerBody().GetAttachedOWRigidbody();
                var planetBody = CampfireSleptAt.GetAttachedOWRigidbody().GetReferenceFrame().GetOWRigidBody();

                WriteInfo($"Teleporting to {planetBody.name}");

                // Set new position
                var relativePosition = CampfireSleptAt.transform.localPosition;//.TransformPoint(CampfireRelativeLocation.localPosition)on;
                var position = planetBody.transform.TransformPoint(relativePosition);
                playerObj.SetPosition(new Vector3(position.x, position.y, position.z));
                playerObj.SetValue("_lastPosition", new Vector3(position.x, position.y, position.z));

                var velocity = planetBody.GetPointTangentialVelocity(position) + planetBody.GetVelocity();
                playerObj.SetVelocity(new Vector3(velocity.x, velocity.y, velocity.z));
                playerObj.SetValue("_currentVelocity", new Vector3(velocity.x, velocity.y, velocity.z));
                playerObj.SetValue("_lastVelocity", new Vector3(velocity.x, velocity.y, velocity.z));

                var acceleration = planetBody.GetAcceleration();
                playerObj.SetValue("_currentAccel", new Vector3(acceleration.x, acceleration.y, acceleration.z));
                playerObj.SetValue("_lastAccel", new Vector3(acceleration.x, acceleration.y, acceleration.z));

                var parentRotation = planetBody.GetRotation();
                var rotation = parentRotation;
                playerObj.SetRotation(new Quaternion(rotation.x, rotation.y, rotation.z, rotation.w));

                var angularVelocity = planetBody.GetAngularVelocity();
                playerObj.SetAngularVelocity(new Vector3(angularVelocity.x, angularVelocity.y, angularVelocity.z));
                playerObj.SetValue("_currentAngularVelocity", new Vector3(angularVelocity.x, angularVelocity.y, angularVelocity.z));
                playerObj.SetValue("_lastAngularVelocity", new Vector3(angularVelocity.x, angularVelocity.y, angularVelocity.z));
                CampfireSleptAt = null;

                if (!Physics.autoSyncTransforms)
                {
                    Physics.SyncTransforms();
                }
            }
            */
        }

        public static void WriteInfo(string msg)
        {
            SharedInstance.ModHelper.Console.WriteLine(msg, MessageType.Info);
        }
    }
}
