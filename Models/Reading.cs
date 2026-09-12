using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MAUI_IOT.Models;

public sealed record Reading(
    int Id,
    int SensorId,
    string Value,
    DateTime Timestamp,
    double? Latitude,
    double? Longitude)
{
    public bool HasLocation => Latitude is not null && Longitude is not null;

    public string LocationText => HasLocation
        ? string.Format(CultureInfo.InvariantCulture, "{0:F5}, {1:F5}", Latitude, Longitude)
        : string.Empty;
}
