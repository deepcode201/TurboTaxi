using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
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
            _http = http;
            _logger = logger;
        }

        // ==== Google cavab? üçün daxili DTO-lar ====
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

        // ==== OSRM (Open Source Routing Machine) DTO-lar ====
        private class OsrmResponse
        {
            public string? Code { get; set; }
            public List<OsrmRoute>? Routes { get; set; }
        }

        private class OsrmRoute
        {
            public double Distance { get; set; } // meters
            public double Duration { get; set; } // seconds
            public string? Geometry { get; set; } // encoded polyline
        }

        public async Task<RouteEstimateResponse> EstimateAsync(
            RouteEstimateRequest request,
            CancellationToken ct = default)
        {
            _logger.LogInformation($"?? Route Request:");
            _logger.LogInformation($"   Origin:      [{request.Origin.Latitude:F6}, {request.Origin.Longitude:F6}]");
            _logger.LogInformation($"   Destination: [{request.Destination.Latitude:F6}, {request.Destination.Longitude:F6}]");

            var apiKey = _config["GoogleMaps:ApiKey"];
            var useGoogle = !string.IsNullOrWhiteSpace(apiKey);

            _logger.LogInformation($"   Google API Key: {(useGoogle ? "? Available" : "? Not configured")}");

            RouteEstimateResponse? result = null;

            // ? Try Google Routes API first
            if (useGoogle)
            {
                _logger.LogInformation($"?? Attempting Google Routes API...");
                result = await TryGoogleRoutesAsync(request, apiKey!, ct);
                
                if (result.Success)
                {
                    _logger.LogInformation($"? Google Routes API succeeded!");
                    return result;
                }
                
                _logger.LogWarning($"?? Google Routes API failed: {result.Message}");
            }

            // ? Fallback to OSRM (Open Source Routing Machine) - FREE!
            _logger.LogInformation($"?? Attempting OSRM (Open Source Routing)...");
            result = await TryOsrmAsync(request, ct);
            
            if (result.Success)
            {
                _logger.LogInformation($"? OSRM succeeded!");
                return result;
            }

            _logger.LogError($"? All routing APIs failed!");
            return new RouteEstimateResponse
            {
                Success = false,
                Message = "Unable to calculate route. All routing services failed."
            };
        }

        private async Task<RouteEstimateResponse> TryGoogleRoutesAsync(
            RouteEstimateRequest request,
            string apiKey,
            CancellationToken ct)
        {
            try
            {
                var departureTime = DateTime.UtcNow.AddMinutes(5).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

                var googleRequest = new
                {
                    origin = new
                    {
                        location = new
                        {
                            latLng = new
                            {
                                latitude = request.Origin.Latitude,
                                longitude = request.Origin.Longitude
                            }
                        }
                    },
                    destination = new
                    {
                        location = new
                        {
                            latLng = new
                            {
                                latitude = request.Destination.Latitude,
                                longitude = request.Destination.Longitude
                            }
                        }
                    },
                    travelMode = "DRIVE",
                    routingPreference = "TRAFFIC_AWARE_OPTIMAL",
                    departureTime
                };

                using var httpReq = new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://routes.googleapis.com/directions/v2:computeRoutes");

                httpReq.Headers.Add("X-Goog-Api-Key", apiKey);
                httpReq.Headers.Add("X-Goog-FieldMask",
                    "routes.distanceMeters,routes.duration,routes.polyline.encodedPolyline");

                httpReq.Content = JsonContent.Create(googleRequest);

                var httpResp = await _http.SendAsync(httpReq, ct);

                if (!httpResp.IsSuccessStatusCode)
                {
                    var errorBody = await httpResp.Content.ReadAsStringAsync(ct);
                    _logger.LogError($"   Google API HTTP {(int)httpResp.StatusCode}: {errorBody}");
                    
                    return new RouteEstimateResponse
                    {
                        Success = false,
                        Message = $"Google API HTTP error: {(int)httpResp.StatusCode}"
                    };
                }

                var responseBody = await httpResp.Content.ReadAsStringAsync(ct);
                
                GoogleComputeRoutesResponse? googleData = null;
                using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(responseBody)))
                {
                    googleData = await JsonSerializer.DeserializeAsync<GoogleComputeRoutesResponse>(
                        stream,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                        ct);
                }

                var route = googleData?.Routes?.FirstOrDefault();
                if (route is null || string.IsNullOrWhiteSpace(route.Polyline?.EncodedPolyline))
                {
                    _logger.LogWarning($"   Google API returned no routes or empty polyline");
                    return new RouteEstimateResponse
                    {
                        Success = false,
                        Message = "Google API returned no routes"
                    };
                }

                var distanceKm = route.DistanceMeters / 1000.0;
                var durationMinutes = ParseDurationToMinutes(route.Duration);

                _logger.LogInformation($"   ? Distance: {distanceKm:F2}km, Duration: {durationMinutes}min, Polyline: {route.Polyline.EncodedPolyline.Length} chars");

                return new RouteEstimateResponse
                {
                    Success = true,
                    Message = "Route calculated via Google Maps",
                    Data = new RouteEstimateData
                    {
                        DistanceKm = Math.Round(distanceKm, 2),
                        DurationMinutes = durationMinutes,
                        EncodedPolyline = route.Polyline.EncodedPolyline,
                        Price = new RoutePriceDto()
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"   Google Routes API exception");
                return new RouteEstimateResponse
                {
                    Success = false,
                    Message = $"Google API exception: {ex.Message}"
                };
            }
        }

        private async Task<RouteEstimateResponse> TryOsrmAsync(
            RouteEstimateRequest request,
            CancellationToken ct)
        {
            try
            {
                // OSRM uses lng,lat format (opposite of Google)!
                var url = $"https://router.project-osrm.org/route/v1/driving/" +
                          $"{request.Origin.Longitude},{request.Origin.Latitude};" +
                          $"{request.Destination.Longitude},{request.Destination.Latitude}" +
                          $"?overview=full&geometries=polyline";

                _logger.LogInformation($"   OSRM URL: {url}");

                var response = await _http.GetAsync(url, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogError($"   OSRM HTTP {(int)response.StatusCode}: {errorBody}");
                    
                    return new RouteEstimateResponse
                    {
                        Success = false,
                        Message = $"OSRM HTTP error: {(int)response.StatusCode}"
                    };
                }

                var responseBody = await response.Content.ReadAsStringAsync(ct);
                
                OsrmResponse? osrmData = null;
                using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(responseBody)))
                {
                    osrmData = await JsonSerializer.DeserializeAsync<OsrmResponse>(
                        stream,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                        ct);
                }

                if (osrmData?.Code != "Ok" || osrmData.Routes == null || osrmData.Routes.Count == 0)
                {
                    _logger.LogWarning($"   OSRM returned no routes or error code: {osrmData?.Code}");
                    return new RouteEstimateResponse
                    {
                        Success = false,
                        Message = $"OSRM returned no routes (code: {osrmData?.Code})"
                    };
                }

                var route = osrmData.Routes.First();
                var distanceKm = route.Distance / 1000.0;
                var durationMinutes = (int)Math.Round(route.Duration / 60.0);

                _logger.LogInformation($"   ? Distance: {distanceKm:F2}km, Duration: {durationMinutes}min, Polyline: {route.Geometry?.Length ?? 0} chars");

                return new RouteEstimateResponse
                {
                    Success = true,
                    Message = "Route calculated via OSRM (Open Source)",
                    Data = new RouteEstimateData
                    {
                        DistanceKm = Math.Round(distanceKm, 2),
                        DurationMinutes = durationMinutes,
                        EncodedPolyline = route.Geometry,
                        Price = new RoutePriceDto()
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"   OSRM exception");
                return new RouteEstimateResponse
                {
                    Success = false,
                    Message = $"OSRM exception: {ex.Message}"
                };
            }
        }

        private static int ParseDurationToMinutes(string? duration)
        {
            if (string.IsNullOrWhiteSpace(duration))
                return 0;

            // "1320s" ? 1320 saniy?
            if (duration.EndsWith("s") &&
                int.TryParse(duration.TrimEnd('s'), out var seconds))
            {
                return (int)Math.Round(seconds / 60.0);
            }

            return 0;
        }
    }
}
