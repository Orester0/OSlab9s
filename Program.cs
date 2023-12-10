using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace onlyServer
{
    public class WeatherData
    {
        public double latitude { get; set; }
        public double longitude { get; set; }
        public double generationtime_ms { get; set; }
        public int utc_offset_seconds { get; set; }
        public string timezone { get; set; }
        public string timezone_abbreviation { get; set; }
        public double elevation { get; set; }
        public HourlyData hourly { get; set; }
        public HourlyUnits hourly_units { get; set; }

        public class HourlyData
        {
            public string time_format { get; set; }
            public List<string> time { get; set; }
            public List<double> temperature_2m { get; set; }
            public List<int> relative_humidity_2m { get; set; }
            public List<int> precipitation_probability { get; set; }
            public List<double> rain { get; set; }
            public List<double> showers { get; set; }
            public List<double> snowfall { get; set; }
            public List<int> cloud_cover { get; set; }
            public List<int> cloud_cover_low { get; set; }
            public List<int> cloud_cover_mid { get; set; }
            public List<int> cloud_cover_high { get; set; }

            public override string ToString()
            {
                return $"TimeFormat: {time_format}, " +
                       $"Time: [{string.Join(", ", time)}], " +
                       $"Temperature2m: [{string.Join(", ", temperature_2m)}], " +
                       $"RelativeHumidity2m: [{string.Join(", ", relative_humidity_2m)}], " +
                       $"PrecipitationProbability: [{string.Join(", ", precipitation_probability)}], " +
                       $"Rain: [{string.Join(", ", rain)}], " +
                       $"Showers: [{string.Join(", ", showers)}], " +
                       $"Snowfall: [{string.Join(", ", snowfall)}], " +
                       $"CloudCover: [{string.Join(", ", cloud_cover)}], " +
                       $"CloudCoverLow: [{string.Join(", ", cloud_cover_low)}], " +
                       $"CloudCoverMid: [{string.Join(", ", cloud_cover_mid)}], " +
                       $"CloudCoverHigh: [{string.Join(", ", cloud_cover_high)}]";
            }
        }

        public class HourlyUnits
        {
            public string time { get; set; }
            public string temperature_2m { get; set; }
            public string relative_humidity_2m { get; set; }
            public string precipitation_probability { get; set; }
            public string rain { get; set; }
            public string showers { get; set; }
            public string snowfall { get; set; }
            public string cloud_cover { get; set; }
            public string cloud_cover_low { get; set; }
            public string cloud_cover_mid { get; set; }
            public string cloud_cover_high { get; set; }
        }

        public override string ToString()
        {
            return $"Latitude: {latitude}, " +
                   $"Longitude: {longitude}, " +
                   $"GenerationTimeMs: {generationtime_ms}, " +
                   $"UtcOffsetSeconds: {utc_offset_seconds}, " +
                   $"Timezone: {timezone}, " +
                   $"TimezoneAbbreviation: {timezone_abbreviation}, " +
                   $"Elevation: {elevation}, " +
                   $"Hourly: {hourly}, " +
                   $"HourlyUnits: {hourly_units}";
        }
    }
    class Program
    {
        private static readonly object lockObject = new object();
        static bool subscribed = true;
        static WeatherData currentWeather = new WeatherData();
        static string currentCity = "varash";
        static List<Tuple<string, string>> availableCities = new List<Tuple<string, string>>(){
             new Tuple<string, string>("varash", "latitude=51.3509&longitude=25.8474"),
             new Tuple<string, string>("lviv", "latitude=49.8383&longitude=24.0232"),
             new Tuple<string, string>("vinnytsia", "latitude=49.2322&longitude=28.4687"),
             new Tuple<string, string>("duisburg", "latitude=51.4325&longitude=6.7652"),
        };


        static void Main(string[] args)
        {

            Timer weatherUpdateTimer = new Timer(UpdateWeatherData, null, TimeSpan.Zero, TimeSpan.FromSeconds(20));
            StartServerAsync();

            Task.Delay(1000).Wait();

            while (true)
            {
                // Server continues to do other tasks or can exit if needed
                // For this example, the server does not perform any additional tasks
            }
        }

        static void UpdateWeatherData(object state)
        {
            string coordinates = availableCities
            .Where(city => city.Item1 == currentCity)
            .Select(city => city.Item2)
            .FirstOrDefault();
            if (coordinates != null)
            {
                Console.WriteLine($"Значення для {currentCity} : {coordinates}");
            }
            else
            {
                Console.WriteLine($"Місто {currentCity} не знайдено.");
            }
            //string programAPI = "https://api.open-meteo.com/v1/forecast?latitude=52.52&longitude=13.41&hourly=temperature_2m,relative_humidity_2m,precipitation_probability,rain,showers,snowfall,cloud_cover,cloud_cover_low,cloud_cover_mid,cloud_cover_high";

            string programAPI = "https://api.open-meteo.com/v1/forecast?" + coordinates + "&hourly=temperature_2m,relative_humidity_2m,precipitation_probability,rain,showers,snowfall,cloud_cover,cloud_cover_low,cloud_cover_mid,cloud_cover_high&forecast_days=1";
            Task<WeatherData> weatherDataTask = MakeRequestAndSaveToJson(programAPI);
            currentWeather = weatherDataTask.Result;
            Console.WriteLine("Weather data updated.");
        }

        static async Task StartServerAsync()
        {
            while (true)
            {
                using (var server = new NamedPipeServerStream("WeatherApp"))
                {
                    await server.WaitForConnectionAsync();

                    using (StreamReader reader = new StreamReader(server))
                    using (StreamWriter writer = new StreamWriter(server))
                    {
                        while (server.IsConnected)
                        {
                            try
                            {
                                var line = reader.ReadLine();

                                lock (lockObject)
                                {
                                    if (line == null)
                                    {
                                        Console.WriteLine("Client disconnected. Waiting for reconnection...");
                                        break;
                                    }

                                    if (line.ToLower() == "exit")
                                    {
                                        Console.WriteLine("Client requested exit");
                                        break;
                                    }
                                    else if (line.ToLower() == "subscribe")
                                    {
                                        Console.WriteLine("Client subscribed");
                                        subscribed = true;

                                    }
                                    else if (line.ToLower() == "unsubscribe")
                                    {
                                        Console.WriteLine("Client unsubscribed");
                                        subscribed = false;

                                    }
                                    else if (line.ToLower().StartsWith("citychange"))
                                    {
                                        int index = line.IndexOf(' ');
                                        string city;
                                        if (index != -1 && index < line.Length - 1)
                                        {
                                            city = line.Substring(index + 1);
                                            if (city.ToLower() == "varash" || city.ToLower() == "lviv" ||
                                            city.ToLower() == "vinnytsia" || city.ToLower() == "duisburg")
                                            {
                                                currentCity = city;
                                                writer.WriteLine("City changed");
                                                writer.Flush();
                                                Console.WriteLine($"Client changed city to {city}");
                                                UpdateWeatherData(currentCity);
                                            }
                                            else
                                            {
                                                writer.WriteLine("Wrong city. Request cancelled");
                                                writer.Flush();
                                            }
                                        }
                                        else
                                        {
                                            writer.WriteLine("Wrong city. Request cancelled");
                                            writer.Flush();

                                        }
                                    }
                                    else if (line.ToLower() == "get")
                                    {
                                        if (subscribed)
                                        {
                                            string output = JsonConvert.SerializeObject(currentWeather);
                                            Console.WriteLine("Client got data");
                                            writer.WriteLine(output);
                                            writer.Flush();
                                        }
                                        else
                                        {
                                            Console.WriteLine("Client is not subscribed");
                                            writer.WriteLine("You are not subscribed");
                                            writer.Flush();
                                        }
                                    }
                                }
                            }
                            catch (IOException)
                            {
                                Console.WriteLine("Client disconnected. Waiting for reconnection...");
                                break;
                            }
                        }
                    }
                }
            }
        }




        static async Task<WeatherData> MakeRequestAndSaveToJson(string apiUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync(apiUrl);
                    response.EnsureSuccessStatusCode();
                    string jsonResult = await response.Content.ReadAsStringAsync();
                    System.IO.File.WriteAllText("result.json", jsonResult);
                    WeatherData data = JsonConvert.DeserializeObject<WeatherData>(jsonResult);
                    Console.WriteLine(data);
                    return data;
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine($"Помилка при виконанні запиту: {e.Message}");
                    return null; // або можна повернути пусті рядки або інші значущі помилки
                }
            }
        }


    }
}