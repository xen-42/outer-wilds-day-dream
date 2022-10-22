using DayDream.API;
using DayDream.Atmosphere;
using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using OWML.Utils;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DayDream;

class DayDream : ModBehaviour
{
    public static DayDream SharedInstance { get; private set; }

    public static bool SeeSun { get; private set; }
    public static bool ShowAtmosphere { get; private set; }
    public static float SunAngle { get; private set; }
    public static bool SeeSolarSystem { get; private set; }
    public static bool DreamAtAnyFire { get; private set; }
    public static float SunIntensity { get; private set; }
    public static float ShadowIntensity { get; private set; }

    public static float AmbientLightIntensity { get; private set; } = 1.0f;

    public Campfire CampfireSleptAt;
    public RelativeLocationData CampfireRelativeLocation;

    private float[] _previousCullLayerDistances = null;
    private bool _sceneLoaded = false;
    private Quaternion _baseRotation;
    private Vector3 _rotationAxis;

    private bool _initNextTick = false;
    private float _ticksUntilTeleport;
    private float _defaultCameraFarPlaneDist;

    private Light ambientLight;

    private GameObject[] _atmos;

    public readonly List<OWCamera> Cameras = new();

    public override object GetApi() => new DayDreamAPI();

	public override void Configure(IModConfig config)
    {
        base.Configure(config);

        var newSunIntensity = config.GetSettingsValue<float>("Sun intensity");
        var flag1 = newSunIntensity != SunIntensity;
        SunIntensity = newSunIntensity;

        var newShadowIntensity = config.GetSettingsValue<float>("Shadow intensity");
        flag1 |= newShadowIntensity != ShadowIntensity;
        ShadowIntensity = newShadowIntensity;

        var newAmbientLightIntensity = config.GetSettingsValue<float>("Ambient light intensity");
        var flag2 = newAmbientLightIntensity != AmbientLightIntensity;
        AmbientLightIntensity = newAmbientLightIntensity;

        var newSunAngle = config.GetSettingsValue<float>("Sun angle");
        var flag3 = newSunAngle != SunAngle;
        SunAngle = newSunAngle;

        var newSeeSun = config.GetSettingsValue<bool>("See sun");
        var flag4 = newSeeSun != SeeSun;
        SeeSun = newSeeSun;

        var newSeeSolarSystem = config.GetSettingsValue<bool>("See solar system");
        flag4 |= (newSeeSolarSystem != SeeSolarSystem);
        SeeSolarSystem = newSeeSolarSystem;

        DreamAtAnyFire = config.GetSettingsValue<bool>("Dream at any fire");

        var newShowAtmosphere = config.GetSettingsValue<bool>("Show atmosphere");
        var flagAtmo = newShowAtmosphere != ShowAtmosphere;
        ShowAtmosphere = newShowAtmosphere;

        if (_sceneLoaded)
        {
            WriteInfo("Settings changed");
            if (flag1 && Locator.GetDreamWorldController().IsInDream()) Locator.GetDreamWorldController().ApplySunOverrides(Locator.GetPlayerCamera(), default);
            if (flag2 && ambientLight != null) ambientLight.intensity = AmbientLightIntensity;
            if (flag3) SetDreamWorldAngle();
            if (flag4) ResetSolarSystemVisibility();
            if (flagAtmo)
            {
                // If its now on just try and turn all atmos on
                if (ShowAtmosphere && _atmos != null)
                {
                    foreach (var atmo in _atmos)
                    {
                        atmo.SetActive(true);
                    }
                }
            }
        }
    }

