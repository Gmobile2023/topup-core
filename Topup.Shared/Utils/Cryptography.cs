using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Topup.Shared.Utils;

public static partial class Cryptography
{
    //public static readonly string Key = ConfigurationManager.AppSettings["Encryption_Key"];
    public static readonly Encoding Encoder = Encoding.UTF8;

    public static string EncryptMd5(this string clearText, bool toUpper = false)
    {
        var md5Crys = MD5.Create();
        var plainByte = Encoding.UTF8.GetBytes(clearText);
        var signateByte = md5Crys.ComputeHash(plainByte);

        var s = new StringBuilder();
        foreach (var b in signateByte) s.Append(toUpper ? b.ToString("x2").ToUpper() : b.ToString("x2"));

        return s.ToString();
    }

    public static string TripleDesDecrypt(string cypherText, string key)
    {
        var des = CreateDes(key);
        var ct = des.CreateDecryptor();
        var input = Convert.FromBase64String(cypherText);
        var output = ct.TransformFinalBlock(input, 0, input.Length);
        return Encoding.UTF8.GetString(output);
    }

    public static TripleDES CreateDes(string key)
    {
        // MD5 md5 = new MD5CryptoServiceProvider();
        //
        key = EncryptMd5(key);
        key = key.Substring(0, 24);
        var des = TripleDES.Create();
        // var desKey= md5.ComputeHash(Encoding.UTF8.GetBytes(key));
        des.Key = Encoding.UTF8.GetBytes(key);
        des.IV = new byte[des.BlockSize / 8];
        des.Padding = PaddingMode.PKCS7;
        des.Mode = CipherMode.ECB;
        return des;
    }


