namespace Fr.Wireplumber.Model.Props;

/// <summary>
/// Channel names
/// </summary>
public enum ChannelNameEnum : uint
{
    /// <summary>
    /// Unknown
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Not available
    /// </summary>
    NotAvailable = 1,

    /// <summary>
    /// Mono channel
    /// </summary>
    Mono = 2,

    /// <summary>
    /// Front left
    /// </summary>
    FrontLeft = 3,

    /// <summary>
    /// Front right
    /// </summary>
    FrontRight = 4,

    /// <summary>
    /// Front center
    /// </summary>
    FrontCenter = 5,

    /// <summary>
    /// Low Frequency Effect Channel (LFE)
    /// </summary>
    LowFrequencyEffect = 6,

    /// <summary>
    /// Left side
    /// </summary>
    SideLeft = 7,

    /// <summary>
    /// Right side
    /// </summary>
    SideRight = 8,

    /// <summary>
    /// Front left center
    /// </summary>
    FrontLeftCenter = 9,

    /// <summary>
    /// Front right center
    /// </summary>
    FrontRightCenter = 10,

    /// <summary>
    /// Rear center
    /// </summary>
    RearCenter = 11,

    /// <summary>
    /// Rear left
    /// </summary>
    RearLeft = 12,

    /// <summary>
    /// Rear right
    /// </summary>
    RearRight = 13,

    /// <summary>
    /// Top center
    /// </summary>
    TopCenter = 14,

    /// <summary>
    /// Top front left
    /// </summary>
    TopFrontLeft = 15,

    /// <summary>
    /// Top front center
    /// </summary>
    TopFrontCenter = 16,

    /// <summary>
    /// Top front right
    /// </summary>
    TopFrontRight = 17,

    /// <summary>
    /// Top rear left
    /// </summary>
    TopRearLeft = 18,

    /// <summary>
    /// Top rear center
    /// </summary>
    TopRearCenter = 19,

    /// <summary>
    /// Top rear right
    /// </summary>
    TopRearRight = 20,

    /// <summary>
    /// Rear left center
    /// </summary>
    RearLeftCenter = 21,

    /// <summary>
    /// Rear right center
    /// </summary>
    RearRightCenter = 22,

    /// <summary>
    /// Front left wide
    /// </summary>
    FrontLeftWide = 23,

    /// <summary>
    /// Front right wide
    /// </summary>
    FrontRightWide = 24,

    /// <summary>
    /// Low Frequency Effect Channel 2 (LFE)
    /// </summary>
    LowFrequencyEffect2 = 25,

    /// <summary>
    /// Front left high
    /// </summary>
    FrontLeftHigh = 26,

    /// <summary>
    /// Front center high
    /// </summary>
    FrontCenterHigh = 27,

    /// <summary>
    /// Front right high
    /// </summary>
    FrontRightHigh = 28,

    /// <summary>
    /// Front left center
    /// </summary>
    TopFrontLeftCenter = 29,

    /// <summary>
    /// Front right center
    /// </summary>
    TopFrontRightCenter = 30,

    /// <summary>
    /// Top side left
    /// </summary>
    TopSideLeft = 31,

    /// <summary>
    /// Top side right
    /// </summary>
    TopSideRight = 32,

    /// <summary>
    /// Left Low Frequency Effect Channel (LFE)
    /// </summary>
    LeftLowFrequencyEffect = 33,

    /// <summary>
    /// Right Low Frequency Effect Channel (LFE)
    /// </summary>
    RightLowFrequencyEffect = 34,

    /// <summary>
    /// Bottom Center
    /// </summary>
    BottomCenter = 35,

    /// <summary>
    /// BottomLeftCenter
    /// </summary>
    BottomLeftCenter = 36,

    /// <summary>
    /// BottomRightCenter
    /// </summary>
    BottomRightCenter = 37,

    /// <summary>
    /// Pro-audio channels
    /// </summary>
    AuxChannel0 = 0x1000,

