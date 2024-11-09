using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using ServiceStack;

namespace GMB.Topup.Shared;

public static class ServiceStackHelper
{
    public static void SetLicense()
    {
        var licenseUtils = typeof(LicenseUtils);
        
        // Sử dụng Reflection để tìm lớp nội bộ __ActivatedLicense
        var activatedLicenseType = licenseUtils.GetNestedType("__ActivatedLicense", BindingFlags.NonPublic);
        if (activatedLicenseType != null)
        {
            var licenseKey = new LicenseKey
            {
                Expiry = DateTime.Now.AddYears(100), // Thêm 100 năm cho license
                Ref = "ServiceStack",
                Name = "Enterprise",
                Type = LicenseType.Enterprise
            };

            // Lấy constructor nội bộ của __ActivatedLicense
            var constructor = activatedLicenseType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(LicenseKey) }, null);
            if (constructor != null)
            {
                var activatedLicense = constructor.Invoke(new object[] { licenseKey });

                // Tìm field __activatedLicense và gán giá trị
                var activatedLicenseField = activatedLicenseType.GetField("__activatedLicense", BindingFlags.NonPublic | BindingFlags.Static);
                if (activatedLicenseField != null)
                {
                    activatedLicenseField.SetValue(null, activatedLicense);
                    
                    // Xóa cảnh báo liên quan đến license
                    var warningMessageField = licenseUtils.GetField("LicenseWarningMessage", BindingFlags.NonPublic | BindingFlags.Static);
                    if (warningMessageField != null)
                    {
                        warningMessageField.SetValue(null, null);
                    }
                }
            }
        }
    }
    
    //  public static void ApplyPatch2()
    // {
    //     // Tạo một instance của Harmony với ID duy nhất
    //     var harmony = new Harmony("com.example.patch");
    //
    //     // Patch 1: Bỏ qua kiểm tra license trong RegisterLicense
    //     // var registerLicenseMethod = typeof(LicenseUtils).GetMethod("RegisterLicense", BindingFlags.Public | BindingFlags.Static);
    //     // if (registerLicenseMethod != null)
    //     // {
    //     //     var prefix = new HarmonyMethod(typeof(ServiceStackHelper).GetMethod(nameof(RegisterLicensePrefix)));
    //     //     harmony.Patch(registerLicenseMethod, prefix: prefix);
    //     // }
    //
    //     // Patch 2: Bỏ qua kiểm tra license trong VerifyLicenseKeyText
    //     var verifyLicenseMethod = typeof(LicenseUtils).GetMethod(
    //         "VerifyLicenseKeyText",
    //         BindingFlags.Public | BindingFlags.Static,
    //         null,
    //         new[] { typeof(string), typeof(LicenseKey).MakeByRefType() },  // MakeByRefType dùng cho out tham số
    //         null);
    //     if (verifyLicenseMethod != null)
    //     {
    //         var prefix = new HarmonyMethod(typeof(ServiceStackHelper).GetMethod(nameof(VerifyLicensePrefix)));
    //         harmony.Patch(verifyLicenseMethod, prefix: prefix);
    //     }
    // }
    //
    // // Prefix để bỏ qua kiểm tra trong RegisterLicense
    // public static bool RegisterLicensePrefix(ref string licenseKeyText)
    // {
    //     // Bỏ qua kiểm tra license key
    //     Console.WriteLine("Bypassing license check in RegisterLicense!");
    //
    //     // Trả về false để bỏ qua phương thức gốc
    //     return false;
    // }
    //
    // // Prefix để bỏ qua kiểm tra trong VerifyLicenseKeyText
    // public static bool VerifyLicensePrefix(string licenseKeyText, ref LicenseKey key, ref bool __result)
    // {
    //     // Giả mạo kết quả trả về là true (license hợp lệ)
    //     __result = true;
    //
    //     // Tạo và gán giá trị cho tham số out LicenseKey
    //     key = new LicenseKey
    //     {
    //         Name = "Fake License",
    //         Ref = "FakeRef",
    //         Type = LicenseType.Enterprise,
    //         Expiry = DateTime.UtcNow.AddYears(100), // Ngày hết hạn giả
    //     };
    //
    //     // Trả về false để bỏ qua phương thức gốc
    //     return false;
    // }
}