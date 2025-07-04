using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;
using SER.Utilitties.NetCore.Utilities;
using Microsoft.Extensions.Options;
using SER.Utilitties.NetCore.Configuration;
using System.Collections;
using Dapper;
using System.Data;

namespace SER.Utilitties.NetCore.Services
{
    public class PostgresQLService
    {
        private readonly ILogger _logger;
        private IConfiguration _config;
        private readonly IHttpContextAccessor _contextAccessor;
        private IMemoryCache _cache;
        public static readonly ILoggerFactory MyLoggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddFilter((category, level) => category == DbLoggerCategory.Database.Command.Name
                        && level == LogLevel.Information)
                .AddConsole();
        });
        private readonly IOptionsMonitor<SERRestOptions> _optionsDelegate;

        // Configuration constants for better maintainability
        private const int DefaultCommandTimeout = 120;
        private const int DefaultPageSize = 20;

        public PostgresQLService(
            ILoggerFactory loggerFactory,
            IHttpContextAccessor httpContextAccessor,
            IMemoryCache memoryCache,
            IOptionsMonitor<SERRestOptions> optionsDelegate,
            IConfiguration config)
        {
            _logger = loggerFactory.CreateLogger("PostgresQLService");
            _config = config;
            _contextAccessor = httpContextAccessor;
            _cache = memoryCache;
            _optionsDelegate = optionsDelegate;
        }

        // Helper method to get connection string from configuration
        private string GetConnectionString()
        {
            return _optionsDelegate.CurrentValue.ConnectionString;
        }

        /// <summary>
        /// Helper method to configure NpgsqlCommand with query, timeout, and parameters
        /// Eliminates code duplication across multiple database operations
        /// </summary>
        private void ConfigureCommand(NpgsqlCommand cmd, string query, Dictionary<string, object> parameters = null, List<NpgsqlParameter> npgsqlParams = null)
        {
            cmd.CommandText = query;
            cmd.CommandTimeout = DefaultCommandTimeout;

            if (parameters != null)
                cmd.Parameters.AddRange(cmd.SetSqlParamsPsqlSQL(parameters, _logger));

            if (npgsqlParams != null && npgsqlParams.Count > 0)
                cmd.Parameters.AddRange(npgsqlParams.ToArray());

            LogCommandExecution(cmd);
        }

        /// <summary>
        /// Helper method to log command execution details
        /// </summary>
        private void LogCommandExecution(NpgsqlCommand cmd)
        {
            _logger.LogInformation($"Executed DbCommand [Parameters=[{ParamsToString(cmd.Parameters.ToArray())}], " +
                $"CommandType={cmd.CommandType}, CommandTimeout='{cmd.CommandTimeout}']\n" +
                $"      Query\n      {cmd.CommandText}");
        }

        /// <summary>
        /// Helper method to convert parameters to string for logging
        /// </summary>
        /// <summary>
        /// Helper method to convert parameters to string for logging
        /// </summary>
        public static string ParamsToString(NpgsqlParameter[] parameters)
        {
            return "{" + string.Join(",", parameters.Select(kv => kv.ParameterName + "=" + kv.Value).ToArray()) + "}";
        }


        private string Filter<E>(out Dictionary<string, object> Params, string prefix) where E : class
        {
            var expresion = new StringBuilder();
            Params = new Dictionary<string, object>();

            // Filter By
            if (_contextAccessor.HttpContext.Request.Query.Any(x => x.Key.Equals("filter_by")))
            {
                var columnStr = _contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("filter_by")).Value.ToString();
                string pattern = @"\/|\|";
                string[] columns = Regex.Split(columnStr, pattern);
                var properties = new Dictionary<string, Type>();
                var divider = new List<string>();
                Match match = Regex.Match(columnStr, pattern);

                foreach (var propertyInfo in typeof(E).GetProperties())
                {
                    if (!propertyInfo.GetCustomAttributes(true).Any(x => x.GetType() == typeof(JsonIgnoreAttribute))
                        && !propertyInfo.GetCustomAttributes(true).Any(x => x.GetType() == typeof(NotMappedAttribute))
                        && !propertyInfo.GetCustomAttributes(true).Where(x => x.GetType() == typeof(ColumnAttribute)).Any(attr => ((ColumnAttribute)attr).TypeName == "geography"
                        || ((ColumnAttribute)attr).TypeName == "jsonb")
                        && !(propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(IList<>))
                        && !(propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                        && !(propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>))
                        && !(typeof(ICollection).IsAssignableFrom(propertyInfo.PropertyType)))
                        properties.Add(propertyInfo.Name, propertyInfo.PropertyType);
                }

                // query filtro por AND o OR
                foreach (var (value, index) in columns.Select((v, i) => (v, i)))
                {
                    if (index > 0)
                    {
                        match = match.NextMatch();
                        if (match.Success)
                        {
                            if (match.Value == "/") divider.Add(" AND ");
                            else divider.Add(" OR ");
                        }

                    }
                    else
                    {
                        if (match.Success)
                        {
                            if (match.Value == "/") divider.Add(" AND ");
                            else divider.Add(" OR ");
                        }
                    }
                }

                //Procesamiento query
                string dividerOld = "";
                foreach (var (column, index) in columns.Select((v, i) => (v, i)))
                {

                    if (index > 0)
                    {
                        if (index > 1 && dividerOld != divider[index - 1])
                        {
                            expresion.Append(")");
                            expresion.Append(divider[index - 1]);
                            expresion.Append("(");
                        }
                        else expresion.Append(divider[index - 1]);
                        dividerOld = divider[index - 1];
                    }
                    else expresion.Append("(");

                    var patternStr = @"\=|¬|<=|>=|>|<";
                    string[] value = Regex.Split(column, patternStr);
                    if (string.IsNullOrEmpty(value[1])) break;

                    //initialClass = currentClass.ToLower().First().ToString() + ".";
                    //if (!properties.Keys.Contains(value[0]) && value[0] != "$")
                    //    initialClass = "";

                    if (value[0] == "all")
                    {
                        prefix += ".";
                        foreach (var (field, i) in properties.Select((v, i) => (v, i)))
                        {
                            ConcatFilter<E>(Params, expresion, string.Format("@P_{0}_", i + index), field.Key, value[1], column, prefix,
                                typeProperty: field.Value, index: i);
                        }
                        break;
                    }
                    var paramName = string.Format("@P_{0}_", index);
                    ConcatFilter<E>(Params, expresion, paramName, value[0], value[1], column, "");

                }
                expresion.Append(")");
                _logger.LogInformation("{0}", expresion.ToString());

            }
            return expresion.ToString();
        }

        private void ConcatFilter<E>(Dictionary<string, object> Params, StringBuilder expresion, string paramName,
            string key, string value, string column, string initialClass, Type typeProperty = null, int? index = null)
             where E : class
        {
            if (typeof(E).Name == "ApplicationUser" || typeof(E).Name == "ApplicationRole")
            {
                key = key.ToSnakeCase();
            }

            var select = "";
            var enable = true;
            var patternStr = @"\=|¬|<=|>=|>|<";
            Match matchStr = Regex.Match(column, patternStr);

            if (typeProperty != null && typeProperty == typeof(string))
            {
                Params.Add(paramName, $"%{value}%");
                select = string.Format("{0}{1} ilike {2}", initialClass, key, paramName);
            }
            else if (typeProperty != null && TypeExtensions.IsNumber(typeProperty))
            {
                Params.Add(paramName, $"%{value}%");
                select = string.Format("{0}{1}::text ilike {2}", initialClass, key, paramName);
            }
            else
            {
                if (matchStr.Success && matchStr.Value == "¬")
                {
                    Params.Add(paramName, $"%{value}%");
                    select = string.Format("{0}{1}::text ilike {2}", initialClass, key, paramName);
                }
                else if (value.ToLower().Trim() == "null")
                {
                    select = string.Format("{0}{1} is NULL", initialClass, key);
                    //Params.Add(paramName, "NULL");
                }
                else if (int.TryParse(value, out int number))
                {
                    select = AddParam(Params, paramName, key, initialClass, matchStr, number, matchStr.Value);
                }
                //else if (float.TryParse(value, out float fnumber))
                //{
                //    select = string.Format("{0}{1} = {2}", initialClass, key, paramName);
                //    Params.Add(paramName, fnumber);
                //}
                else if (double.TryParse(value, out double dnumber))
                {
                    //select = string.Format("{0}{1} = {2}", initialClass, key, paramName);
                    //Params.Add(paramName, dnumber);
                    select = AddParam(Params, paramName, key, initialClass, matchStr, dnumber, matchStr.Value);
                }
                //else if (decimal.TryParse(value, out decimal denumber))
                //{
                //    select = string.Format("{0}{1} = {2}", initialClass, key, paramName);
                //    Params.Add(paramName, denumber);
                //}
                else if (bool.TryParse(value, out bool boolean))
                {
                    select = string.Format("{0}{1} = {2}", initialClass, key, paramName);
                    Params.Add(paramName, boolean);
                }
                else if (DateTime.TryParse(value, out DateTime dateTime) == true)
                {
                    Params.Add(paramName, dateTime);
                    select = string.Format("{0}{1}::date = {2}::date", initialClass, key, paramName);
                }
                else
                {
                    if (typeProperty != null && typeProperty != typeof(string))
                    {
                        enable = false;
                    }

                    if (matchStr.Success)
                    {
                        if (matchStr.Value == "=")
                        {
                            if (value.Contains(";"))
                            {
                                paramName = $"{key}s";

                                var array = value.ToString().Split(";");
                                var isNumber = true;
                                foreach (var b in array.Select(x => int.TryParse(x, out int number))) if (!b) isNumber = false;
                                if (isNumber) Params.Add(paramName, array.Select(int.Parse).ToArray());
                                else Params.Add(paramName, array);
                                select = string.Format("{0}{1}", initialClass, key) + " in ({" + paramName + "})";
                            }
                            else
                            {
                                Params.Add(paramName, value.ToString());
                                select = string.Format("{0}{1} = {2}", initialClass, key, paramName);
                            }
                        }
                        else
                        {
                            Params.Add(paramName, $"%{value}%");
                            select = string.Format("{0}{1} ilike {2}", initialClass, key, paramName);
                        }
                    }
                }
            }

            if (enable)
            {
                if (index != null && index > 0 && expresion.Length > 1)
                    expresion.Append(" OR ");
                expresion.Append(select);
            }

        }

        private string AddParam(Dictionary<string, object> Params, string paramName,
            string key, string initialClass, Match matchStr, dynamic value, string paramValue)
        {
            string select = string.Format("{0}{1} {2} {3}", initialClass, key, paramValue, paramName);
            if (matchStr.Success && matchStr.Value == "¬")
            {
                select = string.Format("{0}{1}::text ilike {2}", initialClass, key, paramName);
                Params.Add(paramName, $"%{value}%");
            }
            else
            {
                Params.Add(paramName, value);
            }
            return select;
        }


        #region Dapper Methods - Modern Simplified Alternatives

        /// <summary>
        /// Execute a query and return dynamic results using Dapper
        /// 🚀 MUCH simpler than GetDataFromDBAsync - reduces 50+ lines to 1 line
        /// Perfect for quick queries where you don't need strong typing
        /// </summary>
        public async Task<IEnumerable<dynamic>> GetDataWithDapper(string query, object parameters = null)
        {
            using var connection = new NpgsqlConnection(GetConnectionString());
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation($"Executing Dapper query: {query}");
                if (parameters != null)
                    _logger.LogInformation($"Parameters: {JsonSerializer.Serialize(parameters)}");

                return await connection.QueryAsync(query, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Dapper Error {0} {1}", ex.Message, ex.StackTrace);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogDebug($"Dapper Query Time: {stopwatch.Elapsed} ({stopwatch.Elapsed.Milliseconds}ms)");
            }
        }

        /// <summary>
        /// Execute a query and return strongly typed results using Dapper
        /// 🎯 Automatic object mapping - no more manual reflection!
        /// Replaces the complex generic GetDataFromDBAsync for simple cases
        /// </summary>
        public async Task<IEnumerable<T>> GetDataWithDapper<T>(string query, object parameters = null)
        {
            using var connection = new NpgsqlConnection(GetConnectionString());
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation($"Executing Dapper query for {typeof(T).Name}: {query}");
                if (parameters != null)
                    _logger.LogInformation($"Parameters: {JsonSerializer.Serialize(parameters)}");

                return await connection.QueryAsync<T>(query, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Dapper Error {0} {1}", ex.Message, ex.StackTrace);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogDebug($"Dapper Query Time: {stopwatch.Elapsed} ({stopwatch.Elapsed.Milliseconds}ms)");
            }
        }

        /// <summary>
        /// Execute a query and return a single result using Dapper
        /// 📄 Perfect for single-record queries - much simpler than current implementation
        /// </summary>
        public async Task<T> GetSingleWithDapper<T>(string query, object parameters = null)
        {
            using var connection = new NpgsqlConnection(GetConnectionString());
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation($"Executing Dapper single query for {typeof(T).Name}: {query}");
                if (parameters != null)
                    _logger.LogInformation($"Parameters: {JsonSerializer.Serialize(parameters)}");

                return await connection.QueryFirstOrDefaultAsync<T>(query, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Dapper Error {0} {1}", ex.Message, ex.StackTrace);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogDebug($"Dapper Query Time: {stopwatch.Elapsed} ({stopwatch.Elapsed.Milliseconds}ms)");
            }
        }

        /// <summary>
        /// Execute non-query commands (INSERT, UPDATE, DELETE) using Dapper
        /// ⚡ Returns number of affected rows - much cleaner than current commit=true approach
        /// </summary>
        public async Task<int> ExecuteWithDapper(string query, object parameters = null)
        {
            using var connection = new NpgsqlConnection(GetConnectionString());
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation($"Executing Dapper command: {query}");
                if (parameters != null)
                    _logger.LogInformation($"Parameters: {JsonSerializer.Serialize(parameters)}");

                return await connection.ExecuteAsync(query, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Dapper Error {0} {1}", ex.Message, ex.StackTrace);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogDebug($"Dapper Command Time: {stopwatch.Elapsed} ({stopwatch.Elapsed.Milliseconds}ms)");
            }
        }

        /// <summary>
        /// Execute scalar queries using Dapper (COUNT, SUM, MAX, etc.)
        /// 🔢 Much simpler than GetCountDBAsync for custom scalar queries
        /// </summary>
        public async Task<T> GetScalarWithDapper<T>(string query, object parameters = null)
        {
            using var connection = new NpgsqlConnection(GetConnectionString());
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation($"Executing Dapper scalar query: {query}");
                if (parameters != null)
                    _logger.LogInformation($"Parameters: {JsonSerializer.Serialize(parameters)}");

                return await connection.ExecuteScalarAsync<T>(query, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Dapper Error {0} {1}", ex.Message, ex.StackTrace);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogDebug($"Dapper Scalar Time: {stopwatch.Elapsed} ({stopwatch.Elapsed.Milliseconds}ms)");
            }
        }


        #endregion

        #region Advanced Helper Methods - Further Optimization

        /// <summary>
        /// Dapper version of GetCountDBAsync - much simpler implementation
        /// </summary>
        public async Task<int> GetCountWithDapper(string query, object parameters = null)
        {
            string countQuery = @"SELECT COUNT(*) FROM ( " + query + " ) as p";
            return await GetScalarWithDapper<int>(countQuery, parameters);
        }

        /// <summary>
        /// Optimized version of GetCountDBAsync using Dapper for better performance
        /// 🚀 Uses Dapper internally for maximum efficiency
        /// </summary>
        public async Task<int> GetCountDBAsync(string query, Dictionary<string, object>? Params = null)
        {
            // Usar DynamicParameters de Dapper para mejor control
            var parameters = new DynamicParameters();
            if (Params != null)
            {
                foreach (var param in Params)
                {
                    parameters.Add(param.Key, param.Value);
                }
            }

            string countQuery = @"SELECT COUNT(*) FROM ( " + query + " ) as p";
            return await GetScalarWithDapper<int>(countQuery, parameters);
        }

        /// <summary>
        /// Simplified method for common pagination scenarios
        /// Combines query execution and count in a single efficient operation
        /// </summary>
        public async Task<(IEnumerable<T> Results, int TotalCount)> GetPagedResultsAsync<T>(
            string query, object parameters = null, int page = 1, int pageSize = 20)
        {
            // Execute count and data queries in parallel for better performance
            var countTask = GetCountWithDapper(query, parameters);

            var offset = (page - 1) * pageSize;
            var pagedQuery = $"{query} LIMIT @PageSize OFFSET @Offset";

            var queryParams = parameters != null ?
                new Dictionary<string, object>(parameters.GetType().GetProperties()
                    .ToDictionary(p => p.Name, p => p.GetValue(parameters))) :
                new Dictionary<string, object>();

            queryParams["PageSize"] = pageSize;
            queryParams["Offset"] = offset;

            var dataTask = GetDataWithDapper<T>(pagedQuery, queryParams);

            await Task.WhenAll(countTask, dataTask);

            return (await dataTask, await countTask);
        }

        /// <summary>
        /// Execute multiple queries in a single transaction for better performance
        /// Useful for complex operations that require atomicity
        /// </summary>
        public async Task<List<T>> ExecuteTransactionAsync<T>(
            List<(string Query, object Parameters)> operations,
            Func<List<IEnumerable<dynamic>>, List<T>> resultProcessor = null)
        {
            using var connection = new NpgsqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();
            var results = new List<IEnumerable<dynamic>>();

            try
            {
                foreach (var (query, parameters) in operations)
                {
                    var result = await connection.QueryAsync(query, parameters, transaction);
                    results.Add(result);
                }

                await transaction.CommitAsync();

                return resultProcessor != null ? resultProcessor(results) : new List<T>();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Bulk insert operation optimized for large datasets
        /// Much faster than individual inserts for large data volumes
        /// </summary>
        public async Task<int> BulkInsertAsync<T>(string tableName, IEnumerable<T> entities) where T : class
        {
            using var connection = new NpgsqlConnection(GetConnectionString());
            await connection.OpenAsync();

            var properties = typeof(T).GetProperties()
                .Where(p => !p.GetCustomAttributes(typeof(NotMappedAttribute), false).Any())
                .ToArray();

            var columns = string.Join(", ", properties.Select(p => p.Name.ToSnakeCase()));
            var parameters = string.Join(", ", properties.Select((p, i) => $"@{p.Name}"));

            var query = $"INSERT INTO {tableName} ({columns}) VALUES ({parameters})";

            return await connection.ExecuteAsync(query, entities);
        }

        public async Task<string> GetDataFromDBAsync(string query, Dictionary<string, object> Params = null,
            string OrderBy = "", string GroupBy = "", bool commit = false, bool jObject = false, bool json = true, string take = null, string page = null,
            string queryCount = null, string connection = null)
        {
            return await GetDataFromDBAsync<object>(query, Params: Params, // NpgsqlParams: NpgsqlParams,
                OrderBy: OrderBy, GroupBy: GroupBy, commit: commit, jObject: jObject, json: json, connection: connection,
                takeStr: take, pageStr: page, queryCount: queryCount, allowQueryCollection: false);
        }

        /// Executes a SQL query asynchronously against a PostgreSQL database and returns the result in various formats.
        /// </summary>
        /// <typeparam name="E">The entity type for mapping results (must be a class).</typeparam>
        /// <param name="query">The SQL query to execute.</param>
        /// <param name="Params">Optional dictionary of parameter names and values to pass to the query.</param>
        /// <param name="NpgsqlParams">Optional list of NpgsqlParameter objects for advanced parameterization.</param>
        /// <param name="OrderBy">Optional ORDER BY clause to append to the query.</param>
        /// <param name="GroupBy">Optional GROUP BY clause to append to the query.</param>
        /// <param name="commit">Indicates whether to commit the transaction (if applicable).</param>
        /// <param name="jObject">If true, returns a single JSON object; otherwise, returns a JSON array.</param>
        /// <param name="json">If true, returns the result as a JSON string; otherwise, returns a dynamic object.</param>
        /// <param name="serialize">If true, serializes the result (not currently used).</param>
        /// <param name="connection">Optional connection string to use; if null, uses the default connection.</param>
        /// <param name="prefix">Optional prefix for query customization.</param>
        /// <param name="whereArgs">Optional WHERE clause arguments for query customization.</param>
        /// <param name="locationClauseWhere"> 0 -> antes del group by, 1 -> despues del group by , 2 -> despues del order by. Default 0</param>
        /// <returns>
        /// A task representing the asynchronous operation. The result is either a JSON string or a dynamic object,
        /// depending on the <paramref name="json"/> parameter.
        /// </returns>
        /// <exception cref="Exception">Throws if there is an error executing the query.</exception>
        public async Task<dynamic> GetDataFromDBAsync<E>(string query, Dictionary<string, object> Params = null,
            string OrderBy = "", string GroupBy = "", bool commit = false, bool allowQueryCollection = true,
            bool jObject = false, bool json = true, string connection = null, string prefix = null,
            string? pageStr = null, string? takeStr = null, string? queryCount = null, bool skipClauseWhere = false, int locationClauseWhere = 0) where E : class
        {
            using var conn = new NpgsqlConnection(connection ?? GetConnectionString());
            var stopwatch = Stopwatch.StartNew();

            StringBuilder sb = new();
            PagedResultBase pageResult = null;

            Params ??= new Dictionary<string, object>();

            try
            {
                // Usar DynamicParameters de Dapper para mejor control
                var parameters = new DynamicParameters();
                if (Params != null)
                {
                    foreach (var param in Params)
                    {
                        // Manejo especial para arrays
                        if (param.Value != null &&
                           (param.Value.GetType().IsArray ||
                           (param.Value is IEnumerable && !(param.Value is string))))
                        {
                            // Asegurar que el nombre del parámetro no tenga formato especial incompatible
                            string paramName = param.Key;
                            if (paramName.StartsWith("{") && paramName.EndsWith("}"))
                            {
                                // Extraer nombre sin llaves
                                paramName = paramName.Substring(1, paramName.Length - 2);

                                // Si no tiene @, aseguramos que lo tenga
                                if (!paramName.StartsWith("@"))
                                    paramName = "@" + paramName;
                            }
                            else if (!paramName.StartsWith("@"))
                            {
                                paramName = "@" + paramName;
                            }

                            // Agregar con tipo DbType.Object para asegurar que PostgreSQL lo trate como array
                            parameters.Add(paramName, param.Value, DbType.Object);
                        }
                        else
                        {
                            if (_optionsDelegate.CurrentValue.DebugMode)
                            {
                                _logger.LogDebug("Adding parameter: {Key} = {Value}", param.Key, param.Value);
                            }
                            parameters.Add(param.Key, param.Value);
                        }
                    }
                }

                // Si hay parámetros Npgsql, añadirlos a los parámetros de Dapper
                // se ejecuta el commit de una transaccion
                if (commit)
                {
                    // _logger.LogInformation($"Executing Dapper query: {query}");
                    if (!_optionsDelegate.CurrentValue.DebugMode)
                    {
                        await conn.ExecuteAsync(query, parameters);
                    }
                    else
                    {
                        await conn.ExecuteWithLoggingAsync<string>(
                            query,
                            parameters,
                            commandTimeout: DefaultCommandTimeout,
                            logger: _logger,
                            resolveParameters: false
                        );
                    }
                    return null;
                }

                var currentClass = typeof(E).Name;
                if (currentClass.Equals("ApplicationUser"))
                    currentClass = "user";
                else if (currentClass.Equals("ApplicationRole"))
                    currentClass = "role";

                var clauseWhere = Filter<E>(out Dictionary<string, object> ParamsRequest, prefix ?? currentClass.ToLower().First().ToString());
                foreach (var param in ParamsRequest)
                {
                    parameters.Add(param.Key, param.Value);
                }

                // se ubica el WHERE en la consulta antes del group by
                if (locationClauseWhere == 0)
                {
                    query = GetQueryWithWhere(query, clauseWhere, Params, skipClauseWhere: skipClauseWhere);
                }


                var jsonResult = string.Empty;

                // group by
                if (!string.IsNullOrEmpty(GroupBy))
                {
                    query = string.Format("{0}\nGROUP BY {1}", query, GroupBy);
                }

                // se ubica el WHERE en la consulta despues del group by
                if (locationClauseWhere == 1)
                {
                    query = GetQueryWithWhere(query, clauseWhere, Params, skipClauseWhere: skipClauseWhere);
                }

                // Order By
                if (_contextAccessor.HttpContext.Request.Query.Any(x => x.Key.Equals("order_by")) && allowQueryCollection)
                {
                    OrderBy = _contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("order_by")).Value.ToString();
                    //var initialClass = currentClass.ToLower().First();
                    string pattern = "\\.";
                    Match match = Regex.Match(OrderBy, pattern);
                    if (!match.Success)
                    {
                        if (!string.IsNullOrEmpty(prefix))
                        {
                            string[] paramsOrderBy = OrderBy.Split(',');
                            OrderBy = string.Join(", ", paramsOrderBy.Select(x => $"{prefix}." + x.Trim()));
                        }
                    }
                }

                if (!string.IsNullOrEmpty(OrderBy))
                {
                    query = string.Format("{0}\nORDER BY {1}", query, OrderBy);
                }

                // se ubica el WHERE en la consulta despues del order by
                if (locationClauseWhere == 2)
                {
                    query = GetQueryWithWhere(query, clauseWhere, Params, skipClauseWhere: skipClauseWhere);
                }

                bool pagination = false;
                Task<int> countTask = null;
                // Pagination
                if ((_contextAccessor.HttpContext.Request.Query.Any(x => x.Key.Equals("page")) && allowQueryCollection) || !string.IsNullOrEmpty(pageStr))
                {
                    // parameters ??= new Dictionary<string, object>();
                    pageResult = new PagedResultBase();
                    pagination = true;

                    // cuando pagination_type = 1 no se ejecuta el count
                    if (allowQueryCollection && int.TryParse(_contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("pagination_type")).Value.ToString(), out int paginationType) && paginationType == 1)
                    {
                        countTask = null;
                    }
                    else
                    {
                        // Execute count and data queries in parallel for better performance
                        countTask = GetCountWithDapper(queryCount ?? query, parameters);
                    }

                    var pageRequest = allowQueryCollection ? _contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("page")).Value.ToString() : null;
                    int page = !string.IsNullOrEmpty(pageStr) ?
                        int.Parse(pageStr) :
                        string.IsNullOrEmpty(pageRequest) ? 1 : int.Parse(pageRequest);

                    // Pagination
                    var pageSizeRequest = allowQueryCollection ? _contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("take")).Value.ToString() : null;
                    int pageSize = !string.IsNullOrEmpty(takeStr) ?
                        int.Parse(takeStr) :
                        string.IsNullOrEmpty(pageSizeRequest) ? DefaultPageSize : int.Parse(pageSizeRequest);


                    var offset = (page - 1) * pageSize;
                    query = $"{query} LIMIT @PageSize OFFSET @Offset";

                    parameters.Add("PageSize", pageSize);
                    parameters.Add("Offset", offset);
                    pageResult.current_page = page;
                    pageResult.page_size = pageSize;

                }

                 // se ubica el WHERE en la consulta despues de pagination
                if (locationClauseWhere == 3)
                {
                    query = GetQueryWithWhere(query, clauseWhere, Params, skipClauseWhere: skipClauseWhere);
                }

                Task<string> dataTask = null;
                query = SqlCommandExt.ReplaceInbyAny(query);

                if (json)
                {
                    if (jObject)
                        query = string.Format(@"SELECT row_to_json(t) FROM ({0}) t", query);
                    else
                        query = string.Format(@"SELECT COALESCE(array_to_json(array_agg(row_to_json(t))), '[]') FROM ({0}) t", query);

                    // _logger.LogInformation($"Executing Dapper query: {query}");

                    if (pagination)
                    {
                        dataTask = !_optionsDelegate.CurrentValue.DebugMode ?
                            conn.ExecuteScalarAsync<string>(query, parameters) :
                            conn.ExecuteScalarWithLoggingAsync<string>(
                                query,
                                parameters,
                                commandTimeout: DefaultCommandTimeout,
                                logger: _logger,
                                resolveParameters: false
                            );
                    }
                    else
                    {
                        jsonResult = !_optionsDelegate.CurrentValue.DebugMode ?
                            await conn.ExecuteScalarAsync<string>(query, parameters) :
                            await conn.ExecuteScalarWithLoggingAsync<string>(
                                query,
                                parameters,
                                commandTimeout: DefaultCommandTimeout,
                                logger: _logger,
                                resolveParameters: false
                            );

                        jsonResult ??= jObject ? string.Empty : "[]";
                    }
                }
                else
                {
                    // if (_optionsDelegate.CurrentValue.DebugMode) _logger.LogInformation("Executing Dapper query: {query}", query);

                    // Obtener los datos como dynamic para tener acceso a todos los campos
                    var results = !_optionsDelegate.CurrentValue.DebugMode ?
                        await conn.QueryAsync(query, parameters) :
                        await conn.QueryWithLoggingAsync(
                            query,
                            parameters,
                            commandTimeout: DefaultCommandTimeout,
                            logger: _logger,
                            resolveParameters: false
                        );

                    // Construir JSON manualmente para mejor control
                    if (!pagination) jsonResult = BuildJsonFromDynamicResults(results, jObject);
                }

                if (pagination && dataTask != null)
                {
                    var results = "";
                    if (countTask == null)
                    {
                        results = await dataTask;
                        pageResult.row_count = 0;
                        pageResult.page_count = 0;
                    }
                    else
                    {
                        await Task.WhenAll(countTask, dataTask);

                        var res = (await dataTask, await countTask);

                        pageResult.row_count = res.Item2;
                        pageResult.page_count = (int)Math.Ceiling((double)pageResult.row_count / pageResult.page_size);
                        results = res.Item1;
                    }

                    sb.Append(JsonSerializer.Serialize(pageResult));
                    sb.Replace("}", ",", sb.Length - 2, 2);
                    sb.Append("\n\"results\": ");
                    sb.Append(results ?? "[]");
                    sb.Append('}');

                    jsonResult = sb.ToString();

                }

                return jsonResult;
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Dapper Error {0} {1}", ex.Message, ex.StackTrace);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogInformation($"Dapper Query Time: {stopwatch.Elapsed} ({stopwatch.Elapsed.Milliseconds}ms)");
            }
        }

        #endregion


        #region Helper Methods - Additional Utilities

        private static string GetQueryWithWhere(string query, string clauseWhere, Dictionary<string, object> Params, bool skipClauseWhere = false)
        {
            if (!Params.Any())
            {
                if (!string.IsNullOrEmpty(clauseWhere) && !skipClauseWhere) query = string.Format("{0}\nWHERE {1}", query, clauseWhere);
                else if (!string.IsNullOrEmpty(clauseWhere) && skipClauseWhere) query = string.Format("{0}\nAND {1}", query, clauseWhere);
            }
            else
            {
                if (!string.IsNullOrEmpty(clauseWhere)) query = string.Format("{0}\nAND {1}", query, clauseWhere);
            }
            return query;
        }

        private string BuildJsonFromDynamicResults(IEnumerable<dynamic> results, bool jObject = false)
        {
            var sb = new StringBuilder(1024); // Pre-allocate reasonable size

            if (!jObject)
                sb.Append('[');

            bool isFirstRow = true;
            foreach (var row in results)
            {
                if (!isFirstRow && !jObject)
                    sb.Append(',');

                sb.Append('{');

                // Convertir el dynamic row a IDictionary para acceder a las propiedades
                var dict = (IDictionary<string, object>)row;
                bool isFirstProperty = true;

                foreach (var kvp in dict)
                {
                    if (!isFirstProperty)
                        sb.Append(',');

                    // Write property name
                    sb.Append('"').Append(kvp.Key).Append("\":");

                    // Write value
                    AppendJsonValueFromDynamic(sb, kvp.Value);

                    isFirstProperty = false;
                }

                sb.Append('}');
                isFirstRow = false;

                // Si jObject=true, solo queremos el primer objeto
                if (jObject)
                    break;
            }

            if (!jObject)
                sb.Append(']');
            else if (isFirstRow) // No había resultados y jObject=true
                sb.Append("{}");

            return sb.ToString();
        }

        private static void AppendJsonValueFromDynamic(StringBuilder sb, object value)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            var type = value.GetType();

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.String:
                    var str = (string)value;
                    // Check if it's already JSON
                    if (str.Length > 0 && (str[0] == '{' || str[0] == '['))
                        sb.Append(str);
                    else
                        sb.Append('"').Append(str.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r")).Append('"');
                    break;

                case TypeCode.Boolean:
                    sb.Append(((bool)value) ? "true" : "false");
                    break;

                case TypeCode.DateTime:
                    sb.Append('"').Append(((DateTime)value).ToString("o")).Append('"');
                    break;

                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Byte:
                case TypeCode.SByte:
                    sb.Append(value.ToString());
                    break;

                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    sb.Append(value.ToString().Replace(',', '.')); // Ensure decimal point for JSON
                    break;

                default:
                    // Handle special types
                    if (type == typeof(TimeSpan))
                    {
                        sb.Append('"').Append(value.ToString()).Append('"');
                    }
                    else if (type.IsArray)
                    {
                        HandleArrayValue(sb, value);
                    }
                    else if (value is Guid guid)
                    {
                        sb.Append('"').Append(guid.ToString()).Append('"');
                    }
                    else
                    {
                        // Default to string representation
                        sb.Append('"').Append(value.ToString().Replace("\"", "\\\"")).Append('"');
                    }
                    break;
            }
        }

        private static void HandleArrayValue(StringBuilder sb, object arrayValue)
        {
            sb.Append('[');

            if (arrayValue is int[] intArray)
            {
                for (int i = 0; i < intArray.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(intArray[i]);
                }
            }
            else if (arrayValue is string[] stringArray)
            {
                for (int i = 0; i < stringArray.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append('"').Append(stringArray[i].Replace("\"", "\\\"")).Append('"');
                }
            }
            else if (arrayValue is Array array)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    AppendJsonValueFromDynamic(sb, array.GetValue(i));
                }
            }

            sb.Append(']');
        }
        #endregion
    }

}