using System;
using System.Diagnostics;
using GMB.Topup.Discovery.Requests.TopupGateways;
using ServiceStack;
using Xunit;
using Xunit.Abstractions;

namespace GMB.Topup.UnitTest.Balance;

public class BalanceAccountTest
{
    private readonly ITestOutputHelper _output;

    public BalanceAccountTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Get_Sims_Paged_And_Sorted_And_Filtered()
    {
        var client = new JsonServiceClient("http://localhost:6899");
        try
        {
            client.Get<string>(new GateBillQueryRequest
            {
                Vendor = "ABC",
                ReceiverInfo = "ABC",
                ProviderCode = "ABC",
                TransRef = "ÁDASD"
            });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }


        const int number = 1000;
        const int machines = 100;

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < number; i++) Console.WriteLine(i);
        stopwatch.Stop();

        _output.WriteLine("Duration to generate {1:n0} ids: {0:n0} ms", stopwatch.ElapsedMilliseconds,
            number * machines);
        _output.WriteLine("Number of ids generated in 1 ms: {0:n0}", number * machines / stopwatch.ElapsedMilliseconds);
        _output.WriteLine("Number of ids generated in 1 s: {0:n0}",
            (int) (number * machines / (stopwatch.ElapsedMilliseconds / 1000.0)));
    }
}