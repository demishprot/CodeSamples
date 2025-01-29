using System.Collections.Generic;

namespace Samples
{
	namespace Services
	{
		namespace PlayerModules
		{
			public interface IModule
			{
				EPlayerModuleType Type { get; set; }
			}
			public interface IEntity
			{
				string CustomID { get; set; }
				List<KeyValuePair<string, string>> Actions { get; set; }
				string P_Module { get; set; }
				bool P_Ignore { get; set; }
			}
			public class Entity : IEntity
			{
				public string CustomID { get; set; }
				public List<KeyValuePair<string, string>> Actions { get; set; }
				public string P_Module {  get; set; }
				public bool P_Ignore { get; set; }
			}
			public class Module : IModule
			{
				public EPlayerModuleType Type { get; set; }
			}
			internal enum EQuestionType
			{
				START,
				END,
				STANDALONE
			}
			public class Question : Module
			{
				internal string Id { get; set; }
				internal List<KeyValuePair<string, string>> P_Selections { get; set; }
				internal string P_Value { get; set; }
			}
			public class ConditionBlock : Module
			{
				internal List<KeyValuePair<string, string>> Conditions { get; set; }
			}
		}

	}

}