using System.ComponentModel.DataAnnotations;

namespace Entities.DTOs;

public sealed class MessageResponse
{
    public string Message { get; set; } = "";
}

public sealed class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
    [MaxLength(254, ErrorMessage = "Email quá dài.")]
    public string Email { get; set; } = "";
}

public sealed class ResetPasswordRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
    [MaxLength(254)] public string Email { get; set; } = "";
    [RegularExpression("^[0-9]{6}$", ErrorMessage = "Mã xác thực gồm 6 chữ số.")]
    [Required(ErrorMessage = "Vui lòng nhập mã xác thực.")]
    public string Token { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [MinLength(6, ErrorMessage = "Mật khẩu cần ít nhất 6 ký tự.")]
    [MaxLength(128, ErrorMessage = "Mật khẩu quá dài.")]
    public string Password { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu.")]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận chưa khớp.")]
    public string ConfirmPassword { get; set; } = "";
}
