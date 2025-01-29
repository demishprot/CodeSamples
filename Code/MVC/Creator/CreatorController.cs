using Samples.Menu;
using Samples.Response;
using Samples.Services;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NTextCat.Commons;
using SimpleUi.Abstracts;
using SimpleUi.Signals;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using Zenject;
using IInitializable = Zenject.IInitializable;

namespace Samples
{
	namespace PlayerCreator
	{
		public class CreatorController : UiController<CreatorView>, IInitializable
		{
			private enum EInputMode
			{
				Edit,
				Add
			}
			private const string _containerName = "CreatorContainer";
			private bool _isOpen;
			private bool _isLoad;
			private string _language;
			private string _oldLanguage;
			private int _languageButtonIndex;
			private readonly List<string> _languages = new() { "ar", "cs", "de", "el", "en", "es", "fi", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "sv", "tr", "uk", "zh" };
			private readonly List<string> _voices = new() { "M", "M1", "M10", "M2", "M3", "M4", "M5", "F", "F1", "F2", "F3", "F4", "F5" };
			private Dictionary<string, string> _typeVoiceMap;
			private int _eventIndex;
			private List<InPlayerEvent> _events;
			private bool _verticalNormPosChanging;
			private Dictionary<string, ButtonModel> _buttons;
			private CancellationTokenSource _cts;
			private List<ItemMenu> _items;
			private Tween _tweenOnUnTranslated;
			private string _typeToCreate;
			private string _option;
			private string _cachedMediaString;
			private bool _proccesingMessageIsShow;
			private string _placement;
			private InPlayerEvent _editingEvent;

			private readonly SignalBus _signalBus;
			private readonly CreatorView _view;
			private readonly ILanguageService _languageService;
			private readonly IPlayerService _playerService;
			private readonly IPlaylistService _playlistService;
			private readonly ISoundService _soundService;
			private readonly ILocalizationService _localizationService;
			private readonly ILanguagePairParseService _languagePairParseService;
			private readonly ICreatorService _creatorService;
			public CreatorController(
				SignalBus signalBus,
				CreatorView view,
				ILanguageService languageService,
				IPlayerService playerService,
				IPlaylistService playlistService,
				ISoundService soundService,
				ILocalizationService localizationService,
				ILanguagePairParseService languagePairParseService,
				ICreatorService creatorService)