    private void Start()
    {
        SharedInstance = this;

		Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

		GlobalMessenger.AddListener("EnterDreamWorld", OnEnterDreamWorld);
        GlobalMessenger.AddListener("ExitDreamWorld", OnExitDreamWorld);

        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    void OnDestroy()
    {
		GlobalMessenger.RemoveListener("EnterDreamWorld", OnEnterDreamWorld);
		GlobalMessenger.RemoveListener("ExitDreamWorld", OnExitDreamWorld);

		SceneManager.sceneLoaded -= OnSceneLoaded;
		SceneManager.sceneUnloaded -= OnSceneUnloaded;
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

        _initNextTick = true;
    }

    void OnSceneUnloaded(Scene scene)
    {
        Cameras.Clear();
    }

	private void SetDreamWorldAngle()
    {
        WriteInfo($"Setting sun angle to {SunAngle}");

        var dreamWorld = Locator.GetAstroObject(AstroObject.Name.DreamWorld);
        var rotation = _baseRotation * Quaternion.AngleAxis(SunAngle - 90, _rotationAxis);

        var flag = Locator.GetDreamWorldController().IsInDream();
        var oldParent = flag ? Locator.GetPlayerBody().transform.parent : null;
        var rafts = GameObject.FindObjectsOfType<DreamRaftController>();
        var raftParents = rafts != null ? new Transform[rafts.Length] : new Transform[0];
        var sealRafts = GameObject.FindObjectsOfType<SealRaftController>();
        var sealRaftParents = sealRafts != null ? new Transform[sealRafts.Length] : new Transform[0];

        WriteInfo($"Moving {rafts.Length} DreamRaft(s) and {sealRafts.Length} SealRaft(s)");

        if (flag) Locator.GetPlayerBody().transform.SetParent(dreamWorld.transform);
        for (int i = 0; i < rafts.Length; i++)
        {
            raftParents[i] = rafts[i].transform.parent;
            rafts[i].transform.SetParent(dreamWorld.transform);
        }
        for (int i = 0; i < sealRafts.Length; i++)
        {
            sealRaftParents[i] = sealRafts[i].transform.parent;
            sealRafts[i].transform.SetParent(dreamWorld.transform);
        }

        dreamWorld.GetOWRigidbody().SetRotation(rotation);

        if (flag) Locator.GetPlayerBody().transform.SetParent(oldParent);
        for (int i = 0; i < rafts.Length; i++)
        {
            rafts[i].transform.SetParent(raftParents[i]);
        }
        for (int i = 0; i < sealRafts.Length; i++)
        {
            sealRafts[i].transform.SetParent(sealRaftParents[i]);
        }

        float freqPerLoop = 0.1f; //Rotation per loop
        dreamWorld.GetOWRigidbody().SetAngularVelocity(dreamWorld.transform.TransformDirection(Vector3.left) * freqPerLoop * 2f * Mathf.PI / 1320f);
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
                    // We reset the farClipPlane but put its value into layerCullDistances except for the sun layer.
                    _previousCullLayerDistances = camera.mainCamera.layerCullDistances;
                    var distances = new float[32];
                    for (int i = 0; i < 32; i++)
                    {
                        distances[i] = (i == LayerMask.NameToLayer("Sun")) ? 0 : 2000f;
                    }
                    camera.mainCamera.layerCullDistances = distances;
                    camera.farClipPlane = Locator.GetDreamWorldController().GetValue<float>("_prevPlayerCameraFarPlaneDist");
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
                camera.farClipPlane = _defaultCameraFarPlaneDist;
                camera.mainCamera.cullingMask |= 1 << LayerMask.NameToLayer("Sun");
            }
        }
    }

    private void OnEnterDreamWorld()
    {
        ambientLight.enabled = true;
        SetCameraSettings(true);
    }

    private void OnExitDreamWorld()
    {
        ambientLight.enabled = false;
        SetCameraSettings(false);

        if (CampfireSleptAt != null) _ticksUntilTeleport = 2;
    }

    private void Update()
    {
        if (_initNextTick)
        {
            _initNextTick = false;
            Init();
        }

        if (_ticksUntilTeleport >= 0) _ticksUntilTeleport--;

        if (_ticksUntilTeleport == 0)
        {
            TeleportToCampfire();
        }
    }

    private void Init()
    {
        Cameras.Add(Locator.GetPlayerCamera());
        _defaultCameraFarPlaneDist = Locator.GetPlayerCamera().farClipPlane;

        GameObject ambientLightObj = new GameObject();
        ambientLightObj.transform.SetParent(Locator.GetPlayerTransform());
        ambientLightObj.transform.localPosition = Vector3.zero;
        ambientLight = ambientLightObj.AddComponent<Light>();
        ambientLight.renderingLayerMask = Locator.GetSunController().GetValue<Light>("_ambientLight").renderingLayerMask;
        ambientLight.renderMode = Locator.GetSunController().GetValue<Light>("_ambientLight").renderMode;
        ambientLight.shadows = LightShadows.None;
        ambientLight.color = Color.white;
        ambientLight.range = 500f;
        ambientLight.intensity = AmbientLightIntensity;
        ambientLight.enabled = false;

        var sector1 = GameObject.Find("DreamWorld_Body/Sector_DreamWorld/Sector_DreamZone_1").GetComponent<Sector>();
        var sector2 = GameObject.Find("DreamWorld_Body/Sector_DreamWorld/Sector_DreamZone_2").GetComponent<Sector>();
        var sector3 = GameObject.Find("DreamWorld_Body/Sector_DreamWorld/Sector_DreamZone_3").GetComponent<Sector>();
        var sector4 = GameObject.Find("DreamWorld_Body/Sector_DreamWorld/Sector_DreamZone_4").GetComponent<Sector>();

        _atmos = new GameObject[]
        {
            AtmosphereBuilder.Make(sector1.gameObject, sector1),
            AtmosphereBuilder.Make(sector2.gameObject, sector2),
            AtmosphereBuilder.Make(sector3.gameObject, sector3),
            AtmosphereBuilder.Make(sector4.gameObject, sector4)
        };
    }

    private void TeleportToCampfire()
    {
        // Teleport back to the campfire
        var playerObj = Locator.GetPlayerBody().GetAttachedOWRigidbody();
        var planetBody = CampfireSleptAt.GetAttachedOWRigidbody().GetReferenceFrame().GetOWRigidBody();

        WriteInfo($"Teleporting to {planetBody.name}");

        var newWorldPos = CampfireSleptAt.transform.TransformPoint(CampfireRelativeLocation.localPosition);
        var forwards = (CampfireSleptAt.transform.position - newWorldPos).normalized;
        var upwards = (newWorldPos - planetBody.transform.position).normalized;
        var newWorldRot = Quaternion.LookRotation(forwards, upwards);

        playerObj.WarpToPositionRotation(newWorldPos, newWorldRot);
        playerObj.SetVelocity(planetBody.GetPointVelocity(newWorldPos));

        CampfireSleptAt = null;
    }

    public static void WriteInfo(string msg)
    {
        SharedInstance.ModHelper.Console.WriteLine(msg, MessageType.Info);
    }
}
