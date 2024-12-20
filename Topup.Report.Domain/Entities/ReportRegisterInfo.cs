﻿using MongoDbGenericRepository.Models;

namespace Topup.Report.Domain.Entities;

public class ReportRegisterInfo : Document
{
    public string Name { get; set; }
    public string Code { get; set; }
    public string Content { get; set; }
    public string EmailSend { get; set; }
    public string EmailCC { get; set; }
    public bool IsAuto { get; set; }
    public string AccountList { get; set; }
    public string Providers { get; set; }
    public string Extend { get; set; }
    public int Total { get; set; }
}