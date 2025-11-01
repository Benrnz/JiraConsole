namespace BensJiraConsole;

public class JiraGreenhopperClient
{
    private const string BaseUrl = "https://javlnsupport.atlassian.net/rest/greenhopper/1.0/";

    public async Task<string> GetSprintReportAsync(int sprintBoardId, int sprintId)
    {
        var url = $"{BaseUrl}rapid/charts/sprintreport?rapidViewId={sprintBoardId}&sprintId={sprintId}";

        var response = await App.Http.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("ERROR calling Greenhopper sprint report API!");
            Console.WriteLine(response.StatusCode);
            Console.WriteLine(response.ReasonPhrase);
            Console.WriteLine(url);
        }

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        return responseJson;
    }
}
