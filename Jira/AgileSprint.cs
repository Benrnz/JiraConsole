namespace BensJiraConsole.Jira;

public record AgileSprint(
    int Id,
    string State,
    string Name,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    int BoardId,
    string Goal,
    DateTimeOffset CompleteDate);
