using System.Collections.Generic;
using HLS.Paygate.Kpp.Domain.Entities;
using HLS.Paygate.Shared.Common;

namespace HLS.Paygate.Kpp.Domain.Exporting;

public interface IExportDataExcel
{
    FileDto KppExportToFile(List<AccountDto> list, string name);
}