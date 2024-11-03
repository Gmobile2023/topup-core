﻿using System.Threading.Tasks;

namespace HLS.Paygate.Shared.UniqueIdGenerator;

public interface ITransCodeGenerator
{
    Task<string> TransCodeGeneratorAsync(string prefix = "T");
    Task<long> AutoCloseIndex(string provider, bool success);
    Task ResetAutoCloseIndex(string provider);
    Task<int> GetAutoCloseIndex(string provider);
}