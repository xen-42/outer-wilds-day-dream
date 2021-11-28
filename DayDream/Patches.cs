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
			if (____insideDream)
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

		public static bool SealRaftFixedUpdate(SealRaftController __instance, AlignToSurfaceFluidDetector ____fluidDetector, Vector3 ____origDrag, Transform ____farNode, Transform ____nearNode,
			OWRigidbody ____raftBody, LightSensor ____nearSensor, LightSensor ____farSensor, ref Transform ____autoTargetNode, ref Transform ____anchorNode, float ____minSpeed, float ____maxSpeed, 
			OWAudioSource ____audioSource)
		{
			if (Locator.GetDreamWorldController()?.GetAttachedOWRigidbody() == null) return true;
			
			Vector3 normalVector = Locator.GetDreamWorldController().GetAttachedOWRigidbody().transform.TransformDirection(Vector3.up).normalized;

			____fluidDetector.SetDragFactor(____origDrag);
			Vector3 toDirection = ____farNode.position - ____nearNode.position;
			Vector3 vector = OWPhysics.FromToAngularVelocity(Vector3.ProjectOnPlane(__instance.transform.forward, normalVector), toDirection);
			____raftBody.AddAngularVelocityChange(vector.normalized * Time.deltaTime * 0.1f);
			int num = 0;
			if (____nearSensor.IsIlluminated())
			{
				num--;
			}
			if (____farSensor.IsIlluminated())
			{
				num++;
			}
			float num2 = 3f;
			float num3 = 5f;
			float b = 10f;
			if (____autoTargetNode != null)
			{
				// Original method hardcodes that the surface normal is in the y direction. Bad.
				Vector3 vector2 = ____autoTargetNode.position - __instance.transform.position;

				// Instead of setting y to 0, subtract normal component to surface
				float normalComponent2 = Vector3.Dot(vector2, normalVector);
				vector2 -= normalComponent2 * normalVector;

				float magnitude = vector2.magnitude;
				float d = Mathf.Min(magnitude, num3);
				____raftBody.AddAcceleration(vector2.normalized * d);
				if (magnitude < num2)
				{
					____anchorNode = ____autoTargetNode;
					____autoTargetNode = null;
				}
			}
			else if (num != 0)
			{
				Vector3 vector3 = ((num > 0) ? ____farNode : ____nearNode).position - __instance.transform.position;

				// Instead of setting y to 0, subtract normal component to surface
				float normalComponent3 = Vector3.Dot(vector3, normalVector);
				vector3 -= normalComponent3 * normalVector;

				____raftBody.AddAcceleration(vector3.normalized * num3);
			}
			else if (____anchorNode != null)
			{
				____fluidDetector.SetDragFactor(new Vector3(5f, ____origDrag.y, 5f));
				Vector3 vector4 = ____anchorNode.position - __instance.transform.position;

				// Instead of setting y to 0, subtract normal component to surface
				float normalComponent4 = Vector3.Dot(vector4, normalVector);
				vector4 -= normalComponent4 * normalVector;

				float magnitude2 = vector4.magnitude;
				if (magnitude2 > num2)
				{
					____anchorNode = null;
				}
				else
				{
					float d2 = Mathf.Min(magnitude2, b);
					____raftBody.AddAcceleration(vector4.normalized * d2);
				}
			}
			else
			{
				Vector3 vector5 = ____nearNode.position - __instance.transform.position;

				// Instead of setting y to 0, subtract normal component to surface
				float normalComponent5 = Vector3.Dot(vector5, normalVector);
				vector5 -= normalComponent5 * normalVector;

				Vector3 vector6 = ____farNode.position - __instance.transform.position;

				// Instead of setting y to 0, subtract normal component to surface
				float normalComponent6 = Vector3.Dot(vector6, normalVector);
				vector6 -= normalComponent6 * normalVector;

				if (vector5.sqrMagnitude < num2 * num2)
				{
					____anchorNode = ____nearNode;
				}
				else if (vector6.sqrMagnitude < num2 * num2)
				{
					____anchorNode = ____farNode;
				}
			}
			float magnitude3 = (____raftBody.GetVelocity() - ____raftBody.GetOrigParentBody().GetVelocity()).magnitude;
			float num4 = Mathf.InverseLerp(____minSpeed, ____maxSpeed, magnitude3);
			____audioSource.SetLocalVolume(num4);
			bool flag = num4 > 0f;
			if (!____audioSource.isPlaying && flag)
			{
				____audioSource.Play();
				return false;
			}
			if (____audioSource.isPlaying && !flag)
			{
				____audioSource.Stop();
			}
			return false;
		}
	}
}
