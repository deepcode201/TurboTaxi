using System;
using System.Collections.Generic;
using System.Linq;

namespace TurboTaxi.Infrastructure.Helpers
{
    /// <summary>
    /// Helper for calculating distance between a point and a polyline.
    /// Used for off-route detection.
    /// </summary>
    public static class PolylineDistanceHelper
    {
        /// <summary>
        /// Decodes a Google encoded polyline string into lat/lng coordinates.
        /// </summary>
        public static List<(double Lat, double Lng)> DecodePolyline(string encodedPolyline)
        {
            if (string.IsNullOrWhiteSpace(encodedPolyline))
                return new List<(double, double)>();

            var coordinates = new List<(double, double)>();
            int index = 0;
            int lat = 0, lng = 0;

            while (index < encodedPolyline.Length)
            {
                int shift = 0, result = 0;
                int b;
                do
                {
                    b = encodedPolyline[index++] - 63;
                    result |= (b & 0x1f) << shift;
                    shift += 5;
                } while (b >= 0x20);

                int dlat = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
                lat += dlat;

                shift = 0;
                result = 0;
                do
                {
                    b = encodedPolyline[index++] - 63;
                    result |= (b & 0x1f) << shift;
                    shift += 5;
                } while (b >= 0x20);

                int dlng = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
                lng += dlng;

                coordinates.Add((lat / 1E5, lng / 1E5));
            }

            return coordinates;
        }

        /// <summary>
        /// Calculate minimum distance (in meters) from a point to a polyline.
        /// </summary>
        public static double MinDistanceToPolyline(double pointLat, double pointLng, List<(double Lat, double Lng)> polyline)
        {
            if (polyline == null || polyline.Count < 2)
                return double.MaxValue;

            double minDistance = double.MaxValue;

            for (int i = 0; i < polyline.Count - 1; i++)
            {
                var segmentStart = polyline[i];
                var segmentEnd = polyline[i + 1];

                double distance = DistanceToSegment(pointLat, pointLng, segmentStart, segmentEnd);
                if (distance < minDistance)
                    minDistance = distance;
            }

            return minDistance;
        }

        /// <summary>
        /// Calculate distance from a point to a line segment (in meters).
        /// </summary>
        private static double DistanceToSegment(double pLat, double pLng, (double Lat, double Lng) a, (double Lat, double Lng) b)
        {
            // Convert to radians
            var pLatRad = DegreesToRadians(pLat);
            var pLngRad = DegreesToRadians(pLng);
            var aLatRad = DegreesToRadians(a.Lat);
            var aLngRad = DegreesToRadians(a.Lng);
            var bLatRad = DegreesToRadians(b.Lat);
            var bLngRad = DegreesToRadians(b.Lng);

            // Distance from point to segment endpoints
            var distToA = HaversineDistance(pLat, pLng, a.Lat, a.Lng);
            var distToB = HaversineDistance(pLat, pLng, b.Lat, b.Lng);

            // If segment is a point
            if (Math.Abs(a.Lat - b.Lat) < 1e-9 && Math.Abs(a.Lng - b.Lng) < 1e-9)
                return distToA;

            // Project point onto line segment
            var segmentLength = HaversineDistance(a.Lat, a.Lng, b.Lat, b.Lng);
            if (segmentLength < 1) // Very short segment
                return Math.Min(distToA, distToB);

            // Parametric projection
            var t = ((pLat - a.Lat) * (b.Lat - a.Lat) + (pLng - a.Lng) * (b.Lng - a.Lng)) /
                    ((b.Lat - a.Lat) * (b.Lat - a.Lat) + (b.Lng - a.Lng) * (b.Lng - a.Lng));

            t = Math.Max(0, Math.Min(1, t)); // Clamp to segment

            var projLat = a.Lat + t * (b.Lat - a.Lat);
            var projLng = a.Lng + t * (b.Lng - a.Lng);

            return HaversineDistance(pLat, pLng, projLat, projLng);
        }

        /// <summary>
        /// Haversine distance between two points (in meters).
        /// </summary>
        public static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; // Earth radius in meters
            var dLat = DegreesToRadians(lat2 - lat1);
            var dLon = DegreesToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c; // meters
        }

        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
    }
}
