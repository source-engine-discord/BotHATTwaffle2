using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services.Calendar;
using BotHATTwaffle2.Services.Steam;
using BotHATTwaffle2.Util;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Google.Apis.Calendar.v3.Data;

namespace BotHATTwaffle2.Services.Playtesting
{
    public class RequestBuilder
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.Red;

        //The values that are used by the wizard to build the test. Also used for editing the event.
        private readonly string[] _arrayValues =
        {
            "Date", "Emails", "Game", "MapName", "Discord", "Imgur", "Workshop", "Type", "Description", "Spawns",
            "PreviousTest", "Server"
        };

        private readonly GoogleCalendar _calendar;
        private readonly SocketCommandContext _context;
        private readonly DataService _dataService;
        private readonly InteractiveService _interactive;
        private readonly bool _isDms;
        private readonly LogHandler _log;
        private readonly PlaytestService _playtestService;

        //Help text used for the wizard / Updating information
        private readonly string[] _wizardText =
        {
            "Enter the desired time for the test in the `CT timezone`. Ideal times are between `12:00-18:00 CT`. Required format: `MM/DD/YYYY HH:MM`\n" +
            "Example: `2/17/2019 14:00`",
            "Enter email addresses of the creators in a comma separated format.\n" +
            "Example: `tophattwaffle@gmail.com, doug@tophattwaffle.com`",
            "Please type `CSGO` or `TF2` to specify what game this test is for.",
            "Enter the map name.\n" +
            "Example: `Facade 2`",
            "To add yourself as the creator, mention yourself.\n" +
            "Please provide creators in a comma separated list.\n <https://support.discordapp.com/hc/en-us/articles/206346498-Where-can-I-find-my-User-Server-Message-ID->\n" +
            "Example: `111939018500890624, 560667510580576266`",
            "Please provide the imgur album containing your level images.\n" +
            "Example: `https://imgur.com/a/2wJaI`",
            "Please provide the workshop URL for your level.\n" +
            "Example: `https://steamcommunity.com/sharedfiles/filedetails/?id=267340686`",
            "Please type `Casual` or `Competitive` for the type of test.\n" +
            "Example: `Casual`",
            "Please type what you'd like to get our of this playtest.\n" +
            "Example: `Testing timings along with newly added cover. Hope to flesh out any major issues.`",
            "Please type the number of spawns for each team in your level.\n" +
            "Example: `8`",
            "If you have one, please enter the previous date you playtested this level on. If you don't have one, type `none`. Required format: `MM/DD/YYYY HH:MM`\n" +
            "Example: `2/17/2019 14:00`",
            "Please type the server ID that you'd like your server to be on. Type `none` for no preference. Type `list` to display valid servers.\n" +
            "Example: `can`"
        };

        private readonly Workshop _workshop;
        private bool _dateChecked = true;
        private IUserMessage _embedMessage;

        private IUserMessage _instructionsMessage;

        private IEnumerable<PlaytestRequest> _otherRequests;
        private bool _requireAbort;
        private Events _scheduledTests;
        private PlaytestRequest _testRequest;
        private SocketMessage _userMessage;

        public RequestBuilder(SocketCommandContext context, InteractiveService interactive, DataService data,
            LogHandler log,
            GoogleCalendar calendar, PlaytestService playtestService)
        {
            _context = context;
            _interactive = interactive;
            _dataService = data;
            _log = log;
            _calendar = calendar;
            _playtestService = playtestService;

            //Make the test object
            _testRequest = new PlaytestRequest();
            _isDms = context.IsPrivate;
            _workshop = new Workshop();
            _otherRequests = null;
            _scheduledTests = null;
        }

        /// <summary>
        ///     Entry point for moderation staff to work with playtest requests.
        /// </summary>
        /// <param name="display">The embed message to attach to for displaying updates</param>
        /// <returns></returns>
        public async Task SchedulePlaytestAsync(IUserMessage display)
        {
            var playtestRequests = DatabaseUtil.GetAllPlaytestRequests().ToList();
            _embedMessage = display;

            //If there are no requests, don't enter interactive mode. Just return
            if (playtestRequests.Count == 0)
                return;

            await _embedMessage.ModifyAsync(x => x.Content = "Type `exit` to abort at any time.");
            _instructionsMessage = await _context.Channel.SendMessageAsync("Type the ID of the playtest to schedule.");

            //Finds the correct playtest request to work with.
            var id = 0;
            while (true)
            {
                _userMessage = await _interactive.NextMessageAsync(_context);

                if (_userMessage == null ||
                    _userMessage.Content.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    await CancelRequest();
                    return;
                }

                if (int.TryParse(_userMessage.Content, out id) && id >= 0 && id < playtestRequests.Count)
                    break;

                await _userMessage.DeleteAsync();
            }

            //Set the request based on the chosen request.
            _testRequest = playtestRequests[id];

            await ConfirmSchedule();
        }

