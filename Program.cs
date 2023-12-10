﻿using Newtonsoft.Json;
using Npgsql;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        public HourlyUnits hourly_units { get; set; }
        public HourlyData hourly { get; set; }

        public override string ToString()
        {
            return $"Latitude: {latitude}, Longitude: {longitude}, GenerationTimeMs: {generationtime_ms}, UtcOffsetSeconds: {utc_offset_seconds}, Timezone: {timezone}, TimezoneAbbreviation: {timezone_abbreviation}, Elevation: {elevation}, \nHourlyUnits: {hourly_units}, \nHourly: {hourly}";
        }
    }

    public class HourlyUnits
    {
        public string time { get; set; }
        public string temperature_2m { get; set; }
        public string relative_humidity_2m { get; set; }
        public string apparent_temperature { get; set; }
        public string precipitation_probability { get; set; }
        public string weather_code { get; set; }
        public string cloud_cover { get; set; }
        public string wind_speed_10m { get; set; }

        public override string ToString()
        {
            return $"Time: {time}, Temperature2m: {temperature_2m}, RelativeHumidity2m: {relative_humidity_2m}, ApparentTemperature: {apparent_temperature}, PrecipitationProbability: {precipitation_probability}, WeatherCode: {weather_code}, CloudCover: {cloud_cover}, WindSpeed10m: {wind_speed_10m}";
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
            return $"Time: [{string.Join(", ", time)}], Temperature2m: [{string.Join(", ", temperature_2m)}], RelativeHumidity2m: [{string.Join(", ", relative_humidity_2m)}], ApparentTemperature: [{string.Join(", ", apparent_temperature)}], PrecipitationProbability: [{string.Join(", ", precipitation_probability)}], WeatherCode: [{string.Join(", ", weather_code)}], CloudCover: [{string.Join(", ", cloud_cover)}], WindSpeed10m: [{string.Join(", ", wind_speed_10m)}]";
        }
    }

    class Program
    {
        private static readonly object lockObject = new object();
        static bool subscribed = false;
        static WeatherData currentWeather = new WeatherData();
        static string currentCity = "lviv";
        static List<Tuple<string, string>> availableCities = new List<Tuple<string, string>>(){
             new Tuple<string, string>("varash", "latitude=51.3509&longitude=25.8474"),
             new Tuple<string, string>("lviv", "latitude=49.8383&longitude=24.0232"),
             new Tuple<string, string>("vinnytsia", "latitude=49.2322&longitude=28.4687"),
             new Tuple<string, string>("duisburg", "latitude=51.4325&longitude=6.7652"),


        };


        static string connectionString = "Host=localhost; port=1488; Username=postgres;Password=zxcOrest1;Database=oslab9db";


        static void Main(string[] args)
        {
            // BD LOCATION + 







            // BD LOCATION -

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
                    cmd.CommandText = "INSERT INTO WeatherData (latitude, longitude, generationtime_ms, utc_offset_seconds, timezone, timezone_abbreviation, elevation) VALUES (@Latitude, @Longitude, @GenerationTimeMs, @UtcOffsetSeconds, @Timezone, @TimezoneAbbreviation, @Elevation)";

                    // Додаємо параметри
                    cmd.Parameters.AddWithValue("@Latitude", weatherData.latitude);
                    cmd.Parameters.AddWithValue("@Longitude", weatherData.longitude);
                    cmd.Parameters.AddWithValue("@GenerationTimeMs", weatherData.generationtime_ms);
                    cmd.Parameters.AddWithValue("@UtcOffsetSeconds", weatherData.utc_offset_seconds);
                    cmd.Parameters.AddWithValue("@Timezone", weatherData.timezone);
                    cmd.Parameters.AddWithValue("@TimezoneAbbreviation", weatherData.timezone_abbreviation);
                    cmd.Parameters.AddWithValue("@Elevation", weatherData.elevation);

                    // Виконуємо команду
                    cmd.ExecuteNonQuery();
                }

                // Закриваємо з'єднання
                connection.Close();
            }
        }

        static void getDataFromSQL(string connectionString)
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();

                // Створюємо команду SQL для вибірки всіх даних з таблиці WeatherData
                using (NpgsqlCommand cmd = new NpgsqlCommand("SELECT * FROM WeatherData", connection))
                {
                    using (NpgsqlDataReader reader = cmd.ExecuteReader())
                    {
                        // Читаємо дані та виводимо їх на екран
                        while (reader.Read())
                        {
                            Console.WriteLine($"Latitude: {reader["latitude"]}, Longitude: {reader["longitude"]}, GenerationTimeMs: {reader["generationtime_ms"]}, UtcOffsetSeconds: {reader["utc_offset_seconds"]}, Timezone: {reader["timezone"]}, TimezoneAbbreviation: {reader["timezone_abbreviation"]}, Elevation: {reader["elevation"]}");
                        }
                    }
                }

                // Закриваємо з'єднання
                connection.Close();
            }
        }

        static void UpdateWeatherData()
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

            string programAPI = "https://api.open-meteo.com/v1/forecast?" + coordinates + "&hourly=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation_probability,weather_code,cloud_cover,wind_speed_10m&forecast_days=1";
            Task<WeatherData> weatherDataTask = MakeRequestAndSaveToJson(programAPI);
            currentWeather = weatherDataTask.Result;
            Console.WriteLine("Weather data updated.");

            // BD



            addDataToSQL(connectionString, currentWeather);
            Console.WriteLine("zxc");
            getDataFromSQL(connectionString);


            // BD
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
                                                UpdateWeatherData();
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