using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DayDream;

[HarmonyPatch]
public class Patches
{
	private static DreamLanternItem _safeLantern = null;
	private static readonly DreamArrivalPoint.Location[] _validLocations = new DreamArrivalPoint.Location[]
	{
		DreamArrivalPoint.Location.Zone1,
		DreamArrivalPoint.Location.Zone2,
		DreamArrivalPoint.Location.Zone3,
		DreamArrivalPoint.Location.Zone4
	};

	[HarmonyPrefix]
	[HarmonyPatch(typeof(DreamWorldController), nameof(DreamWorldController.ApplySunOverrides))]
	public static bool DreamWorldController_ApplySunOverrides(SunLightController.SunOverrideSettings __1, ref SunLightController.SunOverrideSettings __result, bool ____insideDream)
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

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Campfire), nameof(Campfire.OnStopSleeping))]
	public static bool Campfire_OnStopSleeping(Campfire __instance)
	{
		if (DayDream.DreamAtAnyFire)
		{
			RelativeLocationData relativeLocation = new RelativeLocationData(Locator.GetPlayerBody(), __instance.GetComponentInParent<OWRigidbody>(), __instance.transform);

			DreamArrivalPoint.Location location = _validLocations[(int)Random.Range(0, _validLocations.Length)];
			DayDream.WriteInfo($"Waking up at {location}");
			DreamArrivalPoint arrivalPoint = Locator.GetDreamArrivalPoint(location);
			DreamCampfire dreamCampfire = Locator.GetDreamCampfire(location);
			if (Locator.GetToolModeSwapper().GetItemCarryTool().GetHeldItemType() != ItemType.DreamLantern)
                {
				if(_safeLantern == null)
                    {
					// Search for the first lantern we can steal from the Stranger
					foreach (DreamLanternItem lamp in GameObject.FindObjectsOfType<DreamLanternItem>())
					{
						if (lamp.GetLanternType() == DreamLanternType.Functioning)
						{
							_safeLantern = lamp;
							break;
						}
					}
				}
			}
			Locator.GetToolModeSwapper().GetItemCarryTool().PickUpItemInstantly(_safeLantern);
			Locator.GetDreamWorldController().EnterDreamWorld(dreamCampfire, arrivalPoint, relativeLocation);

			DayDream.SharedInstance.CampfireSleptAt = __instance;
			DayDream.SharedInstance.CampfireRelativeLocation = relativeLocation;

			return false;
		}
		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(PartyPathAction), nameof(PartyPathAction.StartFollowPath))]
	public static void PartyPathAction_StartFollowPath(GhostController ____controller)
	{
		// The ghost rotation is hardcoded only for ghosts going to the house party, this is the first function I could find to hook onto and fix it
		var up = Locator.GetAstroObject(AstroObject.Name.DreamWorld).GetOWRigidbody().transform.TransformDirection(Vector3.up).normalized;
		____controller.transform.rotation = Quaternion.FromToRotation(____controller.transform.up, up);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SealRaftController), nameof(SealRaftController.FixedUpdate))]
	public static bool SealRaftController_FixedUpdate(SealRaftController __instance, AlignToSurfaceFluidDetector ____fluidDetector, Vector3 ____origDrag, Transform ____farNode, Transform ____nearNode,
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
