namespace Entities.Game;

/// <summary>
/// Các vợt đã có model trong game (Unity: "Assets/Resources/Shop/Shop Catalog.asset"). Trang quản trị chỉ đăng bán được những
/// vợt này: game bỏ qua mã không có model, còn bản game cũ hơn 25/11/2026 thì không tải được hồ sơ khi gặp mã lạ. Khi thêm
/// model mới, thêm một dòng ở đây (kiểm tra bằng test ServerKnowsEverySkinThatHasAModel của Unity).
/// </summary>
public static class PaddleSkins
{
    public sealed record Skin(string Code, string Title, string Description, string Collection, bool Premium);

    public const string Prefix = "paddle.", Default = "paddle.default";

    public static readonly IReadOnlyList<Skin> All =
    [
        new("paddle.gemstone", "GEMSTONE", "Viền đá quý, sắc vàng và mặt carbon. Ánh lấp lánh tinh tế khi xoay dưới ánh sáng.", "BỘ SƯU TẬP TINH ANH", true),
        new("paddle.thot", "THOT", "Hoa văn nổi bật cho phong cách thi đấu riêng. Chọn Tokenpad hoặc Elite để sở hữu.", "BỘ SƯU TẬP DẤU ẤN", false),
        new("paddle.dragonruby", "DRAGON RUBY", "Rồng phương Đông chạm nổi trên mặt sơn mài đen. Thân rồng hồng ngọc quấn quanh cán bọc da, đầu rồng sừng vàng ngự ở chuôi.", "BỘ SƯU TẬP TINH ANH", true),
    ];

    public static Skin? Find(string? code) => All.FirstOrDefault(s => s.Code == code);
}
