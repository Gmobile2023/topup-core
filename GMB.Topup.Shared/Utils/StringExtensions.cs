using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace GMB.Topup.Shared.Utils;

public static class StringExtensions
{
    public static string ConvertToUnSign(this string s)
    {
        var regex = new Regex("\\p{IsCombiningDiacriticalMarks}+");
        var temp = s.Normalize(NormalizationForm.FormD);
        return regex.Replace(temp, string.Empty).Replace('\u0111', 'd').Replace('\u0110', 'D');
    }

    public static string EncryptTripDes(this string s,
        string privatekey = "93E3378F9B7ABA6285CD6837BC38BC4C025DF2C2AD2C8A80")
    {
        // return s;
        return Cryptography.EncryptTripDes(s, privatekey);
    }

    public static string DecryptTripleDes(this string s,
        string privatekey = "93E3378F9B7ABA6285CD6837BC38BC4C025DF2C2AD2C8A80")
    {
        //return s;
        return Cryptography.DecryptTripleDes(s, privatekey);
    }

    public static string Md5(this string s)
    {
        return s.EncryptMd5();
    }

    public static string GenerateCode_Backup(string prefix, string key)
    {
        try
        {
            var code = Guid.NewGuid().ToString().GetHashCode().ToString("x").ToUpper();
            var rand = new Random();
            var date = DateTime.Now.ToString("yy");
            return prefix + date + rand.Next(000000000, 999999999) + code;
            //var redisGenerator = new RedisGenerator();
            //return redisGenerator.GeneratorCode(key, prefix);
        }
        catch (Exception)
        {
            return DateTime.Now.ToString("ddmmyyyyhhmmss");
        }
    }

    public static string ProductCodeGen(this string categoryCode, decimal value)
    {
        return $"{categoryCode}_{Math.Floor(value > 1000 ? value / 1000 : value)}";
    }

    public static IEnumerable<string> SplitFixedLength(this string str, int n)
    {
        if (string.IsNullOrEmpty(str) || n < 1) throw new ArgumentException();
        return Enumerable.Range(0, str.Length / n + 1)
            .Select(i => str.Substring(i * n, Math.Min(n, str.Length - i * n)));
    }

    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source,
        CancellationToken cancellationToken = default)
    {
        var list = new List<T>();
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false)) list.Add(item);

        return list;
    }
    public static string Base64Encode(this string plainText)
    {
        var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(plainTextBytes);
    }
}