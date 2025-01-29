using Samples.Response;
using Cysharp.Threading.Tasks;
using System;

namespace Samples
{
	namespace Services
	{
		public interface IModerationService
		{
			UniTask<IBaseResponse<ModerationAPIResponse>> Moderate(Uri url);
		}
	}
}


