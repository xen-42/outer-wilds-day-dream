using OWML.Common;
using OWML.ModHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Reflection;

namespace DayDream
{
    public class Patches
    {
        public static bool ApplySunOverrides(SunLightController.SunOverrideSettings __1, ref SunLightController.SunOverrideSettings __result, bool ____insideDream)
		{ 
			if(____insideDream)
            {
				__1.sunShadowStrength = DayDream.ShadowIntensity;
				__1.sunIntensity = DayDream.SunIntensity;
				__1.ambientIntensity = 0f; //Doesn't do anything it seems
				__result = __1;
				return false;
			}

            return true;
        }

		public static bool OnStopSleeping(Campfire __instance)
		{
			if (DayDream.DreamAtAnyFire)
			{
				RelativeLocationData relativeLocation = new RelativeLocationData(Locator.GetPlayerBody(), __instance.GetComponentInParent<OWRigidbody>(), __instance.transform);

				DreamArrivalPoint.Location location = DreamArrivalPoint.Location.Zone2;
				DreamArrivalPoint arrivalPoint = Locator.GetDreamArrivalPoint(location);
				DreamCampfire dreamCampfire = Locator.GetDreamCampfire(location);
				if (Locator.GetToolModeSwapper().GetItemCarryTool().GetHeldItemType() != ItemType.DreamLantern)
					Locator.GetToolModeSwapper().GetItemCarryTool().PickUpItemInstantly(GameObject.FindObjectsOfType<DreamLanternItem>()[3]);
				Locator.GetDreamWorldController().EnterDreamWorld(dreamCampfire, arrivalPoint, relativeLocation);

				DayDream.SharedInstance.CampfireSleptAt = __instance;
				DayDream.SharedInstance.CampfireRelativeLocation = relativeLocation;

				return false;
			}
			return true;
		}