    /// <summary>
    /// Pro-audio channel 0
    /// </summary>
    AuxStart = AuxChannel0,

    /// <summary>
    /// Pro-audio channel 1
    /// </summary>
    AuxChannel1 = 0x1001,

    /// <summary>
    /// Pro-audio channel 2
    /// </summary>
    AuxChannel2 = 0x1002,

    /// <summary>
    /// Pro-audio channel 3
    /// </summary>
    AuxChannel3 = 0x1003,

    /// <summary>
    /// Pro-audio channel 4
    /// </summary>
    AuxChannel4 = 0x1004,

    /// <summary>
    /// Pro-audio channel 5
    /// </summary>
    AuxChannel5 = 0x1005,

    /// <summary>
    /// Pro-audio channel 6
    /// </summary>
    AuxChannel6 = 0x1006,

    /// <summary>
    /// Pro-audio channel 7
    /// </summary>
    AuxChannel7 = 0x1007,

    /// <summary>
    /// Pro-audio channel 8
    /// </summary>
    AuxChannel8 = 0x1008,

    /// <summary>
    /// Pro-audio channel 9
    /// </summary>
    AuxChannel9 = 0x1009,

    /// <summary>
    /// Pro-audio channel 10
    /// </summary>
    AuxChannel10 = 0x100A,

    /// <summary>
    /// Pro-audio channel 11
    /// </summary>
    AuxChannel11 = 0x100B,

    /// <summary>
    /// Pro-audio channel 12
    /// </summary>
    AuxChannel12 = 0x100C,

    /// <summary>
    /// Pro-audio channel 13
    /// </summary>
    AuxChannel13 = 0x100D,

    /// <summary>
    /// Pro-audio channel 14
    /// </summary>
    AuxChannel14 = 0x100E,

    /// <summary>
    /// Pro-audio channel 15
    /// </summary>
    AuxChannel15 = 0x100F,

    /// <summary>
    /// Pro-audio channel 16
    /// </summary>
    AuxChannel16 = 0x1010,

    /// <summary>
    /// Pro-audio channel 17
    /// </summary>
    AuxChannel17 = 0x1011,

    /// <summary>
    /// Pro-audio channel 18
    /// </summary>
    AuxChannel18 = 0x1012,

    /// <summary>
    /// Pro-audio channel 19
    /// </summary>
    AuxChannel19 = 0x1013,

    /// <summary>
    /// Pro-audio channel 20
    /// </summary>
    AuxChannel20 = 0x1014,

    /// <summary>
    /// Pro-audio channel 21
    /// </summary>
    AuxChannel21 = 0x1015,

    /// <summary>
    /// Pro-audio channel 22
    /// </summary>
    AuxChannel22 = 0x1016,

    /// <summary>
    /// Pro-audio channel 23
    /// </summary>
    AuxChannel23 = 0x1017,

    /// <summary>
    /// Pro-audio channel 24
    /// </summary>
    AuxChannel24 = 0x1018,

    /// <summary>
    /// Pro-audio channel 25
    /// </summary>
    AuxChannel25 = 0x1019,

    /// <summary>
    /// Pro-audio channel 26
    /// </summary>
    AuxChannel26 = 0x101A,

    /// <summary>
    /// Pro-audio channel 27
    /// </summary>
    AuxChannel27 = 0x101B,

    /// <summary>
    /// Pro-audio channel 28
    /// </summary>
    AuxChannel28 = 0x101C,

    /// <summary>
    /// Pro-audio channel 29
    /// </summary>
    AuxChannel29 = 0x101D,

    /// <summary>
    /// Pro-audio channel 30
    /// </summary>
    AuxChannel30 = 0x101E,

    /// <summary>
    /// Pro-audio channel 31
    /// </summary>
    AuxChannel31 = 0x101F,

    /// <summary>
    /// Pro-audio channel 32
    /// </summary>
    AuxChannel32 = 0x1020,

