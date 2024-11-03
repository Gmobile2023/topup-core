using System;
using System.IO;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HLS.Paygate.Report.Domain.Repositories;

public class FileUploadRepository : IFileUploadRepository
{
    private readonly ILogger<FileUploadRepository> _logger;

    public FileUploadRepository(IConfiguration configuration,
        ILogger<FileUploadRepository> log)
    {
        _configuration = configuration;
        _logger = log;
    }

    private IConfiguration _configuration { get; }

    public string UploadFileToServer(string fileName)
    {
        try
        {
            _logger.LogInformation($"UploadFileToServer request: {fileName}");
            //Check tao thư muc
            var folder = DateTime.Now.ToString("ddMMyyyy");
            var serverPath = $"/Uploads/ReportFiles/{folder}/";
            var createFolder = CreateFtpDirectory(_configuration["FtpServer:Url"] + serverPath);
            if (!createFolder) return null;
            var _nameFile = Guid.NewGuid() + ".csv";
            var fileUrl = serverPath + "/" + _nameFile;
            var pathServer = _configuration["FtpServer:Url"] + fileUrl;
            var request = (FtpWebRequest) WebRequest.Create(pathServer);
            request.Method = WebRequestMethods.Ftp.UploadFile;
            request.Credentials = new NetworkCredential(_configuration["FtpServer:Username"],
                _configuration["FtpServer:Password"]);
            request.UsePassive = true;
            var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            var br = new BinaryReader(fs);
            var numBytes = new FileInfo(fileName).Length;
            var fileBytes = br.ReadBytes((int) numBytes);
            request.ContentLength = fileBytes.Length;
            var requestStream = request.GetRequestStream();
            requestStream.Write(fileBytes, 0, fileBytes.Length);
            requestStream.Close();
            var response = (FtpWebResponse) request.GetResponse();
            _logger.LogInformation("Upload File To FTP Complete, status {0}", response.StatusDescription);
            Console.WriteLine("Upload File To FTP Complete, status {0}", response.StatusDescription);
            response.Close();
            return _configuration["FtpServer:UrlViewFile"] + fileUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError("Upload file to FTP servie error: " + ex);
            Console.WriteLine("Upload file to FTP servie error: {0}", ex);
            return null;
        }
    }

    public string UploadFileToDataServer(byte[] fileBytes, string nameFile)
    {
        try
        {
            //Check tao thư muc
            var folder = DateTime.Now.ToString("yyyyMMdd");
            var serverPath = $"/Uploads/ReportFiles/{folder}";
            var createFolder = CreateFtpDirectory(_configuration["FtpServer:Url"] + serverPath);
            if (!createFolder) return null;
            var fileUrl = serverPath + "/" + nameFile;
            var pathServer = _configuration["FtpServer:Url"] + fileUrl;
            var request = (FtpWebRequest) WebRequest.Create(pathServer);
            request.Method = WebRequestMethods.Ftp.UploadFile;
            request.Credentials = new NetworkCredential(_configuration["FtpServer:Username"],
                _configuration["FtpServer:Password"]);
            request.UsePassive = true;
            request.ContentLength = fileBytes.Length;
            var requestStream = request.GetRequestStream();
            requestStream.Write(fileBytes, 0, fileBytes.Length);
            requestStream.Close();
            var response = (FtpWebResponse) request.GetResponse();
            _logger.LogInformation("Upload File To FTP Complete, status {0}", response.StatusDescription);
            Console.WriteLine("Upload File To FTP Complete, status {0}", response.StatusDescription);
            response.Close();
            return _configuration["FtpServer:UrlViewFile"] + fileUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError("Upload file to FTP servie error: " + ex);
            Console.WriteLine("Upload file to FTP servie error: {0}", ex);
            return null;
        }
    }
  
    public bool DeleteFileOnFtpServer(string fileName, bool isLink = true)
    {
        try
        {
            var f = fileName;
            var fileUrl = f.Replace(_configuration["FtpServer:UrlViewFile"], "");
            var pathServer = isLink ? _configuration["FtpServer:Url"] + fileUrl : fileUrl;
            _logger.LogInformation($"Delete File:{pathServer}");
            var request = (FtpWebRequest) WebRequest.Create(pathServer);
            request.Method = WebRequestMethods.Ftp.UploadFile;
            request.Credentials = new NetworkCredential(_configuration["FtpServer:Username"],
                _configuration["FtpServer:Password"]);
            // Get the object used to communicate with the server.

            request.Method = WebRequestMethods.Ftp.DeleteFile;
            var response = (FtpWebResponse) request.GetResponse();
            //Console.WriteLine("Delete status: {0}", response.StatusDescription);
            response.Close();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("DeleteFileOnFtpServer error: " + ex);
            return false;
        }
    }

