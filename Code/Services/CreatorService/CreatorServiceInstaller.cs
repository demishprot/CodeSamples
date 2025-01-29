using Zenject;


namespace Samples
{
	namespace Services
	{
		public class CreatorServiceInstaller : Installer<CreatorServiceInstaller>
		{
			public override void InstallBindings()
			{
				Container.BindInterfacesAndSelfTo<CreatorService>().AsSingle();
			}
		}
	}
}


