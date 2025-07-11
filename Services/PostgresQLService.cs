﻿using Microsoft.Extensions.Configuration;
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

namespace SER.Utilitties.NetCore.Services
{
    public class PostgresService : IPostgresQLService
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

        public PostgresService(
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

        /// <summary>
        /// Helper method to execute database operations with standardized error handling and timing
        /// </summary>
        private async Task<T> ExecuteWithTimingAsync<T>(string connectionString, string query,
            Dictionary<string, object> parameters, List<NpgsqlParameter> npgsqlParams,
            Func<NpgsqlCommand, Task<T>> operation, string operationName = "Database Operation")
        {
            var stopwatch = Stopwatch.StartNew();

            await using var connection = new NpgsqlConnection(connectionString);
            try
            {
                await connection.OpenAsync();
                stopwatch.Start();

                using var cmd = connection.CreateCommand();
                ConfigureCommand(cmd, query, parameters, npgsqlParams);

                return await operation(cmd);
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Error in {OperationName}: {Message} {StackTrace} {Data}\n{InnerException}",
                    operationName, ex.Message, ex.StackTrace, ex.Data, ex.InnerException);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogDebug($"{operationName} Time total: {stopwatch.Elapsed} ({stopwatch.Elapsed.Milliseconds}ms)");
            }
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

        private string Pagination<E>(string query, out PagedResultBase result,
            Dictionary<string, object> Params, List<NpgsqlParameter> NpgsqlParams = null) where E : class
        {
            result = new PagedResultBase();
            if (int.TryParse(_contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("page")).Value.ToString(), out int pageNumber))
            {
                StringBuilder st = new StringBuilder();
                var ParamsPagination = new Dictionary<string, object>();
                int count = Params == null ? 0 : Params.Count;

                // Pagination

                var pageSizeRequest = _contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("take")).Value;
                //var currentPageRequest = _contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("page")).Value;
                int pageSize = string.IsNullOrEmpty(pageSizeRequest) ? DefaultPageSize : int.Parse(pageSizeRequest);
                //int pageNumber = string.IsNullOrEmpty(currentPageRequest) ? 1 : int.Parse(currentPageRequest);

                for (int i = 0; i < 2; i++)
                {
                    var param = $"@P_{count + i}_";
                    if (i == 0)
                    {
                        st.Append("LIMIT ");
                        st.Append(param);
                        ParamsPagination.Add(param, pageSize);
                    }
                    else
                    {
                        st.Append(" OFFSET ");
                        st.Append(param);
                        var value = (pageNumber * pageSize) - pageSize;
                        ParamsPagination.Add(param, value);
                    }
                }

                result.current_page = pageNumber;
                result.page_size = pageSize;

                if (int.TryParse(_contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("pagination_type")).Value.ToString(), out int paginationType) && paginationType == 1)
                {
                    result.row_count = 0;
                    result.page_count = 0;
                }
                else
                {
                    // Note: This synchronous method should be avoided in favor of PaginationAsync
                    // Keeping for backward compatibility but ideally callers should use async versions
                    result.row_count = GetCountDBAsync(query, Params, NpgsqlParams: NpgsqlParams).GetAwaiter().GetResult();

                    var pageCount = (double)result.row_count / pageSize;
                    result.page_count = (int)Math.Ceiling(pageCount);
                }

                foreach (var param in ParamsPagination)
                    Params.Add(param.Key, param.Value);

