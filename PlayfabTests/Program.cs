// See https://aka.ms/new-console-template for more information
using PlayFab;

var _loginRequest = new PlayFab.ClientModels.LoginWithCustomIDRequest
{
    CreateAccount = true,
    CustomId = $"RankExtractor-123456"

};

//Login
PlayFabSettings.staticSettings.TitleId = "12D56";
var result = await PlayFabClientAPI.LoginWithCustomIDAsync(_loginRequest);
var gameRequest = new PlayFab.ClientModels.CurrentGamesRequest
{
    BuildVersion = "dummy",
    Region = PlayFab.ClientModels.Region.EUWest,
    TagFilter = new PlayFab.ClientModels.CollectionFilter()
    {
        Includes = new List<PlayFab.ClientModels.Container_Dictionary_String_String>()
                        {
                            new PlayFab.ClientModels.Container_Dictionary_String_String{ Data = new Dictionary<string, string>(){{"Version", "502609227" } }}
                        }
    }
};


//GetServer 
var serversGlobalList = await PlayFabClientAPI.GetCurrentGamesAsync(gameRequest);

var serversFilteredList = serversGlobalList.Result.Games.Where(w => w.Tags["IsOfficial"] is "true" &&
                                                       w.Tags["ServerName"].Contains("South America") &&
                                                       w.Tags["GameMode"] is "Frontline" or "Invasion" &&
                                                       w.Tags["Players"].Split(',', StringSplitOptions.RemoveEmptyEntries).Length > 0).OrderBy(w => w.Tags["QueueName"]).ThenBy(w => w.Tags["ServerName"]);
//Get Players
foreach (var server in serversFilteredList)
{
    var playersPlayFabIds = server.Tags["Players"].Split(',', StringSplitOptions.RemoveEmptyEntries);
    var rankData = await GetRanks(playersPlayFabIds);
    //Get Players Stats
    foreach(var player in rankData)
    {
        Console.WriteLine($"Playfab = {player.Result.PlayFabId}");
        Console.WriteLine($"AccountInfo = {player.Result.InfoResultPayload.UserReadOnlyData.First().Value.Value}");
        foreach (var stat in player.Result.InfoResultPayload.PlayerStatistics)
        {
             Console.WriteLine($"\t Stat = {stat.StatisticName} - {stat.Value}");           
        }
    }
}





Console.WriteLine("Finished");
Console.ReadLine();


async Task<List<PlayFabResult<PlayFab.ClientModels.GetPlayerCombinedInfoResult>>> GetRanks(IEnumerable<string> playersPlayFabIds)
{
    if (!playersPlayFabIds.Any())
        return new List<PlayFabResult<PlayFab.ClientModels.GetPlayerCombinedInfoResult>>();

    var toCheck = new List<string>(playersPlayFabIds);
    List<Task<PlayFab.PlayFabResult<PlayFab.ClientModels.GetPlayerCombinedInfoResult>>> CombinedInfoTasks = new();

    var pageSize = 25;
    var pullSize = pageSize;
    var callCount = toCheck.Count / pageSize;

    var infoRequests = new List<PlayFab.ProfilesModels.EntityProfileBody>();
    if (toCheck.Count > 0)
    {
        for (int page = 0; page <= callCount; page++)
        {
            //Last items to get
            if (page == callCount)
            {
                pullSize = toCheck.Count % pageSize;
            }
            var start = page * pageSize;
            var end = (page * pageSize) + pullSize;


            var playersInfo = await PlayFabProfilesAPI.GetProfilesAsync(new PlayFab.ProfilesModels.GetEntityProfilesRequest()
            {
                Entities = toCheck.ToArray()[start..end].Select(w => new PlayFab.ProfilesModels.EntityKey() { Id = w, Type = "title_player_account" }).ToList()
            });
            if (playersInfo.Result is not null)
            {
                infoRequests.AddRange(playersInfo.Result.Profiles);
            }
        }
    }


    foreach (var entityProfile in infoRequests)
    {

        if (entityProfile != null)
        {
            CombinedInfoTasks.Add(PlayFabClientAPI.GetPlayerCombinedInfoAsync(new PlayFab.ClientModels.GetPlayerCombinedInfoRequest()
            {
                InfoRequestParameters = new PlayFab.ClientModels.GetPlayerCombinedInfoRequestParams()
                {
                    GetPlayerProfile = true,
                    GetPlayerStatistics = true,
                    GetUserAccountInfo = true,
                    GetUserData = true,
                    GetUserReadOnlyData = true,
                    GetUserVirtualCurrency = false
                },
                PlayFabId = entityProfile.Lineage.MasterPlayerAccountId

            }));


        }
    }
    Task.WaitAll(CombinedInfoTasks.ToArray());
    List<PlayFabResult<PlayFab.ClientModels.GetPlayerCombinedInfoResult>> finalResults = new();
    foreach (var result in CombinedInfoTasks)
    {
        PlayFabResult<PlayFab.ClientModels.GetPlayerCombinedInfoResult> playerStatsData = await result;
        finalResults.Add(playerStatsData);

    }
    return finalResults;

}