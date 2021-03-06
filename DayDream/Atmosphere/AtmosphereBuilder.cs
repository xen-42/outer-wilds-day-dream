using UnityEngine;

namespace DayDream.Atmosphere
{
    // Stolen from New Horizons
    public static class AtmosphereBuilder
    {
        private static readonly int InnerRadius = Shader.PropertyToID("_InnerRadius");
        private static readonly int OuterRadius = Shader.PropertyToID("_OuterRadius");
        private static readonly int SkyColor = Shader.PropertyToID("_SkyColor");

        private static Color skyColour = new Color(0.05f, 1f, 1f, 1f);

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
                meshRenderer.material.SetColor(SkyColor, skyColour);
            }
            var sectoredAtmo = atmo.AddComponent<SectoredAtmosphere>();
            sectoredAtmo.SetSector(sector);
            atmo.SetActive(false);

            AddFog(atmo, 800f);

            return atmo;
        }

        // From NewHorizons fog builder
        private static void AddFog(GameObject rootGO, float size)
        {
            GameObject fogGO = new GameObject("FogSphere");
            fogGO.SetActive(false);
            fogGO.transform.parent = rootGO.transform;
            fogGO.transform.localScale = Vector3.one;

            // Going to copy from dark bramble
            var dbFog = GameObject.Find("DarkBramble_Body/Atmosphere_DB/FogLOD");
            var dbPlanetaryFogController = GameObject.Find("DarkBramble_Body/Atmosphere_DB/FogSphere_DB").GetComponent<PlanetaryFogController>();

            MeshFilter MF = fogGO.AddComponent<MeshFilter>();
            MF.mesh = dbFog.GetComponent<MeshFilter>().mesh;

            MeshRenderer MR = fogGO.AddComponent<MeshRenderer>();
            MR.materials = dbFog.GetComponent<MeshRenderer>().materials;
            MR.allowOcclusionWhenDynamic = true;

            PlanetaryFogController PFC = fogGO.AddComponent<PlanetaryFogController>();
            PFC.fogLookupTexture = dbPlanetaryFogController.fogLookupTexture;
            PFC.fogTint = skyColour;
            PFC.fogRadius = size * 1.2f;
            PFC.fogDensity = 0.1f;
            PFC.fogExponent = 1f;

            fogGO.transform.position = rootGO.transform.position;

            fogGO.SetActive(true);
        }
    }
}