    public static string DecryptCodeImedia(string encryptedText, string key)
    {
        var input = Convert.FromBase64String(encryptedText);
        var des = CreateDES(key);
        var ct = des.CreateDecryptor();
        try
        {
            var output = ct.TransformFinalBlock(input, 0, input.Length);
            var r = Encoding.ASCII.GetString(output);
            return r;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static TripleDES CreateDES(string key)
    {
        var des = TripleDES.Create();
        des.KeySize = 192;
        des.Mode = CipherMode.CBC;
        des.Padding = PaddingMode.PKCS7;
        des.Key = Encoding.ASCII.GetBytes(key);
        des.IV = GetValidIv(key, 8);
        return des;
    }
 

    private static byte[] GetValidIv(string initVector, int validLength)
    {
        string sTemp;

        if (initVector.Length > validLength)
        {
            sTemp = initVector.Substring(0, validLength);
        }
        else
        {
            sTemp = initVector;
            while (sTemp.Length != validLength) sTemp = sTemp + ' ';
        }

        return Encoding.ASCII.GetBytes(sTemp);
    }

    public static string Sign(string dataToSign, string privateFile)
    {
        try
        {
            
            var privateKeyText = File.ReadAllText("files/" + privateFile);
            var privateKeyBlocks = privateKeyText.Split("-", StringSplitOptions.RemoveEmptyEntries);
            var privateKeyBytes = Convert.FromBase64String(privateKeyBlocks[1]);
            using var rsa = RSA.Create();

            if (privateKeyBlocks[0] == "BEGIN PRIVATE KEY")
                rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
            else if (privateKeyBlocks[0] == "BEGIN RSA PRIVATE KEY")
                rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
            var sig = rsa.SignData(
                Encoding.UTF8.GetBytes(dataToSign),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            var signature = Convert.ToBase64String(sig);
            return signature;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public static bool Verify(string data, string signature, string publicFile)
    {
        try
        {
            // byte[] publicPemBytes = File.ReadAllBytes(publicFile);
            // using var publicX509 = new X509Certificate2(publicPemBytes);
            // var rsa = publicX509.GetRSAPublicKey();
            Console.WriteLine($"Verify sig:{publicFile}");
            var publicFileText = File.ReadAllText("files/" + publicFile);
            var publicKeyBlocks = publicFileText.Split("-", StringSplitOptions.RemoveEmptyEntries);
            var publicKeyBytes = Convert.FromBase64String(publicKeyBlocks[1]);
            using var rsa = RSA.Create();

            if (publicKeyBlocks[0] == "BEGIN PUBLIC KEY")
                rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
            else if (publicKeyBlocks[0] == "BEGIN CERTIFICATE") rsa.ImportRSAPublicKey(publicKeyBytes, out _);

            var dataByteArray = Encoding.UTF8.GetBytes(data);
            var signatureByteArray = Convert.FromBase64String(signature);
            return rsa.VerifyData(
                dataByteArray,
                signatureByteArray,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Verify sig error:{e.Message}");
            return false;
        }
    }

    #region tripdes, Mode: CBC, Padding: None

    private static readonly byte[] Hex2Byte =
    {
        0x00, 0x01, 0x02,
        0x03,
        0x04, 0x05, 0x06,
        0x07,
        0x08, 0x09, 0x0A,
        0x0B,
        0x0C, 0x0D, 0x0E,
        0x0F
    };

    private static byte[] FromHex(string s)
    {
        var ba = new byte[s.Length / 2];
        for (var i = 0; i < ba.Length; i++)
            ba[i] = byte.Parse(s.Substring(2 * i, 2),
                NumberStyles.HexNumber);

        return ba;
    }

    private static string ToHex(byte[] ba)
    {
        var sb = new StringBuilder(2 * ba.Length);
        for (var i = 0; i < ba.Length; i++) sb.Append(ba[i].ToString("X2"));

        return sb.ToString().ToUpper();
    }

    private static byte[] FillBlock(byte[] dataTemp, int blockLength)
    {
        var i = dataTemp.Length % blockLength;
        var length = i == 0 ? dataTemp.Length : dataTemp.Length - i + blockLength;
        var data = new byte[length];
        if (i != 0)
            for (var j = 0; j < length; j++)
                if (j < dataTemp.Length)
                    data[j] = dataTemp[j];
                else
                    data[j] = 0xFF;
        else
            data = dataTemp;

        return data;
    }

    public static string HashSHA256(string value)
    {
        var Sb = new StringBuilder();

        using var hash = SHA256.Create();
        var enc = Encoding.UTF8;
        var result = hash.ComputeHash(enc.GetBytes(value));
        foreach (var b in result)
            Sb.Append(b.ToString("x2"));
        return Sb.ToString();
    }

    public static string HashSHA1(string text)
    {
        using var sh = SHA1.Create();
        var hash = new StringBuilder();
        var bytes = Encoding.UTF8.GetBytes(text);
        var b = sh.ComputeHash(bytes);
        foreach (var a in b)
        {
            var h = a.ToString("x2");
            hash.Append(h);
        }

        return hash.ToString();
    }

    public static string RamdonStringForSalt(int max_length = 32)
    {
        var random = RandomNumberGenerator.Create();
        var salt = new byte[max_length];
        random.GetNonZeroBytes(salt);
        return Convert.ToBase64String(salt);
    }

    public static string HMASHA1(string input, byte[] key)
    {
        var myhmacsha1 = new HMACSHA1(key);
        var byteArray = Encoding.ASCII.GetBytes(input);
        var stream = new MemoryStream(byteArray);
        return myhmacsha1.ComputeHash(stream).Aggregate("", (s, e) => s + $"{e:x2}", s => s);
    }

    public static string HMASHA1Base64(string input, byte[] key)
    {
        var myhmacsha1 = new HMACSHA1(key);
        var byteArray = Encoding.ASCII.GetBytes(input);
        var stream = new MemoryStream(byteArray);
        return Convert.ToBase64String(myhmacsha1.ComputeHash(stream));
    }


    public static string EncryptTripDes(this string plainText, string privateKey)
    {
        var key = privateKey;
        var keyArray = FromHex(key);
        var tdes = TripleDES.Create();
        tdes.Mode = CipherMode.CBC;
        tdes.Padding = PaddingMode.None;

        byte[] myIv = {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
        var crypt = tdes.CreateEncryptor(keyArray, myIv);

        var plain = Encoding.UTF8.GetBytes(plainText);
        plain = FillBlock(plain, 8);

        var cipher = crypt.TransformFinalBlock(plain, 0, plain.Length);
        return ToHex(cipher);
    }

    #region 3DES

    public static string DefaultTripleEncrypt(string source, string key)
    {
        var desCryptoProvider = TripleDES.Create();
        var hashMd5Provider = MD5.Create();

        var byteHash = hashMd5Provider.ComputeHash(Encoding.UTF8.GetBytes(key));
        desCryptoProvider.Key = byteHash;
        desCryptoProvider.Mode = CipherMode.ECB;
        var byteBuff = Encoding.UTF8.GetBytes(source);

        var encoded =
            Convert.ToBase64String(desCryptoProvider.CreateEncryptor()
                .TransformFinalBlock(byteBuff, 0, byteBuff.Length));
        return encoded;
    }

    public static string DefaultTripleDecrypt(string encodedText, string key)
    {
        var desCryptoProvider = TripleDES.Create();
        var hashMd5Provider = MD5.Create();

        var byteHash = hashMd5Provider.ComputeHash(Encoding.UTF8.GetBytes(key));
        desCryptoProvider.Key = byteHash;
        desCryptoProvider.Mode = CipherMode.ECB;
        var byteBuff = Convert.FromBase64String(encodedText);

        var plaintext = Encoding.UTF8.GetString(desCryptoProvider.CreateDecryptor()
            .TransformFinalBlock(byteBuff, 0, byteBuff.Length));
        return plaintext;
    }

    #endregion

    //decryption

    private static byte[] ConvertHexToByte(string strHex)
    {
        var strTemp = "";
        strTemp = strHex.ToUpper();
        if (strTemp.Length % 2 != 0) throw new Exception();

        var length = strTemp.Length / 2;
        var keySec = new byte[length];
        var Hex = "0123456789ABCDEF";
        for (var i = 0; i < length; i++)
        {
            var ch0 = strTemp[2 * i];
            var ch1 = strTemp[2 * i + 1];
            var loByte = Hex.IndexOf(ch0);
            var hiByte = Hex.IndexOf(ch1);
            var lo = Hex2Byte[loByte];
            var hi = Hex2Byte[hiByte];

            int inlo = lo;
            int inhi = hi;
            var value = inlo * 16 + inhi;
            keySec[i] = (byte) value;
        }

        return keySec;
    }

    public static string DecryptTripleDes(this string cipherString, string privateKey)
    {
        var key = privateKey;
        var keyArray = FromHex(key);
        byte[] myIv = {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
        var plain = ConvertHexToByte(cipherString);
        var tdes = TripleDES.Create();

        tdes.Mode = CipherMode.CBC;
        tdes.Padding = PaddingMode.None;

        var cTransform = tdes.CreateDecryptor(keyArray, myIv);
        var resultArray = cTransform.TransformFinalBlock(plain, 0, plain.Length);
        var plaintText = Encoding.UTF8.GetString(resultArray);
        var t = plaintText.Contains("?");
        // Console.Write(call);
        return Regex.Replace(plaintText, "[^A-Za-z0-9]", "");
    }

    #endregion
}