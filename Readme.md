JIRA CONSOLE
============
Intent: To provide a quick commandline tool to run known JQL and other established search tasks against the Jira API.
In addition, provide a means to automate running of these tasks.
The application provides a structure to quickly add a new search task by implementing the `IJiraExportTask` interface. The application will discover all implementations and provide a commandline
option to run one task.

SETUP
-----
1. Download the .NET 9 SDK from https://dotnet.microsoft.com/en-us/download/dotnet/9.0
    1. Or Run `winget install Microsoft.DotNet.SDK.9`

2. Clone the code
`git clone https://github.com/Benrnz/JiraConsole.git`

3. Obtain the Jira API Token from your Atlassian account. Follow the instructions at https://id.atlassian.com/manage-profile/security

4. Add a `Secrets.cs` file using the template provided in the code `Secrets.rename-me.cs`. The file must be called `Secrets.cs` and placed in the same directory as the `Program.cs` file.
This file will contain your Jira API Token and email address used for authentication. The `git.ignore` file will ensure this file is not committed to the repository.

5. Build the code:
   1. `cd .\JiraConsole`
   2. `dotnet build`

6. Run the code:
`dotnet run`

Note: for subsequent runs, you can simply run the .\BensJiraConsole\bin\Debug\net9.0\BensJiraConsole.exe directly, or use the `dotnet run` command again.

Output Files
------------
Files will be exported to `C:\Downloads\JiraExports`

Automatically Run a Task without User Input
-------------------------------------------
Obtain the task key by running the application with no arguments. The task key will be displayed in the list of tasks.
For example
```
Jira Console Exporter tool.  Select a task to execute, or 'exit' to quit.
1: Calculate Overall PM Plan Release Burn Up (PMPLAN_RBURNUP)
```
For the first task the key is `PMPLAN_RBURNUP`.

Run the task with the key as an argument:
``.\BensJiraConsole\bin\Debug\net9.0\BensJiraConsole.exe PMPLAN_RBURNUP``
