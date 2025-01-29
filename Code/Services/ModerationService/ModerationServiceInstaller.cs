using Zenject;

namespace Samples
{
	namespace Services
	{
		public class ModerationServiceInstaller : Installer<ModerationServiceInstaller>
		{
			public override void InstallBindings()
			{
				Container.BindInterfacesAndSelfTo<ModerationService>().AsSingle();
			}
		}
	}
}


