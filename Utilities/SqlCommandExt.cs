using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SER.Utilitties.NetCore.Utilities
{
    public static class SqlCommandExt
    {
        public static NpgsqlParameter[] SetSqlParamsPsqlSQL(this NpgsqlCommand command, Dictionary<string, object> Params = null,
           ILogger _logger = null)
        {
            List<NpgsqlParameter> SqlParameters = new();

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
                    else if (pair.Value.GetType() == typeof(Guid))
                    {
                        var param = new NpgsqlParameter(pair.Key, NpgsqlDbType.Uuid)
                        {
                            Value = pair.Value
                        };

                        SqlParameters.Add(param);
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
                        else if (pair.Value.GetType() == typeof(Guid[]))
                        {
                            foreach (var param in command.GetArrayParameters((Guid[])pair.Value, pair.Key))
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

                if (typeof(T) == typeof(Guid))
                {
                    var paramGuid = new NpgsqlParameter(paramName, NpgsqlDbType.Uuid)
                    {
                        Value = value
                    };

                    parameters.Add(paramGuid);
                }
                else
                {
                    parameters.Add(new NpgsqlParameter(paramName, value));
                }

                //_logger.LogInformation("@{0}={1}", paramName, value);
            }
            cmd.CommandText = cmd.CommandText.Replace("{" + paramNameRoot + "}", string.Join(", ", parameterNames));

            return parameters.ToArray();
        }

        public static string ReplaceInbyAny(string input)
        {
            // Busca patrones como: campo IN ({param})
            // Soporta: campo IN ({param}) y campo NOT IN ({param}), con o sin espacios
            var pattern = @"\b(\w+(?:\.\w+)?)\s+(NOT\s+)?IN\s*\(\s*\{\s*(\w+)\s*\}\s*\)";

            return Regex.Replace(input, pattern, m =>
            {
                string campo = m.Groups[1].Value;
                bool esNotIn = !string.IsNullOrWhiteSpace(m.Groups[2].Value);
                string variable = m.Groups[3].Value;

                if (esNotIn)
                    return $"{campo} != ALL(@{variable})";
                else
                    return $"{campo} = ANY(@{variable})";
            }, RegexOptions.IgnoreCase);
        }


        public static string MakeParamsQuery(List<string> contentValues, bool start = false, string @operator = "AND")
        {
            if (contentValues == null || contentValues.Count == 0)
                return string.Empty;

            // Si el operador es OR, lo convertimos a mayúsculas
            @operator = @operator?.Trim().ToUpper() == "OR" ? "OR" : "AND";

            // Si el operador es AND, lo dejamos como está
            if (@operator != "AND" && @operator != "OR")
                throw new ArgumentException("El operador debe ser 'AND' o 'OR'.");

            StringBuilder result = new();
            bool first = true;
            foreach (string data in contentValues)
            {
                var value = ReplaceInbyAny(data);
                // Console.WriteLine($" ================== Orginal: {data} Value: {value}");
                if (!start)
                {
                    if (first)
                    {
                        result.Append(" WHERE ( ");
                        first = false;
                    }
                    else
                        result.Append($" {@operator} ");
                }
                else
                {
                    result.Append($" {@operator} ");
                }
                result.Append(value);
            }

            if (!start) result.Append(" ) ");
            return result.ToString();
        }

        public static string MakeParamsQuery(List<ParamCondition> conditions, bool start = false)
        {
            if (conditions == null || conditions.Count == 0)
                return string.Empty;

            StringBuilder result = new StringBuilder();
            bool first = true;

            foreach (var cond in conditions)
            {
                var value = ReplaceInbyAny(cond.Expression);
                var op = cond.Operator?.Trim().ToUpper() == "OR" ? "OR" : "AND";

                if (!start)
                {
                    if (first)
                    {
                        result.Append(" WHERE ( ");
                        result.Append(value);
                        first = false;
                    }
                    else
                    {
                        result.Append($" {op} ");
                        result.Append(value);
                    }
                }
                else
                {
                    result.Append($" {op} ");
                    result.Append(value);
                }
            }

            if (!start) result.Append(" ) ");
            return result.ToString();
        }

    }

    public class ParamCondition
    {
        public string Expression { get; set; }  // Ej: "c.id in {{categorias}}"
        public string Operator { get; set; }    // "AND" o "OR"
    }
}
