using UnityEngine;

namespace DayDream.Atmosphere
{
    // Stolen from New Horizons
    public static class AtmosphereBuilder
    {
        private static readonly int InnerRadius = Shader.PropertyToID("_InnerRadius");
        private static readonly int OuterRadius = Shader.PropertyToID("_OuterRadius");
        private static readonly int SkyColor = Shader.PropertyToID("_SkyColor");

        public static GameObject Make(GameObject rootBody, Sector sector)
        {
            GameObject atmo = GameObject.Instantiate(GameObject.Find("TimberHearth_Body/Atmosphere_TH/AtmoSphere"), rootBody.transform, true);
            atmo.transform.name = "DreamworldAtmosphere";
            atmo.transform.localPosition = Vector3.up * -800;
            atmo.transform.localScale = Vector3.one * 1000;
            foreach (var meshRenderer in atmo.GetComponentsInChildren<MeshRenderer>())
            {
                meshRenderer.material.SetFloat(InnerRadius, 800f);
                meshRenderer.material.SetFloat(OuterRadius, 1000);
                meshRenderer.material.SetColor(SkyColor, new Color(0.05f, 1f, 1f, 1f));
            }
            var sectoredAtmo = atmo.AddComponent<SectoredAtmosphere>();
            sectoredAtmo.SetSector(sector);
            atmo.SetActive(false);

            return atmo;
        }
    }
}
