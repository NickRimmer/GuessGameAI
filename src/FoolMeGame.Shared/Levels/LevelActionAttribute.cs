namespace FoolMeGame.Shared.Levels;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public class LevelActionAttribute : Attribute
{
    public const string OnAllLevels = "*";
    public const string OnBaseLevel = "";
    public const string TextCommand = "__text__";

    public string LevelName { get; }
    public string ActionName { get; }

    public LevelActionAttribute(string actionName, string levelName = OnAllLevels)
    {
        ActionName = actionName;
        LevelName = levelName;
    }
}
