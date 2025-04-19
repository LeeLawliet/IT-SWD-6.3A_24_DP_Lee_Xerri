namespace LocationService.DTO
{
    public class WeatherForecastDTO
    {
        public LocationDto Location { get; set; }
        public CurrentDto Current { get; set; }
    }

    public class LocationDto { public string Name { get; set; } public string Region { get; set; } }
    public class CurrentDto { public double TempC { get; set; } public string Condition { get; set; } }
}
