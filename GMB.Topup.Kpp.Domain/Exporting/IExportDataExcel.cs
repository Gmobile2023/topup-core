using System.Collections.Generic;
using GMB.Topup.Kpp.Domain.Entities;
using GMB.Topup.Shared.Common;

namespace GMB.Topup.Kpp.Domain.Exporting;

public interface IExportDataExcel
{
    FileDto KppExportToFile(List<AccountDto> list, string name);
}