                return st.ToString();
            }
            else return string.Empty;
        }

        // Async version of pagination method to avoid deadlocks with .Result
        private async Task<string> PaginationAsync<E>(string query, PagedResultBase result,
            Dictionary<string, object> Params, List<NpgsqlParameter> NpgsqlParams = null) where E : class
        {
            if (int.TryParse(_contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("page")).Value.ToString(), out int pageNumber))
            {
                StringBuilder st = new StringBuilder();
                var ParamsPagination = new Dictionary<string, object>();
                int count = Params == null ? 0 : Params.Count;

                // Pagination
                var pageSizeRequest = _contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("take")).Value;
                int pageSize = string.IsNullOrEmpty(pageSizeRequest) ? DefaultPageSize : int.Parse(pageSizeRequest);

                for (int i = 0; i < 2; i++)
                {
                    var param = $"@P_{count + i}_";
                    if (i == 0)
                    {
                        st.Append("LIMIT ");
                        st.Append(param);
                        ParamsPagination.Add(param, pageSize);
                    }
                    else
                    {
                        st.Append(" OFFSET ");
                        st.Append(param);
                        var value = (pageNumber * pageSize) - pageSize;
                        ParamsPagination.Add(param, value);
                    }
                }

                result.current_page = pageNumber;
                result.page_size = pageSize;

                if (int.TryParse(_contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("pagination_type")).Value.ToString(), out int paginationType) && paginationType == 1)
                {
                    result.row_count = 0;
                    result.page_count = 0;
                }
                else
                {
                    result.row_count = await GetCountDBAsync(query, Params, NpgsqlParams: NpgsqlParams);
                    var pageCount = (double)result.row_count / pageSize;
                    result.page_count = (int)Math.Ceiling(pageCount);
                }

                foreach (var param in ParamsPagination)
                    Params.Add(param.Key, param.Value);

                return st.ToString();
            }
            else return string.Empty;
        }

        // Async version of pagination method to avoid deadlocks with .Result  
        private async Task<string> PaginationAsync(string query, int take, int page, PagedResultBase result,
         Dictionary<string, object> Params, List<NpgsqlParameter> NpgsqlParams = null)
        {
            StringBuilder st = new StringBuilder();
            var ParamsPagination = new Dictionary<string, object>();
            int count = Params == null ? 0 : Params.Count;

            int pageSize = take == 0 ? DefaultPageSize : take;
            int pageNumber = page == 0 ? 1 : page;

            for (int i = 0; i < 2; i++)
            {
                var param = $"@P_{count + i}_";
                if (i == 0)
                {
                    st.Append("LIMIT ");
                    st.Append(param);
                    ParamsPagination.Add(param, pageSize);
                }
                else
                {
                    st.Append(" OFFSET ");
                    st.Append(param);
                    var value = (pageNumber * pageSize) - pageSize;
                    ParamsPagination.Add(param, value);
                }
            }

            result.current_page = pageNumber;
            result.page_size = pageSize;

            if (int.TryParse(_contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("pagination_type")).Value.ToString(), out int paginationType) && paginationType == 1)
            {
                result.row_count = 0;
                result.page_count = 0;
            }
            else
            {
                result.row_count = await GetCountDBAsync(query, Params, NpgsqlParams: NpgsqlParams);
                var pageCount = (double)result.row_count / pageSize;
                result.page_count = (int)Math.Ceiling(pageCount);
            }

            foreach (var param in ParamsPagination)
                Params.Add(param.Key, param.Value);

            return st.ToString();
        }

        private string Pagination(string query, int take, int page, out PagedResultBase result,
         Dictionary<string, object> Params, List<NpgsqlParameter> NpgsqlParams = null)
        {
            result = new PagedResultBase();
            StringBuilder st = new StringBuilder();
            var ParamsPagination = new Dictionary<string, object>();
            int count = Params == null ? 0 : Params.Count;

            int pageSize = take == 0 ? DefaultPageSize : take;
            int pageNumber = page == 0 ? 1 : page;

            for (int i = 0; i < 2; i++)
            {
                var param = $"@P_{count + i}_";
                if (i == 0)
                {
                    st.Append("LIMIT ");
                    st.Append(param);
                    ParamsPagination.Add(param, pageSize);
                }
                else
                {
                    st.Append(" OFFSET ");
                    st.Append(param);
                    var value = (pageNumber * pageSize) - pageSize;
                    ParamsPagination.Add(param, value);
                }
            }

            result.current_page = pageNumber;
            result.page_size = pageSize;

            if (int.TryParse(_contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("pagination_type")).Value.ToString(), out int paginationType) && paginationType == 1)
            {
                result.row_count = 0;
                result.page_count = 0;
            }
            else
            {
                // Note: This synchronous method should be avoided in favor of PaginationAsync
                // Keeping for backward compatibility but ideally callers should use async versions
                result.row_count = GetCountDBAsync(query, Params, NpgsqlParams: NpgsqlParams).GetAwaiter().GetResult();
                var pageCount = (double)result.row_count / pageSize;
                result.page_count = (int)Math.Ceiling(pageCount);
            }

            foreach (var param in ParamsPagination)
                Params.Add(param.Key, param.Value);

            return st.ToString();
        }

        public async Task<int> GetCountDBAsync(string query, Dictionary<string, object> Params = null, List<NpgsqlParameter> NpgsqlParams = null)
        {
            string countQuery = @"SELECT COUNT(*) FROM ( " + query + " ) as p";

            return await ExecuteWithTimingAsync(
                GetConnectionString(),
                countQuery,
                Params,
                NpgsqlParams,
                async cmd => int.Parse((await cmd.ExecuteScalarAsync()).ToString()),
                "GetCountDBAsync"
            );
        }

        public async Task<string> GetDataFromDBAsync(string query, Dictionary<string, object> Params = null,
         string OrderBy = "", string GroupBy = "", bool commit = false, bool jObject = false, bool json = true, string take = null, string page = null,
         string queryCount = null, string connection = null, List<NpgsqlParameter> NpgsqlParams = null)
        {
            string SqlConnectionStr = connection ?? GetConnectionString();

            StringBuilder sb = new StringBuilder();
            string Query = query;
            PagedResultBase pageResult = null;

            if (!commit)
            {
                if (!string.IsNullOrEmpty(GroupBy))
                {
                    Query = string.Format("{0}\nGROUP BY {1}", Query, GroupBy);
                }

                // Order By
                if (!string.IsNullOrEmpty(OrderBy))
                {
                    Query = string.Format("{0}\nORDER BY {1}", Query, OrderBy);
                }

                // Pagination
                if (take != null && page != null && int.TryParse(take, out int n) && int.TryParse(page, out int m))
                {
                    Params ??= new Dictionary<string, object>();
                    pageResult = new PagedResultBase();
                    var paginate = await PaginationAsync(queryCount ?? Query, n, m, pageResult, Params, NpgsqlParams: NpgsqlParams);

                    if (!string.IsNullOrEmpty(paginate))
                        Query = string.Format("{0}\n{1}", Query, paginate);

                    if (pageResult != null)
                    {
                        sb.Append(JsonSerializer.Serialize(pageResult));
                        sb.Replace("}", ",", sb.Length - 2, 2);
                        sb.Append("\n\"results\": ");
                    }
                }

                if (json)
                {
                    if (jObject)
                        Query = string.Format(@"SELECT row_to_json(t) FROM ({0}) t", Query);
                    else
                        Query = string.Format(@"SELECT COALESCE(array_to_json(array_agg(row_to_json(t))), '[]') FROM ({0}) t", Query);
                }
            }

            Stopwatch sw = new();
            //var conn = _context.Database.GetDbConnection();

            await using (NpgsqlConnection _conn = new NpgsqlConnection(SqlConnectionStr))
            {
                try
                {
                    await _conn.OpenAsync();
                    //if (conn.State != ConnectionState.Open)
                    //{
                    //    await conn.OpenAsync();
                    //}

                    sw.Start();
                    using var cmd = _conn.CreateCommand();
                    {
                        ConfigureCommand(cmd, Query, Params);
                        if (commit)
                        {
                            // Insert some data                            
                            await cmd.ExecuteNonQueryAsync();
                        }
                        else
                        {
                            var dataReader = await cmd.ExecuteReaderAsync();
                            if (dataReader.HasRows)
                            {
                                if (!json)
                                {
                                    List<string> cols = new List<string>();
                                    int ncols = dataReader.FieldCount;
                                    for (int i = 0; i < ncols; ++i)
                                    {
                                        cols.Add(dataReader.GetName(i));
                                    }

                                    if (!jObject)
                                        sb.Append("[");

                                    //process each row
                                    var index = 0;
                                    while (await dataReader.ReadAsync())
                                    {
                                        index = 0;
                                        sb.Append("{");
                                        foreach (string col in cols)
                                        {
                                            if (dataReader.IsDBNull(index))
                                            {
                                                sb.AppendFormat("\"{0}\":null, ", col);
                                            }
                                            else if (dataReader.GetFieldType(index) == typeof(string))
                                            {
                                                if (!string.IsNullOrEmpty(dataReader[col].ToString())
                                                    && (dataReader[col].ToString().First().Equals('{')
                                                    || dataReader[col].ToString().First().Equals('[')))
                                                    sb.AppendFormat("\"{0}\":{1}, ", col, dataReader[col]);
                                                else
                                                    sb.AppendFormat("\"{0}\":\"{1}\", ", col, dataReader[col]);
                                            }
                                            else if (dataReader.GetFieldType(index) == typeof(Array))
                                            {
                                                sb.AppendFormat("\"{0}\":[{1}], ", col, string.Join(",", dataReader[col] as int[]));
                                            }
                                            else if (dataReader.GetFieldType(index) == typeof(bool))
                                            {
                                                sb.AppendFormat("\"{0}\":{1}, ", col, dataReader[col].ToString().ToLower());
                                            }
                                            else if (dataReader.GetFieldType(index) == typeof(DateTime))
                                            {
                                                sb.AppendFormat("\"{0}\":\"{1}\", ", col, ((DateTime)dataReader[col]).ToString("o"));
                                            }
                                            else if (dataReader.GetFieldType(index) == typeof(TimeSpan))
                                            {
                                                sb.AppendFormat("\"{0}\":\"{1}\", ", col, dataReader[col]);
                                            }
                                            else
                                                sb.AppendFormat("\"{0}\":{1}, ", col, dataReader[col]);
                                            index++;
                                        }
                                        sb.Replace(", ", "},", sb.Length - 2, 2);
                                    }

                                    if (!jObject)
                                        sb.Replace("},", "}]", sb.Length - 2, 2);
                                    else
                                        sb.Replace("},", "}", sb.Length - 2, 2);
                                }
                                else
                                {
                                    while (await dataReader.ReadAsync())
                                    {
                                        if (dataReader.IsDBNull(0))
                                        {
                                            sb.Append("");
                                            continue;
                                        }
                                        if (dataReader.GetFieldType(0) == typeof(string))
                                        {
                                            sb.Append(dataReader.GetString(0));
                                        }
                                        else if (dataReader.GetFieldType(0) == typeof(int))
                                        {
                                            sb.Append(dataReader.GetInt32(0));
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (!jObject)
                                    sb.Append("[]");
                            }
                            dataReader.Dispose();
                            dataReader.Close();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogCritical("Error {0} {1} {2}\n{3}", ex.Message, ex.StackTrace, ex.Data, ex.InnerException);
                    throw;
                }
                finally
                {
                    //_conn.Close();
                    sw.Stop();
                    //_logger.LogDebug($"Closing connection to database {_context.GetType().name}");
                    _logger.LogDebug($"Executed DbCommand Time total: {sw.Elapsed} ({sw.Elapsed.Milliseconds}ms)");
                }
            }
            if (pageResult != null)
            {
                sb.Append("}");
            }
            string response = sb.ToString();
            //if (jObject == false)
            //{
            //    if (response.Equals("[]"))
            //        response = string.Empty;
            //}
            //else
            if (jObject == true)
            {
                if (response.Equals("{}"))
                    response = string.Empty;
            }
            return response;
        }


        public async Task<dynamic> GetDataFromDBAsync<E>(string query, Dictionary<string, object> Params = null, List<NpgsqlParameter> NpgsqlParams = null,
           string OrderBy = "", string GroupBy = "", bool commit = false, bool jObject = false, bool json = true,
           bool serialize = false, string connection = null, string prefix = null, string whereArgs = null)
            where E : class
        {
            string SqlConnectionStr = connection ?? GetConnectionString();
            StringBuilder sb = new StringBuilder();
            string Query = query;
            PagedResultBase pageResult = null;
            if (serialize) json = false;


            if (!commit)
            {
                var currentClass = typeof(E).Name;
                if (currentClass.Equals("ApplicationUser"))
                    currentClass = "user";
                else if (currentClass.Equals("ApplicationRole"))
                    currentClass = "role";

                var clauseWhere = Filter<E>(out Dictionary<string, object> ParamsRequest, prefix ?? currentClass.ToLower().First().ToString());

                if (Params == null && whereArgs == null)
                {
                    Params = new Dictionary<string, object>();
                    if (!string.IsNullOrEmpty(clauseWhere))
                        Query = string.Format("{0}\nWHERE {1}", Query, clauseWhere);
                }
                else
                {
                    if (!string.IsNullOrEmpty(clauseWhere))
                        Query = string.Format("{0}\nAND ({1})", Query, clauseWhere);
                }

                if (Params == null)
                    Params = new Dictionary<string, object>();

                foreach (var param in ParamsRequest)
                    Params.Add(param.Key, param.Value);

                if (!string.IsNullOrEmpty(GroupBy))
                {
                    Query = string.Format("{0}\nGROUP BY {1}", Query, GroupBy);
                }

                // Order By
                if (_contextAccessor.HttpContext.Request.Query.Any(x => x.Key.Equals("order_by")))
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
                    Query = string.Format("{0}\nORDER BY {1}", Query, OrderBy);
                }

                // Pagination
                if (_contextAccessor.HttpContext.Request.Query.Any(x => x.Key.Equals("page")))
                {
                    Params ??= new Dictionary<string, object>();
                    pageResult = new PagedResultBase();
                    var paginate = await PaginationAsync<E>(Query, pageResult, Params, NpgsqlParams: NpgsqlParams);

                    if (!string.IsNullOrEmpty(paginate))
                        Query = string.Format("{0}\n{1}", Query, paginate);

                    if (pageResult != null)
                    {
                        if (!serialize)
                        {
                            sb.Append(JsonSerializer.Serialize(pageResult));
                            sb.Replace("}", ",", sb.Length - 2, 2);
                            sb.Append("\n\"results\": ");
                        }
                    }
                }

                if (json)
                {
                    if (jObject)
                        Query = string.Format(@"SELECT row_to_json(t) FROM ({0}) t", Query);
                    else
                        Query = string.Format(@"SELECT COALESCE(array_to_json(array_agg(row_to_json(t))), '[]') FROM ({0}) t", Query);
                }
            }

            Stopwatch sw = new Stopwatch();
            var list = new List<E>();

            await using (NpgsqlConnection _conn = new NpgsqlConnection(SqlConnectionStr))
            {
                try
                {
                    await _conn.OpenAsync();
                    sw.Start();
                    using var cmd = _conn.CreateCommand();
                    ConfigureCommand(cmd, Query, Params, NpgsqlParams);
                    if (commit)
                    {
                        // Insert some data                            
                        await cmd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        var dataReader = await cmd.ExecuteReaderAsync();
                        if (dataReader.HasRows)
                        {
                            if (serialize)
                            {
                                List<string> cols = new List<string>();
                                int ncols = dataReader.FieldCount;
                                for (int i = 0; i < ncols; ++i)
                                {
                                    cols.Add(dataReader.GetName(i));
                                }
                                //process each row
                                var index = 0;
                                var props = typeof(E).GetProperties();
                                while (await dataReader.ReadAsync())
                                {
                                    var obj = Activator.CreateInstance(typeof(E));
                                    index = 0;
                                    foreach (string col in cols)
                                    {
                                        var propertyInfo = props.SingleOrDefault(x => x.Name == col);
                                        if (propertyInfo == null) propertyInfo = props.SingleOrDefault(x => x.Name == col.ToCamelCase());

                                        if (propertyInfo == null) continue;
                                        if (dataReader.IsDBNull(index))
                                        {
                                            obj.GetType().GetProperty(propertyInfo.Name)?.SetValue(obj, null, null);
                                        }
                                        //dynamic value = typeof(JsonExtensions)
                                        //    .GetMethod("ElementToObject")
                                        //    .MakeGenericMethod(propertyInfo.PropertyType)
                                        //    .Invoke(null, new object[] { dataReader[col] });
                                        else
                                            obj.GetType().GetProperty(propertyInfo.Name)?.SetValue(obj, dataReader[col], null);

                                        index++;

                                    }
                                    list.Add((E)obj);
                                }
                            }
                            else if (!json)
                            {
                                List<string> cols = new List<string>();
                                int ncols = dataReader.FieldCount;
                                for (int i = 0; i < ncols; ++i)
                                {
                                    cols.Add(dataReader.GetName(i));
                                }

                                if (!jObject)
                                    sb.Append("[");

                                //process each row
                                var index = 0;
                                while (await dataReader.ReadAsync())
                                {
                                    index = 0;
                                    sb.Append("{");
                                    foreach (string col in cols)
                                    {
                                        if (dataReader.IsDBNull(index))
                                        {
                                            sb.AppendFormat("\"{0}\":null, ", col);
                                        }
                                        else if (dataReader.GetFieldType(index) == typeof(string))
                                        {
                                            if (!string.IsNullOrEmpty(dataReader[col].ToString())
                                                && (dataReader[col].ToString().First().Equals('{')
                                                || dataReader[col].ToString().First().Equals('[')))
                                                sb.AppendFormat("\"{0}\":{1}, ", col, dataReader[col]);
                                            else
                                                sb.AppendFormat("\"{0}\":\"{1}\", ", col, dataReader[col]);
                                        }
                                        else if (dataReader.GetFieldType(index) == typeof(Array))
                                        {
                                            sb.AppendFormat("\"{0}\":[{1}], ", col, string.Join(",", dataReader[col] as int[]));
                                        }
                                        else if (dataReader.GetFieldType(index) == typeof(bool))
                                        {
                                            sb.AppendFormat("\"{0}\":{1}, ", col, dataReader[col].ToString().ToLower());
                                        }
                                        else if (dataReader.GetFieldType(index) == typeof(DateTime))
                                        {
                                            sb.AppendFormat("\"{0}\":\"{1}\", ", col, ((DateTime)dataReader[col]).ToString("o"));
                                        }
                                        else if (dataReader.GetFieldType(index) == typeof(TimeSpan))
                                        {
                                            sb.AppendFormat("\"{0}\":\"{1}\", ", col, dataReader[col]);
                                        }
                                        else
                                            sb.AppendFormat("\"{0}\":{1}, ", col, dataReader[col]);
                                        index++;
                                    }
                                    sb.Replace(", ", "},", sb.Length - 2, 2);
                                }

                                if (!jObject)
                                    sb.Replace("},", "}]", sb.Length - 2, 2);
                                else
                                    sb.Replace("},", "}", sb.Length - 2, 2);
                            }
                            else
                            {
                                while (await dataReader.ReadAsync())
                                {
                                    if (dataReader.IsDBNull(0))
                                    {
                                        sb.Append("");
                                        continue;
                                    }
                                    if (dataReader.GetFieldType(0) == typeof(string))
                                    {
                                        sb.Append(dataReader.GetString(0));
                                    }
                                    else if (dataReader.GetFieldType(0) == typeof(int))
                                    {
                                        sb.Append(dataReader.GetInt32(0));
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (!jObject)
                                sb.Append("[]");
                        }
                        dataReader.Dispose();
                        dataReader.Close();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogCritical("Error {0} {1} {2}\n{3}", ex.Message, ex.StackTrace, ex.Data, ex.InnerException);
                    throw;
                }
                finally
                {
                    //_conn.Close();
                    sw.Stop();
                    //_logger.LogDebug($"Closing connection to database {_context.GetType().name}");
                    _logger.LogInformation($"Executed DbCommand Time total: {sw.Elapsed} ({sw.Elapsed.Milliseconds}ms)");
                }
            }
            if (pageResult != null)
            {
                sb.Append("}");
            }
            string response = sb.ToString();
            //if (jObject == false)
            //{
            //    if (response.Equals("[]"))
            //        response = string.Empty;
            //}
            //else
            if (jObject == true)
            {
                if (response.Equals("{}"))
                    response = string.Empty;
                if (serialize)
                    return list.FirstOrDefault();
            }
            if (serialize)
            {
                if (pageResult != null)
                {
                    PagedResult<E> result = new PagedResult<E>();
                    foreach (var propertyInfo in typeof(PagedResultBase).GetProperties())
                    {
                        var currentValue = propertyInfo.GetValue(pageResult);
                        result.GetType().GetProperty(propertyInfo.Name)?.SetValue(result, currentValue, null);
                    }
                    result.results = list;
                    return result;
                }
                return list;
            }
            return response;
        }


    }

}