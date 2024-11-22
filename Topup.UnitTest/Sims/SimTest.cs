namespace Topup.UnitTest.Sims;

public class SimTest
{
    // private readonly ITestOutputHelper _output;
    // private readonly Mock<IPaygateMongoRepository> _repository;
    // private readonly Mock<ISimRepository> _simRepository;
    // private readonly Mock<ICommandRepository> _command;
    // private readonly Mock<SimService> _simService;
    // private readonly Mock<IBus> _bus;
    // private readonly Mock<ICommonService> _commonService;
    //
    // public SimTest(ITestOutputHelper output)
    // {
    //     _repository = new Mock<IPaygateMongoRepository>();
    //     _command = new Mock<ICommandRepository>();
    //     _simService = new Mock<SimService>();
    //     _commonService = new Mock<ICommonService>();
    //     _output = output;
    //     _simRepository = new Mock<ISimRepository>();
    //     _bus = new Mock<IBus>();
    // }
    //
    // #region MyRegion
    //
    // // [Fact]
    // // public async Task Sim_Get_By_Number_Test()
    // // {
    // //     _simService.Setup(p => p.SimGetAsync("0969898839", null)).Returns(Task.FromResult(new SimDto()
    // //     {
    // //         Iccid = "123456792214",
    // //         SimNumber = "0969898839",
    // //         Id = Guid.NewGuid(),
    // //         Description = "TEST",
    // //         Status = SimStatus.Active,
    // //         Vendor = "VTE"
    // //     }));
    // //     var simDto = new SimDto
    // //     {
    // //         Iccid = "123456792214",
    // //         SimNumber = "0969898839",
    // //         Id = Guid.NewGuid(),
    // //         Description = "TEST",
    // //         Vendor = "VTE",
    // //         SimAppType = SimAppType.MyViettel,
    // //         MyViettelPass = "12313132",
    // //         IsSimPostpaid = false,
    // //         Status = SimStatus.Active,
    // //         LastTransTime = DateTime.Now,
    // //         IsInprogress = false,
    // //         TransTimesInDay = 0
    // //     };
    // //
    // //     _repository.Setup(x => x.GetOneAsync<Sim,Guid>(x => x.SimNumber == "432434"))
    // //         .Returns(Task.FromResult(new Sim()
    // //         {
    // //             Iccid = "123456792214",
    // //             SimNumber = "0969898839",
    // //             Id = Guid.NewGuid(),
    // //             Description = "TEST",
    // //             Status = SimStatus.Active,
    // //             Vendor = "VTE"
    // //         }));
    // //     var sim = new SimService(_repository.Object, _command.Object);
    // //     var result = await sim.SimGetAsync("0969898839", null);
    // //     Assert.NotNull(result);
    // // }
    //
    // #endregion
    //
    //
    // [Fact]
    // public async Task Sim_Insert_Test()
    // {
    //     var simDto = new SimDto
    //     {
    //         Iccid = "123456792214",
    //         SimNumber = "0969898839",
    //         Id = Guid.NewGuid(),
    //         Description = "TEST",
    //         Vendor = "VTE",
    //         SimAppType = SimAppType.MyViettel,
    //         MyViettelPass = "12313132",
    //         IsSimPostpaid = false,
    //         Status = SimStatus.Active,
    //         LastTransTime = DateTime.Now,
    //         IsInprogress = false,
    //         TransTimesInDay = 0
    //     };
    //     //Arrange
    //     _repository.Setup(p =>
    //             p.AddOneAsync<Sim>(simDto.ConvertTo<Sim>()))
    //         .Returns(Task.FromResult(true));
    //     var sim = new SimService(_repository.Object, _command.Object,_commonService.Object,_simRepository.Object);
    //     //Act
    //     var result = await sim.SimInsertAsync(simDto);
    //     _output.WriteLine($"Sim insert done - Simnumber: {result.SimNumber}");
    //     //Assert
    //     Assert.Equal("096989883911", result.SimNumber); //passs
    // }
    //
    // [Fact]
    // public async Task Sim_Update_Test()
    // {
    //     var simDto = new SimDto
    //     {
    //         Iccid = "123456792214",
    //         SimNumber = "0969898839",
    //         Id = Guid.NewGuid(),
    //         Description = "TEST",
    //         Vendor = "VTE",
    //         SimAppType = SimAppType.MyViettel,
    //         MyViettelPass = "12313132",
    //         IsSimPostpaid = false,
    //         Status = SimStatus.Active,
    //         LastTransTime = DateTime.Now,
    //         IsInprogress = false,
    //         TransTimesInDay = 0
    //     };
    //
    //     _repository.Setup(p =>
    //             p.UpdateOneAsync<Sim>(simDto.ConvertTo<Sim>()))
    //         .Returns(Task.FromResult(true));
    //     var sim = new SimService(_repository.Object, _command.Object,_commonService.Object,_simRepository.Object);
    //     var result = await sim.SimUpdateAsync(simDto);
    //     _output.WriteLine($"Sim update done return: {result}");
    //     Assert.True(result);
    // }
}