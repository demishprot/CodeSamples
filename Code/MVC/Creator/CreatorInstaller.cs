using SimpleUi;
using Zenject;

namespace Samples
{
	namespace PlayerCreator
	{
		public class CreatorInstaller : MonoInstaller
		{
			public override void InstallBindings()
			{
				Container.BindUiView<CreatorController, CreatorView>(GetComponent<CreatorView>(), transform.parent);
				Container.DeclareSignal<CreatorSignal>();
				Container.BindSignal<CreatorSignal>().ToMethod<CreatorController>(x => x.Signal).FromResolve();
			}
		}
	}
}
