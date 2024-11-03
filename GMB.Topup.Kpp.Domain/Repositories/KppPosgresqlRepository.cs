using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GMB.Topup.Kpp.Domain.Entities;
using Microsoft.Extensions.Logging;
using ServiceStack.OrmLite;

namespace GMB.Topup.Kpp.Domain.Repositories;

public interface IKppPosgreRepository
{
    Task<List<Transaction>> GetDataTransPayment(string accountCode, DateTime date);
    Task<List<Transfer>> GetDataTransfer(string accountCode, DateTime date);
    Task<List<kpp_account>> GetKppAccount(string accountCode = "");
}

public class KppPosgreRepository : IKppPosgreRepository
{
    private readonly ILogger<KppPosgreRepository> _log;
    private readonly IPostgreConnectionFactory _postgre;

    public KppPosgreRepository(IPostgreConnectionFactory postgre,
        ILogger<KppPosgreRepository> log)
    {
        _log = log;
        _postgre = postgre;
    }

    public async Task<List<Transaction>> GetDataTransPayment(string accountCode, DateTime date)
    {
        try
        {
            using var data = _postgre.OpenDbConnection();
            var fromDate = date.Date;
            var toDate = date.Date.AddDays(1);

            //var query = data.From<Transaction>().Where(c => c.created_date >= date && c.created_date < toDate);
            //var list = data.Select(query);

            var list = string.IsNullOrEmpty(accountCode)
                ? await data.SelectAsync<Transaction>(c => c.created_date >= date && c.created_date < toDate)
                : await data.SelectAsync<Transaction>(c => c.created_date >= date && c.created_date < toDate && c.account_code == accountCode);

            return list;
        }
        catch (Exception ex)
        {
            _log.LogError(
                $"{date.ToString("yyyy-MM-dd")} GetDataTransPayment Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            return new List<Transaction>();
        }
    }

    public async Task<List<Transfer>> GetDataTransfer(string accountCode, DateTime date)
    {
        try
        {
            // return readFileTransfer("transfer.csv");
            using var data = _postgre.OpenDbConnection();
            var fromDate = date.Date;
            var toDate = date.Date.AddDays(1);
            var list = string.IsNullOrEmpty(accountCode)
                ? await data.SelectAsync<Transfer>(c => c.created_date >= date && c.created_date < toDate && c.status == 1)
                : await data.SelectAsync<Transfer>(c =>
                    c.created_date >= date && c.created_date < toDate && c.status == 1 &&
                    (c.sender == accountCode || c.receiver == accountCode));

            return list;
        }
        catch (Exception ex)
        {
            _log.LogError(
                $"{date.ToString("yyyy-MM-dd")} GetDataTransfer Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            return new List<Transfer>();
        }
    }

    public async Task<List<kpp_account>> GetKppAccount(string accountCode = "")
    {
        try
        {
            // return readFileKppAccount("kppaccount.csv");
            using var data = _postgre.OpenDbConnection();
            var list = string.IsNullOrEmpty(accountCode)
                ? await data.SelectAsync<kpp_account>()
                : await data.SelectAsync<kpp_account>(c => c.account_code == accountCode);
            return list;
        }
        catch (Exception ex)
        {
            _log.LogError($"{accountCode} GetKppAccount Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            return new List<kpp_account>();
        }
    }

    private List<Transaction> readFileTransaction(string file)
    {
        var data = File.ReadAllLines(file);
        var list = new List<Transaction>();
        foreach (var item in data)
        {
            if (!item.StartsWith("Id"))
            {
                var s = item.Split('\t');
                var t = new Transaction()
                {
                    Id = Convert.ToInt32(s[0]),
                    trans_code = s[1],
                    account_code = s[2],
                    type = s[3],
                    amount = Convert.ToDecimal(s[4]),
                    receiver = s[5],
                    telco = s[6],
                    created_date = Convert.ToDateTime(s[7]),
                    kpp_trans_id = s[8],
                    kpp_response = s[9],
                    status = Convert.ToInt32(s[10]),
                    balance = Convert.ToDecimal(s[11]),
                    ended_date = Convert.ToDateTime(s[12]),
                    discount_rate = Convert.ToDecimal(s[17]),
                    trans_amount = Convert.ToDecimal(s[15]),
                };

                list.Add(t);
            }
        }

        return list;
    }

    private List<Transfer> readFileTransfer(string file)
    {
        var data = File.ReadAllLines(file);
        var list = new List<Transfer>();
        foreach (var item in data)
        {
            if (!item.StartsWith("id"))
            {
                var s = item.Split('\t');
                var t = new Transfer()
                {
                    id = Convert.ToInt32(s[0]),
                    sender = s[1],
                    receiver = s[2],
                    amount = Convert.ToDecimal(s[3]),
                    trans_id = s[4],
                    created_date = Convert.ToDateTime(s[5]),
                    ended_date = Convert.ToDateTime(s[6]),
                    status = Convert.ToInt32(s[7]),
                    kpp_trans_id = s[8],
                    kpp_response = s[9],
                    is_deposit = s[10],
                };

                list.Add(t);
            }
        }

        return list;
    }

    private List<kpp_account> readFileKppAccount(string file)
    {
        var data = File.ReadAllLines(file);
        var list = new List<kpp_account>();
        foreach (var item in data)
        {
            if (!item.StartsWith("id"))
            {
                var s = item.Split('\t');
                var t = new kpp_account()
                {
                    id = Convert.ToInt32(s[0]),
                    account_code = s[1],
                    account_type = s[2],
                    mobile = s[3],
                    airtime_discount_rate = Convert.ToDecimal(s[11]),
                };

                list.Add(t);
            }
        }

        return list;
    }
}