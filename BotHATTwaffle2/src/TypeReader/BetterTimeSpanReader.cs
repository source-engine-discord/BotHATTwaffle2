using System;
using System.Threading.Tasks;
using Discord.Commands;

namespace BotHATTwaffle2.TypeReader
{
    public class BetterTimeSpanReader : Discord.Commands.TypeReader
    {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input,
            IServiceProvider services)
        {
            var weeks = 0;
            var days = 0;
            var hours = 0;
            var minutes = 0;
            var seconds = 0;

            var maxDays = 10000000;
            var maxHours = 240000000;
            var maxMinutes = 2147483647;
            var maxSeconds = 2147483647;

            if (!int.TryParse(input, out var parsed) && !input.Contains("."))
            {
                var pointer = 0;
                while (input.Length > 1)
                    try
                    {
                        if (!char.IsLetter(input, pointer))
                        {
                            pointer++;
                        }
                        else
                        {
                            var unit = input[pointer];
                            switch (unit)
                            {
                                case 'w':
                                    int.TryParse(input.Substring(0, 1), out var weeksFirstDigit);
                                    int.TryParse(input.Substring(0, pointer), out weeks);
                                    weeks = weeksFirstDigit > 0 ? weeks > maxDays || weeks == 0 ? (maxDays / 7) : weeks : weeks;
                                    break;
                                case 'd':
                                    int.TryParse(input.Substring(0, 1), out var daysFirstDigit);
                                    int.TryParse(input.Substring(0, pointer), out days);
                                    days = daysFirstDigit > 0 ? days > maxDays || days == 0 ? maxDays : days : days;
                                    break;
                                case 'h':
                                    int.TryParse(input.Substring(0, 1), out var hoursFirstDigit);
                                    int.TryParse(input.Substring(0, pointer), out hours);
                                    hours = hoursFirstDigit > 0
                                        ? hours > maxHours || hours == 0 ? maxHours : hours
                                        : hours;
                                    break;
                                case 'm':
                                    int.TryParse(input.Substring(0, 1), out var minutesFirstDigit);
                                    int.TryParse(input.Substring(0, pointer), out minutes);
                                    minutes = minutesFirstDigit > 0
                                        ? minutes > maxMinutes || minutes == 0 ? maxMinutes : minutes
                                        : minutes;
                                    break;
                                case 's':
                                    int.TryParse(input.Substring(0, 1), out var secondsFirstDigit);
                                    int.TryParse(input.Substring(0, pointer), out seconds);
                                    seconds = secondsFirstDigit > 0
                                        ? seconds > maxSeconds || seconds == 0 ? maxSeconds : seconds
                                        : seconds;
                                    break;
                                default:
                                    //Non-valid character.
                                    return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed,
                                        "Could not parse TimeSpan"));
                            }

                            input = input.Remove(0, pointer + 1);
                            pointer = 0;
                        }
                    }
                    catch
                    {
                        //Missing character.
                        return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed,
                            "Count not parse TimeSpan"));
                    }

                //Slip weeks into the mute span.
                days = days + (weeks * 7);
                //sets values back to 0 to stop TimeSpan looping back around to 0
                seconds = minutes == maxMinutes ? 0 : seconds;
                minutes = hours == maxHours ? 0 : minutes;
                hours = days == maxDays ? 0 : hours;

                return Task.FromResult(TypeReaderResult.FromSuccess(new TimeSpan(days, hours, minutes, seconds)));
            }

            double.TryParse(input, out var duration);
            return Task.FromResult(TypeReaderResult.FromSuccess(TimeSpan.FromMinutes(duration)));
        }
    }
}