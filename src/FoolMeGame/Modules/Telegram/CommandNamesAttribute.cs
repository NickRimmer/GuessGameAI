namespace FoolMeGame.Modules.Telegram;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class CommandNamesAttribute : Attribute
{
    public CommandNamesAttribute(params string[] commands)
    {
        Commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    public IReadOnlyCollection<string> Commands { get; }
}
