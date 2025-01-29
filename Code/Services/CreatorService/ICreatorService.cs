using Samples.Menu;
using Samples.Response;
using Cysharp.Threading.Tasks;
using ModestTree.Util;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


namespace Samples
{
	namespace Services
	{
		public interface ICreatorService
		{
			ValuePair<string, string> Ids { get; }
			List<InPlayerEvent> Events { get; set; }
			Dictionary<string, ECreatorType> Types { get; }
			Dictionary<string, List<string>> Options { get; }
			Dictionary<string, ECreatorMedia> Media { get; }
			Dictionary<string, List<string>> Placements { get; }
			UniTask<IBaseResponse> Init(string language, string courseId, string playlistId, string playlistName, bool invisible);
			IBaseResponse Clear();
			UniTask<IBaseResponse<int>> Add(string carret, string type, string @string, ECreatorType typeOfType, string voice, bool editRequest, List<string> options, string media, string placement);
			IBaseResponse Delete(List<string> keys);
			UniTask<IBaseResponse> Accept();
			IBaseResponse SoftCancel();
			UniTask<IBaseResponse<string>> SelectMedia(ECreatorMedia mediaEnum);
			IBaseResponse<string> SelectMedia(string id, Texture2D texture2D);

		}
	}
}


