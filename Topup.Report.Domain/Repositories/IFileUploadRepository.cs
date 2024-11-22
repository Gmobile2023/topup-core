namespace Topup.Report.Domain.Repositories;

public interface IFileUploadRepository
{
    string UploadFileToServer(string fileName);

    string UploadFileToDataServer(byte[] fileBytes, string fileName);

    bool DeleteFileOnFtpServer(string fileName, bool islink = true);
    string UploadFileReportPartnerToDataServer(string partnerCode, byte[] fileBytes, string nameFile);
}