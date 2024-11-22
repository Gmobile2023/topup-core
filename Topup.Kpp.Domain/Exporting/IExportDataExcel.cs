using System.Collections.Generic;
using Topup.Shared.Common;
using Topup.Kpp.Domain.Entities;

namespace Topup.Kpp.Domain.Exporting;

public interface IExportDataExcel
{
    FileDto KppExportToFile(List<AccountDto> list, string name);
}