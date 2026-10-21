using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.Routes;

namespace TurboTaxi.Infrastructure.Services
{
    public class RouteEstimationService : IRouteEstimationService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _http;
        private readonly IFareCalculatorService _fareCalculator;
        private readonly IPromoCodeService _promoCodeService;
        private readonly ILogger<RouteEstimationService> _logger;

        public RouteEstimationService(
            IConfiguration config,
            HttpClient http,
            IFareCalculatorService fareCalculator,
            IPromoCodeService promoCodeService,
            ILogger<RouteEstimationService> logger)
        {
            _config = config;
            _http = http;
            _fareCalculator = fareCalculator;
            _promoCodeService = promoCodeService;
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
                await ApplyPriceAsync(result, request);
                return result;
            }
            _logger.LogWarning("[1/2] Failed: {Msg}", result.Message);

            // ── Attempt 2: TRAFFIC_UNAWARE (same Google road network) ────────
            _logger.LogInformation("[2/2] Google TRAFFIC_UNAWARE...");
            result = await CallGoogleAsync(request, apiKey, trafficAware: false, ct);
            if (result.Success)
            {
                _logger.LogInformation("[2/2] Succeeded");
                await ApplyPriceAsync(result, request);
                return result;
            }
            _logger.LogError("[2/2] Failed: {Msg}", result.Message);

            // ── Attempt 3: OSRM (road-following fallback, no API key) ───────
            _logger.LogInformation("[3/3] OSRM public routing...");
            result = await CallOsrmAsync(request, ct);
            if (result.Success)
            {
                _logger.LogInformation("[3/3] Succeeded");
                await ApplyPriceAsync(result, request);
                return result;
            }

