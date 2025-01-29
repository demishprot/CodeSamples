using Samples.Menu;
using Samples.Response;
using Samples.Services.PlayerModules;
using System.Collections.Generic;
using UnityEngine.Events;

namespace Samples
{
	namespace Services
	{
		public interface IPlayerModulesService
		{
			UnityAction<int> HandleSnapTo { set; }
			UnityAction<int> HandleOnClick { set; }
			void Init(List<InPlayerEvent> events, List<ItemMenu> items, List<ItemMenuView> views);
			IBaseResponse<IModule> SetupModule(string id, EPlayerModuleType type, List<string> options);
			IBaseResponse<IEntity> SetupEntity(string id, List<string> options);
			IBaseResponse TryGetAction(string id, out UnityAction action);
			IBaseResponse TryGetActions(string id, out List<UnityAction> actions);
			IBaseResponse PlayModule(string id);
			IBaseResponse Clear();
		}

	}

}