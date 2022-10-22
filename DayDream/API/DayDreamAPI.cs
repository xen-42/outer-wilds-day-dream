namespace DayDream.API;

public class DayDreamAPI : IDayDreamAPI
{
	public void RegisterCamera(OWCamera OWCamera) => DayDream.SharedInstance.Cameras.Add(OWCamera);
}
