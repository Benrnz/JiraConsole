namespace BensJiraConsole;

public record AgileSprint(
    int Id,
    string State,
    string Name,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    int OriginBoardId,
    string Goal);
