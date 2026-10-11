namespace TradingSystem.Core.Enums;

/// <summary>
/// Loại tài sản giao dịch trên thị trường
/// </summary>
public static class AssetType
{
    public const string Stock = "S";              // Cổ phiếu cơ sở
    public const string CoveredWarrant = "W";     // Chứng quyền có bảo đảm
    public const string Derivative = "D";         // Hợp đồng tương lai phái sinh
    public const string Index = "I";              // Chỉ số thị trường (VNINDEX, VN30)
}

/// <summary>
/// Sàn giao dịch niêm yết
/// </summary>
public static class Exchange
{
    public const string Hose = "HOSE";
    public const string Hnx = "HNX";
    public const string Upcom = "UPCOM";
}

/// <summary>
/// Chiều vị thế giao dịch 2 chiều
/// </summary>
public static class PositionSide
{
    public const string Long = "LONG";
    public const string Short = "SHORT";
}

/// <summary>
/// Chiều lệnh khớp chủ động
/// </summary>
public static class OrderSide
{
    public const string Buy = "B";   // Mua chủ động (khớp tại giá Ask)
    public const string Sell = "S";  // Bán chủ động (khớp tại giá Bid)
    public const string Cross = "";  // Khớp định kỳ ATO / ATC
}

/// <summary>
/// Hành động phát tín hiệu thuật toán
/// </summary>
public static class SignalActionType
{
    public const string OpenLong = "OPEN_LONG";
    public const string CloseLong = "CLOSE_LONG";
    public const string OpenShort = "OPEN_SHORT";
    public const string CloseShort = "CLOSE_SHORT";
    public const string RiskOn = "RISK_ON";
    public const string RiskOff = "RISK_OFF";
    public const string Overweight = "OVERWEIGHT";
    public const string Underweight = "UNDERWEIGHT";
}
