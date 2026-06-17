using System;
using System.Collections.Generic;
using System.Text;

namespace MAUI_IOT.Models
{
    public sealed record class DeviceSummary(
        int Id,
        string Name,
        string Type,
        bool IsOnline,
        string LatestReading);
    
}
