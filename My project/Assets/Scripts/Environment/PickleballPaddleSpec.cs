using UnityEngine;

public static class PickleballPaddleSpec
{
    // Pro Tour - UPA-A Specs
    public const float MaxCombinedLengthWidth = 0.6096f;  // 24 in
    public const float MaxLength               = 0.4318f;  // 17 in
    public const float MaxThicknessPro         = 0.024f;   // 0.945 in (24mm)
    public const float MaxWeightPro            = 0.2835f;  // 10.0 oz

    // Standard Dimensions
    public const float StandardLength      = 0.4064f;  // 16 in
    public const float StandardWidth       = 0.2032f;  // 8 in
    public const float FaceLength          = 0.2921f;  // ~11.5 in
    public const float HandleLength        = 0.1143f;  // ~4.5 in
    public const float HandleDiameter      = 0.0333f;  // ~1.3 in
    public const float StandardThicknessMid = 0.014f;  // 14mm

    // Typical Weight
    public const float MinWeightTypical = 0.198f;  // 7.0 oz
    public const float MaxWeightTypical = 0.241f;  // 8.5 oz

    // Performance Constants
    public const float MaxPEF   = 0.385f;
    public const float MaxPBCoR = 0.43f;
}
