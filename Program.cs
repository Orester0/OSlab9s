using Newtonsoft.Json;
using Npgsql;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;

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
        public string сity { get; set; }

        public DateTime requestTime { get; set; }

        public HourlyData hourly { get; set; }

        public override string ToString()
        {
            return $"Latitude: {latitude}, Longitude: {longitude}, GenerationTimeMs: {generationtime_ms}, UtcOffsetSeconds: {utc_offset_seconds}, Timezone: {timezone}, TimezoneAbbreviation: {timezone_abbreviation}, Elevation: {elevation}, City: {сity}, DateTime: {requestTime}, Hourly: {hourly}";
        }

    }

    public class HourlyData
    {
        public List<string> time { get; set; }
        public List<double> temperature_2m { get; set; }
        public List<int> relative_humidity_2m { get; set; }
        public List<double> apparent_temperature { get; set; }
        public List<int> precipitation_probability { get; set; }
        public List<int> weather_code { get; set; }
        public List<int> cloud_cover { get; set; }
        public List<double> wind_speed_10m { get; set; }

        public override string ToString()
        {
            return $" Temperature2m: [{string.Join(", ", temperature_2m)}], RelativeHumidity2m: [{string.Join(", ", relative_humidity_2m)}], ApparentTemperature: [{string.Join(", ", apparent_temperature)}], PrecipitationProbability: [{string.Join(", ", precipitation_probability)}], WeatherCode: [{string.Join(", ", weather_code)}], CloudCover: [{string.Join(", ", cloud_cover)}], WindSpeed10m: [{string.Join(", ", wind_speed_10m)}]";
        }
    }

    class Program
    {
        private static readonly object lockObject = new object();
        static bool subscribed = false;
        static WeatherData currentWeather = new WeatherData();
        static string currentCity = "duisburg";
        static DateTime requestTime;
        static List<Tuple<string, string>> availableCities = new List<Tuple<string, string>>(){
             new Tuple<string, string>("varash", "latitude=51.3509&longitude=25.8474"),
             new Tuple<string, string>("lviv", "latitude=49.8383&longitude=24.0232"),
             new Tuple<string, string>("vinnytsia", "latitude=49.2322&longitude=28.4687"),
             new Tuple<string, string>("duisburg", "latitude=51.4325&longitude=6.7652"),


        };


        static string connectionString = "Host=localhost; port=5432; Username=postgres;Password=1w2e3r4t5y;Database=WeatherData";


        static void Main(string[] args)
        {

            UpdateWeatherData();
            StartServerAsync();

            Task.Delay(1000).Wait();

            while (true)
            {
                // Server continues to do other tasks or can exit if needed
                // For this example, the server does not perform any additional tasks
            }
        }

        static void addDataToSQL(string connectionString, WeatherData weatherData)
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();

                using (NpgsqlCommand cmd = new NpgsqlCommand())
                {
                    cmd.Connection = connection;
                    cmd.CommandText = "INSERT INTO WD (latitude, longitude, generationtime_ms, utc_offset_seconds, timezone, timezone_abbreviation, elevation,temperature_2m,relative_humidity_2m,apparent_temperature,precipitation_probability,weather_code,cloud_cover,wind_speed_10m,date_time,city) VALUES (@Latitude, @Longitude, @GenerationTimeMs, @UtcOffsetSeconds, @Timezone, @TimezoneAbbreviation, @Elevation,@temperature_2m,@relative_humidity_2m,@apparent_temperature,@precipitation_probability,@weather_code,@cloud_cover,@wind_speed_10m,@requestTime,@city)";

                   
                    cmd.Parameters.AddWithValue("@Latitude", weatherData.latitude);
                    cmd.Parameters.AddWithValue("@Longitude", weatherData.longitude);
                    cmd.Parameters.AddWithValue("@GenerationTimeMs", weatherData.generationtime_ms);
                    cmd.Parameters.AddWithValue("@UtcOffsetSeconds", weatherData.utc_offset_seconds);
                    cmd.Parameters.AddWithValue("@Timezone", weatherData.timezone);
                    cmd.Parameters.AddWithValue("@TimezoneAbbreviation", weatherData.timezone_abbreviation);
                    cmd.Parameters.AddWithValue("@temperature_2m", weatherData.hourly.temperature_2m);
                    cmd.Parameters.AddWithValue("@relative_humidity_2m", weatherData.hourly.relative_humidity_2m);
                    cmd.Parameters.AddWithValue("@apparent_temperature", weatherData.hourly.apparent_temperature);
                    cmd.Parameters.AddWithValue("@precipitation_probability", weatherData.hourly.precipitation_probability);
                    cmd.Parameters.AddWithValue("@weather_code", weatherData.hourly.weather_code);
                    cmd.Parameters.AddWithValue("@cloud_cover", weatherData.hourly.cloud_cover);
                    cmd.Parameters.AddWithValue("@wind_speed_10m", weatherData.hourly.wind_speed_10m);
                    cmd.Parameters.AddWithValue("@Elevation", weatherData.elevation);
                    cmd.Parameters.AddWithValue("@requestTime", weatherData.requestTime);
                    cmd.Parameters.AddWithValue("@city", weatherData.сity);

                  
                    cmd.ExecuteNonQuery();
                }

               
                connection.Close();

            }
        }

       
        static void UpdateWeatherData()
        {
            string coordinates = availableCities
            .Where(city => city.Item1 == currentCity)
            .Select(city => city.Item2)
            .FirstOrDefault();

            requestTime = DateTime.Now;
            DataTable res = SearchInDatabase(connectionString);
            if (res.Rows.Count > 0)
            {
                Console.WriteLine("Found data in the database.");

                DataRow row = res.Rows[0];
                int temperature_2mIndex = res.Columns.IndexOf("temperature_2m");

               
                currentWeather = new WeatherData
                {
                    latitude = Convert.ToDouble(row["latitude"]),
                    longitude = Convert.ToDouble(row["longitude"]),
                    generationtime_ms = Convert.ToDouble(row["generationtime_ms"]),
                    utc_offset_seconds = Convert.ToInt32(row["utc_offset_seconds"]),
                    timezone = Convert.ToString(row["timezone"]),
                    timezone_abbreviation = Convert.ToString(row["timezone_abbreviation"]),
                    elevation = Convert.ToDouble(row["elevation"]),
                    сity = Convert.ToString(row["city"]),
                    requestTime = Convert.ToDateTime(row["date_time"]), 

                   hourly = new HourlyData
                    {
                       temperature_2m = row.Field<double[]>(temperature_2mIndex)?.ToList() ?? new List<double>(),
                       relative_humidity_2m = row.Field<int[]>("relative_humidity_2m")?.ToList() ?? new List<int>(),
                       apparent_temperature = row.Field<double[]>("apparent_temperature")?.ToList() ?? new List<double>(),
                       precipitation_probability = row.Field<int[]>("precipitation_probability")?.ToList() ?? new List<int>(),
                       weather_code = row.Field<int[]>("weather_code")?.ToList() ?? new List<int>(),
                       cloud_cover = row.Field<int[]>("cloud_cover")?.ToList() ?? new List<int>(),
                       wind_speed_10m = row.Field<double[]>("wind_speed_10m")?.ToList() ?? new List<double>(),
                   }
                };
                Console.WriteLine(currentWeather);
                Console.WriteLine("Weather data loaded from the database.");
            }
            else
            {

                if (coordinates != null)
                {
                    Console.WriteLine($"Значення для {currentCity} : {coordinates}");
                }
                else
                {
                    Console.WriteLine($"Місто {currentCity} не знайдено.");
                }
                //string programAPI = "https://api.open-meteo.com/v1/forecast?latitude=52.52&longitude=13.41&hourly=temperature_2m,relative_humidity_2m,precipitation_probability,rain,showers,snowfall,cloud_cover,cloud_cover_low,cloud_cover_mid,cloud_cover_high";

                string programAPI = "https://api.open-meteo.com/v1/forecast?" + coordinates + "&hourly=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation_probability,weather_code,cloud_cover,wind_speed_10m&forecast_days=1";
                Task<WeatherData> weatherDataTask = MakeRequestAndSaveToJson(programAPI);
                currentWeather = weatherDataTask.Result;
                currentWeather.requestTime = requestTime;
                currentWeather.сity = currentCity;
                Console.WriteLine("Weather data updated.");
                addDataToSQL(connectionString, currentWeather);
                Console.WriteLine(requestTime);
            }
           
        }

        static async Task StartServerAsync()
        {
            while (true)
            {
                using (var server = new NamedPipeServerStream("WeatherApp"))
                {
                    await server.WaitForConnectionAsync();
                    DateTime currentDateTime = DateTime.Now;
                    using (StreamReader reader = new StreamReader(server))
                    using (StreamWriter writer = new StreamWriter(server))
                    using (StreamWriter file = new StreamWriter("log.txt", true))
                    {
                        while (server.IsConnected)
                        {
                            Console.WriteLine("conected");
                            try
                            {
                                var line = reader.ReadLine();

                                lock (lockObject)
                                {
                                    if (line == null)
                                    {
                                        file.WriteLine(currentDateTime + "Client disconnect");
                                        Console.WriteLine("Client disconnected. Waiting for reconnection...");
                                        break;
                                    }

                                    if (line.ToLower() == "exit")
                                    {
                                        file.WriteLine(currentDateTime + "Client request exit");

                                        Console.WriteLine("Client requested exit");
                                        break;
                                    }
                                    else if (line.ToLower() == "subscribe")
                                    {
                                        file.WriteLine(currentDateTime + "Client subscribed");
                                        Console.WriteLine("Client subscribed");
                                        subscribed = true;

                                    }
                                    else if (line.ToLower() == "unsubscribe")
                                    {
                                        file.WriteLine(currentDateTime + "Client unsubscribed");

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

                                            bool cityExists = false;

                                            foreach(var i in availableCities)
                                            {
                                                if(i.Item1.ToLower() == city.ToLower())
                                                {
                                                    cityExists = true;
                                                    break;
                                                }
                                            }

                                            if (cityExists)
                                            {
                                                currentCity = city;
                                                writer.WriteLine("City changed");
                                                writer.Flush();
                                                Console.WriteLine($"Client changed city to {city}");
                                                file.WriteLine(currentDateTime + $"Client changed city to {city}");
                                                UpdateWeatherData();
                                            }
                                            else
                                            {
                                                writer.WriteLine("Wrong city. Request cancelled");
                                                file.WriteLine(currentDateTime + "Wrong city request");

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
                                            file.WriteLine(currentDateTime + "Client got data");

                                            writer.Flush();
                                        }
                                        else
                                        {
                                            file.WriteLine(currentDateTime + "Client is not subscribed to get data");
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
                    return null; 
                }
            }
        }

        static DataTable SearchInDatabase(string connectionString)
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();

                using (NpgsqlCommand command = new NpgsqlCommand())
                {
                    command.Connection = connection;
                    command.CommandText = "SELECT * FROM WD WHERE DATE_PART('year', date_time) = @YearToSearch AND DATE_PART('month', date_time) = @MonthToSearch AND DATE_PART('day', date_time) = @DayToSearch AND city = @CityToSearch";

                    command.Parameters.AddWithValue("@YearToSearch", requestTime.Year);
                    command.Parameters.AddWithValue("@MonthToSearch", requestTime.Month);
                    command.Parameters.AddWithValue("@DayToSearch", requestTime.Day);
                    command.Parameters.AddWithValue("@CityToSearch", currentCity);

                    using (NpgsqlDataAdapter adapter = new NpgsqlDataAdapter(command))
                    {
                        DataTable dataTable = new DataTable();
                        adapter.Fill(dataTable);

                        return dataTable;
                    }
                }
            }
        }
    }


}

