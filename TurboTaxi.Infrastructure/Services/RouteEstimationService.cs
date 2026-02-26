using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.Routes;

namespace TurboTaxi.Infrastructure.Services
{
    public class RouteEstimationService : IRouteEstimationService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _http;
        private readonly ILogger<RouteEstimationService> _logger;

        public RouteEstimationService(
            IConfiguration config,
            HttpClient http,
            ILogger<RouteEstimationService> logger)
        {
            _config = config;
            _http   = http;
            _logger = logger;
        }

        // ── Google response DTOs ──────────────────────────────────────────
        private class GoogleComputeRoutesResponse
        {
            public List<GoogleRoute>? Routes { get; set; }
        }

        private class GoogleRoute
        {
            public int DistanceMeters { get; set; }
            public string? Duration { get; set; }
            public GooglePolyline? Polyline { get; set; }
        }

        private class GooglePolyline
        {
            public string? EncodedPolyline { get; set; }
        }
        // ─────────────────────────────────────────────────────────────────

        public async Task<RouteEstimateResponse> EstimateAsync(
            RouteEstimateRequest request,
            CancellationToken ct = default)
        {
            _logger.LogInformation(
                "Route | Origin:[{OLat:F6},{OLng:F6}] -> Dest:[{DLat:F6},{DLng:F6}] | {VT}",
                request.Origin.Latitude,
                request.Origin.Longitude,
                request.Destination.Latitude,
                request.Destination.Longitude,
                request.VehicleType);

            var apiKey = _config["GoogleMaps:ApiKey"]?.Trim();

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("Google Maps API key is not configured.");
                return Fail("Google Maps API key is missing.");
            }

            _logger.LogInformation(
                "API Key | Length:{Len} | First6:{First} | Last4:{Last} | Source check: appsettings -> env override possible",
                apiKey.Length,
                apiKey[..Math.Min(6, apiKey.Length)],
                apiKey[^Math.Min(4, apiKey.Length)..]);

            // ── Attempt 1: TRAFFIC_AWARE_OPTIMAL (real-time traffic) ─────────
            _logger.LogInformation("[1/2] Google TRAFFIC_AWARE_OPTIMAL...");
            var result = await CallGoogleAsync(request, apiKey, trafficAware: true, ct);
            if (result.Success)
            {
                _logger.LogInformation("[1/2] Succeeded");
                return result;
            }
            _logger.LogWarning("[1/2] Failed: {Msg}", result.Message);

            // ── Attempt 2: TRAFFIC_UNAWARE (same Google road network) ────────
            _logger.LogInformation("[2/2] Google TRAFFIC_UNAWARE...");
            result = await CallGoogleAsync(request, apiKey, trafficAware: false, ct);
            if (result.Success)
            {
                _logger.LogInformation("[2/2] Succeeded");
                return result;
            }
            _logger.LogError("[2/2] Failed: {Msg}", result.Message);

            return Fail($"Google Routes API failed. Last error: {result.Message}");
        }

        private async Task<RouteEstimateResponse> CallGoogleAsync(
            RouteEstimateRequest request,
            string apiKey,
            bool trafficAware,
            CancellationToken ct)
        {
            var routingPreference = trafficAware ? "TRAFFIC_AWARE_OPTIMAL" : "TRAFFIC_UNAWARE";
            try
            {
                object body = trafficAware
                    ? new
                    {
                        origin        = Loc(request.Origin),
                        destination   = Loc(request.Destination),
                        travelMode    = "DRIVE",
                        routingPreference,
                        departureTime = DateTime.UtcNow.AddMinutes(5)
                                            .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")
                    }
                    : (object)new
                    {
                        origin      = Loc(request.Origin),
                        destination = Loc(request.Destination),
                        travelMode  = "DRIVE",
                        routingPreference
                    };

                using var req = new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://routes.googleapis.com/directions/v2:computeRoutes");

                req.Headers.Add("X-Goog-Api-Key", apiKey);
                req.Headers.Add("X-Goog-FieldMask",
                    "routes.distanceMeters,routes.duration,routes.polyline.encodedPolyline");
                req.Content = JsonContent.Create(body);

                var resp = await _http.SendAsync(req, ct);

                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync(ct);
                    _logger.LogError(
                        "Google [{Pref}] HTTP {Code}: {Body}",
                        routingPreference, (int)resp.StatusCode, err);
                    return Fail($"Google HTTP {(int)resp.StatusCode}: {err}");
                }

                var json = await resp.Content.ReadAsStringAsync(ct);
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
                var data = await JsonSerializer.DeserializeAsync<GoogleComputeRoutesResponse>(
                    stream,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    ct);

                var route = data?.Routes?.FirstOrDefault();
                if (route is null || string.IsNullOrWhiteSpace(route.Polyline?.EncodedPolyline))
                {
                    _logger.LogWarning("Google [{Pref}] returned empty route", routingPreference);
                    return Fail("Google returned no route data.");
                }

                var distanceKm      = Math.Round(route.DistanceMeters / 1000.0, 2);
                var durationMinutes = ParseDurationSeconds(route.Duration);

                _logger.LogInformation(
                    "Google [{Pref}] -> {Dist}km / {Min}min | polyline:{Len}ch",
                    routingPreference,
                    distanceKm,
                    durationMinutes,
                    route.Polyline.EncodedPolyline.Length);

                return new RouteEstimateResponse
                {
                    Success = true,
                    Message = $"Route via Google Maps ({routingPreference})",
                    Data = new RouteEstimateData
                    {
                        DistanceKm      = distanceKm,
                        DurationMinutes = durationMinutes,
                        EncodedPolyline = route.Polyline.EncodedPolyline,
                        Source          = "Google",
                        Price           = new RoutePriceDto()
                    }
                };
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("Google [{Pref}] request timed out.", routingPreference);
                return Fail("Google API request timed out.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Google [{Pref}] exception", routingPreference);
                return Fail($"Google API exception: {ex.Message}");
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static object Loc(Coordinate c) =>
            new { location = new { latLng = new { latitude = c.Latitude, longitude = c.Longitude } } };

        private static int ParseDurationSeconds(string? duration)
        {
            // Google returns e.g. "1320s"
            if (!string.IsNullOrWhiteSpace(duration) &&
                duration.EndsWith('s') &&
                int.TryParse(duration.AsSpan(0, duration.Length - 1), out var seconds))
            {
                return (int)Math.Round(seconds / 60.0);
            }
            return 0;
        }

        private static RouteEstimateResponse Fail(string message) =>
            new() { Success = false, Message = message };
    }
}
