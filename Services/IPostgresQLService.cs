using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;

namespace SER.Utilitties.NetCore.Services;

public interface IPostgresQLService
{
    Task<string> GetDataFromDBAsync(string query, Dictionary<string, object> Params = null,
          string OrderBy = "", string GroupBy = "", bool commit = false, bool jObject = false, bool json = true, string take = null, string page = null,
          string queryCount = null, string connection = null, List<NpgsqlParameter> NpgsqlParams = null);

    Task<dynamic> GetDataFromDBAsync<E>(string query, Dictionary<string, object> Params = null, List<NpgsqlParameter> NpgsqlParams = null,
           string OrderBy = "", string GroupBy = "", bool commit = false, bool jObject = false, bool json = true,
           bool serialize = false, string connection = null, string prefix = null, string whereArgs = null) where E : class;
}