using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace GMB.Topup.Shared.Utils;

public static partial class Cryptography
{
    public static string RSASig(string dataToSign, string privateFile, bool dataToSignBase64 = false)
    {
        try
        {
            var privateKeyText = File.ReadAllText("files/" + privateFile);
            var privateKeyBlocks = privateKeyText.Split("-", StringSplitOptions.RemoveEmptyEntries);
            var privateKeyBytes = Convert.FromBase64String(privateKeyBlocks[1]);
            using var rsa = RSA.Create();

            switch (privateKeyBlocks[0])
            {
                case "BEGIN PRIVATE KEY":
                    rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
                    break;
                case "BEGIN RSA PRIVATE KEY":
                    rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
                    break;
            }

            var sig = rsa.SignData(
                Encoding.UTF8.GetBytes(dataToSignBase64 ? dataToSign.Base64Encode() : dataToSign),
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

    public static bool RSAVerify(string data, string signature, string publicFile)
    {
        try
        {
            var publicFileText = File.ReadAllText("files/" + publicFile);
            var publicKeyBlocks = publicFileText.Split("-", StringSplitOptions.RemoveEmptyEntries);
            var publicKeyBytes = Convert.FromBase64String(publicKeyBlocks[1]);
            using var rsa = RSA.Create();

            switch (publicKeyBlocks[0])
            {
                case "BEGIN PUBLIC KEY":
                    rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
                    break;
                case "BEGIN CERTIFICATE":
                    rsa.ImportRSAPublicKey(publicKeyBytes, out _);
                    break;
            }

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
}