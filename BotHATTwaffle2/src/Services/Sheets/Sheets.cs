using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BotHATTwaffle2.src.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util.Store;

namespace BotHATTwaffle2.Services.Sheets
{
    public class Sheets
    {
        private readonly SheetsService _sheets;
        private readonly DataService _dataService;
        public Sheets(DataService data)
        {
            _dataService = data;
            Console.Write("Getting or checking Sheets OAuth Credentials... ");
            _sheets = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = GetSheetsCredentials(),
                ApplicationName = "BotHATTwaffle 2",
            });
            Console.WriteLine("Done!");
        }

        private UserCredential GetSheetsCredentials()
        {
            using (var stream = new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
            {
                return GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    new[] { SheetsService.Scope.Spreadsheets },
                    "user",
                    CancellationToken.None,
                    new FileDataStore(".credentials/sheets.json")).Result;
            }
        }

        /// <summary>
        /// Gets all test events from the queue
        /// </summary>
        /// <returns>List of events from the google sheet</returns>
        public async Task<List<PlaytestRequest>> GetTestQueueEvents()
        {
            var list = new List<PlaytestRequest>();

            // Define request parameters.
            String spreadsheetId = _dataService.RSettings.ProgramSettings.SheetID;

            //Grab 50 rows, basically all of them.
            String range = "Playtesting Scheduling Queue";

            SpreadsheetsResource.ValuesResource.GetRequest request = _sheets.Spreadsheets.Values.Get(spreadsheetId, range);

            ValueRange response = await request.ExecuteAsync();
            var values = response.Values;

            //Lets parse things into objects. Skip the header
            foreach (var row in values.Skip(1))
            {
                list.Add(new PlaytestRequest
                {
                    Timestamp = DateTime.TryParse(row[0].ToString(), out var ts) ? ts : DateTime.UnixEpoch,
                    TestDate = DateTime.TryParse(row[1].ToString(), out var td) ? td : DateTime.UnixEpoch,
                    Name = row[2].ToString(),
                    Email = row[3].ToString(),
                    AdditionalEmail = row[4].ToString().Split(',').Select(x => x.Trim()).ToList(),
                    MapName = row[5].ToString(),
                    CreatorsDiscord = row[6].ToString().Split(',').Select(x => x.Trim()).ToList(),
                    ImgurAlbum = row[7].ToString(),
                    WorkshopURL = row[8].ToString(),
                    TestType = row[9].ToString(),
                    TestGoals = row[10].ToString(),
                    Spawns = int.TryParse(row[11].ToString(), out var spawns) ? spawns : 0,
                    RadarTested = row[12].ToString().Contains("Yes"),
                    PreviousTestDate = DateTime.TryParse(row[13].ToString(), out var ptd) ? ptd : DateTime.UnixEpoch,
                    Chickens = row[14].ToString(),
                    OtherModCanRun = row[15].ToString().Contains("Yes"),

                    //If the submitter left ideal server blank, the array for that row does not even contain an index.
                    Preferredserver = row.Count < 17 ? "No preference" : row[16].ToString()
                });
            }

            return list;
        }

        /// <summary>
        /// Removes a request based on the submitted timestamp
        /// </summary>
        /// <param name="timeStamp">Timestamp of row to remove</param>
        /// <returns>True if successful, fase otherwise</returns>
        public async Task<bool> RemoveRequest(DateTime timeStamp)
        {
            int row = 0;
            bool found = false;
            var currentQueue = await GetTestQueueEvents();

            for (row = 0; row < currentQueue.Count; row++)
            {
                //Set row int to the found row
                if (currentQueue[row].Timestamp == timeStamp)
                {
                    //Adjust to get correct row number. +1 for missing header from GetTestQueueEvents
                    row = row + 1;
                    found = true;
                    break;
                }
            }

            try
            {
                var request = new Request
                {
                    DeleteDimension = new DeleteDimensionRequest
                    {
                        Range = new DimensionRange
                        {
                            SheetId = 2107922079, //Sheet ID, hard coded.
                            Dimension = "ROWS",
                            StartIndex = row, //Deletes this row
                            EndIndex = row + 1
                        }
                    }
                };
                
                //Create the batch request, and populate it
                var deleteRequest = new BatchUpdateSpreadsheetRequest {Requests = new List<Request>(new[]{ request })};

                //Create the batch.
                var delete = _sheets.Spreadsheets.BatchUpdate(deleteRequest, _dataService.RSettings.ProgramSettings.SheetID);

                //Execute
                var response = await delete.ExecuteAsync();
            }
            catch
            {
                return false;
            }

            return found;
        }
    }
}
