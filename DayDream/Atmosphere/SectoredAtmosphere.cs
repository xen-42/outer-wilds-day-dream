namespace DayDream.Atmosphere
{
    public class SectoredAtmosphere : SectoredMonoBehaviour
    {
        private bool _isSectorOccupied;

        public override void OnSectorOccupantsUpdated()
        {
            if (!DayDream.ShowAtmosphere) return;

            _isSectorOccupied = _sector.ContainsAnyOccupants(DynamicOccupant.Player);

            gameObject.SetActive(_isSectorOccupied);
        }

        public void Update()
        {
            if (!DayDream.ShowAtmosphere || !_isSectorOccupied) gameObject.SetActive(false);
        }
    }
}
