using UnityEngine;

namespace DayDream.Atmosphere
{
    public class SectoredAtmosphere : SectoredMonoBehaviour
    {
        private bool _isSectorOccupied;
        public GameObject atmo;
		public GameObject fog;

        public override void OnSectorOccupantsUpdated()
        {
            _isSectorOccupied = _sector.ContainsAnyOccupants(DynamicOccupant.Player);
        }

        public void Update()
        {
			var fogActive = DayDream.ShowAtmosphere && _isSectorOccupied;
			var atmoActive = fogActive && DayDream.SeeSun;

			if (fogActive != atmo.activeInHierarchy) fog.SetActive(fogActive);
			if (atmoActive != atmo.activeInHierarchy) atmo.SetActive(atmoActive);
        }
    }
}
