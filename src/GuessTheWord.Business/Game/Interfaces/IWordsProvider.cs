namespace GuessTheWord.Business.Game.Interfaces;

public interface IWordsProvider
{
    Task<string> GetNewWordAsync(string language);
}
