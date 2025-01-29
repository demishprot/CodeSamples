using Samples.Response;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text;
using System;
using UnityEngine.Networking;
using UnityEngine;
using System.Security.Policy;

namespace Samples
{
	namespace Services
	{
		public class ModerationService : IModerationService
		{
			private const string API_KEY = ":)";
			private const string API_URL = "https://api.moderatecontent.com/moderate/?";

			public async UniTask<IBaseResponse<ModerationAPIResponse>> Moderate(Uri url)
			{
				try
				{
					WWWForm form = new WWWForm();
					form.AddField("key", API_KEY);
					form.AddField("url", url.ToString());
					UnityWebRequest req = UnityWebRequest.Post(API_URL, form);
					await req.SendWebRequest();

					if (req.responseCode != 200)
					{
						Debug.LogError($"Request terminated with response code: {req.responseCode}");
						Debug.LogError(req.error);
						Debug.LogError(req.result);
						return new BaseResponse<ModerationAPIResponse>()
						{
							StatusCode = Response.StatusCode.Failed
						};
					}
					DownloadHandler dh = req.downloadHandler;

					while (!dh.isDone) await UniTask.Yield();

					string jsonResponse = Encoding.UTF8.GetString(dh.data);

					req.Dispose();
					Debug.Log(jsonResponse);
					return new BaseResponse<ModerationAPIResponse>()
					{
						StatusCode = Response.StatusCode.OK,
						Data = ModerationAPIResponse.FromJson(jsonResponse)
					};

				}
				catch (Exception ex)
				{
					UnityEngine.Debug.LogError($"[Moderate] : {ex.Message}");
					return new BaseResponse<ModerationAPIResponse>()
					{
						StatusCode = Response.StatusCode.Failed
					};
				}
			}
		}
	}
}


