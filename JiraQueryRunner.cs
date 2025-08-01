namespace BensJiraConsole;

public class JiraQueryRunner
{
    public async Task<List<JiraIssue>> SearchJiraIssuesWithJqlAsync(string jql, string[] fields)
    {
        var client = new JiraApiClient();
        var responseJson = await client.PostSearchJqlAsync(jql, fields);
        var mapper = new JiraIssueMapper();
        var results = mapper.MapToJiraIssue(responseJson);
        var totalResults = results.Count;
        while (!mapper.WasLastPage)
        {
            Console.WriteLine($"    {totalResults} results fetched. Fetching next page of results...");
            responseJson = await client.PostSearchJqlAsync(jql, fields, mapper.NextPageToken);
            var moreResults = mapper.MapToJiraIssue(responseJson);
            totalResults += moreResults.Count;
            results.AddRange(moreResults);
        }

        if (totalResults > 100)
        {
            Console.WriteLine($"    {totalResults} total results fetched.");
        }
        return results;
    }

    public async Task<List<JiraIssueWithPmPlan>> SearchJiraIssueLinkedWithPmPlanAsync(string jql, string[] fields)
    {
        var client = new JiraApiClient();
        var responseJson = await client.PostSearchJqlAsync(jql, fields);
        var mapper = new JiraIssueMapper();
        var results = mapper.MapToJiraIssue(
            responseJson,
            (a, b, c, d, e, f) => new JiraIssueWithPmPlan(a, b, c, d, e, f));
        var totalResults = results.Count;
        while (!mapper.WasLastPage)
        {
            Console.WriteLine($"    {totalResults} results fetched. Fetching next page of results...");
            responseJson = await client.PostSearchJqlAsync(jql, fields, mapper.NextPageToken);
            var moreResults = mapper.MapToJiraIssue(
                responseJson,
                (a, b, c, d, e, f) => new JiraIssueWithPmPlan(a, b, c, d, e, f));
            totalResults += moreResults.Count;
            results.AddRange(moreResults);
        }

        if (totalResults > 100)
        {
            Console.WriteLine($"    {totalResults} total results fetched.");
        }
        return results.Cast<JiraIssueWithPmPlan>().ToList();
    }

    public async Task<List<JiraPmPlan>> SearchJiraIdeaWithJqlAsync(string jql, string[] fields)
    {
        var client = new JiraApiClient();
        var responseJson = await client.PostSearchJqlAsync(jql, fields);
        var mapper = new JiraIssueMapper();
        var results = mapper.MapToPmPlan(responseJson);
        var totalResults = results.Count;
        while (!mapper.WasLastPage)
        {
            Console.WriteLine($"    {totalResults} results fetched. Fetching next page of results...");
            responseJson = await client.PostSearchJqlAsync(jql, fields, mapper.NextPageToken);
            var moreResults = mapper.MapToPmPlan(responseJson);
            totalResults += moreResults.Count;
            results.AddRange(moreResults);
        }

        if (totalResults > 100)
        {
            Console.WriteLine($"    {totalResults} total results fetched.");
        }
        return results;
    }
}