    private bool CreateFtpDirectory(string directory)
    {
        try
        {
            //create the directory
            
            _logger.LogInformation($"CreateFtpDirectory request: {directory}");
            var requestDir = (FtpWebRequest) WebRequest.Create(new Uri(directory));
            requestDir.Method = WebRequestMethods.Ftp.MakeDirectory;
            requestDir.Credentials = new NetworkCredential(_configuration["FtpServer:Username"],
                _configuration["FtpServer:Password"]);
            requestDir.UsePassive = true;
            //requestDir.UseBinary = true;
            //requestDir.KeepAlive = false;
            var checkExits = DoesFtpDirectoryExist(directory);
            if (checkExits) return true;
            var response = (FtpWebResponse) requestDir.GetResponse();
            var ftpStream = response.GetResponseStream();
            ftpStream?.Close();
            response.Close();
            _logger.LogInformation($"CreateFtpDirectory return: {response.StatusDescription}-{directory}");
            return true;
        }
        catch (WebException ex)
        {
            _logger.LogInformation($"CreateFtpDirectory {directory} is exsit");
            var response = (FtpWebResponse) ex.Response;
            if (response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
            {
                response.Close();
                return true;
            }

            response.Close();
            return false;
        }
    }

    private bool DoesFtpDirectoryExist(string directory)
    {
        try
        {
            _logger.LogInformation($"Process DoesFtpDirectoryExist request: {directory}");
            var requestDir = (FtpWebRequest) WebRequest.Create(new Uri(directory));
            requestDir.Method = WebRequestMethods.Ftp.ListDirectory;
            requestDir.Credentials = new NetworkCredential(_configuration["FtpServer:Username"],
                _configuration["FtpServer:Password"]);
            requestDir.UsePassive = true;
            _logger.LogInformation($"Process DoesFtpDirectoryExist begin: {directory}");
            var response = (FtpWebResponse) requestDir.GetResponse();
            var ftpStream = response.GetResponseStream();
            ftpStream?.Close();
            response.Close();
            _logger.LogInformation($"DoesFtpDirectoryExist return: {response.StatusDescription}-{directory}");
            return true;
        }
        catch (WebException ex)
        {
            _logger.LogInformation($"DoesFtpDirectoryExist is exist: {directory}| {ex}");
            return false;
        }
    }

    public string UploadFileReportPartnerToDataServer(string partnerCode, byte[] fileBytes, string nameFile)
    {
        try
        {
            //Check tao thư muc
            var folder = DateTime.Now.ToString("yyyyMMdd");
            var serverPath = $"/{partnerCode}/{folder}";
            var createFolder = CreateFtpDirectoryReport(_configuration["FtpServer:Url"] + serverPath);
            if (!createFolder) return null;
            var fileUrl = serverPath + "/" + nameFile;
            var pathServer = _configuration["FtpServer:Url"] + fileUrl;
            var request = (FtpWebRequest)WebRequest.Create(pathServer);
            request.Method = WebRequestMethods.Ftp.UploadFile;
            request.Credentials = new NetworkCredential(_configuration["FtpServer:UserNameReport"],
                _configuration["FtpServer:PasswordReport"]);
            request.UsePassive = true;
            request.ContentLength = fileBytes.Length;
            var requestStream = request.GetRequestStream();
            requestStream.Write(fileBytes, 0, fileBytes.Length);
            requestStream.Close();
            var response = (FtpWebResponse)request.GetResponse();
            _logger.LogInformation("Upload File To FTP Complete, status {0}", response.StatusDescription);
            Console.WriteLine("Upload File To FTP Complete, status {0}", response.StatusDescription);
            response.Close();
            return _configuration["FtpServer:UrlViewFile"] + fileUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError("Upload file to FTP servie error: " + ex);
            Console.WriteLine("Upload file to FTP servie error: {0}", ex);
            return null;
        }
    }

    private bool CreateFtpDirectoryReport(string directory)
    {
        try
        {
            //create the directory
            _logger.LogInformation($"CreateFtpDirectory request: {directory}");
            var requestDir = (FtpWebRequest)WebRequest.Create(new Uri(directory));
            requestDir.Method = WebRequestMethods.Ftp.MakeDirectory;
            requestDir.Credentials = new NetworkCredential(_configuration["FtpServer:UserNameReport"],
                _configuration["FtpServer:PasswordReport"]);
            requestDir.UsePassive = false;
            //requestDir.UseBinary = true;
            //requestDir.KeepAlive = false;
            var checkExits = DoesFtpDirectoryExistReport(directory);
            if (checkExits) return true;
            var response = (FtpWebResponse)requestDir.GetResponse();
            var ftpStream = response.GetResponseStream();
            ftpStream?.Close();
            response.Close();
            _logger.LogInformation($"CreateFtpDirectory return: {response.StatusDescription}-{directory}");
            return true;
        }
        catch (WebException ex)
        {
            _logger.LogInformation($"CreateFtpDirectory {directory} is exsit");
            var response = (FtpWebResponse)ex.Response;
            if (response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
            {
                response.Close();
                return true;
            }

            response.Close();
            return false;
        }
    }


    private bool DoesFtpDirectoryExistReport(string directory)
    {
        try
        {
            _logger.LogInformation($"Process DoesFtpDirectoryExist request: {directory}");
            var requestDir = (FtpWebRequest)WebRequest.Create(new Uri(directory));
            requestDir.Method = WebRequestMethods.Ftp.ListDirectory;
            requestDir.Credentials = new NetworkCredential(_configuration["FtpServer:UserNameReport"],
                _configuration["FtpServer:PasswordReport"]);
            requestDir.UsePassive = true;
            _logger.LogInformation($"Process DoesFtpDirectoryExist begin: {directory}");
            var response = (FtpWebResponse)requestDir.GetResponse();
            var ftpStream = response.GetResponseStream();
            ftpStream?.Close();
            response.Close();
            _logger.LogInformation($"DoesFtpDirectoryExist return: {response.StatusDescription}-{directory}");
            return true;
        }
        catch (WebException ex)
        {
            _logger.LogInformation($"DoesFtpDirectoryExist is exist: {directory}| {ex}");
            return false;
        }
    }

}