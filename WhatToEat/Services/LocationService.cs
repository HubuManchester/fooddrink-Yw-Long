using System.Text.Json;

namespace WhatToEat.Services
{
    public class LocationService
    {
        private readonly HttpClient _httpClient = new();

        // Returns (countryCode, city) e.g. ("GB", "Manchester")
        public async Task<(string CountryCode, string City)> GetLocationAsync()
        {
            try
            {
                var location = await Geolocation.Default.GetLastKnownLocationAsync()
                    ?? await Geolocation.Default.GetLocationAsync(
                        new GeolocationRequest(GeolocationAccuracy.Low,
                            TimeSpan.FromSeconds(5)));

                if (location != null)
                {
                    var placemarks = await Geocoding.Default.GetPlacemarksAsync(
                        location.Latitude, location.Longitude);
                    var placemark = placemarks?.FirstOrDefault();
                    if (placemark != null)
                    {
                        var code = placemark.CountryCode ?? "US";
                        // Try locality first, then AdminArea
                        var city = !string.IsNullOrEmpty(placemark.Locality)
                            ? placemark.Locality
                            : !string.IsNullOrEmpty(placemark.AdminArea)
                                ? placemark.AdminArea
                                : "Unknown City";
                        return (code, city);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocationService] GPS error: {ex.Message}");
            }

            return await GetLocationFromIpAsync();
        }

        // Keep old method for backward compatibility
        public async Task<string> GetCountryCodeAsync()
        {
            var (code, _) = await GetLocationAsync();
            return code;
        }

        private async Task<(string, string)> GetLocationFromIpAsync()
        {
            try
            {
                var json = await _httpClient.GetStringAsync(
                    "http://ip-api.com/json/?fields=countryCode,city");
                using var doc = JsonDocument.Parse(json);
                var code = doc.RootElement.GetProperty("countryCode").GetString() ?? "US";
                var city = doc.RootElement.GetProperty("city").GetString() ?? "Unknown City";
                return (code, city);
            }
            catch
            {
                return ("US", "Unknown City");
            }
        }
    }
}