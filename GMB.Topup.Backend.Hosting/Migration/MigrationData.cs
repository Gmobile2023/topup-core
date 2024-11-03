using System.Data;

namespace GMB.Topup.Backend.Hosting.Migration;

public class MigrationData
{
    public void Migration(IDbConnection data)
    {
        //var connectionFactory = serviceProvider.GetService<IContactConnectionFactory>();
        //using (var data = connectionFactory.Open())
        //{
        //data.CreateTableIfNotExists<Domain.Entities.Command>();
        //data.CreateTableIfNotExists<Domain.Entities.Action>();
        //data.CreateTableIfNotExists<Domain.Entities.Wallet>();
        //data.CreateTableIfNotExists<Domain.Entities.Transaction>();
        //data.CreateTableIfNotExists<Domain.Entities.Settlement>();

        //Mấy bảng này migration ở web rồi
        //data.CreateTableIfNotExists<Domain.Entities.System_Country>();
        //data.CreateTableIfNotExists<Domain.Entities.System_City>();
        //data.CreateTableIfNotExists<Domain.Entities.System_District>();
        //data.CreateTableIfNotExists<Domain.Entities.System_Ward>();
        //}
    }
}