using Samples.Extensions;
using Samples.Repositories;
using Samples.Response;
using Cysharp.Threading.Tasks;
using ModestTree;
using ModestTree.Util;
using NTextCat.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;


namespace Samples
{
	namespace Services
	{
		public struct CreatorEvent
		{
			public string phrase;
			public string carret;
			public string type;
			public string voice;
			public string media;
			public int order;
			public List<string> options;
			public string placement;
		}
		public enum ECreatorType
		{
			None,
			Divisible,
			NoneDivisible
		}
		public enum ECreatorMedia
		{
			None,
			Image,
			Video,
			Music
		}
		public class CreatorService : ICreatorService
		{
			private Queue<Func<UniTask>> _taskQueue = new();
			private ValueWrapper<bool> _taskQueueIsRunningSource = new(false);
			private string _currentLanguage;
			private string _courseId;
			private string _playlistId;
			private List<string> _languages;
			private Playlist _playlist;
			private Dictionary<string, Texture2D> _textures = new();

			public ValuePair<string, string> Ids { get { return new(_courseId, _playlistId); } }
			public List<InPlayerEvent> Events { get; set; }
			public Dictionary<string, ECreatorType> Types { get; } = new()
			{
				{"P", ECreatorType.None},
				{"A", ECreatorType.NoneDivisible},
				{"B", ECreatorType.NoneDivisible},
				{"T", ECreatorType.Divisible},
				{"IMG", ECreatorType.NoneDivisible },
				{"VIDEO", ECreatorType.NoneDivisible }
			};
			public Dictionary<string, ECreatorMedia> Media { get; } = new()
			{
				{"IMG", ECreatorMedia.Image },
				{"VIDEO", ECreatorMedia.Video},
			};
			public Dictionary<string, List<string>> Options { get; } = new()
			{
				{"T", new() { "T", "H1", "H2", "H3", "H4", "CITE" } },
				{"IMG", new() { "IMG", "WIDE" } },
				{"VIDEO", new() { "VIDEO", "WIDE" } },
			};
			public Dictionary<string, List<string>> Placements { get; } = new()
			{
				{"IMG",  new() { "BODY", "MB" } },
				{"VIDEO",  new() { "BODY", "MB" } },
			};