        /// <summary>
        ///     Allows moderator to update or delete a playtest request. If confirmed, the event is added to the calendar.
        /// </summary>
        /// <returns></returns>
        private async Task ConfirmSchedule()
        {
            while (true)
            {
                //Variables used later.
                int index = -1;
                bool isValid = false;

                await Display(
                    "Type the ID of the field you want to edit or type `Schedule` to schedule the playtest.\n" +
                    "Type `delete` to delete this request completely.\n" +
                    "Type `save` to save the request and exit.");

                //User's input
                _userMessage = await _interactive.NextMessageAsync(_context);

                //We want to schedule the event
                if (_userMessage.Content.Equals("schedule", StringComparison.OrdinalIgnoreCase))
                {
                    //Server cannot be none when scheduling. Require it to be valid.
                    if (_testRequest.Preferredserver == "No preference")
                    {
                        index = 11;
                        isValid = true;
                    }
                    else
                        break;
                }

                if (_userMessage.Content.Equals("save", StringComparison.OrdinalIgnoreCase))
                {
                    var saveResult = DatabaseUtil.UpdatePlaytestRequests(_testRequest);
                    var saveEmbed = RebuildEmbed();
                    if (saveResult)
                    {
                        saveEmbed.WithColor(55, 55, 165);
                        await _instructionsMessage.ModifyAsync(x => x.Content = "Request saved!");
                        await _embedMessage.ModifyAsync(x => x.Embed = saveEmbed.Build());
                    }
                    else
                    {
                        saveEmbed.WithColor(165, 55, 55);
                        await _instructionsMessage.ModifyAsync(x => x.Content = "There was an issue saving the request");
                        await _embedMessage.ModifyAsync(x => x.Embed = saveEmbed.Build());
                    }
                    return;
                }

                //Exiting
                    if (_userMessage == null ||
                    _userMessage.Content.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    await CancelRequest();
                    return;
                }

                //Deleting test
                if (_userMessage.Content.Equals("delete", StringComparison.OrdinalIgnoreCase))
                {
                    DatabaseUtil.RemovePlaytestRequest(_testRequest);
                    await _embedMessage.ModifyAsync(x => x.Embed = RebuildEmbed().WithColor(25, 25, 25).Build());
                    await _instructionsMessage.ModifyAsync(x => x.Content = "Request Deleted!");
                    await _userMessage.DeleteAsync();
                    return;
                }

                //Get the relevant index from the user message only if we aren't getting here from a forced server schedule.
                if(!isValid)
                    isValid = int.TryParse(_userMessage.Content, out index);
                
                //Valid number, and between the valid indexes?
                if (isValid && index >= 0 && index <= _arrayValues.Length)
                {
                    //Validate based on the index.
                    await Display(_wizardText[index]);
                    _userMessage = await _interactive.NextMessageAsync(_context);
                    if (_userMessage == null ||
                        _userMessage.Content.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    {
                        await CancelRequest();
                        return;
                    }

                    //Loop until valid, or allow an exit.
                    while (!await ParseTestInformation(_arrayValues[index], _userMessage.Content))
                    {
                        //Invalid, let's try again.
                        _userMessage = await _interactive.NextMessageAsync(_context);

                        if (_userMessage.Content.Equals("exit", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }

                        //No reply - fully exit
                        if (_userMessage == null)
                        {
                            await CancelRequest();
                            return;
                        }
                    }
                }
            }

            //Scheduling
            if(!_context.IsPrivate)
                await _userMessage.DeleteAsync();

            var finalEmbed = RebuildEmbed();

            var eventAdded = await _calendar.AddTestEvent(_testRequest, _context.User);

            //Try added to calendar, if true we can move forward with alerting user of the schedule.
            if (eventAdded)
            {
                //Remove the test from the DB.
                DatabaseUtil.RemovePlaytestRequest(_testRequest);

                //Update display
                finalEmbed.WithColor(new Color(240, 240, 240));
                await _instructionsMessage.ModifyAsync(x => x.Content = "Test Scheduled!");
                await _embedMessage.ModifyAsync(x => x.Embed = finalEmbed.Build());

                //Build the string to mention all creators on the event.
                string mentions = null;
                _testRequest.CreatorsDiscord.ForEach(x => mentions += $"{_dataService.GetSocketGuildUser(x).Mention} ");

                await _dataService.CSGOTestingChannel.SendMessageAsync(
                    $"{mentions.Trim()} your playtest has been scheduled for `{_testRequest.TestDate}` (CT Timezone)");

                await _log.LogMessage($"{_context.User} has scheduled a playtest!\n{_testRequest}", color: LOG_COLOR);

                //Workshop embed.
                var ws = new Workshop();
                var wbEmbed = await ws.HandleWorkshopEmbeds(_context.Message, _dataService,
                    $"[Map Images]({_testRequest.ImgurAlbum}) | [Playtesting Information](https://www.tophattwaffle.com/playtesting)",
                    _testRequest.TestType, GeneralUtil.GetWorkshopIdFromFqdn(_testRequest.WorkshopURL));

                if(wbEmbed != null)
                    await _dataService.CSGOTestingChannel.SendMessageAsync(embed: wbEmbed.Build());
                  
                return;
            }

            //Failed to add to calendar.
            finalEmbed.WithColor(new Color(20, 20, 20));
            await _embedMessage.ModifyAsync(x => x.Embed = finalEmbed.Build());
            await _instructionsMessage.ModifyAsync(x => x.Content =
                "An error occured working with the Google APIs, consult the logs.\n" +
                "The playtest event may still have been created.");
        }

        /// <summary>
        ///     Used to confirm if a user wants to submit their playtest request, or make further changes.
        /// </summary>
        /// <returns></returns>
        private async Task ConfirmRequest()
        {
            while (true)
            {
                bool isValid = false;
                int index = -1;
                await Display(
                    "Type the ID of the field you want to edit or type `Submit` to submit your playtest request.");
                _userMessage = await _interactive.NextMessageAsync(_context);
                if (_userMessage.Content.Equals("submit", StringComparison.OrdinalIgnoreCase))
                {
                    if(_testRequest.Preferredserver != "No preference" && !_testRequest.Game.Equals(DatabaseUtil.GetTestServer(_testRequest.Preferredserver).Game, StringComparison.OrdinalIgnoreCase))
                    {
                        index = 11;
                        isValid = true;
                    }
                    else
                        break;
                }

                if (_userMessage == null ||
                    _userMessage.Content.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    await CancelRequest();
                    return;
                }

                if (!isValid)
                    isValid = int.TryParse(_userMessage.Content, out index);

                if (isValid && index >= 0 && index <= _arrayValues.Length)
                {
                    bool valid = false;
                    await Display(_wizardText[index]);
                    while (!valid)
                    {
                        _userMessage = await _interactive.NextMessageAsync(_context);
                        valid = await ParseTestInformation(_arrayValues[index], _userMessage.Content);

                        if (_userMessage == null ||
                            _userMessage.Content.Equals("exit", StringComparison.OrdinalIgnoreCase))
                        {
                            await CancelRequest();
                            return;
                        }
                    }
                }
            }

            //Avoid rate limiting
            await Task.Delay(1000);
            //Attempt to store request in the DB
            if (DatabaseUtil.AddPlaytestRequests(_testRequest))
            {
                await _instructionsMessage.ModifyAsync(x =>
                    x.Content =
                        $"Request Submitted! If you have any feedback or suggestions of the scheduling process, please send {_dataService.AlertUser} a message!");
                await _embedMessage.ModifyAsync(x => x.Embed = RebuildEmbed().WithColor(240, 240, 240).Build());

                //Give them the quick request if they want to re-test.
                try
                {
                    //Try DM
                    await _context.User.SendMessageAsync(
                        $"Here is a quick request for your test to quickly submit again if something happens with this test.```>Request {_testRequest}```");
                }
                catch
                {
                    //Just reply instead
                    await _context.Channel.SendMessageAsync(
                        $"Here is a quick request for your test to quickly submit again if something happens with this test.```>Request {_testRequest}```");
                }
                
                var schedule = await _playtestService.GetUpcomingEvents(true, false);

                string creators = "";
                foreach (var c in _testRequest.CreatorsDiscord)
                {
                    creators += _dataService.GetSocketGuildUser(c).Mention + " ";
                }

                //Set as alert user just in case.
                string testingAdminMention = _dataService.AlertUser.Mention;
                SocketTextChannel mentionChannel = _dataService.AdminBotsChannel;

                if (_testRequest.Game.Equals("csgo", StringComparison.OrdinalIgnoreCase))
                {
                    testingAdminMention = _dataService.CSGOPlaytestAdmin.Mention;
                    mentionChannel = _dataService.CSGOTestingChannel;
                }
                else if (_testRequest.Game.Equals("tf2",StringComparison.OrdinalIgnoreCase))
                {
                    testingAdminMention = _dataService.TF2PlaytestAdmin.Mention;
                    mentionChannel = _dataService.TF2TestingChannel;
                }

                await _dataService.AdminChannel.SendMessageAsync(
                    $"{testingAdminMention} a new playtest request for `{_testRequest.MapName}` has been submitted by {creators.Trim()}!",
                    embed: schedule.Build());

                //Users to mention.
                string mentions = null;
                _testRequest.CreatorsDiscord.ForEach(x => mentions += $"{_dataService.GetSocketGuildUser(x).Mention} ");

                await mentionChannel.SendMessageAsync($"{mentions} has submitted a playtest request!",
                    embed:
                    (await _workshop.HandleWorkshopEmbeds(_context.Message, _dataService,
                        $"[Map Images]({_testRequest.ImgurAlbum}) | [Playtesting Information](https://www.tophattwaffle.com/playtesting)",
                        _testRequest.TestType, GeneralUtil.GetWorkshopIdFromFqdn(_testRequest.WorkshopURL)))
                    .Build());

                await _log.LogMessage($"{_context.User} has requested a playtest!\n{_testRequest}", color: LOG_COLOR);
            }
            else
            {
                //Failed adding to the database. Give them a quick request to attempt resubmission later.
                await _instructionsMessage.ModifyAsync(x =>
                    x.Content = "An error occured creating the playtest request!");
                await _embedMessage.ModifyAsync(x => x.Embed = RebuildEmbed().WithColor(25, 25, 25).Build());

                //Give them the quick request if they want to re-test.
                try
                {
                    //Try DM
                    await _context.User.SendMessageAsync(
                        $"Here is a quick request for your test to quickly submit again later.```>Request { _testRequest}```");
                }
                catch
                {
                    //Just reply instead
                    await _context.Channel.SendMessageAsync(
                        $"Here is a quick request for your test to quickly submit again later.```>Request { _testRequest}```");
                }
            }
        }

        /// <summary>
        ///     Validates a specific playtest element as valid.
        /// </summary>
        /// <param name="type">Type of data to validate</param>
        /// <param name="data">Data to validate</param>
        /// <returns></returns>
        private async Task ValidateInformationLoop(string type, string data)
        {
            //Try to parse the data, if failed collect new data.
            while (!await ParseTestInformation(type, data))
            {
                //If data is utterly and completely un-usable, back out completely.
                if (_requireAbort)
                {
                    await CancelRequest();
                    await _context.Channel.SendMessageAsync("Unable to parse data. Consult the help documents.");
                    return;
                }

                _userMessage = await _interactive.NextMessageAsync(_context);
                data = _userMessage.Content;

                if (_userMessage == null ||
                    _userMessage.Content.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    await CancelRequest();
                    return;
                }
            }
        }

        /// <summary>
        ///     Entry point for users requesting a playtest using interactive mode.
        /// </summary>
        /// <returns></returns>
        public async Task BuildPlaytestRequestWizard()
        {
            //Make sure users have at least been to the website to consume the playtest requirements.
            while (true)
            {
                await Display(
                    "Please look at the calendar above. Green items are scheduled, blue are already requested. Then try to pick a date that isn't already taken." +
                    "\nTo confirm that you've read the testing requirements, click the line above the image, look for Ido's demands and follow the instructions.");
                _userMessage = await _interactive.NextMessageAsync(_context);
                if (_userMessage == null ||
                    _userMessage.Content.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    await CancelRequest();
                    return;
                }

                //They got the message correct!
                if (_userMessage.Content.Equals("I have read and understand the playtesting requirements",
                    StringComparison.OrdinalIgnoreCase))
                    break;
            }

            //Start the interactive build session.
            //_arrayValues and _wizardText arrays are used to assist in building the request.
            for (var i = 0; i < _arrayValues.Length; i++)
            {
                await Display(_wizardText[i]);
                _userMessage = await _interactive.NextMessageAsync(_context);
                if (_userMessage == null ||
                    _userMessage.Content.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    await CancelRequest();
                    return;
                }

                while (!await ParseTestInformation(_arrayValues[i], _userMessage.Content))
                {
                    //Invalid, let's try again.
                    _userMessage = await _interactive.NextMessageAsync(_context);

                    if (_userMessage == null ||
                        _userMessage.Content.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    {
                        await CancelRequest();
                        return;
                    }
                }
            }

            //Everything is valid - move onto confirmation
            await ConfirmRequest();
        }

        /// <summary>
        ///     Entry point for someone submitting a playtest request from the bulk request template
        /// </summary>
        /// <param name="input">Prebuilt template containing all playtest information</param>
        /// <returns></returns>
        public async Task BuildPlaytestRequestBulk(string[] input)
        {
            foreach (var line in input)
            {
                var splitLine = line.Split(new[] {':'}, 2).Select(x => x.Trim()).ToArray();

                await ValidateInformationLoop(splitLine[0], splitLine[1]);
            }

            //Move onto confirmation
            await ConfirmRequest();
        }

        /// <summary>
        ///     Updates the instructions and display embed to reflect the current state of the playtest request.
        /// </summary>
        /// <param name="instructions">Instructions to display to the user</param>
        /// <returns></returns>
        private async Task Display(string instructions)
        {
            if (_embedMessage == null || _isDms)
                _embedMessage = await _context.Channel.SendMessageAsync("Type `exit` to abort at any time.",
                    embed: RebuildEmbed().Build());
            else
                await _embedMessage.ModifyAsync(x => x.Embed = RebuildEmbed().Build());

            if (_instructionsMessage == null || _isDms)
                _instructionsMessage = await _context.Channel.SendMessageAsync(instructions);
            else
                await _instructionsMessage.ModifyAsync(x => x.Content = instructions);

            //Message exists, and isn't in a DM.
            if (_userMessage != null && !_isDms)
                await _userMessage.DeleteAsync();
        }

        /// <summary>
        ///     Rebuilds the playtest embed with the most up to date information.
        /// </summary>
        /// <returns>EmbedBuilder object containing all relevant information</returns>
        private EmbedBuilder RebuildEmbed()
        {
            var embed = new EmbedBuilder()
                .WithFooter($"Current CT Time: {DateTime.Now}")
                .WithColor(new Color(0x752424));

            if (_testRequest.TestDate != new DateTime())
                embed.AddField("[0] Test Date:", _testRequest.TestDate, true)
                    .WithColor(new Color(0xa53737));

            if (_testRequest.Emails.Count > 0)
                embed.AddField("[1] Emails", string.Join("\n", _testRequest.Emails), true)
                    .WithColor(new Color(0x9a4237));

            if (!string.IsNullOrWhiteSpace(_testRequest.Game))
                embed.AddField("[2] Game", _testRequest.Game, true)
                    .WithColor(new Color(0x8f4d37));

            if (!string.IsNullOrWhiteSpace(_testRequest.MapName))
                embed.AddField("[3] Map Name", _testRequest.MapName, true)
                    .WithColor(new Color(0x8f4d37));

            if (_testRequest.CreatorsDiscord.Count > 0)
                embed.AddField("[4] Creators", string.Join("\n", _testRequest.CreatorsDiscord), true)
                    .WithColor(new Color(0x845837));

            if (!string.IsNullOrWhiteSpace(_testRequest.ImgurAlbum))
                embed.AddField("[5] Imgur Album", _testRequest.ImgurAlbum)
                    .WithColor(new Color(0x796337));

            if (!string.IsNullOrWhiteSpace(_testRequest.WorkshopURL))
                embed.AddField("[6] Workshop URL", _testRequest.WorkshopURL)
                    .WithColor(new Color(0x6e6e37));

            if (!string.IsNullOrWhiteSpace(_testRequest.TestType))
                embed.AddField("[7] Type", _testRequest.TestType, true)
                    .WithColor(new Color(0x637937));

            if (!string.IsNullOrWhiteSpace(_testRequest.TestGoals))
                embed.AddField("[8] Description", _testRequest.TestGoals)
                    .WithColor(new Color(0x588437));

            if (_testRequest.Spawns > 0)
                embed.AddField("[9] Spawns", _testRequest.Spawns, true)
                    .WithColor(new Color(0x4d8f37));

            if (_testRequest.PreviousTestDate != new DateTime())
                embed.AddField("[10] Previous Test:", _testRequest.PreviousTestDate, true)
                    .WithColor(new Color(0x429a37));

            if (!string.IsNullOrWhiteSpace(_testRequest.Preferredserver))
                embed.AddField("[11] Server:", _testRequest.Preferredserver, true)
                    .WithColor(new Color(0x37a537));

            //Only get new playtest conflict information if we get a new date. But always include it
            if (!_dateChecked)
            {
                _otherRequests = DatabaseUtil.GetAllPlaytestRequests();
                _scheduledTests = _calendar.CheckForScheduleConflict(_testRequest.TestDate).Result;
                _dateChecked = true;
            }

            var conflicts = "";
            if (_otherRequests != null && _otherRequests.ToList().Count > 0)
            {
                foreach (var req in _otherRequests.Where(x => x.TestDate == _testRequest.TestDate))
                    conflicts +=
                        $"**Map:** `{req.MapName}`\n**Test Date:** `{req.TestDate}`\n**Status:** `Test Requested`\n\n";
            }

            string modConflicts = "";

            if (_scheduledTests != null && _scheduledTests.Items.Count > 0)
            {
                foreach (var item in _scheduledTests.Items)
                {
                    if (item.Summary.Contains("unavailable", StringComparison.OrdinalIgnoreCase))
                    {
                        modConflicts += $"`{item.Summary.Replace(" - Click for details", "")}`\n";
                    }
                    else
                    {
                        conflicts +=
                            $"**Map:** `{item.Summary}`\n**Test Date:** `{item.Start.DateTime}`\n**Status:** `Scheduled`\n\n";
                    }
                }
            }
            if (conflicts.Length > 1)
            {
                conflicts = conflicts.Trim() +
                            " ```Schedule conflicts have been detected. You may continue to schedule, but your chances of being scheduled are less likely.```";
                embed.AddField("Schedule Conflicts Detected", conflicts);
                embed.WithColor(255, 0, 0);
            }

            if (modConflicts.Length > 1)
            {
                modConflicts = modConflicts.Trim() +
                            " ```These moderators are unavailable to run this test. You may still continue scheduling, as another moderator may accept this test.```";
                embed.AddField("Moderators Unavailable", modConflicts);
            }

            return embed;
        }

        /// <summary>
        ///     Cancels the request. Does some basic cleanup tasks.
        /// </summary>
        /// <returns></returns>
        private async Task CancelRequest()
        {
            if(_userMessage != null)
                await _context.Channel.SendMessageAsync("Request cancelled!");
            else
                await _context.Channel.SendMessageAsync("Interactive builder timed out!");

            await _embedMessage.DeleteAsync();
            await _instructionsMessage.DeleteAsync();
        }

        /// <summary>
        ///     Parses, and validates information before it is stored in a playtest request object.
        /// </summary>
        /// <param name="type">Type of data to parse</param>
        /// <param name="data">Data to parse</param>
        /// <returns>True if information is valid, false otherwise</returns>
        private async Task<bool> ParseTestInformation(string type, string data)
        {
            if (string.IsNullOrWhiteSpace(data))
                data = "Nothing provided yet!";

            switch (type.ToLower())
            {
                case "date":
                    if (DateTime.TryParse(data, out var dt))
                    {
                        if (dt < DateTime.Now)
                        {
                            await Display($"Date cannot be in the past.\nYou provided `{data}`\n" + _wizardText[0]);
                            return false;
                        }

                        if (dt > DateTime.Now.AddMonths(2))
                        {
                            await Display($"You cannot schedule more than 2 months in advance.\nYou provided `{data}`\n" + _wizardText[0]);
                            return false;
                        }

                        _testRequest.TestDate = dt;
                        _dateChecked = false;
                        return true;
                    }

                    await Display($"Unable to parse DateTime.\nYou provided `{data}`\n" + _wizardText[0]);
                    return false;

                case "emails":
                    var emails = data.Split(',').Select(x => x.Trim()).ToArray();

                    //Empty list in case we are editing this.
                    _testRequest.Emails.Clear();
                    _testRequest.Emails.TrimExcess();

                    foreach (var email in emails)
                        if (new EmailAddressAttribute().IsValid(email))
                        {
                            _testRequest.Emails.Add(email);
                        }
                        else
                        {
                            await Display($"Unable to parse email addresses.\nYou provided `{data}`\n" +
                                          _wizardText[1]);
                            return false;
                        }

                    return true;

                case "game":
                    if (data.Contains("csgo", StringComparison.OrdinalIgnoreCase))
                    {
                        _testRequest.Game = "CSGO";
                        return true;
                    }

                    if (data.Contains("tf2", StringComparison.OrdinalIgnoreCase))
                    {
                        _testRequest.Game = "TF2";
                        return true;
                    }

                    await Display($"Invalid game.\nYou provided `{data}`\n" +
                                  _wizardText[2]);
                    return false;
                    

                case "mapname":
                    _testRequest.MapName = data;
                    return true;

                case "discord":
                    var creators = data.Split(',').Select(x => x.Trim());

                    //Empty list in case we are editing this.
                    _testRequest.CreatorsDiscord.Clear();
                    _testRequest.CreatorsDiscord.TrimExcess();

                    foreach (var creator in creators)
                    {
                        var user = _dataService.GetSocketGuildUser(creator);
                        if (user != null)
                        {
                            _testRequest.CreatorsDiscord.Add($"{user.Id}");
                        }
                        else
                        {
                            await Display("Unable to parse creators!\n" +
                                          $"You provided `{data}`\n" + _wizardText[4]);
                            return false;
                        }
                    }

                    return true;

                case "imgur":
                    if (GeneralUtil.GetImgurAlbum(data) == null)
                    {
                        await Display("Invalid Imgur Album!\n" +
                                      $"You provided `{data}`\n" + _wizardText[5]);
                        return false;
                    }

                    _testRequest.ImgurAlbum = data;
                    return true;

                case "workshop":
                    if (GeneralUtil.ValidateWorkshopURL(data))
                    {
                        _testRequest.WorkshopURL = data;
                        return true;
                    }

                    await Display("Invalid Steam Workshop URL!\n" +
                                  $"You provided `{data}`\n" + _wizardText[6]);
                    return false;

                case "type":
                    _testRequest.TestType = data.Contains("comp", StringComparison.OrdinalIgnoreCase)
                        ? "Competitive"
                        : "Casual";

                    return true;

                case "description":
                    _testRequest.TestGoals = string.IsNullOrWhiteSpace(data) ? "No information provided" : data;
                    return true;

                case "spawns":
                    if (int.TryParse(data, out var spawn))
                    {
                        _testRequest.Spawns = spawn;

                        return true;
                    }

                    await Display("Unable to parse number of spawns\n" +
                                  $"You provided `{data}`\n" + _wizardText[9]);
                    return false;

                case "previoustest":
                    _testRequest.PreviousTestDate = DateTime.TryParse(data, out var pt) ? pt : DateTime.UnixEpoch;

                    return true;

                case "server":
                    if (data.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                        data.Equals("no preference", StringComparison.OrdinalIgnoreCase))
                    {
                        _testRequest.Preferredserver = "No preference";
                        return true;
                    }

                    //Make sure the server requested supports the game.
                    var server = DatabaseUtil.GetTestServer(data);
                    if (server != null && server.Game.Equals(_testRequest.Game, StringComparison.OrdinalIgnoreCase))
                    {
                        _testRequest.Preferredserver = server.Address;
                        return true;
                    }

                    var servers = DatabaseUtil.GetAllTestServers(_testRequest.Game);

                    await Display("Unable to to find a valid server\n" +
                                  $"You provided `{data}`\n" +
                                  "Please provide a valid server id. Type `None` for no preference. Possible servers are:\n" +
                                  $"`{string.Join("\n", servers.Select(x => x.ServerId).ToArray())}`");
                    return false;

                default:
                    await Display("Unknown test information.");
                    _requireAbort = true;
                    return false;
            }
        }
    }
}