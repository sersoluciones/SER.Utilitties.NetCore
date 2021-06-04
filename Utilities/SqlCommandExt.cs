using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Utilities
{
    public static class SqlCommandExt
    {
        public static NpgsqlParameter[] SetSqlParamsPsqlSQL(this NpgsqlCommand command, Dictionary<string, object> Params = null,
           ILogger _logger = null)
        {
            List<NpgsqlParameter> SqlParameters = new List<NpgsqlParameter>();
            if (Params != null)
            {
                foreach (var pair in Params)
                {
                    //_logger.LogInformation(0, $"{pair.Key} = {pair.Value}");
                    if (pair.Value is null)
                    {
                        SqlParameters.Add(new NpgsqlParameter(pair.Key, DBNull.Value));
                    }
                    else if (pair.Value.GetType() == typeof(string))
                    {
                        SqlParameters.Add(new NpgsqlParameter(pair.Key, (string)pair.Value));
                    }
                    else if (pair.Value.GetType() == typeof(int))
                    {
                        SqlParameters.Add(new NpgsqlParameter(pair.Key, (int)pair.Value));
                    }
                    else if (pair.Value.GetType() == typeof(bool))
                    {
                        SqlParameters.Add(new NpgsqlParameter(pair.Key, (bool)pair.Value));
                    }
                    else if (pair.Value.GetType() == typeof(decimal))
                    {
                        SqlParameters.Add(new NpgsqlParameter(pair.Key, (decimal)pair.Value));
                    }
                    else if (pair.Value.GetType() == typeof(float))
                    {
                        SqlParameters.Add(new NpgsqlParameter(pair.Key, (float)pair.Value));
                    }
                    else if (pair.Value.GetType() == typeof(long))
                    {
                        SqlParameters.Add(new NpgsqlParameter(pair.Key, (long)pair.Value));
                    }
                    else if (pair.Value.GetType() == typeof(double))
                    {
                        SqlParameters.Add(new NpgsqlParameter(pair.Key, (double)pair.Value));
                    }
                    else if (pair.Value.GetType() == typeof(DateTime))
                    {
                        SqlParameters.Add(new NpgsqlParameter(pair.Key, (DateTime)pair.Value));
                    }
                    else if (pair.Value.GetType().IsArray)
                    {
                        if (pair.Value.GetType() == typeof(string[]))
                        {
                            foreach (var param in command.GetArrayParameters((string[])pair.Value, pair.Key))
                            {
                                SqlParameters.Add(param);
                            }
                        }
                        else if (pair.Value.GetType() == typeof(int[]))
                        {
                            foreach (var param in command.GetArrayParameters((int[])pair.Value, pair.Key))
                            {
                                SqlParameters.Add(param);
                            }
                        }
                    }
                }
            }
            return SqlParameters.ToArray();
        }


        public static NpgsqlParameter[] GetArrayParameters<T>(this NpgsqlCommand cmd, IEnumerable<T> values,
                string paramNameRoot, int start = 1)
        {
            /* An array cannot be simply added as a parameter to a SqlCommand so we need to loop through things and add it manually. 
             * Each item in the array will end up being it's own SqlParameter so the return value for this must be used as part of the
             * IN statement in the CommandText.
             */
            var parameters = new List<NpgsqlParameter>();
            var parameterNames = new List<string>();
            var paramNbr = start;
            foreach (var value in values)
            {
                var paramName = string.Format("@{0}{1}", paramNameRoot, paramNbr++);
                parameterNames.Add(paramName);
                parameters.Add(new NpgsqlParameter(paramName, value));
                //_logger.LogInformation("@{0}={1}", paramName, value);
            }
            cmd.CommandText = cmd.CommandText.Replace("{" + paramNameRoot + "}", string.Join(", ", parameterNames));

            return parameters.ToArray();
        }

        public static string MakeParamsQuery(List<string> contentValues, bool start = false)
        {
            StringBuilder result = new StringBuilder();
            bool first = true;
            foreach (string data in contentValues)
            {
                if (!start)
                {
                    if (first)
                    {
                        result.Append(" WHERE ");
                        first = false;
                    }
                    else
                        result.Append(" AND ");
                }
                else
                {
                    result.Append(" AND ");
                }
                result.Append(data);
            }
            return result.ToString();
        }

    }
}
