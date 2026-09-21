using UnityEngine;

public static class PickleballCourtSpec
{
    public const float CourtLength      = 13.4112f;  // 44 ft 
    public const float CourtWidth       = 6.096f;    // 20 ft 
    public const float KitchenDepth     = 2.1336f;   // 7 ft 
    public const float ServiceAreaDepth = 4.572f;    // 15 ft
    public const float ServiceAreaWidth = 3.048f;    // 10 ft 
    public const float LineWidth        = 0.0508f;   // 2 inch
    public const float NetHeightSide    = 0.9144f;   // 36 inch
    public const float NetHeightCenter  = 0.8636f;   // 34 inch
    public const float HalfCourtWidth   = CourtWidth / 2f;  // 3.048
    public const float HalfCourtLength  = CourtLength / 2f; // 6.7056

    public const float TotalWidthTournament  = 10.3632f; // 34 ft
    public const float TotalLengthTournament = 19.5072f; // 64 ft
}
