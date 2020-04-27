using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Services.FaceIt;

namespace BotHATTwaffle2.Util
{
    internal class HeatmapGenerator
    {
        private static readonly string exeFolderName = @"SourceEngine.Heatmap\";
        private static readonly string fileName = @"SourceEngine.Heatmap.Generator.exe";
        private static readonly string listsDir = exeFolderName + @"lists\";
        private static readonly string outputDir = exeFolderName + @"output";
        private static readonly string pastGameDataRoot = exeFolderName + @"pastData";

        private const ConsoleColor LOG_COLOR = ConsoleColor.DarkRed;
        private static LogHandler _log;
        private static DataService _dataService;

        public static void SetHandlers(LogHandler log, DataService data)
        {
            _dataService = data;
            _log = log;
        }

        /// <summary>
        /// Invokes a heatmap generator instance on the specific files creating heatmap images
        /// </summary>
        /// <param name="listFile">File containing full paths to JSON data of a parsed demo</param>
        /// <param name="radarFilesFolder">Folder containing radar files</param>
        /// <param name="pastGameDataFolder">Folder that contains data from past game parses, should be per-season</param>
        /// <param name="outputFolder">Folder to output images in</param>
        /// <returns>List of file info of output images</returns>
        public static async Task<List<FileInfo>> GenerateHeatMapsByListFile(string listFile, string radarFilesFolder, string pastGameDataFolder, string outputFolder)
        {
            //Make paths full because jimwood.
            string output = new DirectoryInfo(outputDir + "\\" + outputFolder).FullName;
            string overviewFilesDir = new DirectoryInfo(radarFilesFolder).FullName;
            string heatmapJasonDir = new DirectoryInfo($"{pastGameDataRoot}\\{pastGameDataFolder}").FullName;

            //Start the process
            var processStartInfo = new ProcessStartInfo(exeFolderName + fileName,
                $"-inputdatafilepathsfile \"{listFile}\" " +
                $"-overviewfilesdirectory \"{overviewFilesDir}\\\\\" " + 
                $"-heatmapjsondirectory \"{heatmapJasonDir}\\\\\" " + 
                $"-outputheatmapdirectory \"{output}\\\\\" " + 
                $"-heatmapstogenerate all");
            processStartInfo.WorkingDirectory = exeFolderName;

            //Start generator with a 60m timeout
            await AsyncProcessRunner.RunAsync(processStartInfo, 60 * 60 * 1000);

            return GeneralUtil.GetFilesInDirectory(output);
        }

        /// <summary>
        /// Creates a text for required for each map to be hit by the heat map generator
        /// </summary>
        /// <param name="masterList">2D list where the top list represents a map, and the bottom list contains
        ///  full paths to each file to be parsed</param>
        /// <returns>List of text file lists the heat map generator will use</returns>
        public static async Task<List<FileInfo>> CreateListFiles(List<List<List<FaceItGameInfo>>> masterList)
        {
            //Create the dir, or delete the old files if needed.
            Directory.CreateDirectory(listsDir);
            DirectoryInfo dir = new DirectoryInfo(listsDir);
            foreach (var fileInfo in dir.GetFiles())
            {
                fileInfo.Delete();
            }

            //Create the files needed for each map
            List<FileInfo> listFiles = new List<FileInfo>();

            //Loop though each hub tag
            foreach (var hubTag in masterList)
            {
                //Loop though each map under each tag
                foreach (var map in hubTag)
                {
                    //Don't include unknown tagged games
                    //Make sure games are valid, as skipped games aren't included so we might get empty lists.
                    //Holy cow this can really be cleaned up, but I can't be bothered right now.
                    if (map == null || map.Count == 0)
                        continue;

                    if (map[0].Tag.TagName.Equals("unknown", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string outputFile = $"{listsDir}\\{map[0].Tag.TagName}_{map[0].GetMapName()}.txt";

                    var lines = new List<string>();
                    foreach (var m in map)
                    {
                        //Get the full json path for each map, and place it in the tag folder
                        try
                        {
                            lines.Add(m.GetRealJsonLocation().FullName);
                        }
                        catch (Exception e)
                        {
                            await _log.LogMessage($"Unable to GetRealJsonLocation for Game UID: `{m.GetGameUid()}`\n{e}");
                        }
                    }

                    for (int i = 0; i < 4; i++)
                    {
                        
                        try
                        {
                            await _log.LogMessage($"Attempting file write for {outputFile}", false);
                            File.WriteAllLines(outputFile, lines);
                            break;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                            await Task.Delay(5000);
                        }
                    }

                    listFiles.Add(new FileInfo(outputFile));
                }
            }

            return listFiles;
        }
    }
}
