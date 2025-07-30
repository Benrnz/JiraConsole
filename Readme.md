JIRA CONSOLE
============
Intent: To provide a quick commandline tool to run known JQL and other established search tasks against the Jira API.
The application provides a structure to quickly add a new search task by implementing the `IJiraExportTask` interface. The application will discover all implementations and provide a commandline
option to run one task.

SETUP
-----
Download the .NET 9 SDK from https://dotnet.microsoft.com/en-us/download/dotnet/9.0

Or Run
`winget install Microsoft.DotNet.SDK.9`

Clone the code
`git clone https://github.com/Benrnz/JiraConsole.git`

Add a `Secrets.cs` file using the template provided in the code `Secrets.rename-me.cs`
You will will need a Jira API Token and the email address you use to authenticate with Jira.

Build the code

`cd .\JiraConsole`

`dotnet build`

Run the code

`dotnet run`
