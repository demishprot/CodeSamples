using Zenject;

namespace Samples
{
	namespace Services
	{
		public class PlayerModulesServiceInstaller : Installer<PlayerModulesServiceInstaller>
		{
			public override void InstallBindings()
			{
				Container.BindInterfacesAndSelfTo<PlayerModulesService>().AsSingle();
			}
		}

	}

}