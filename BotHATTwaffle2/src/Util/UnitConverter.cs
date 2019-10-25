using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BotHATTwaffle2.Util
{
    internal class UnitConverter
    {
        //temps
        private static readonly string PatternCelsius = @"(^| )[+-]?(\d*\.)?\d+c(( )|($)|(\r\n|\r|\n))";
        private static readonly string PatternFahrenheit = @"(^| )[+-]?(\d*\.)?\d+f(( )|($)|(\r\n|\r|\n))";

        //Metric
        private static readonly string PatternMilimeters = @"(^| )[+-]?(\d*\.)?\d+mm(( )|($)|(\r\n|\r|\n))";
        private static readonly string PatternCentimeters = @"(^| )[+-]?(\d*\.)?\d+cm(( )|($)|(\r\n|\r|\n))";
        private static readonly string PatternMeters = @"(^| )[+-]?(\d*\.)?\d+m(( )|($)|(\r\n|\r|\n))";
        private static readonly string PatternKilometers = @"(^| )[+-]?(\d*\.)?\d+km(( )|($)|(\r\n|\r|\n))";

        //Freedom
        private static readonly string PatternMiles = @"(^| )[+-]?(\d*\.)?\d+mi(( )|($)|(\r\n|\r|\n))";
        private static readonly string PatternFeet = @"(^| )[+-]?(\d*\.)?\d+(ft|')(( )|($)|(\r\n|\r|\n))";
        private static readonly string PatternInches = "(^| )[+-]?(\\d*\\.)?\\d+(in|\")(( )|($)|(\r\n|\r|\n))";

        //Weight
        private static readonly string PatternPounds = @"(^| )[+-]?(\d*\.)?\d+(lb|lbs)(( )|($)|(\r\n|\r|\n))";
        private static readonly string PatternKilograms = @"(^| )[+-]?(\d*\.)?\d+(kg|kgs)(( )|($)|(\r\n|\r|\n))";


        private static readonly Regex RegExC = new Regex(PatternCelsius,
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RegExF = new Regex(PatternFahrenheit,
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RegExMm =
            new Regex(PatternMilimeters, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RegExCm =
            new Regex(PatternCentimeters, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex
            RegExM = new Regex(PatternMeters, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RegExKm =
            new Regex(PatternKilometers, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex
            RegExMi = new Regex(PatternMiles, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RegExFt = new Regex(PatternFeet, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RegExIn =
            new Regex(PatternInches, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RegExNumbersonly = new Regex(@"[+-]?(\d*\.)?\d+", RegexOptions.Compiled);

        private static readonly Regex RegExLb =
            new Regex(PatternPounds, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RegExKg =
            new Regex(PatternKilograms, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static Dictionary<string, string> AutoConversion(string input)
        {
            var dictionary = new Dictionary<string, string>();
            var matches = RegExC.Matches(input);
            if (matches.Count > 0)
                foreach (Match match in matches)
                {
                    double.TryParse(RegExNumbersonly.Match(match.Value).Value, out var value);
                    if (!dictionary.ContainsKey(match.Value.Trim()))
                        dictionary.Add(match.Value.Trim(), $"{CelsiusToFahrenheit(value)}f");
                }

            matches = RegExF.Matches(input);
            if (matches.Count > 0)
                foreach (Match match in matches)
                {
                    double.TryParse(RegExNumbersonly.Match(match.Value).Value, out var value);
                    if (!dictionary.ContainsKey(match.Value.Trim()))
                        dictionary.Add(match.Value.Trim(), $"{FahrenheitToCelsius(value)}c");
                }

            matches = RegExMm.Matches(input);
            if (matches.Count > 0)
                foreach (Match match in matches)
                {
                    double.TryParse(RegExNumbersonly.Match(match.Value).Value, out var value);
                    if (!dictionary.ContainsKey(match.Value.Trim()))
                        dictionary.Add(match.Value.Trim(), $"{MilimetersToInches(value)}in");
                }

            matches = RegExCm.Matches(input);
            if (matches.Count > 0)
                foreach (Match match in matches)
                {
                    double.TryParse(RegExNumbersonly.Match(match.Value).Value, out var value);
                    if (!dictionary.ContainsKey(match.Value.Trim()))
                        dictionary.Add(match.Value.Trim(), $"{CentimetersToInches(value)}in");
                }

            matches = RegExM.Matches(input);
            if (matches.Count > 0)
                foreach (Match match in matches)
                {
                    double.TryParse(RegExNumbersonly.Match(match.Value).Value, out var value);
                    if (!dictionary.ContainsKey(match.Value.Trim()))
                        dictionary.Add(match.Value.Trim(), $"{MetersToFeet(value)}ft");
                }

            matches = RegExKm.Matches(input);
            if (matches.Count > 0)
                foreach (Match match in matches)
                {
                    double.TryParse(RegExNumbersonly.Match(match.Value).Value, out var value);
                    if (!dictionary.ContainsKey(match.Value.Trim()))
                        dictionary.Add(match.Value.Trim(), $"{KilometersToMiles(value)}mi");
                }

            matches = RegExMi.Matches(input);
            if (matches.Count > 0)
                foreach (Match match in matches)
                {
                    double.TryParse(RegExNumbersonly.Match(match.Value).Value, out var value);
                    if (!dictionary.ContainsKey(match.Value.Trim()))
                        dictionary.Add(match.Value.Trim(), $"{MilesToKilometers(value)}km");
                }

            matches = RegExFt.Matches(input);
            if (matches.Count > 0)
                foreach (Match match in matches)
                {
                    double.TryParse(RegExNumbersonly.Match(match.Value).Value, out var value);
                    if (!dictionary.ContainsKey(match.Value.Trim()))
                        dictionary.Add(match.Value.Trim(), $"{FeetToMeter(value)}m");
                }

            matches = RegExIn.Matches(input);
            if (matches.Count > 0)
                foreach (Match match in matches)
                {
                    double.TryParse(RegExNumbersonly.Match(match.Value).Value, out var value);
                    if (!dictionary.ContainsKey(match.Value.Trim()))
                        dictionary.Add(match.Value.Trim(), $"{InchesToCentimeters(value)}cm");
                }

            matches = RegExLb.Matches(input);
            if (matches.Count > 0)
                foreach (Match match in matches)
                {
                    double.TryParse(RegExNumbersonly.Match(match.Value).Value, out var value);
                    if (!dictionary.ContainsKey(match.Value.Trim()))
                        dictionary.Add(match.Value.Trim(), $"{PoundsToKilograms(value)}kg");
                }

            matches = RegExKg.Matches(input);
            if (matches.Count > 0)
                foreach (Match match in matches)
                {
                    double.TryParse(RegExNumbersonly.Match(match.Value).Value, out var value);
                    if (!dictionary.ContainsKey(match.Value.Trim()))
                        dictionary.Add(match.Value.Trim(), $"{KilogramsToPounds(value)}lb");
                }

            return dictionary;
        }

        public static double CelsiusToFahrenheit(double c)
        {
            return Math.Round(9.0 / 5.0 * c + 32, 2);
        }

        public static double FahrenheitToCelsius(double f)
        {
            return Math.Round(5.0 / 9.0 * (f - 32), 2);
        }

        public static double MilesToKilometers(double mi)
        {
            return Math.Round(mi * 1.609344, 2);
        }

        public static double KilometersToMiles(double km)
        {
            return Math.Round(km * 0.621371192, 2);
        }

        public static double InchesToCentimeters(double inches)
        {
            return Math.Round(2.54 * inches, 2);
        }

        public static double CentimetersToInches(double cm)
        {
            return Math.Round(cm / 2.54, 2);
        }

        public static double MilimetersToInches(double mm)
        {
            return Math.Round(mm / 25.4, 2);
        }

        public static double MetersToFeet(double m)
        {
            return Math.Round(m * 3.2808399, 2);
        }

        public static double FeetToMeter(double ft)
        {
            return Math.Round(ft / 3.2808399, 2);
        }

        public static double PoundsToKilograms(double lb)
        {
            return Math.Round(lb * 0.45359237, 2);
        }

        public static double KilogramsToPounds(double kg)
        {
            return Math.Round(kg / 0.45359237, 2);
        }
    }
}