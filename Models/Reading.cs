using System;
using System.Collections.Generic;
using System.Text;

namespace MAUI_IOT.Models;

public sealed record Reading(
    int Id,
    int SensorId,
    string Value,
    DateTime Timestamp);
