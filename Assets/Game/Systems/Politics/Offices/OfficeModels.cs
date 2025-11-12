using System;
using System.Collections.Generic;

namespace Game.Systems.Politics.Offices
{
    [Serializable]
    public class OfficeSeat
    {
        public int SeatIndex;
        public int? HolderId;
        public int StartYear;
        public int EndYear;
        public int? PendingHolderId;
        public int PendingStartYear;
    }

    [Serializable]
    public class ActiveOfficeRecord
    {
        public string OfficeId;
        public int SeatIndex;
        public int EndYear;
    }

    [Serializable]
    public class PendingOfficeRecord
    {
        public string OfficeId;
        public int SeatIndex;
        public int StartYear;
    }

    [Serializable]
    public class OfficeCareerRecord
    {
        public string OfficeId;
        public int SeatIndex;
        public int HolderId;
        public int StartYear;
        public int EndYear;
    }

    [Serializable]
    public class OfficeSeatDescriptor
    {
        public string OfficeId;
        public int SeatIndex;
        public int? HolderId;
        public int StartYear;
        public int EndYear;
        public int? PendingHolderId;
        public int PendingStartYear;
    }

    [Serializable]
    public class OfficeElectionInfo
    {
        public OfficeDefinition Definition;
        public int SeatsAvailable;
        public List<OfficeSeatDescriptor> Seats = new();

        public override string ToString()
        {
            return $"{Definition?.Name} x{SeatsAvailable}";
        }
    }
}
