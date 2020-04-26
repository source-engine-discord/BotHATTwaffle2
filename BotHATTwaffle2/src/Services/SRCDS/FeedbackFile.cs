using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Util;
using CoreRCON.Parsers.Standard;

namespace BotHATTwaffle2.Services.SRCDS
{
    public class FeedbackFile
    {
        public Server Server { get; }
        public string FileName { get; }
        private readonly RconService _rconService;

        public FeedbackFile(Server server, string fileName, RconService rconService)
        {
            Server = server;
            FileName = SetValidFile(fileName);
            _rconService = rconService;

            //Make the directory if needed.
            Directory.CreateDirectory("Feedback");
        }

        public FeedbackFile(FeedbackFileStore feedbackFileStore, RconService rconService)
        {
            Server = DatabaseUtil.GetTestServer(feedbackFileStore.ServerAddress);
            FileName = feedbackFileStore.FileName;
            _rconService = rconService;
        }

        public async Task LogFeedback(string message)
        {
            await LogFeedback(new GenericCommand
            {
                Message = message,
                Player = new Player
                {
                    Name = "Ido",
                    Team = "Bot"
                }
            });
        }
        
        public async Task LogFeedback(GenericCommand genericCommand)
        {
            string message =
                $"{DateTime.Now} - {genericCommand.Player.Name} ({genericCommand.Player.Team}): {genericCommand.Message}";

            for (int i = 0; i < 4; i++)
            {
                try
                {
                    if (!File.Exists(FileName))
                        // Create a file to write to.
                        using (var sw = File.CreateText(FileName))
                        {
                            sw.WriteLine(message);
                        }
                    else
                        // This text is always added, making the file longer over time
                        // if it is not deleted.
                        using (var sw = File.AppendText(FileName))
                        {
                            sw.WriteLine(message);
                        }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    await Task.Delay(5000);
                }
            }

            await _rconService.RconCommand(Server.ServerId, $"say Feedback from {genericCommand.Player.Name} captured!",
                false);
        }

        //Make sure the filepath is correct.
        private string SetValidFile(string fileName)
        {
           if (fileName.Contains(".txt"))
                return "Feedback\\" + fileName;

           return "Feedback\\" + fileName + ".txt";
        }
    }
}
