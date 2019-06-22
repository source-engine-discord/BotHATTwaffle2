using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace BotHATTwaffle2.TypeReader
{
    public class BetterTimeSpanReader : Discord.Commands.TypeReader
    {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            int days = 0;
            int hours = 0;
            int minutes = 0;
            int seconds = 0;

            int maxDays = 10000000;
            int maxHours = 240000000;
            int maxMinutes = 2147483647;
            int maxSeconds = 2147483647;

            if (!int.TryParse(input, out int parsed) && !input.Contains("."))
            {
                int pointer = 0;
                while (input.Length > 1)
                {
                    try
                    {
                        if (!char.IsLetter(input, pointer))
                        {
                            pointer++;
                        }
                        else
                        {

                            char unit = input[pointer];
                            switch (unit)
                            {
                                case 'd':
                                    int.TryParse(input.Substring(0, 1), out int daysFirstDigit);
                                    int.TryParse(input.Substring(0, pointer), out days);
                                    days = daysFirstDigit > 0 ? (days > maxDays || days == 0) ? maxDays : days : days;
                                    break;
                                case 'h':
                                    int.TryParse(input.Substring(0, 1), out int hoursFirstDigit);
                                    int.TryParse(input.Substring(0, pointer), out hours);
                                    hours = hoursFirstDigit > 0 ? (hours > maxHours || hours == 0) ? maxHours : hours : hours;
                                    break;
                                case 'm':
                                    int.TryParse(input.Substring(0, 1), out int minutesFirstDigit);
                                    int.TryParse(input.Substring(0, pointer), out minutes);
                                    minutes = minutesFirstDigit > 0 ? (minutes > maxMinutes || minutes == 0) ? maxMinutes : minutes : minutes;
                                    break;
                                case 's':
                                    int.TryParse(input.Substring(0, 1), out int secondsFirstDigit);
                                    int.TryParse(input.Substring(0, pointer), out seconds);
                                    seconds = secondsFirstDigit > 0 ? (seconds > maxSeconds || seconds == 0) ? maxSeconds : seconds : seconds;
                                    break;
                                default:
                                    //Non-valid character.
                                    return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Count not parse TimeSpan"));
                            }

                            input = input.Remove(0, pointer + 1);
                            pointer = 0;
                        }
                    }
                    catch
                    {
                        //Missing character.
                        return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Count not parse TimeSpan"));
                    }
                }

                //sets values back to 0 to stop TimeSpan looping back around to 0
                seconds = minutes == maxMinutes ? 0 : seconds;
                minutes = hours == maxHours ? 0 : minutes;
                hours = days == maxDays? 0 : hours;

                return Task.FromResult(TypeReaderResult.FromSuccess(new TimeSpan(days, hours, minutes, seconds)));
            }

            double.TryParse(input, out var duration);
            return Task.FromResult(TypeReaderResult.FromSuccess(TimeSpan.FromMinutes(duration)));
        }
    }
}
