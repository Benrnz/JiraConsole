namespace BensJiraConsole;

public interface ISlackClient
{
    Task FindAllChannels(string partialChannelName);
}
