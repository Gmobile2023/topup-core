using ServiceStack.Data;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.PostgreSQL;

namespace GMB.Topup.Kpp.Domain.Repositories;

public interface IPostgreConnectionFactory : IDbConnectionFactory
{

}

public class PostgreConnectionFactory : OrmLiteConnectionFactory, IPostgreConnectionFactory
{
    public PostgreConnectionFactory(string s) : base(s)
    {
    }

    public PostgreConnectionFactory(string s, PostgreSqlDialectProvider provider) : base(s, provider)
    {
    }
}