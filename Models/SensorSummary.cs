using System;
using System.Collections.Generic;
using System.Text;

namespace MAUI_IOT.Models;

public sealed record SensorSummary(
    int Id,
    string SourceDeviceId,
    string Name,
    string Type,
    bool IsOnline,
    string LatestReading);