    /// <summary>
    /// Pro-audio channel 33
    /// </summary>
    AuxChannel33 = 0x1021,

    /// <summary>
    /// Pro-audio channel 34
    /// </summary>
    AuxChannel34 = 0x1022,

    /// <summary>
    /// Pro-audio channel 35
    /// </summary>
    AuxChannel35 = 0x1023,

    /// <summary>
    /// Pro-audio channel 36
    /// </summary>
    AuxChannel36 = 0x1024,

    /// <summary>
    /// Pro-audio channel 37
    /// </summary>
    AuxChannel37 = 0x1025,

    /// <summary>
    /// Pro-audio channel 38
    /// </summary>
    AuxChannel38 = 0x1026,

    /// <summary>
    /// Pro-audio channel 39
    /// </summary>
    AuxChannel39 = 0x1027,

    /// <summary>
    /// Pro-audio channel 40
    /// </summary>
    AuxChannel40 = 0x1028,

    /// <summary>
    /// Pro-audio channel 41
    /// </summary>
    AuxChannel41 = 0x1029,

    /// <summary>
    /// Pro-audio channel 42
    /// </summary>
    AuxChannel42 = 0x102A,

    /// <summary>
    /// Pro-audio channel 43
    /// </summary>
    AuxChannel43 = 0x102B,

    /// <summary>
    /// Pro-audio channel 44
    /// </summary>
    AuxChannel44 = 0x102C,

    /// <summary>
    /// Pro-audio channel 45
    /// </summary>
    AuxChannel45 = 0x102D,

    /// <summary>
    /// Pro-audio channel 46
    /// </summary>
    AuxChannel46 = 0x102E,

    /// <summary>
    /// Pro-audio channel 47
    /// </summary>
    AuxChannel47 = 0x102F,

    /// <summary>
    /// Pro-audio channel 48
    /// </summary>
    AuxChannel48 = 0x1030,

    /// <summary>
    /// Pro-audio channel 49
    /// </summary>
    AuxChannel49 = 0x1031,

    /// <summary>
    /// Pro-audio channel 50
    /// </summary>
    AuxChannel50 = 0x1032,

    /// <summary>
    /// Pro-audio channel 51
    /// </summary>
    AuxChannel51 = 0x1033,

    /// <summary>
    /// Pro-audio channel 52
    /// </summary>
    AuxChannel52 = 0x1034,

    /// <summary>
    /// Pro-audio channel 53
    /// </summary>
    AuxChannel53 = 0x1035,

    /// <summary>
    /// Pro-audio channel 54
    /// </summary>
    AuxChannel54 = 0x1036,

    /// <summary>
    /// Pro-audio channel 55
    /// </summary>
    AuxChannel55 = 0x1037,

    /// <summary>
    /// Pro-audio channel 56
    /// </summary>
    AuxChannel56 = 0x1038,

    /// <summary>
    /// Pro-audio channel 57
    /// </summary>
    AuxChannel57 = 0x1039,

    /// <summary>
    /// Pro-audio channel 58
    /// </summary>
    AuxChannel58 = 0x103A,

    /// <summary>
    /// Pro-audio channel 59
    /// </summary>
    AuxChannel59 = 0x103B,

    /// <summary>
    /// Pro-audio channel 60
    /// </summary>
    AuxChannel60 = 0x103C,

    /// <summary>
    /// Pro-audio channel 61
    /// </summary>
    AuxChannel61 = 0x103D,

    /// <summary>
    /// Pro-audio channel 62
    /// </summary>
    AuxChannel62 = 0x103E,

    /// <summary>
    /// Pro-audio channel 63
    /// </summary>
    AuxChannel63 = 0x103F,

    /// <summary>
    /// Last Aux Channel
    /// </summary>
    AuxLast = 0x1fff,

    /// <summary>
    /// Start of custom channel numbers
    /// </summary>
    CustomStart = 0x10000
}