			{
				_signalBus = signalBus;
				_view = view;
				_languageService = languageService;
				_playerService = playerService;
				_playlistService = playlistService;
				_soundService = soundService;
				_localizationService = localizationService;
				_languagePairParseService = languagePairParseService;
				_creatorService = creatorService;
			}
			public void Initialize()
			{
				_view.Hide = Hide;
				_buttons = new()
				{
					{ "creator.language", new() { Action = LanguageButton, Interactable = true , IsActive = true } },
				};
			}
			public async void Signal(CreatorSignal signal)
			{
				switch (signal.signalType)
				{
					case ESignalType.Open:
						await Show();
						UpdateCreator(default, signal.playlistName);
						break;
				}
			}
			private async UniTask Show()
			{
				async UniTask SetUserLanguage()
				{
					_language = _languageService.GetCurrentLanguage();
					string userLanguage = await _languageService.GetCurrentUserLang();
					if (_language != userLanguage) SwitchLanguage(userLanguage, update: false);
				}
				_isOpen = true;
				var parent = GameObject.Find(_containerName);
				if (parent != null)
				{
					_view.transform.SetParent(parent.transform, false);
					_view.gameObject.SetActive(true);
				}
				Canvas.ForceUpdateCanvases();
				//await SetUserLanguage();
			}
			private async void UpdateCreator(int eventIndex, string playlistName = null, float? contentPosY = null, bool invisible = false, float? bottomAdapterRect = null)
			{
				List<InPlayerEvent> AddEmptyEvents(List<InPlayerEvent> events)
				{
					bool CompareType(InPlayerEvent @event, ECreatorType compare) => _creatorService.Types.TryGetValue(@event.type ?? string.Empty, out ECreatorType type) && type == compare;
					InPlayerEvent empty = new()
					{
						type = "EMPTY"
					};
					List<InPlayerEvent> modified = new();
					if (!CompareType(events.FirstOrDefault(), ECreatorType.None))
					{
						modified.Add(empty);
					}
					for (int i = 0; i < events.Count; i++)
					{
						var @event = events[i];
						var nextEvent = i + 1 >= events.Count ? @event : events[i + 1];
						modified.Add(@event);
						empty.id = @event.id;
						if (!CompareType(@event, ECreatorType.None) && @event.placement != "MB" &&
								(
									(CompareType(@event, ECreatorType.Divisible) && CompareType(nextEvent, ECreatorType.NoneDivisible)) ||
									(CompareType(@event, ECreatorType.Divisible) && CompareType(nextEvent, ECreatorType.Divisible) && !Adjacent(@event, nextEvent)) ||
									(CompareType(@event, ECreatorType.NoneDivisible) && !CompareType(nextEvent, ECreatorType.None)) ||
									i + 1 >= events.Count)
								) modified.Add(empty);
					}
					return modified;
				}
				bool @new = playlistName != null;
				BuildToCreator(invisible);
				ShowLoading(_isOpen && !invisible);
				_creatorService.Events ??= @new ? new() : _playlistService.GetEvents();
				_typeVoiceMap = new();
				_placement = null;
				_language = _languageService.GetCurrentLanguage();
				_languageButtonIndex = _languages?.IndexOf(_language) ?? 0;
				string courseId = _creatorService.Ids.First ?? _playlistService.Ids.First;
				string playlistId = _creatorService.Ids.Second ?? _playlistService.Ids.Second;
				await _creatorService.Init(_language, courseId, playlistId, playlistName, invisible);
				_events = AddEmptyEvents(_creatorService.Events);
				_playerService.Events = _events;
				_playerService.HandleOnClick = HandlerOnClick;
				_playerService.HandleOnTranslated = HandleOnTranslated;
				_playerService.HandleOnUnTranslated = HandleOnUnTranslated;
				var playerResponse = await _playerService.GetPlayer(onlyMainLanguage: true);
				_view.Adapter.Clear();
				if (playerResponse.StatusCode == Response.StatusCode.OK)
				{
					_items = playerResponse.Data;
					for (int i = 0; i < _events.Count; i++)
					{
						int _i = i;
						var @event = _events[i];
						var id = @event.id;
						_typeVoiceMap[@event.type] = @event.voice;
						var item = _items[i];
						item.isNull = false;
						item.onClick ??= () => HandlerOnClick(_i);
						item.onClicks = new()
						{
							() => Add(id),
							() => Delete(id, true)
						};
						string viewType = @event.type != "P" ? @event.type : string.Empty;
						item.specification = new(viewType, viewType);
					}
					_view.Adapter.Show(_items, contentPosY, CalculateIndent());
					await _playerService.Init(_items, _view.Adapter.GetItemViews());
					_playerService.SetSettings((await _playerService.GetSetSettings(0, 0)).Data, 0, _events.Count);
				}
				else
				{
					Debug.LogError(playerResponse.Description);
				}
				SetScrollRectOnValueChanged();
				CalculatePaddingTop();
				SetAdapterRect(bottomAdapterRect);
				UpdateLanguageButton(_language);
				HideLoading(_isOpen && !invisible);
			}
			private void SetScrollRectOnValueChanged()
			{
				_view.Adapter.scrollRect.onValueChanged.RemoveAllListeners();
				_view.Adapter.scrollRect.onValueChanged.AddListener((v) =>
				{
					if (!_verticalNormPosChanging && Input.touchCount > 0 && _view.Adapter.Dragging)
						TryStopCreatorTasks();
				});
			}
			private float CalculateIndent()
			{
				var maybefirstType = _events.Any() ? _events.FirstOrDefault(x => x.type != "P" && x.type != "SP" && x.type != "SETTING").type : string.Empty;
				var firstType = _events.Any() ? _events.First().type : string.Empty;
				float indent = 0.01f;
				if (maybefirstType == "IMG" && firstType == "SP") indent = -0.005f;
				else if (maybefirstType == "IMG") indent = -0.015f;
				return indent;
			}
			private void ShowLoading(bool i = true)
			{
				if (i)
				{
					_signalBus.Fire(new MenuSignal(ESignalType.Lock));
					_signalBus.Fire(new LoadingSignal(LoadingSignalType.SetImage, _languageService.GetCurrentLanguage()));
					_signalBus.OpenWindow<LoadingWindow>(EWindowLayer.Project);
				}
				_isLoad = true;
			}
			private void HideLoading(bool i = true)
			{
				if (i)
				{
					_signalBus.BackWindow(EWindowLayer.Project);
					_signalBus.Fire(new MenuSignal(ESignalType.Unlock));
				}
				_isLoad = false;
			}
			private void CalculatePaddingTop()
			{
				var rect = _view.Adapter.scrollRect.viewport.rect;
				_view.Adapter.verticalLayoutGroup.padding.top = (int)(rect.height);
			}
			private void SetAdapterRect(float? bottom)
			{
				var rect = _view.Adapter.GetComponent<RectTransform>();
				rect.offsetMin = new Vector2(0, bottom ?? 600f);
			}
			private void BuildToCreator(bool invisible)
			{
				_signalBus.Fire(new MenuSignal(ESignalType.BuildToCreator, _buttons, _view.Adapter.scrollRect)
				{
					enable = invisible
				});
			}
			private void ShowGrid(List<ItemMenu> items)
			{
				_signalBus.Fire(new MenuSignal(ESignalType.SetListAdapter, items, "creatorAdapter"));
			}
			private void UpdateMenu()
			{
				_signalBus.Fire(new MenuSignal(ESignalType.UpdateButtons, _buttons));
			}
			private void LanguageButton()
			{
				if (!_languages.Any()) return;
				_languageButtonIndex++;
				if (_languageButtonIndex >= _languages.Count) _languageButtonIndex = 0;
				string newLanguage = _languages[_languageButtonIndex];
				_cts?.Cancel();
				_cts = new();
				if (newLanguage != _language) SwitchLanguage(newLanguage, 3000, _cts.Token);
				UpdateLanguageButton(newLanguage);
			}
			private async void SwitchLanguage(string newLanguage, int millisecondsDelay = default, CancellationToken? cancellationToken = null, bool update = true)
			{
				try
				{
					if (cancellationToken != null && millisecondsDelay != default)
					{
						await UniTask.Delay(millisecondsDelay, cancellationToken: cancellationToken ?? default);
						cancellationToken?.ThrowIfCancellationRequested();
					}
					_oldLanguage ??= _language;
					_languageService.SetLanguage(newLanguage);
					if (update) UpdateCreator(_eventIndex);
				}
				catch
				{
					Debug.Log("language changing canceled");
				}
			}
			private void UpdateLanguageButton(string language)
			{
				_buttons["creator.language"].Text = language;
				UpdateMenu();
			}
			private void ShowGridVoices(string carret, UnityAction<string, string> Select)
			{
				var newList = new List<ItemMenu>();
				newList.Add(new()
				{
					contentTypeIndex = 0,
					name = new("<color=red>X</color>", "<color=red>X</color>"),
					onClick = () => HideGrid(true)
				});
				foreach (var voice in _voices)
				{
					newList.Add(new()
					{
						contentTypeIndex = 0,
						name = new(voice, voice),
						onClick = () => Select(carret, voice)
					});
				}
				ShowGrid(newList);
			}
			private void ShowGridOptions(string carret, string type, UnityAction<string, string> Select)
			{
				var newList = new List<ItemMenu>();
				newList.Add(new()
				{
					contentTypeIndex = 0,
					name = new("<color=red>X</color>", "<color=red>X</color>"),
					onClick = () => HideGrid(true)
				});
				foreach (var option in _creatorService.Options[type])
				{
					newList.Add(new()
					{
						contentTypeIndex = 0,
						name = new(option, option),
						onClick = () => Select(carret, option)
					});
				}
				ShowGrid(newList);
			}
			private void ShowGridPlacements(string carret, string type, UnityAction<string, string> Select)
			{
				var newList = new List<ItemMenu>();
				newList.Add(new()
				{
					contentTypeIndex = 0,
					name = new("<color=red>X</color>", "<color=red>X</color>"),
					onClick = () => HideGrid(true)
				});
				foreach (var placement in _creatorService.Placements[type])
				{
					newList.Add(new()
					{
						contentTypeIndex = 0,
						name = new(placement, placement),
						onClick = () => Select(carret, placement)
					});
				}
				ShowGrid(newList);
			}
			private void Add(string carret)
			{
				Cancel();
				void Select(string carret, string type = null, string voice = null, string option = null, string media = null)
				{
					HideGrid(false);
					if (type != null) _typeToCreate = type;
					if (voice != null) _typeVoiceMap[_typeToCreate] = voice;
					if (option != null) _option = option;
					if (media != null) _cachedMediaString = media;
					if (_creatorService.Types[_typeToCreate] == ECreatorType.None)
					{
						HandleOnSubmitInputField(carret, null, EInputMode.Add);
					}
					else if (_creatorService.Media.TryGetValue(_typeToCreate, out ECreatorMedia mediaEnum) is bool isMediaType && isMediaType && _cachedMediaString == null)
					{
						SelectMedia(mediaEnum);
					}
					else if ((!_typeVoiceMap.TryGetValue(_typeToCreate, out voice) || voice.IsNullOrEmpty()) && !isMediaType)
					{
						ShowGridVoices(carret, (carret, voice) => Select(carret: carret, voice: voice));
					}
					else if (_option == null && _creatorService.Options.ContainsKey(_typeToCreate) && !isMediaType)
					{
						ShowGridOptions(carret, _typeToCreate, (carret, option) => Select(carret: carret, option: option));
					}
					else if (isMediaType)
					{
						HandleOnSubmitInputField(carret, null, EInputMode.Add);
					}
					else
					{
						InitInputFieldEvents(EInputMode.Add, carret);
					}
				}
				async void SelectMedia(ECreatorMedia mediaEnum)
				{
					var selectMediaResponse = await _creatorService.SelectMedia(mediaEnum);
					if (selectMediaResponse.StatusCode == StatusCode.OK)
					{
						Select(carret: carret, media: selectMediaResponse.Data);
					}
					else
					{
						Cancel();
					}
				}
				void ShowGridTypes()
				{
					var newList = new List<ItemMenu>();
					newList.Add(new()
					{
						contentTypeIndex = 0,
						name = new("<color=red>X</color>", "<color=red>X</color>"),
						onClick = () => HideGrid(true)
					});
					foreach (var type in _creatorService.Types)
					{
						newList.Add(new()
						{
							contentTypeIndex = 0,
							name = new(type.Key, type.Key),
							onClick = () => Select(carret: carret, type: type.Key)
						});
					}
					ShowGrid(newList);
				}
				var @event = _creatorService.Events.FirstOrDefault(@event => @event.id == carret);
				_eventIndex = _events.IndexOf(@event);
				ShowGridTypes();
			}
			private void HideGrid(bool cancel)
			{
				if (cancel) Cancel();
				ShowGrid(null);
			}
			private void Cancel()
			{
				_typeToCreate = null;
				_option = null;
				_placement = null;
				_cachedMediaString = null;
				_creatorService.SoftCancel();
			}
			private int Delete(string id, bool update = false)
			{
				HideGrid(false);
				var @event = _events.FirstOrDefault(@event => @event.id == id);
				_eventIndex = _events.IndexOf(@event);
				var events = _creatorService.Types.TryGetValue(@event.type ?? "P", out ECreatorType type) && type == ECreatorType.Divisible ? GetAdjacentEventsKeys(_eventIndex) : new() { @event.id };
				var response = _creatorService.Delete(events);
				if (response.StatusCode == StatusCode.OK && update)
				{
					ClearJsonPlaylistData();
					SoftUpdateCreator();
					_creatorService.Accept();
				}
				else if (response.StatusCode == StatusCode.Failed)
				{
					Debug.LogError(response.Description);
				}
				return events.Count;
			}
			private List<string> GetAdjacentEventsKeys(int index)
			{
				return GetAdjacentEvents(index).Select(e => e.id).ToList();
			}
			private bool Adjacent(InPlayerEvent first, InPlayerEvent second)
			{
				return first.type == second.type && (first.options == second.options || (first.options != null && second.options != null && first.options.SequenceEqual(second.options)));
			}
			private List<InPlayerEvent> GetAdjacentEvents(int index)
			{
				if (index < 0 || index >= _events.Count) return new();
				var target = _events[index];
				var previousEvents = _events.Take(index).Reverse().TakeWhile(e => Adjacent(e, target)).Reverse().ToList();
				var nextEvents = _events.Skip(index).TakeWhile(e => Adjacent(e, target)).ToList();
				previousEvents.Add(_events[index]);
				previousEvents.AddRange(nextEvents);
				return previousEvents.Distinct().ToList();
			}
			private void HandlerOnClick(int index)
			{
				HideGrid(true);
				TryStopCreatorTasks();
				CheckTextAndRemoveUTag();
				_eventIndex = index;
				ChangeVerticalNormPos();
				CheckTextAndAddUTag();
			}
			private void TryStopCreatorTasks()
			{

			}
			private void ChangeVerticalNormPos()
			{
				_verticalNormPosChanging = true;
				_view.Adapter.SnapTo(_eventIndex, () => _verticalNormPosChanging = false);
			}
			private void CheckTextAndRemoveUTag(int phraseIndex = -1)
			{
				var itemView = _view.Adapter.GetItemView(phraseIndex >= 0 ? phraseIndex : _eventIndex);
				if (itemView != null)
				{
					if (itemView.text != null)
					{
						if (itemView.text.TranslateModeOn) return;
						itemView.text.OnLongPressExit(false);
						itemView.text.UnClick();
						itemView.repeatIndicator.SetLine(0, PlayerReader.ERepeatIndicatorType.S);
					}
					if (itemView.name != null)
					{
						itemView.name.text = itemView.name_1;
					}
					if ((itemView.name != null || itemView.text != null) && itemView.buttons?.Any() == true)
					{
						itemView.buttons[0].transform.parent.gameObject.SetActive(false);
					}
				}
			}
			private void CheckTextAndAddUTag()
			{
				var itemView = _view.Adapter.GetItemView(_eventIndex);
				if (itemView != null)
				{
					if (itemView.text != null)
					{
						int textIndex = _view.Adapter.GetTextIndex(itemView, _eventIndex);
						itemView.text.UTagSet(textIndex.ToString());
						itemView.repeatIndicator.SetLine(1, PlayerReader.ERepeatIndicatorType.S);
					}
					if (itemView.name != null)
					{
						itemView.name.text = $"<u>{itemView.name_1}</u>";
					}
					if ((itemView.name != null || itemView.text != null) && itemView.buttons?.Any() == true)
					{
						itemView.buttons[0].transform.parent.gameObject.SetActive(true);
					}
				}
			}
			private void HandleOnTranslated(OnTranslatedModel model)
			{
				HideGrid(true);
				_proccesingMessageIsShow = false;
				string s = model.s;
				bool ss = model.ss;
				int linksCount = model.linksCount;
				int textIndex = model.textIndex;
				int index = model.index;
				TryStopCreatorTasks();
				CheckTextAndRemoveUTag();
				_eventIndex = index + textIndex - (linksCount - 1);
				CheckTextAndAddUTag();
				StartEditingEvent();
			}
			private void HandleOnUnTranslated(string s, bool ss)
			{
				_tweenOnUnTranslated.Kill();
				_tweenOnUnTranslated = DOVirtual.DelayedCall(2f, () =>
				{
					_signalBus.Fire(new MenuSignal(ESignalType.DeactivateLine));
				});
				if (ss) return;
				var views = _view.Adapter.GetItemViews();
				DOVirtual.DelayedCall(0.201f, () => CheckTextAndAddUTag());
				foreach (var view in views)
				{
					if (view.text != null)
					{
						view.text.text.DOColor(new Color(0f, 0f, 0f, 1), 0.2f);
					}
					if (!view.translation && view.description != null && view.translatedBuble != null)
					{
						view.description.gameObject.SetActive(false);
					}
					if (!view.transcriptionOn && view.transcription != null && view.translatedBuble != null)
					{
						view.transcription.gameObject.SetActive(false);
					}
					if (view.translatedBuble != null && view.name != null && view.name.text != view.name_1)
					{
						view.name.text = view.name_1;
					}
					if ((view.name != null || view.text != null) && view.buttons?.Any() == true)
					{
						view.buttons[0].transform.parent.gameObject.SetActive(false);
					}
				}
			}
			private void StartEditingEvent()
			{
				string @string;
				void Select(string carret, string voice = null, string option = null, string placement = null)
				{
					HideGrid(false);
					if (voice != null) _editingEvent.voice = voice;
					if (option != null) _editingEvent.options = new() { option };
					if (placement != null) _editingEvent.placement = placement;
					if (_editingEvent.voice.IsNullOrEmpty())
					{
						ShowGridVoices(carret, (carret, voice) => Select(carret: carret, voice: voice));
					}
					else if (_editingEvent.options == null && _editingEvent.placement == "BODY" && _creatorService.Options.ContainsKey(_editingEvent.type))
					{
						ShowGridOptions(carret, _editingEvent.type, (carret, option) => Select(carret: carret, option: option));
					}
					else
					{
						InitInputFieldEvents(EInputMode.Edit, carret, @string);
					}
				}
				_editingEvent = _events[_eventIndex];
				if (_creatorService.Types.TryGetValue(_editingEvent.type, out ECreatorType type) && type == ECreatorType.Divisible)
				{
					@string = GetAdjacentEvents(_eventIndex)
						.Select(adjEvent =>
							adjEvent.phrase?.TryGetValue(_language, out object obj) == true ? obj.ToString() : string.Empty)
						.ToSeparatedString(" ");
				}
				else
				{
					@string = _editingEvent.phrase?.TryGetValue(_language, out object obj) == true ? obj.ToString() : string.Empty;
				}
				if (_creatorService.Media.TryGetValue(_editingEvent.type, out ECreatorMedia mediaEnum))
				{
					_editingEvent.placement = null;
					_editingEvent.options = null;
					_editingEvent.voice = null;
					_view.InputField.DeactivateInputField();
					if (_editingEvent.placement == null && _creatorService.Placements.ContainsKey(_editingEvent.type))
					{
						ShowGridPlacements(_editingEvent.id, _editingEvent.type, (carret, placement) => Select(carret: carret, placement: placement));
					}
				}
				else if (_creatorService.Types[_editingEvent.type] != ECreatorType.None)
				{
					InitInputFieldEvents(EInputMode.Edit, _editingEvent.id, @string);
				}
			}
			private async void HandleOnSubmitInputField(string carret, string @string, EInputMode mode)
			{
				async UniTask Edit()
				{
					if (_editingEvent.img != null) _creatorService.SelectMedia(_editingEvent.media, _editingEvent.img.texture);
					var createResponse = await _creatorService.Add(
							carret: carret,
							type: _editingEvent.type,
							@string: @string ?? (_editingEvent.phrase?.TryGetValue(_language, out object @object) == true ? @object.ToString() : string.Empty),
							typeOfType: _creatorService.Types[_editingEvent.type],
							voice: _editingEvent.voice,
							editRequest: true,
							_editingEvent.options,
							_editingEvent.media,
							_editingEvent.placement);

					if (createResponse.StatusCode == Response.StatusCode.OK)
					{
						int deleteCount = Delete(_editingEvent.id);
						_eventIndex += createResponse.Data - deleteCount;
						SoftUpdateCreator();
						ClearJsonPlaylistData();
					}
					else if (createResponse.StatusCode == Response.StatusCode.Failed)
					{
						_creatorService.SoftCancel();
						Debug.LogError(createResponse.Description);
					}
				}
				async UniTask Add()
				{
					List<string> options = !_option.IsNullOrEmpty() && _option != _typeToCreate ? new() { _option } : null;
					var createResponse = await _creatorService.Add(
						carret: carret,
						type: _typeToCreate,
						@string: @string,
						typeOfType: _creatorService.Types[_typeToCreate],
						voice: _typeVoiceMap.TryGetValue(_typeToCreate, out string voice) ? voice : null,
						editRequest: false,
						options,
						_cachedMediaString,
						_placement ?? "BODY");
					if (createResponse.StatusCode == Response.StatusCode.OK)
					{
						_eventIndex += createResponse.Data;
						SoftUpdateCreator();
						ClearJsonPlaylistData();
					}
					else if (createResponse.StatusCode == Response.StatusCode.Failed)
					{
						_creatorService.SoftCancel();
						Debug.LogError(createResponse.Description);
					}
					_option = null;
					_cachedMediaString = null;
				}

				ShowTextMessage("creator.processing");
				switch (mode)
				{
					case EInputMode.Edit: await Edit(); break;
					case EInputMode.Add: await Add(); break;
				}
				HideTextMessage();
				_creatorService.Accept();
			}
			private void SoftUpdateCreator()
			{
				UpdateCreator(_eventIndex > _events.Count ? default : _eventIndex, contentPosY: _view.Adapter.scrollRect.verticalNormalizedPosition, invisible: true, bottomAdapterRect: _view.Adapter.GetComponent<RectTransform>().offsetMin.y);
			}
			private void ClearJsonPlaylistData()
			{
				_playlistService.Delete(_creatorService.Ids.First ?? _playlistService.Ids.First, _creatorService.Ids.Second ?? _playlistService.Ids.Second, false, true);
			}
			private void ShowTextMessage(string param)
			{
				_proccesingMessageIsShow = true;
				_signalBus.Fire(new MenuSignal(ESignalType.ShowText, $"<color=yellow>{_localizationService.GetLocalizedString(param)}</color>", false, false));
			}
			private void HideTextMessage()
			{
				if (!_proccesingMessageIsShow) return;
				_proccesingMessageIsShow = false;
				_signalBus.Fire(new MenuSignal(ESignalType.HideText));
			}
			private void InitInputFieldEvents(EInputMode mode, string carret, string @string = null)
			{
				_view.InputField.text = @string ?? string.Empty;
				_view.InputField.onSubmit.RemoveAllListeners();
				_view.InputField.onDeselect.RemoveAllListeners();
				_view.InputField.onSubmit.AddListener((s) => HandleOnSubmitInputField(carret, s, mode));
				_view.InputField.onDeselect.AddListener((s) => Cancel());
#if UNITY_EDITOR
				_view.InputField.lineType = TMPro.TMP_InputField.LineType.MultiLineSubmit;
#endif
				_view.InputField.ActivateInputField();
			}
			private void Hide()
			{
				HideGrid(true);
				TryStopCreatorTasks();
				_cts?.Cancel();
				if (_oldLanguage != null) _languageService.SetLanguage(_oldLanguage);
				_oldLanguage = null;
				_eventIndex = default;
				_events = null;
				_creatorService.Events = null;
				_items = null;
				_languageButtonIndex = default;
				_view.Adapter.Clear();
				_creatorService.Clear();
				_isOpen = false;
				_typeVoiceMap = null;
				_playerService.Clear();
				_signalBus.Fire(new MenuSignal(ESignalType.BuildToMenu));
			}
		}
	}
}
