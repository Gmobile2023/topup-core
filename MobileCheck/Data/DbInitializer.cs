using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using MobileCheck.Models;
using MongoDB.Driver;
using MongoDB.Entities;

namespace MobileCheck.Data;

public class DbInitializer
{
    public static async Task InitDb(WebApplication app)
    {
        await DB.InitAsync("MobileCheck", MongoClientSettings
            .FromConnectionString(app.Configuration.GetConnectionString("Mongodb")));

        await DB.Index<MobileInfo>()
            .Key(x => x.Mobile, KeyType.Text)
            .CreateAsync();
    }
}
