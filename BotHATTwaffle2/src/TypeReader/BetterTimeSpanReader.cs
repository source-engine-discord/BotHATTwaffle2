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

            if (!input.All(char.IsNumber) && !input.Contains("."))
            {
                int pointer = 0;
                while (1 < input.Length)
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
                                int.TryParse(input.Substring(0, pointer), out days);
                                break;
                            case 'h':
                                int.TryParse(input.Substring(0, pointer), out hours);
                                break;
                            case 'm':
                                int.TryParse(input.Substring(0, pointer), out minutes);
                                break;
                            case 's':
                                int.TryParse(input.Substring(0, pointer), out seconds);
                                break;
                            default:
                                //Non-valid character.
                                return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Count not parse TimeSpan"));
                        }

                        input = input.Remove(0, pointer + 1);
                        pointer = 0;
                    }
                }
                return Task.FromResult(TypeReaderResult.FromSuccess(new TimeSpan(days, hours, minutes, seconds)));
            }

            double.TryParse(input, out var duration);
            return Task.FromResult(TypeReaderResult.FromSuccess(TimeSpan.FromMinutes(duration)));
        }
    }
}
