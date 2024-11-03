using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using ServiceStack;

namespace HLS.Paygate.Shared;

public static class ServiceStackHelper
{
    public static void SetLicense()
    {
        var licenseUtils = typeof(LicenseUtils);
        var members =
            licenseUtils.FindMembers(MemberTypes.All, BindingFlags.NonPublic | BindingFlags.Static, null, null);
        Type activatedLicenseType = null;
        foreach (var memberInfo in members)
            if (memberInfo.Name.Equals("__activatedLicense", StringComparison.OrdinalIgnoreCase) &&
                memberInfo is FieldInfo fieldInfo)
                activatedLicenseType = fieldInfo.FieldType;

        if (activatedLicenseType != null)
        {
            var licenseKey = new LicenseKey
            {
                Expiry = DateTime.Now.AddYears(100),
                Ref = "ServiceStack",
                Name = "Enterprise",
                Type = LicenseType.Enterprise
            };
            var constructor = activatedLicenseType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(LicenseKey) }, null);
            if (constructor != null)
            {
                var activatedLicense = constructor.Invoke(new object[] { licenseKey });
                var activatedLicenseField =
                    licenseUtils.GetField("__activatedLicense", BindingFlags.NonPublic | BindingFlags.Static);
                if (activatedLicenseField != null)
                    activatedLicenseField.SetValue(null, activatedLicense);
            }
        }
    }
}

public static class ServiceStackHelper1
{
    public static void Activate()
    {
        var license = new LicenseKey
        {
            Expiry = DateTime.MaxValue,
            Hash = string.Empty,
            Name = "activated.com",
            Ref = "1",
            Type = LicenseType.Enterprise
        };

        var type = typeof(LicenseUtils).GetNestedType("__ActivatedLicense",
            BindingFlags.Static | BindingFlags.NonPublic);
        var activate_license = Activator.CreateInstance(type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance, null,
            new object[1] { license }, CultureInfo.InvariantCulture);
        var setter =
            typeof(LicenseUtils).GetMethod("__setActivatedLicense", BindingFlags.Static | BindingFlags.NonPublic);
        setter.Invoke(null, new[] { activate_license });

        var features = LicenseUtils.ActivatedLicenseFeatures();
        Debug.Assert(features == LicenseFeature.All);
    }
}