            _logger.LogError("[3/3] Failed: {Msg}", result.Message);
            return Fail($"All routing providers failed. Last error: {result.Message}");
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
                    var err = await ReadMaybeCompressedAsync(resp, ct);
                    _logger.LogError(
                        "Google [{Pref}] HTTP {Code}: {Body}",
                        routingPreference, (int)resp.StatusCode, err);
                    return Fail($"Google HTTP {(int)resp.StatusCode}: {err}");
                }

                var json = await ReadMaybeCompressedAsync(resp, ct);
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
                        Source          = "Google"
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

        private async Task<RouteEstimateResponse> CallOsrmAsync(
            RouteEstimateRequest request,
            CancellationToken ct)
        {
            var baseUrl = _config["Routing:OsrmBaseUrl"]?.Trim() 
                ?? "http://router.project-osrm.org";

            var retryCount = int.TryParse(
                _config["Routing:OsrmRetryCount"], 
                out var rc) ? rc : 3;

            var retryDelayMs = int.TryParse(
                _config["Routing:OsrmRetryDelayMs"], 
                out var rd) ? rd : 1000;

            var url =
                $"{baseUrl.TrimEnd('/')}/route/v1/driving/" +
                $"{request.Origin.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                $"{request.Origin.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)};" +
                $"{request.Destination.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                $"{request.Destination.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                "?overview=full&geometries=polyline";

            for (int attempt = 1; attempt <= retryCount; attempt++)
            {
                try
                {
                    _logger.LogInformation(
                        "[OSRM] Attempt {Attempt}/{Max} to {Url}", 
                        attempt, retryCount, baseUrl);

                    using var req = new HttpRequestMessage(HttpMethod.Get, url);

                    req.Headers.TryAddWithoutValidation("User-Agent",
                        "TurboTaxi/1.0 (+http://turbotaxi.az)");
                    req.Headers.TryAddWithoutValidation("Accept", "application/json");

                    using var resp = await _http.SendAsync(req, ct);
                    var json = await ReadMaybeCompressedAsync(resp, ct);

                    if (!resp.IsSuccessStatusCode)
                    {
                        _logger.LogWarning(
                            "[OSRM] HTTP {Code} on attempt {Attempt}/{Max}: {Body}", 
                            (int)resp.StatusCode, attempt, retryCount, json);

                        // Retry on 403 (Forbidden) or 429 (Rate Limited)
                        if ((int)resp.StatusCode == 403 || (int)resp.StatusCode == 429)
                        {
                            if (attempt < retryCount)
                            {
                                var delay = retryDelayMs * attempt;
                                _logger.LogInformation(
                                    "[OSRM] Rate limited/forbidden, waiting {Ms}ms before retry #{Next}", 
                                    delay, attempt + 1);

                                await Task.Delay(delay, ct);
                                continue; // Retry
                            }
                        }

                        // Final attempt or non-retryable error
                        if (attempt == retryCount)
                        {
                            return Fail(
                                $"OSRM HTTP {(int)resp.StatusCode} " +
                                $"after {retryCount} attempts");
                        }
                        continue;
                    }

                    // ✅ Success - parse response
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("routes", out var routes) || 
                        routes.GetArrayLength() == 0)
                    {
                        _logger.LogWarning("[OSRM] No routes returned");
                        return Fail("OSRM returned no route data.");
                    }

                    var route = routes[0];
                    if (!route.TryGetProperty("geometry", out var geometryEl))
                        return Fail("OSRM route missing geometry.");

                    var encoded = geometryEl.GetString();
                    if (string.IsNullOrWhiteSpace(encoded))
                        return Fail("OSRM route geometry is empty.");

                    var distanceMeters = route.TryGetProperty("distance", 
                        out var distEl) ? distEl.GetDouble() : 0;
                    var durationSeconds = route.TryGetProperty("duration", 
                        out var durEl) ? durEl.GetDouble() : 0;

                    var distanceKm = Math.Round(distanceMeters / 1000.0, 2);
                    var durationMinutes = (int)Math.Max(1, 
                        Math.Round(durationSeconds / 60.0));

                    _logger.LogInformation(
                        "[OSRM] ✅ Success on attempt {Attempt}: {Dist}km / {Min}min | polyline:{Len}ch",
                        attempt,
                        distanceKm,
                        durationMinutes,
                        encoded.Length);

                    return new RouteEstimateResponse
                    {
                        Success = true,
                        Message = "Route via OSRM",
                        Data = new RouteEstimateData
                        {
                            DistanceKm = distanceKm,
                            DurationMinutes = durationMinutes,
                            EncodedPolyline = encoded,
                            Source = "OSRM"
                        }
                    };
                }
                catch (TaskCanceledException)
                {
                    _logger.LogError(
                        "[OSRM] Timeout on attempt {Attempt}/{Max}", 
                        attempt, retryCount);

                    if (attempt == retryCount)
                        return Fail("OSRM request timeout.");

                    var delay = retryDelayMs * attempt;
                    _logger.LogInformation(
                        "[OSRM] Timeout, waiting {Ms}ms before retry #{Next}", 
                        delay, attempt + 1);

                    await Task.Delay(delay, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "[OSRM] Exception on attempt {Attempt}/{Max}", 
                        attempt, retryCount);

                    if (attempt == retryCount)
                        return Fail($"OSRM exception: {ex.Message}");

                    var delay = retryDelayMs * attempt;
                    _logger.LogInformation(
                        "[OSRM] Exception, waiting {Ms}ms before retry #{Next}", 
                        delay, attempt + 1);

                    await Task.Delay(delay, ct);
                }
            }

            return Fail("OSRM failed after all retries.");
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// OSRM (nginx) gzips the body when Accept-Encoding is advertised.
        /// Parsing those bytes as JSON throws "'0x1F' is an invalid start of a value".
        /// </summary>
        private static async Task<string> ReadMaybeCompressedAsync(
            HttpResponseMessage response,
            CancellationToken ct)
        {
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length == 0)
                return string.Empty;

            if (StartsWithJson(bytes))
                return Encoding.UTF8.GetString(bytes);

            Stream stream;
            if (IsGzip(bytes))
                stream = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
            else if (HasEncoding(response, "br"))
                stream = new BrotliStream(new MemoryStream(bytes), CompressionMode.Decompress);
            else if (HasEncoding(response, "deflate"))
                stream = new DeflateStream(new MemoryStream(bytes), CompressionMode.Decompress);
            else
                return Encoding.UTF8.GetString(bytes);

            using (stream)
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                return await reader.ReadToEndAsync(ct);
        }

        private static bool IsGzip(ReadOnlySpan<byte> bytes) =>
            bytes.Length >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B;

        private static bool StartsWithJson(ReadOnlySpan<byte> bytes)
        {
            foreach (var b in bytes)
            {
                if (b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
                    continue;
                return b is (byte)'{' or (byte)'[';
            }

            return false;
        }

        private static bool HasEncoding(HttpResponseMessage response, string encoding) =>
            response.Content.Headers.ContentEncoding
                .Any(value => value.Contains(encoding, StringComparison.OrdinalIgnoreCase));

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

        private async Task ApplyPriceAsync(
            RouteEstimateResponse response,
            RouteEstimateRequest request)
        {
            if (response.Data == null)
                return;

            var originalFare = await _fareCalculator.CalculateEstimatedFareAsync(
                request.VehicleType.ToString(),
                response.Data.DistanceKm,
                response.Data.DurationMinutes,
                DateTime.UtcNow,
                "Baku");

            response.Data.Price = new RoutePriceDto
            {
                Original = (double)originalFare,
                Discount = 0,
                Final = (double)originalFare,
                Currency = "AZN"
            };

            if (request.UserId.HasValue && request.UserId.Value > 0)
            {
                var promo = await _promoCodeService.ApplyActivePromoAsync(
                    request.UserId.Value,
                    originalFare,
                    consumeUsage: false);

                if (promo.Applied)
                {
                    response.Data.Price = new RoutePriceDto
                    {
                        Original = (double)promo.OriginalFare,
                        Discount = (double)promo.DiscountAmount,
                        Final = (double)promo.FinalFare,
                        Currency = "AZN"
                    };
                }
            }

            _logger.LogInformation(
                "Route estimate price: {Fare:F2} AZN ({VehicleType}, {DistanceKm} km, {DurationMin} min)",
                originalFare,
                request.VehicleType,
                response.Data.DistanceKm,
                response.Data.DurationMinutes);
        }

        private static RouteEstimateResponse Fail(string message) =>
            new() { Success = false, Message = message };
    }
}
