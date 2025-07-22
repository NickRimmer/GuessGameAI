using System.Diagnostics.CodeAnalysis;
using FoolMeGame.Shared.Data;
using FoolMeGame.Shared.Data.Models;
namespace FoolMeGame.Shared.Levels;

public class LevelsProvider
{
    private readonly DbStorage _db;
    private readonly Dictionary<string, IReadOnlyCollection<LevelActionInfo>> _actions;

    public LevelsProvider(IEnumerable<ILevelAction> levelActions, DbStorage db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _actions = BuildActions(levelActions);
    }

    public bool HasLevel(string levelName) =>
        _actions.ContainsKey(levelName);

    public bool TryGet(long chatId, string actionName, [NotNullWhen(true)] out ILevelAction? levelAction, [NotNullWhen(true)] out string? foundActionName)
    {
        var chat = _db.ChatSystemSettings.FindById(chatId) ??
            new ChatSystemSettings { ChatId = chatId };

        if (!_actions.TryGetValue(chat.CurrentLevelName ?? string.Empty, out var actions))
            throw new ArgumentException($"Level '{chat.CurrentLevelName}' does not exist.", nameof(chat.CurrentLevelName));

        var action = actions.FirstOrDefault(x => x.ActionName.Equals(actionName, StringComparison.OrdinalIgnoreCase))
            ?? _actions[LevelActionAttribute.OnAllLevels].FirstOrDefault(x => x.ActionName.Equals(actionName, StringComparison.OrdinalIgnoreCase));

        levelAction = action?.LevelAction;
        foundActionName = action?.ActionName;

        return levelAction != null;
    }

    private static Dictionary<string, IReadOnlyCollection<LevelActionInfo>> BuildActions(IEnumerable<ILevelAction> actions)
    {
        return actions
            .Select(x => new {
                Level = x,
                Attributes = x.GetType().GetCustomAttributes(typeof(LevelActionAttribute), false)
                    .OfType<LevelActionAttribute>()
                    .ToList(),
            })
            .Where(x => x.Attributes.Count > 0)
            .SelectMany(x => x.Attributes.Select(a => new {
                x.Level,
                a.LevelName,
                a.ActionName,
            }))
            .GroupBy(x => x.LevelName)
            .ToDictionary(x => x.Key, x => (IReadOnlyCollection<LevelActionInfo>) x.Select(action => new LevelActionInfo(action.Level, action.ActionName)).ToList());
    }

    private record LevelActionInfo(ILevelAction LevelAction, string ActionName);
}
