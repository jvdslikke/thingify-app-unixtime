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

        while (!stoppingToken.IsCancellationRequested)
        {
            // measure
            var measuredOn = DateTime.Now;
            var measurement = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // create request data
            var things = new List<Thing>();

            var unixTimeThing = new Thing(
                "UnixTime",
                "Unix time"
            );
            unixTimeThing.MeasurementUnit = "seconds";

            things.Add(unixTimeThing);

            unixTimeThing.Measurements.Add(new Measurement(measuredOn, measurement));

            _logger.LogInformation("Sending data to service");
            
            _ = PostThings(things, thingsApiHttpClient);

            var timeToWaitToNextSecond = measuredOn.Add(_scanInterval) - DateTime.Now;
            _logger.LogInformation("Waiting for {WaitTimeSecs} seconds", timeToWaitToNextSecond.TotalSeconds);

            await Task.Delay(timeToWaitToNextSecond, stoppingToken);
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
                var result = await httpClient.PatchAsync("http://localhost:5036/api-rest/things", thingsJson);

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