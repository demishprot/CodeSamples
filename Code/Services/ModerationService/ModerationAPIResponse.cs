using Newtonsoft.Json;

namespace Samples
{
	namespace Services
	{
		[System.Serializable]
		public struct ModerationAPIResponse
		{
			public readonly bool IsNotAdult { get { return error_code == 0 && rating_index != 3; } }
			public int rating_index;
			public int error_code;
			public static ModerationAPIResponse FromJson(string jsonResponse)
			{
				ModerationAPIResponse parsed = JsonConvert.DeserializeObject<ModerationAPIResponse>(jsonResponse);
				return parsed;
			}
		}
	}
}