		/*
		public static bool DreamWorldControllerFixedUpdate(DreamWorldController __instance, ref bool ____exitingDream, ref bool ____closingEyes, float ____exitDreamTime, 
			GhostGrabController ____activeGhostGrabController, bool ____outsideLanternBounds, SimulationCamera ____simulationCamera, ProxyShadowLight ____proxyShadowLight,
			ref bool ____insideDream, ref bool ____waitingToLightLantern, Sector ____dreamWorldSector, DreamLanternSocket ____playerLanternSocket, DreamLanternItem ____playerLantern,
			DreamArrivalPoint ____dreamArrivalPoint, ref DreamCampfire ____dreamCampfire, bool ____suitUpOnWake, DreamWakeType ____wakeType, ref float ____prevPlayerCameraFarPlaneDist,
			float ____cachedCamDegreesY, PlayerCameraEffectController ____playerCamEffectController, HeightmapAmbientLightRenderer ____playerCamAmbientLightRenderer)
        {
            if (DayDream.SharedInstance.CampfireSleptAt == null || !(____exitingDream && ____closingEyes && Time.time > ____exitDreamTime)) return true;

			var EnterLanternBounds = typeof(DreamWorldController).GetMethod("EnterLanternBounds", BindingFlags.NonPublic | BindingFlags.Instance);
			var OnDreamCampfireExtinguished = typeof(DreamWorldController).GetMethod("OnDreamCampfireExtinguished", BindingFlags.NonPublic | BindingFlags.Instance);

			// So that we do the -= on the event thing (calling the function does that)
			OnDreamCampfireExtinguished.Invoke(__instance, new object[] { });

			Locator.GetPlayerCameraDetector().GetComponent<AudioDetector>().DeactivateAllVolumes(0f);
			if (____activeGhostGrabController != null)
			{
				____activeGhostGrabController.ReleasePlayer();
			}
			if (____outsideLanternBounds)
			{
				EnterLanternBounds.Invoke(__instance, new object[] { });
			}

			____simulationCamera.OnExitDreamWorld();
			SunLightController.UnregisterSunOverrider(__instance);
			if (____proxyShadowLight != null)
			{
				____proxyShadowLight.enabled = true;
			}
			____exitingDream = false;
			____closingEyes = false;
			____insideDream = false;
			____waitingToLightLantern = false;
			if (Locator.GetToolModeSwapper().GetItemCarryTool().GetHeldItemType() != ItemType.DreamLantern)
			{
				Locator.GetToolModeSwapper().GetItemCarryTool().DropItemInstantly(____dreamWorldSector, __instance.transform);
				if (____playerLanternSocket != null)
				{
					Locator.GetToolModeSwapper().GetItemCarryTool().UnsocketItemInstantly(____playerLanternSocket);
				}
				else
				{
					Locator.GetToolModeSwapper().GetItemCarryTool().PickUpItemInstantly(____playerLantern);
				}
			}

			____playerLantern.OnExitDreamWorld();

			var campfire = DayDream.SharedInstance.CampfireSleptAt;
			var relativeSleepLocation = DayDream.SharedInstance.CampfireRelativeLocation;
			var planetBody = campfire.GetAttachedOWRigidbody().GetReferenceFrame().GetAstroObject().GetOWRigidbody();
			Locator.GetPlayerBody().MoveToRelativeLocation(relativeSleepLocation, planetBody, campfire.transform);
			//Locator.GetPlayerBody().SetAngularVelocity(planetBody.GetAngularVelocity());

			var forward = campfire.transform.position - Locator.GetPlayerBody().transform.position;
			var up = Locator.GetPlayerBody().transform.position - planetBody.transform.position;
			Locator.GetPlayerBody().SetRotation(Quaternion.LookRotation(forward, up));
			GlobalMessenger.FireEvent("WarpPlayer");
			if (!Physics.autoSyncTransforms)
			{
				Physics.SyncTransforms();
			}

			Locator.GetPlayerCameraController().SetDegreesY(____cachedCamDegreesY);
			PlayerSectorDetector playerSectorDetector2 = Locator.GetPlayerSectorDetector();
			playerSectorDetector2.RemoveFromAllSectors();
			Sector sector2 = campfire.GetSector();
			while (sector2 != null)
			{
				sector2.GetTriggerVolume().AddObjectToVolume(playerSectorDetector2.gameObject);
				sector2 = sector2.GetParentSector();
			}
			if (Locator.GetRingWorldController() != null)
			{
				Locator.GetRingWorldController().OnExitDreamWorld();
			}

			//____dreamArrivalPoint.OnExitDreamWorld();
			//____dreamCampfire.OnExitDreamWorld();
			____dreamCampfire = null;
			Locator.GetPlayerDetector().GetComponent<ForceApplier>().SkipNextFrame();
			Locator.GetPlayerBody().GetComponent<AlignPlayerWithForce>().SkipNextFrame();

			__instance.ExtinguishDreamRaft();
			Locator.GetAudioMixer().UnmixDreamWorld();
			Locator.GetAudioMixer().UnmixSleepAtCampfire(1f);
			
			if (____suitUpOnWake)
			{
				Locator.GetPlayerSuit().SuitUp(false, true, false);
				Locator.GetPlayerSuit().PutOnHelmetAfterDelay(2f);
			}

			____playerCamEffectController.OpenEyes(0.5f, false);
			ReticleController.Show();
			Locator.GetPromptManager().SetPromptsVisible(true);
			if (____playerCamAmbientLightRenderer != null)
			{
				____playerCamAmbientLightRenderer.enabled = false;
			}

			var _playerCamera = Locator.GetPlayerCamera();
			_playerCamera.cullingMask |= 1 << LayerMask.NameToLayer("Sun");
			_playerCamera.farClipPlane = ____prevPlayerCameraFarPlaneDist;
			____prevPlayerCameraFarPlaneDist = 0f;
			_playerCamera.mainCamera.backgroundColor = Color.black;
			_playerCamera.postProcessingSettings.screenSpaceReflectionAvailable = false;
			_playerCamera.postProcessingSettings.ambientOcclusionAvailable = true;
			if (____wakeType != DreamWakeType.CampfireExtinguished && ____wakeType != DreamWakeType.Asphyxiation)
			{
				Locator.GetPlayerAudioController().OnExitDreamWorld(global::AudioType.PlayerGasp_Medium);
			}

			GlobalMessenger.FireEvent("ExitDreamWorld");

			DayDream.SharedInstance.CampfireSleptAt = null;

			return false;
		}
		*/

		public static void StartFollowPath(PartyPathAction __instance, GhostController ____controller)
        {
			// The ghost rotation is hardcoded only for ghosts going to the house party, this is the first function I could find to hook onto and fix it
			var up = Locator.GetAstroObject(AstroObject.Name.DreamWorld).GetOWRigidbody().transform.TransformDirection(Vector3.up).normalized;
			____controller.transform.rotation = Quaternion.FromToRotation(____controller.transform.up, up);
		}
	}
}
