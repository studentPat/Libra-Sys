using System.Data.Common;

namespace LibraSys.Api.Data;

public interface IDbConnectionFactory
{
    DbConnection CreateConnection();
}
