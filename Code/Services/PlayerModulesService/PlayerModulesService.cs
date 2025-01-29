using Samples.Extensions;
using Samples.Menu;
using Samples.Response;
using Samples.Services.PlayerModules;
using Cysharp.Threading.Tasks;
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
		public class PlayerModulesService : IPlayerModulesService
		{
			private enum ELogicalOperator
			{
				AND,
				OR,
				ANDNOT,
				ORNOT,
				XOR,
				XORNOT
			}

			private const string THIS_KEY_WORD = "THIS";
			private Dictionary<string, IModule> _modules;
			private Dictionary<string, IEntity> _entities;
			private List<ItemMenu> _items;
			private List<ItemMenuView> _views;
			private List<InPlayerEvent> _events;

			public UnityAction<int> HandleSnapTo { get; set; }
			public UnityAction<int> HandleOnClick { get; set; }

			public void Init(List<InPlayerEvent> events, List<ItemMenu> items, List<ItemMenuView> views)
			{
				_events = events;
				_items = items;
				_views = views;
				ProcessingConditions();
			}
			private bool TryGetValue<TValue>(List<string> strings, string key, out TValue value)
			{
				value = default;
				if (strings == null) return false;
				string fieldPrefix = key + "=";
				string mapPrefix = key + ".";
				foreach (string @string in strings)
				{
					if (@string.StartsWith(fieldPrefix.ToUpper()))
					{
						string extractedValue = @string.Substring(fieldPrefix.Length);
						if (typeof(TValue) == typeof(string))
						{
							value = (TValue)(object)extractedValue;
						}
						else if (typeof(TValue) == typeof(bool) && extractedValue.Equals("true", StringComparison.OrdinalIgnoreCase))
						{
							value = (TValue)(object)true;
						}
						else if (typeof(TValue) == typeof(bool) && extractedValue.Equals("false", StringComparison.OrdinalIgnoreCase))
						{
							value = (TValue)(object)false;
						}
						else if (typeof(TValue).IsEnum)
						{
							if (Enum.TryParse(typeof(TValue), extractedValue, true, out object enumValue))
							{
								value = (TValue)enumValue;
							}
						}
					}
					else if (@string.StartsWith(mapPrefix.ToUpper()))
					{
						string mapKey = @string.Substring(mapPrefix.Length);
						int equalsIndex = mapKey.IndexOf('=');
						if (equalsIndex != -1)
						{
							string listKey = mapKey.Substring(0, equalsIndex);
							string listValue = mapKey.Substring(equalsIndex + 1);
							if (typeof(TValue) == typeof(List<KeyValuePair<string, string>>))
							{
								var list = (List<KeyValuePair<string, string>>)(object)value ?? new();
								list.Add(new(listKey, listValue));
								value = (TValue)(object)list;
							}
						}
					}
				}
				return !EqualityComparer<TValue>.Default.Equals(value, default(TValue));
			}
			public IBaseResponse<IEntity> SetupEntity(string id, List<string> options)
			{
				try
				{
					if (_modules?.ContainsKey(id) == true)
					{
						return new BaseResponse<IEntity>()
						{
							StatusCode = StatusCode.NotFound,
						};
					}
					var entity = new Entity()
					{
						CustomID = TryGetValue(options, "ID", out string customID) is bool hasID && hasID ? customID : null,
						Actions = TryGetValue(options, "ACTIONS", out List<KeyValuePair<string, string>> actions) is bool hasActions && hasActions ? actions : null
					};
					bool hasAny = hasID || hasActions;
					_entities ??= new();
					if (hasAny) _entities[id] = entity;
					return new BaseResponse<IEntity>()
					{
						StatusCode = hasAny ? StatusCode.OK : StatusCode.NotFound,
						Data = entity
					};
				}
				catch (Exception ex)
				{
					Debug.LogError(ex.Message);
					return new BaseResponse<IEntity>()
					{
						Description = $"[SetupModule] : {ex.Message}",
						StatusCode = Response.StatusCode.Failed
					};
				}
			}
			public IBaseResponse<IModule> SetupModule(string id, EPlayerModuleType type, List<string> options)
			{
				try
				{
					Module Create() => type switch
					{
						EPlayerModuleType.QS or EPlayerModuleType.QE or EPlayerModuleType.YN or EPlayerModuleType.RATE => new Question()
						{
							Type = type,
							Id = TryGetValue(options, "ID", out string questionId) ? questionId : null
						},
						EPlayerModuleType.IF or EPlayerModuleType.ELIF or EPlayerModuleType.ENDIF => new ConditionBlock()
						{
							Type = type,
							Conditions = TryGetValue(options, "CONDITIONS", out List<KeyValuePair<string, string>> conditions) ? conditions : default,
						},
						_ => default,
					};
					void SpecialProcessing(Module module)
					{
						switch (module)
						{
							case Question question:
								switch (question.Type)
								{
									case EPlayerModuleType.YN:
										question.P_Selections = new() { new("Y", THIS_KEY_WORD), new("N", THIS_KEY_WORD) };
										break;
									case EPlayerModuleType.RATE:
										question.P_Selections = new() { new("1", THIS_KEY_WORD), new("2", THIS_KEY_WORD), new("3", THIS_KEY_WORD), new("4", THIS_KEY_WORD), new("5", THIS_KEY_WORD) };
										break;
									case EPlayerModuleType.QS:
										_entities.Where(entity => entity.Value.P_Module.IsNullOrEmpty()).ForEach(entity => entity.Value.P_Ignore = true);
										break;
									case EPlayerModuleType.QE:
										var last = _modules.LastOrDefault(module => module.Value.Type == EPlayerModuleType.QS);
										if (last.Equals(default)) break;
										var entities = _entities
											.Where(entity => entity.Value.P_Module.IsNullOrEmpty() && !entity.Value.P_Ignore);
										var selections = new List<KeyValuePair<string, string>>();
										foreach (var entity in entities)
										{
											if (entity.Value?.Actions != null)
											{
												entity.Value.P_Module = id;
												foreach (var action in entity.Value.Actions)
												{
													selections.Add(new KeyValuePair<string, string>(action.Value, entity.Key));
												}
											}
										}
										((Question)last.Value).P_Selections = selections;
										break;
								}
								break;
						}
					}
					_modules ??= new();
					var module = Create();
					SpecialProcessing(module);
					_modules[id] = module;
					return new BaseResponse<IModule>()
					{
						StatusCode = StatusCode.OK,
						Data = module
					};
				}
				catch (Exception ex)
				{
					Debug.LogError(ex.Message);
					return new BaseResponse<IModule>()
					{
						Description = $"[SetupModule] : {ex.Message}",
						StatusCode = Response.StatusCode.Failed
					};
				}
			}
			private int GetMaxIndex(List<string> IDs) => _events
									.Select((e, index) => new { Event = e, Index = index })
									.Where(x => IDs.Contains(x.Event.id))
									.OrderByDescending(x => x.Index)
									.Select(x => x.Index)
									.First();
			public IBaseResponse PlayModule(string id)
			{
				try
				{
					if (_modules?.TryGetValue(id, out IModule module) == true)
					{
						switch (module)
						{
							case Question question:
								if (question.P_Selections == null) break;
								var IDs = new List<string>();
								foreach (var selection in question.P_Selections)
								{
									IDs.Add(selection.Value);
								}
								IDs = IDs.Where(ID => ID != THIS_KEY_WORD).ToList();
								HandleSnapTo?.Invoke(IDs.Any() ? GetMaxIndex(IDs) : _events.FindIndex(@event => @event.id == id));
								return new BaseResponse()
								{
									StatusCode = StatusCode.OK
								};

						}
					}
					if (_modules?.Any(module => module.Value is Question question && question.P_Selections?.Any(keyValuePair => keyValuePair.Value == id) == true) == true)
					{
						HandleOnClick?.Invoke(_events.FindIndex(@event => @event.id == id) + 1);
						return new BaseResponse()
						{
							StatusCode = StatusCode.OK
						};
					}
					return new BaseResponse()
					{
						StatusCode = StatusCode.NotFound
					};
				}
				catch (Exception ex)
				{
					Debug.LogError(ex.Message);
					return new BaseResponse()
					{
						Description = $"[PlayModule] : {ex.Message}",
						StatusCode = Response.StatusCode.Failed
					};
				}
			}
			public IBaseResponse ProcessingConditions()
			{
				bool TrueCondition(ConditionBlock conditionBlock)
				{
					bool IsTrue(string condition)
					{
						var parts = condition.ToUpper().Split('.');
						if (parts.Length != 3) return false;
						string library = parts[0].Trim();
						string key = parts[1].Trim();
						string value = parts[2].Trim();
						return library switch
						{
							"QUESTION" =>
								_modules.Any(keyValuePair => keyValuePair.Value is Question question && question.Id?.ToUpper() == key?.ToUpper() && question.P_Value?.ToUpper() == value?.ToUpper()),
							_ => false,
						};
					}
					var booleanConditions = conditionBlock.Conditions?
						.Select(condition => new KeyValuePair<ELogicalOperator, bool>(condition.Key.ToUpper().ToEnum<ELogicalOperator>(), IsTrue(condition.Value)))
						.ToList();
					return booleanConditions?.Aggregate(booleanConditions.First().Value, (accum, current) =>
					current.Key switch
					{
						ELogicalOperator.AND => accum && current.Value,
						ELogicalOperator.OR => accum || current.Value,
						ELogicalOperator.ANDNOT => accum && !current.Value,
						ELogicalOperator.ORNOT => accum || !current.Value,
						ELogicalOperator.XOR => accum ^ current.Value,
						ELogicalOperator.XORNOT => accum ^ !current.Value,
						_ => accum
					}) ?? false;
				}
				try
				{
					if (_modules == null)
					{
						return new BaseResponse()
						{
							StatusCode = StatusCode.NotFound,
						};
					}
					bool groupHasTrueCondition = false;
					List<KeyValuePair<string, ConditionBlock>> conditionBlocks = _modules
						.Where(keyValuePair => keyValuePair.Value is ConditionBlock block)
						.Select(keyValuePair => new KeyValuePair<string, ConditionBlock>(keyValuePair.Key, (ConditionBlock)keyValuePair.Value))
						.ToList();
					for (int i = 0; i < conditionBlocks.Count; i++)
					{
						bool currentCondition = false;
						var current = conditionBlocks[i];
						var next = i + 1 < conditionBlocks.Count ? conditionBlocks[i + 1] : default;
						int startIndex = _events.FindIndex(@event => @event.id == current.Key);
						int endIndex = _events.FindIndex(@event => @event.id == next.Key) is int index && index >= 0 ? index : _events.Count;
						for (int j = startIndex; j < endIndex; j++)
						{
							var @event = _events[j];
							var view = _views[j];
							var item = _items[j];
							if (current.Value.Type == EPlayerModuleType.ENDIF || (!(current.Value.Type == EPlayerModuleType.ELIF && groupHasTrueCondition) && TrueCondition(current.Value)))
							{
								currentCondition = true;
								bool viewIsActive = view.gameObject.activeSelf;
								item.activeSelf = true;
								if (!viewIsActive)
								{
									view.gameObject.SetActive(true);
									item.OnConditionActive?.Invoke();
								}
							}
							else
							{
								currentCondition = false;
								bool viewIsActive = view.gameObject.activeSelf;
								item.activeSelf = false;
								if (viewIsActive)
								{
									view.gameObject.SetActive(false);
								}
							}
						}
						if (currentCondition) groupHasTrueCondition = true;
					}
					return new BaseResponse()
					{
						StatusCode = StatusCode.OK
					};
				}
				catch (Exception ex)
				{
					Debug.LogError($"[ProcessingConditions] : {ex.Message}");
					return new BaseResponse()
					{
						StatusCode = StatusCode.Failed
					};
				}
			}
			private void ActionForQuestion(Question question, string value, List<string> IDs, string id)
			{
				question.P_Value = value;
				ProcessingConditions();
				HandleOnClick?.Invoke(IDs.Any() ? GetMaxIndex(IDs) + 1 : _events.FindIndex(@event => @event.id == id) + 1);
			}
			public IBaseResponse TryGetAction(string id, out UnityAction action)
			{
				try
				{
					action = null;
					if (_modules == null)
					{
						return new BaseResponse()
						{
							StatusCode = StatusCode.NotFound,
						};
					}
					foreach (var pair in _modules)
					{
						switch (pair.Value)
						{
							case Question question:
								if (question.P_Selections == null) break;
								var IDs = new List<string>();
								foreach (var selection in question.P_Selections)
								{
									IDs.Add(selection.Value);
									if (selection.Value == id)
									{
										IDs = IDs.Where(ID => ID != THIS_KEY_WORD).ToList();
										action = () => ActionForQuestion(question, selection.Key, IDs, id);
									}
								}
								break;
						}
					}
					return new BaseResponse()
					{
						StatusCode = action != null ? StatusCode.OK : StatusCode.NotFound,
					};
				}
				catch (Exception ex)
				{
					action = null;
					Debug.LogError(ex.Message);
					return new BaseResponse()
					{
						Description = $"[TryGetAction] : {ex.Message}",
						StatusCode = Response.StatusCode.Failed
					};
				}
			}
			public IBaseResponse TryGetActions(string id, out List<UnityAction> actions)
			{
				try
				{
					actions = null;
					if (_modules?.TryGetValue(id, out IModule module) == true)
					{
						actions ??= new();
						switch (module)
						{
							case Question question:
								if (question.P_Selections == null) break;
								var IDs = new List<string>();
								foreach (var selection in question.P_Selections)
								{
									if (selection.Value != THIS_KEY_WORD) IDs.Add(selection.Value);
									actions.Add(() => ActionForQuestion(question, selection.Key, IDs, id));
								}
								break;
						}
					}
					return new BaseResponse()
					{
						StatusCode = actions != null ? StatusCode.OK : StatusCode.NotFound,
					};
				}
				catch (Exception ex)
				{
					actions = null;
					Debug.LogError(ex.Message);
					return new BaseResponse()
					{
						Description = $"[TryGetActions] : {ex.Message}",
						StatusCode = Response.StatusCode.Failed
					};
				}
			}
			public IBaseResponse Clear()
			{
				try
				{
					_modules = null;
					_events = null;
					_items = null;
					_views = null;
					_entities = null;
					HandleOnClick = null;
					HandleSnapTo = null;
					return new BaseResponse()
					{
						StatusCode = StatusCode.OK
					};
				}
				catch (Exception ex)
				{
					Debug.LogError(ex.Message);
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