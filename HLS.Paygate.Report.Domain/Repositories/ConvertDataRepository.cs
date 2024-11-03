using HLS.Paygate.Report.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HLS.Paygate.Report.Domain.Repositories
{
    public class ConvertDataRepository
    {
        public Task<List<ReportCardStockByDate>> FillterStock(List<ReportCardStockByDate> lst, DateTime fromDate, DateTime toDate)
        {
            try
            {
                #region 0.Trong kỳ

                var listKy = lst.Where(c => c.CreatedDate <= toDate.ToUniversalTime()
                                            && c.CreatedDate >= fromDate.ToUniversalTime());

                var listGroupKy = from x in listKy
                                  group x by new { x.StockCode, x.ProductCode, x.CategoryCode, x.CardValue }
                    into g
                                  select new ReportCardStockByDate
                                  {
                                      StockCode = g.Key.StockCode,
                                      ProductCode = g.Key.ProductCode,
                                      CategoryCode = g.Key.CategoryCode,
                                      CardValue = g.Key.CardValue,
                                      Decrease = g.Sum(c => c.Decrease),
                                      Increase = g.Sum(c => c.Increase),
                                      IncreaseOther = g.Sum(c => c.IncreaseOther),
                                      IncreaseSupplier = g.Sum(c => c.IncreaseSupplier),
                                      Sale = g.Sum(c => c.Sale),
                                      ExportOther = g.Sum(c => c.ExportOther)
                                  };

                #endregion

                #region 1.Đầu kỳ

                var listBefore = lst.Where(c => c.CreatedDate < fromDate.ToUniversalTime());

                var listGroupBefore = from x in listBefore
                                      group x by new { x.StockCode, x.ProductCode, x.CategoryCode, x.CardValue }
                    into g
                                      select new ReportCardStockByDate
                                      {
                                          StockCode = g.Key.StockCode,
                                          ProductCode = g.Key.ProductCode,
                                          CategoryCode = g.Key.CategoryCode,
                                          CardValue = Convert.ToInt32(g.Key.CardValue),
                                          CreatedDate = g.Max(c => c.CreatedDate)
                                      };

                var listViewBefore = from x in listGroupBefore
                                     join yc in listBefore on x.ProductCode equals yc.ProductCode
                                     where x.CategoryCode == yc.CategoryCode && x.CardValue == yc.CardValue
                                                                             && x.CreatedDate == yc.CreatedDate
                                     select new ReportCardStockByDate
                                     {
                                         StockCode = x.StockCode,
                                         ProductCode = x.ProductCode,
                                         CategoryCode = x.CategoryCode,
                                         CardValue = x.CardValue,
                                         InventoryBefore = Convert.ToInt32(yc.InventoryAfter)
                                     };

                #endregion

                #region 2.Cuối kỳ

                var listAfter = lst.Where(c => c.CreatedDate <= toDate.ToUniversalTime());

                var listGroupAfter = from x in listAfter
                                     group x by new { x.StockCode, x.ProductCode, x.CategoryCode, x.CardValue }
                    into g
                                     select new ReportCardStockByDate
                                     {
                                         StockCode = g.Key.StockCode,
                                         ProductCode = g.Key.ProductCode,
                                         CategoryCode = g.Key.CategoryCode,
                                         CardValue = Convert.ToInt32(g.Key.CardValue),
                                         CreatedDate = g.Max(c => c.CreatedDate)
                                     };

                var listViewAfter = from x in listGroupAfter
                                    join yc in listAfter on x.ProductCode equals yc.ProductCode
                                    where x.CategoryCode == yc.CategoryCode && x.CardValue == yc.CardValue
                                                                            && x.CreatedDate == yc.CreatedDate
                                    select new ReportCardStockByDate
                                    {
                                        StockCode = x.StockCode,
                                        ProductCode = x.ProductCode,
                                        CategoryCode = x.CategoryCode,
                                        CardValue = x.CardValue,
                                        InventoryAfter = Convert.ToInt32(yc.InventoryAfter)
                                    };

                #endregion


                var listView = from after in listViewAfter
                               join k in listGroupKy on after.ProductCode equals k.ProductCode into gk
                               from ky in gk.DefaultIfEmpty()
                               join d in listViewBefore on after.ProductCode equals d.ProductCode into gd
                               from before in gd.DefaultIfEmpty()
                               select new ReportCardStockByDate
                               {
                                   StockCode = after.StockCode,
                                   ProductCode = after.ProductCode,
                                   CardValue = after.CardValue,
                                   InventoryBefore = before?.InventoryBefore ?? 0,
                                   InventoryAfter = after?.InventoryAfter ?? 0,
                                   IncreaseSupplier = ky != null ? Convert.ToInt32(ky.IncreaseSupplier) : 0,
                                   IncreaseOther = ky != null ? Convert.ToInt32(ky.IncreaseOther) : 0,
                                   Sale = ky != null ? Convert.ToInt32(ky.Sale) : 0,
                                   ExportOther = ky != null ? Convert.ToInt32(ky.ExportOther) : 0
                               };


                return Task.FromResult(listView.ToList());
            }
            catch (Exception ex)
            {
                //_logger.LogError($"FillterStock error {ex}");
                return Task.FromResult(new List<ReportCardStockByDate>());
            }
        }

        public Task<List<ReportCardStockProviderByDate>> FillterStockProvider(List<ReportCardStockProviderByDate> lst, DateTime fromDate, DateTime toDate)
        {
            try
            {
                #region 0.Trong kỳ

                var listKy = lst.Where(c => c.CreatedDate <= toDate.ToUniversalTime()
                                            && c.CreatedDate >= fromDate.ToUniversalTime());

                var listGroupKy = from x in listKy
                                  group x by new { x.StockCode, x.ProviderCode, x.ProductCode, x.CategoryCode, x.CardValue } into g
                                  select new ReportCardStockProviderByDate
                                  {
                                      ProviderCode = g.Key.ProviderCode,
                                      StockCode = g.Key.StockCode,
                                      ProductCode = g.Key.ProductCode,
                                      CategoryCode = g.Key.CategoryCode,
                                      CardValue = g.Key.CardValue,
                                      Decrease = g.Sum(c => c.Decrease),
                                      Increase = g.Sum(c => c.Increase),
                                      IncreaseOther = g.Sum(c => c.IncreaseOther),
                                      IncreaseSupplier = g.Sum(c => c.IncreaseSupplier),
                                      Sale = g.Sum(c => c.Sale),
                                      ExportOther = g.Sum(c => c.ExportOther)
                                  };

                #endregion

                #region 1.Đầu kỳ

                var listBefore = lst.Where(c => c.CreatedDate < fromDate.ToUniversalTime());

                var listGroupBefore = from x in listBefore
                                      group x by new { x.ProviderCode, x.StockCode, x.ProductCode, x.CategoryCode, x.CardValue } into g
                                      select new ReportCardStockProviderByDate
                                      {
                                          ProviderCode = g.Key.ProviderCode,
                                          StockCode = g.Key.StockCode,
                                          ProductCode = g.Key.ProductCode,
                                          CategoryCode = g.Key.CategoryCode,
                                          CardValue = Convert.ToInt32(g.Key.CardValue),
                                          CreatedDate = g.Max(c => c.CreatedDate)
                                      };

                var listViewBefore = from x in listGroupBefore
                                     join yc in listBefore on x.ProductCode equals yc.ProductCode
                                     where x.CategoryCode == yc.CategoryCode && x.CardValue == yc.CardValue && x.ProviderCode == yc.ProviderCode
                                                                             && x.CreatedDate == yc.CreatedDate
                                     select new ReportCardStockProviderByDate
                                     {
                                         ProviderCode = x.ProviderCode,
                                         StockCode = x.StockCode,
                                         ProductCode = x.ProductCode,
                                         CategoryCode = x.CategoryCode,
                                         CardValue = x.CardValue,
                                         InventoryBefore = Convert.ToInt32(yc.InventoryAfter)
                                     };

                #endregion

                #region 2.Cuối kỳ

                var listAfter = lst.Where(c => c.CreatedDate <= toDate.ToUniversalTime());

                var listGroupAfter = from x in listAfter
                                     group x by new { x.ProviderCode, x.StockCode, x.ProductCode, x.CategoryCode, x.CardValue }
                    into g
                                     select new ReportCardStockProviderByDate
                                     {
                                         ProviderCode = g.Key.ProviderCode,
                                         StockCode = g.Key.StockCode,
                                         ProductCode = g.Key.ProductCode,
                                         CategoryCode = g.Key.CategoryCode,
                                         CardValue = Convert.ToInt32(g.Key.CardValue),
                                         CreatedDate = g.Max(c => c.CreatedDate)
                                     };

                var listViewAfter = from x in listGroupAfter
                                    join yc in listAfter on x.ProductCode equals yc.ProductCode
                                    where x.CategoryCode == yc.CategoryCode && x.CardValue == yc.CardValue
                                           && x.ProviderCode == yc.ProviderCode && x.CreatedDate == yc.CreatedDate
                                    select new ReportCardStockProviderByDate
                                    {
                                        ProviderCode = x.ProviderCode,
                                        StockCode = x.StockCode,
                                        ProductCode = x.ProductCode,
                                        CategoryCode = x.CategoryCode,
                                        CardValue = x.CardValue,
                                        InventoryAfter = Convert.ToInt32(yc.InventoryAfter)
                                    };

                #endregion


                var listView = from after in listViewAfter
                               join k in listGroupKy on after.ProductCode equals k.ProductCode into gk
                               from ky in gk.DefaultIfEmpty()
                               join d in listViewBefore on after.ProductCode equals d.ProductCode into gd
                               from before in gd.DefaultIfEmpty()
                               select new ReportCardStockProviderByDate
                               {
                                   ProviderCode = after.ProviderCode,
                                   StockCode = after.StockCode,
                                   ProductCode = after.ProductCode,
                                   CardValue = after.CardValue,
                                   InventoryBefore = before?.InventoryBefore ?? 0,
                                   InventoryAfter = after?.InventoryAfter ?? 0,
                                   IncreaseSupplier = ky != null ? Convert.ToInt32(ky.IncreaseSupplier) : 0,
                                   IncreaseOther = ky != null ? Convert.ToInt32(ky.IncreaseOther) : 0,
                                   Sale = ky != null ? Convert.ToInt32(ky.Sale) : 0,
                                   ExportOther = ky != null ? Convert.ToInt32(ky.ExportOther) : 0
                               };


                return Task.FromResult(listView.ToList());
            }
            catch (Exception ex)
            {
                // _logger.LogError($"FillterStockProvider error {ex}");
                return Task.FromResult(new List<ReportCardStockProviderByDate>());
            }
        }
    }
}
