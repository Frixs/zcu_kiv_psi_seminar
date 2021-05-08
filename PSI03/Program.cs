using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSI03
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Gathering data...");
            Console.WriteLine();

            RunAndForget(async () =>
            {
                // Get ISS data
                var iss = await GetIssDataAsync();
                if (!iss.Status)
                {
                    Console.WriteLine("Failed to retrieve the data!");
                    return;
                }

                var yesterday = await GetSunDataAsync(iss.Timestamp.AddDays(-1), iss.Longitude, iss.Latitude);
                var today = await GetSunDataAsync(iss.Timestamp, iss.Longitude, iss.Latitude);
                var tomorrow = await GetSunDataAsync(iss.Timestamp.AddDays(+1), iss.Longitude, iss.Latitude);

                // Time between the sun events
                TimeSpan nightToToday = today.SunriseAt - yesterday.SunsetAt;
                TimeSpan sunlight = today.SunsetAt - today.SunriseAt;
                TimeSpan nightFromToday = tomorrow.SunriseAt - today.SunsetAt;

                bool watchable = false;
                TimeSpan watchableDelta = TimeSpan.Zero;
                SunTransition sunTransition;
                DayType dayType;

                // Yesterday -> Today -> Tomorrow
                if (iss.Timestamp.CompareTo(yesterday.SunsetAt) < 0) // "IIS time before yesterday's sunset"
                {
                    sunTransition = SunTransition.BeforeSunset;
                    dayType = DayType.Yesterday;
                    watchableDelta = yesterday.SunsetAt - iss.Timestamp; // calculate time diff
                }
                else if (iss.Timestamp.CompareTo(yesterday.SunsetAt + nightToToday / 2) < 0) // Split each time period to halves - the second half is for the next if statement (sunrise)
                {
                    sunTransition = SunTransition.AfterSunset;
                    dayType = DayType.Yesterday;
                    watchableDelta = iss.Timestamp - yesterday.SunsetAt;
                }
                else if (iss.Timestamp.CompareTo(today.SunriseAt) < 0)
                {
                    sunTransition = SunTransition.BeforeSunrise;
                    dayType = DayType.Today;
                    watchableDelta = today.SunriseAt - iss.Timestamp;
                }
                else if (iss.Timestamp.CompareTo(today.SunriseAt + sunlight / 2) < 0)
                {
                    sunTransition = SunTransition.AfterSunrise;
                    dayType = DayType.Today;
                    watchableDelta = iss.Timestamp - today.SunriseAt;
                }
                else if (iss.Timestamp.CompareTo(today.SunsetAt) < 0)
                {
                    sunTransition = SunTransition.BeforeSunset;
                    dayType = DayType.Today;
                    watchableDelta = today.SunsetAt - iss.Timestamp;
                }
                else if (iss.Timestamp.CompareTo(today.SunsetAt + nightFromToday / 2) < 0)
                {
                    sunTransition = SunTransition.AfterSunset;
                    dayType = DayType.Today;
                    watchableDelta = iss.Timestamp - today.SunsetAt;
                }
                else if (iss.Timestamp.CompareTo(tomorrow.SunriseAt) < 0)
                {
                    sunTransition = SunTransition.BeforeSunrise;
                    dayType = DayType.Tomorrow;
                    watchableDelta = tomorrow.SunriseAt - iss.Timestamp;
                }
                else
                {
                    sunTransition = SunTransition.AfterSunrise;
                    dayType = DayType.Tomorrow;
                    watchableDelta = iss.Timestamp - tomorrow.SunriseAt;
                }

                // Check the watchable condition...
                if ((sunTransition == SunTransition.BeforeSunrise || sunTransition == SunTransition.AfterSunset)
                    && watchableDelta > TimeSpan.FromHours(1) && watchableDelta <= TimeSpan.FromHours(2))
                    watchable = true;

                // Write down the results
                Console.WriteLine("[ISS]");
                Console.WriteLine($"Longitude: {iss.Longitude}");
                Console.WriteLine($"Latitude: {iss.Latitude}");
                Console.WriteLine($"Time: {iss.Timestamp:yyyy-MM-dd HH:mm:ss} (UTC)");
                Console.WriteLine($"Sun position: {sunTransition} ({dayType})");
                Console.WriteLine($"Watchable: {watchable}");
            });

            Console.ReadLine();
        }

        static async Task<(bool Status, double Longitude, double Latitude, DateTime Timestamp)> GetIssDataAsync()
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync("http://api.open-notify.org/iss-now.json");
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();

            double longitude = 0;
            double latitude = 0;
            double unixTimestamp = 0;
            DateTime timestamp = default;
            string message = string.Empty;
            try
            {
                JObject jo = JObject.Parse(responseBody);
                longitude = double.Parse(jo["iss_position"]["longitude"].ToString(), CultureInfo.InvariantCulture);
                latitude = double.Parse(jo["iss_position"]["latitude"].ToString(), CultureInfo.InvariantCulture);
                unixTimestamp = long.Parse(jo["timestamp"].ToString());
                message = jo["message"].ToString();

                timestamp = UnixTimeStampToDateTime(unixTimestamp);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.GetType()}: {ex.Message}");
            }

            client.Dispose();

            // If successfully retrieved data
            if (message.Equals("success"))
                return (true, longitude, latitude, timestamp);
            else
                return (false, default, default, default);
        }

        static async Task<(bool Status, DateTime SunriseAt, DateTime SunsetAt)> GetSunDataAsync(DateTime day, double longitude, double latitude)
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync($"https://api.sunrise-sunset.org/json?lat={latitude}&lng={longitude}&date={day:yyyy-MM-dd}&formatted=0");
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();

            string status = string.Empty;
            DateTime sunrise = default;
            DateTime sunset = default;
            try
            {
                JObject jo = JObject.Parse(responseBody);
                sunrise = DateTime.Parse(jo["results"]["sunrise"].ToString(), CultureInfo.CurrentCulture).ToUniversalTime();
                sunset = DateTime.Parse(jo["results"]["sunset"].ToString(), CultureInfo.CurrentCulture).ToUniversalTime();
                status = jo["status"].ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.GetType()}: {ex.Message}");
            }

            client.Dispose();

            // If successfully retrieved data
            if (status.Equals("OK"))
                return (true, sunrise, sunset);
            else
                return (false, default, default);
        }

        static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }

        static void RunAndForget(Action action)
        {
            try
            {
                Task.Run(action);
            }
            catch
            { }
        }

        enum SunTransition
        {
            BeforeSunset,
            AfterSunset,
            BeforeSunrise,
            AfterSunrise
        }

        enum DayType
        {
            Yesterday,
            Today,
            Tomorrow
        }
    }
}
