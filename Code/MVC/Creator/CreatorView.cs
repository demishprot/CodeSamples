using Samples.Menu;
using SimpleUi.Abstracts;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Samples
{
	namespace PlayerCreator
	{
		public class CreatorView : UiView
		{
			[field: SerializeField] public ScrollViewAdapter Adapter { get; set; }
			[field: SerializeField] public TMP_InputField InputField { get; set; }
			public UnityAction Hide { get; set; }
			protected override void OnDisable()
			{
				Hide?.Invoke();
			}
		}
	}
}
