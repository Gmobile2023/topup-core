using System;
using System.Collections.Generic;
using System.Text;

namespace HLS.Paygate.Report.Domain.Repositories
{
    public interface IPosgresqlRepository
    {
        void Test();
    }

    public class PosgresqlRepository : IPosgresqlRepository
    {
        public void Test()
        {

        }
    }
}
