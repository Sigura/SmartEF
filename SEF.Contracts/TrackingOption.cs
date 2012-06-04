using System;

namespace SEF.Contracts
{
    [Flags]
    public enum TrackingOption
    {
        RefreshAfterSave = 1,
        WithoutRefresh = 2,
        NoTracking = 4
    }
}