			private readonly IAuthManager _authManager;
			private readonly IBaseRepository<User> _userRepository;
			private readonly IBaseRepository<Playlist> _playlistRepository;
			private readonly IFunctionsService _functionsService;
			private readonly IGPTService _GPTService;
			private readonly ILanguagePairParseService _languagePairParseService;
			private readonly INativeGalleryService _nativeGalleryService;
			private readonly IImageRepository<Texture2D> _imageRepository;
			private readonly IVideoRepository<Video> _videoRepository;
			public CreatorService(
				IAuthManager authManager,
				IBaseRepository<User> userRepository,
				IBaseRepository<Playlist> playlistRepository,
				IFunctionsService functionsService,
				IGPTService gPTService,
				ILanguagePairParseService languagePairParseService,
				INativeGalleryService nativeGalleryService,
				IImageRepository<Texture2D> imageRepository,
				IVideoRepository<Video> videoRepository)
			{
				_authManager = authManager;
				_userRepository = userRepository;
				_playlistRepository = playlistRepository;
				_functionsService = functionsService;
				_GPTService = gPTService;
				_languagePairParseService = languagePairParseService;
				_nativeGalleryService = nativeGalleryService;
				_imageRepository = imageRepository;
				_videoRepository = videoRepository;
			}
			public async UniTask<IBaseResponse> Init(string language, string courseId, string playlistId, string playlistName, bool invisible)
			{
				try
				{
					_currentLanguage = language;
					_languages ??= new() { _currentLanguage };
					if (!_languages.Contains(_currentLanguage)) _languages.Add(_currentLanguage);
					_courseId = courseId;
					_playlistId = playlistId;
					if (!invisible) await UpdatePlaylist(playlistName);
					return new BaseResponse()
					{
						StatusCode = Response.StatusCode.OK
					};
				}
				catch (Exception ex)
				{
					return new BaseResponse()
					{
						Description = $"[Init] : {ex.Message}",
						StatusCode = Response.StatusCode.Failed
					};
				}
			}
			private string Hex(int size)
			{
				byte[] data = new byte[size];
				System.Random random = new();
				random.NextBytes(data);
				return BitConverter.ToString(data).Replace("-", "").ToLower();
			}
			private async UniTask UpdatePlaylist(string name)
			{
				bool @new = name != null;
				List<string> AddCurrentLanguage(List<string> list)
				{
					if (list?.Contains(_currentLanguage) != true)
					{
						list ??= new();
						list.Add(_currentLanguage);
					}
					return list;
				}
				var playlists = await _playlistRepository.GetAll(_courseId);
				if (@new)
				{
					int order = playlists.Max(x => x.order) + 1;
					_playlistId = $"{_courseId}.{_authManager.User.UserId}.{order:D3}";
					_playlist = new()
					{
						id = _playlistId,
						order = order,
						visible = EVisible.ADMIN.ToString(),
						name = new Dictionary<string, string>() { { _currentLanguage, name } },
						phrase_languages = _languages,
						learn_languages = _languages,
						authors = new() { _authManager.User.UserId }
					};
				}
				else
				{
					_playlist = playlists.FirstOrDefault(x => x.id == _playlistId);
					_playlist.learn_languages = AddCurrentLanguage(_playlist.learn_languages);
					_playlist.phrase_languages = AddCurrentLanguage(_playlist.phrase_languages);
				}
				await _playlistRepository.Set(_playlist, _courseId, _playlistId);
			}
			public async UniTask<IBaseResponse<int>> Add(string carret, string type, string @string, ECreatorType typeOfType, string voice, bool editRequest, List<string> options, string media, string placement)
			{
				bool Adjacent(InPlayerEvent playerEvent, CreatorEvent creatorEvent) => playerEvent.type == creatorEvent.type && (playerEvent.options == creatorEvent.options || (playerEvent.options != null && creatorEvent.options != null && playerEvent.options.SequenceEqual(creatorEvent.options)));
				try
				{
					var newCreatorEvents = new List<CreatorEvent>();
					var splitList = @string?.Replace("\r", "").Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
					if (splitList == null || splitList.IsEmpty()) splitList = new string[1] { null };
					int order = 0;
					for (int i = 0; i < splitList.Length; i++)
					{
						var split = splitList[i];
						switch (typeOfType)
						{
							case ECreatorType.Divisible:
#if UNITY_EDITOR
								char[] punctuation = new char[] { ',', '.', '!', '?' };
								var divideResponseText = new BaseResponse<GPTDivideResponseModel>()
								{
									StatusCode = StatusCode.OK,
									Data = new()
									{
										divide_sentences = split.Split(punctuation, StringSplitOptions.RemoveEmptyEntries).ToList()
									}
								};
#else
								var divideResponseText = await _GPTService.Divide(split, improve: false);
#endif
								if (divideResponseText.StatusCode != StatusCode.OK) return new BaseResponse<int>()
								{
									StatusCode = divideResponseText.StatusCode,
									Description = divideResponseText.Description,
								};
								var @event = Events.Find(@event => @event.id == carret);
								var currentIndex = Events.IndexOf(@event);
								var nextEvent = currentIndex >= 0 && currentIndex < Events.Count - 1 ? Events[currentIndex + 1] : Events.ElementAtOrDefault(currentIndex + 1);
								var compareEvent = new CreatorEvent()
								{
									type = type,
									options = options
								};
								if (!editRequest && i == 0 && Adjacent(@event, compareEvent))
								{
									newCreatorEvents.Add(new()
									{
										carret = carret,
										type = "P",
										order = order,
									});
									order++;
								}
								for (int j = 0; j < divideResponseText.Data.divide_sentences.Count; j++)
								{
									string phrase = divideResponseText.Data.divide_sentences[j];
									newCreatorEvents.Add(new()
									{
										carret = carret,
										type = type,
										phrase = phrase.IsNullOrEmpty() ? null : phrase,
										voice = phrase.IsNullOrEmpty() ? null : voice,
										order = order,
										options = options
									});
									order++;
								}
								if (i + 1 != splitList.Length || (Adjacent(nextEvent, compareEvent) && !editRequest))
								{
									newCreatorEvents.Add(new()
									{
										carret = carret,
										type = "P",
										order = order
									});
								}
								break;
							case ECreatorType.NoneDivisible:
								newCreatorEvents.Add(new()
								{
									carret = carret,
									type = type,
									phrase = split.IsNullOrEmpty() ? null : split,
									voice = split.IsNullOrEmpty() ? null : voice,
									order = order,
									options = options,
									media = media,
									placement = placement
								});
								break;
							default:
								newCreatorEvents.Add(new()
								{
									carret = carret,
									type = type,
									order = order,
									options = options,
								});
								break;
						}
						order++;
					}
					var creatorEvents = new Dictionary<string, CreatorEvent>();
					for (int i = 0; i < newCreatorEvents.Count; i++)
					{
						int index = i;
						string id = Hex(10);
						var creatorEvent = newCreatorEvents[i];
						creatorEvents[$"{_currentLanguage}/{id}"] = creatorEvent;
						var parsed = _languagePairParseService.Parse(creatorEvent.phrase, true);
					}
					Upload(creatorEvents);
					return new BaseResponse<int>()
					{
						StatusCode = Response.StatusCode.OK,
						Data = newCreatorEvents.Count
					};
				}
				catch (Exception ex)
				{

					return new BaseResponse<int>()
					{
						Description = $"[Add] : {ex.Message}",
						StatusCode = Response.StatusCode.Failed
					};
				}
			}
			public IBaseResponse Delete(List<string> keys)
			{
				try
				{
					_taskQueue.Enqueue(async () => await _functionsService.UserDeleteEvents(_courseId, _playlistId, keys));
					Events.RemoveAll(@event => keys.Contains(@event.id));
					return new BaseResponse()
					{
						StatusCode = Response.StatusCode.OK,
					};
				}
				catch (Exception ex)
				{
					return new BaseResponse()
					{
						Description = $"[Delete] : {ex.Message}",
						StatusCode = Response.StatusCode.Failed
					};
				}
			}
			private void Upload(Dictionary<string, CreatorEvent> creatorEvents)
			{
				if (creatorEvents == null || creatorEvents.Count == 0)
				{
					return;
				}
				var docs = Events.ToDictionary(@event => @event.id, @event => @event);
				var orderMap = new Dictionary<string, int>();
				var sortedCreatedEvents = creatorEvents.OrderBy(x => x.Value.order).ToDictionary(x => x.Key, y => y.Value);
				var keys = new List<string>() { null };
				keys.AddRange(docs.Keys);
				foreach (string carret in keys)
				{
					bool hasValue = docs.TryGetValue(carret ?? string.Empty, out InPlayerEvent @event);
					string playlistId = hasValue ? @event.playlistId : _playlistId;
					if (hasValue)
					{
						@event.order += orderMap.TryGetValue(playlistId, out int value) ? value : 0;
						docs[carret] = @event;
					}
					foreach (var key in sortedCreatedEvents.Keys)
					{
						var created = sortedCreatedEvents[key];
						if (created.carret == carret || (carret == null && created.carret == null))
						{
							var split = key.Split('/');
							string language = split[0];
							string id = split[1];
							if (!orderMap.ContainsKey(playlistId)) orderMap[playlistId] = 0;
							orderMap[playlistId] += 5;
							docs[id] = new()
							{
								id = id,
								order = orderMap[playlistId] + (hasValue ? docs[carret].order : 0),
								type = created.type,
								playlistId = playlistId,
								courseId = _courseId,
								voice = created.voice,
								options = created.options,
								phrase = created.phrase != null ? new() { { language, created.phrase } } : null,
								media = created.media,
								img = created.media != null && _textures.TryGetValue(created.media, out Texture2D texture) ? Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100.0f) : null,
								placement = created.placement,
							};
						}
					}
				}
				var @new = new List<InPlayerEvent>();
				foreach (var key in docs.Keys)
				{
					if (key.IsNullOrEmpty()) continue;
					var @event = docs[key];
					@new.Add(@event);
				}
				Events = @new.OrderBy(@event => @event.order).ToList();
				_taskQueue.Enqueue(async () => await _functionsService.UserAddEvents(_courseId, _playlistId, creatorEvents));
			}
			public IBaseResponse<string> SelectMedia(string id, Texture2D texture2D)
			{
				try
				{
					_textures[id] = texture2D;
					return new BaseResponse<string>()
					{
						StatusCode = Response.StatusCode.OK,
						Data = id
					};
				}
				catch (Exception ex)
				{
					return new BaseResponse<string>()
					{
						Description = $"[SelectMedia] : {ex.Message}",
						StatusCode = Response.StatusCode.Failed
					};
				}
			}
			public async UniTask<IBaseResponse<string>> SelectMedia(ECreatorMedia mediaEnum)
			{
				try
				{
					return mediaEnum switch
					{
						ECreatorMedia.Image => await SelectImage(),
						ECreatorMedia.Video => await SelectVideo(),
						_ => new BaseResponse<string>()
						{
							StatusCode = StatusCode.Failed
						},
					};
					async UniTask<IBaseResponse<string>> SelectImage()
					{
						var pickImageResponse = await _nativeGalleryService.SelectImage();
						if (pickImageResponse.StatusCode != Response.StatusCode.OK)
						{
							return new BaseResponse<string>()
							{
								StatusCode = pickImageResponse.StatusCode
							};
						}
						var id = $"some/path/{_authManager.User.UserId}/{_courseId}/someImage_{Guid.NewGuid()}.png";
						_textures[id] = pickImageResponse.Data;
						_taskQueue.Enqueue(async () => await _imageRepository.Put(pickImageResponse.Data, id));
						return new BaseResponse<string>()
						{
							StatusCode = Response.StatusCode.OK,
							Data = id
						};
					}
					async UniTask<IBaseResponse<string>> SelectVideo()
					{
						var videoPathResponse = await _nativeGalleryService.SelectVideo();
						if (videoPathResponse.StatusCode != Response.StatusCode.OK)
						{
							return new BaseResponse<string>()
							{
								StatusCode = videoPathResponse.StatusCode
							};
						}
						var id = $"some/path/{_authManager.User.UserId}/{_courseId}/someVideo_{Guid.NewGuid()}.{videoPathResponse.Data[(videoPathResponse.Data.LastIndexOf('.') + 1)..]}";
						_taskQueue.Enqueue(async () => await _videoRepository.Put(new() { url = videoPathResponse.Data }, id));
						return new BaseResponse<string>()
						{
							StatusCode = Response.StatusCode.OK,
							Data = id
						};
					}
				}
				catch (Exception ex)
				{
					return new BaseResponse<string>()
					{
						Description = $"[SelectMedia] : {ex.Message}",
						StatusCode = Response.StatusCode.Failed
					};
				}
			}
			public async UniTask<IBaseResponse> Accept()
			{
				try
				{
					await _taskQueue.RunTasks(_taskQueueIsRunningSource);
					return new BaseResponse()
					{
						StatusCode = Response.StatusCode.OK
					};
				}
				catch (Exception ex)
				{
					return new BaseResponse()
					{
						Description = $"[Accept] : {ex.Message}",
						StatusCode = Response.StatusCode.Failed
					};
				}
			}
			public IBaseResponse SoftCancel()
			{
				try
				{
					_taskQueue.SoftClear(_taskQueueIsRunningSource);
					_textures.Clear();
					return new BaseResponse()
					{
						StatusCode = Response.StatusCode.OK
					};
				}
				catch (Exception ex)
				{
					return new BaseResponse()
					{
						Description = $"[SoftCancel] : {ex.Message}",
						StatusCode = Response.StatusCode.Failed
					};
				}
			}
			public IBaseResponse Clear()
			{
				try
				{
					_languages = null;
					_courseId = null;
					_playlistId = null;
					_playlist = default;
					Events = null;
					return new BaseResponse()
					{
						StatusCode = Response.StatusCode.OK
					};
				}
				catch (Exception ex)
				{
					return new BaseResponse()
					{
						Description = $"[Clear] : {ex.Message}",
						StatusCode = Response.StatusCode.Failed
					};
				}
			}
		}
	}
}


