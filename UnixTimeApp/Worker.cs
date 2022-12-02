using System.Threading.Tasks;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Net.Http;
using System.Linq;
using Microsoft.Extensions.Hosting;
using System.IO.Ports;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace UnixTimeApp;

public class Worker : BackgroundService
{
    private static readonly TimeSpan _scanInterval = TimeSpan.FromSeconds(1); 

    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var thingsApiHttpClient = new HttpClient();

        var stopwatch = new Stopwatch();

        while (!stoppingToken.IsCancellationRequested)
        {
            stopwatch.Restart();

            var things = new List<Thing>();

            var rainForecastThing = new Thing(
                "rainforecast",
                "Rain forecast"
            );
            rainForecastThing.MeasurementUnit = "ticks";

            things.Add(rainForecastThing);

            rainForecastThing.Measurements.Add(new Measurement(DateTime.Now, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

            stopwatch.Stop();

            var waitTime = _scanInterval - stopwatch.Elapsed;
            if (waitTime.Ticks < 0)
            {
                waitTime = _scanInterval;
            }
            _logger.LogInformation("Sending data to service and waiting for {WaitTimeSecs} seconds", waitTime.TotalSeconds);
            
            _ = PostThings(things, thingsApiHttpClient);

            await Task.Delay(waitTime, stoppingToken);
        }
    }

    private async Task PostThings(IEnumerable<Thing> things, HttpClient httpClient)
    {
        var thingsJson = new StringContent(
            JsonSerializer.Serialize(things),
            Encoding.UTF8,
            Application.Json);

        using (_logger.BeginScope("Api call"))
        {
            _logger.LogInformation("API call started");
            try
            {
                var result = await httpClient.PatchAsync("http://thingify-core:80/api-rest/things", thingsJson);

                result.EnsureSuccessStatusCode();
                _logger.LogInformation("API call completed succesfully");
            }
            catch(HttpRequestException apiCallError)
            {
                _logger.LogError(apiCallError, "API call failed");
            }
        }
    }
}