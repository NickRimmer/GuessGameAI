using FoolMeGame.Shared.Levels;
using FoolMeGame.Shared.Telegram;
namespace GuessTheWord.Business.Game.Actions;

[LevelAction("cancel", GameInfo.LevelName)]
public class CancelAction : ILevelAction
{
    private readonly LevelsManager _levelsManager;
    private readonly GuessTheWord.Business.Levels.Onboarding.CancelAction _cancelAction;

    public CancelAction(LevelsManager levelsManager, GuessTheWord.Business.Levels.Onboarding.CancelAction cancelAction)
    {
        _levelsManager = levelsManager ?? throw new ArgumentNullException(nameof(levelsManager));
        _cancelAction = cancelAction ?? throw new ArgumentNullException(nameof(cancelAction));
    }

    public async Task HandleAsync(TelegramCommand command)
    {
        // as an idea, we can ask confirmation before cancelling

        await _cancelAction.CancelRoomAsync(command.ChatId);
        _levelsManager.SetBaseLevel();
    }
}
