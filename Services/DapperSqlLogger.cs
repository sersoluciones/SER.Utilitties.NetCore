using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Services
{
    /// <summary>
    /// Interceptor para logging detallado de consultas SQL de Dapper
    /// </summary>
    public class DapperSqlLogger : IDisposable
    {
        private readonly DbCommand _command;
        private readonly ILogger _logger;
        private readonly Stopwatch _stopwatch;
        private readonly string _operationName;
        private readonly bool _resolveParameters;


        public DapperSqlLogger(DbCommand command, ILogger logger, string operationName = "SQL Query",
         bool resolveParameters = true)
        {
            _command = command;
            _logger = logger;
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();
            _resolveParameters = resolveParameters;

            // Log al inicio de la consulta
            LogInitialCommand();
        }

        private void LogInitialCommand()
        {
            var parameters = GetParametersString();

            _logger.LogInformation(
                "[Dapper] Ejecutando {OperationType}: {CommandText} {Parameters}",
                _operationName,
                _command.CommandText,
                parameters
            );

            // Si está habilitado el nivel Debug, también mostramos la consulta con parámetros resueltos
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                if (_resolveParameters)
                {
                    _logger.LogDebug(
                        "[Dapper] Consulta con valores resueltos: {ResolvedQuery}",
                        GetResolvedQuery()
                    );
                }
                else
                {
                    _logger.LogDebug(
                        "[Dapper] Consulta con parámetros posicionales: {PositionalQuery}",
                        GetPositionalQuery()
                    );
                }
            }
        }


        /// <summary>
        /// Obtiene la consulta SQL con los parámetros posicionales ($1, $2, etc.)
        /// Con soporte especial para arrays en cláusulas IN
        /// </summary>
        private string GetPositionalQuery()
        {
            var positionalQuery = _command.CommandText;
            bool isNpgsql = _command is NpgsqlCommand;

            if (!isNpgsql)
                return positionalQuery;

            // Diccionario para almacenar los parámetros por nombre
            var namedParams = new Dictionary<string, string>();

            // Primero, identificamos todos los casos de IN con arrays para tratarlos especialmente
            var arrayInParams = new HashSet<string>();

            foreach (DbParameter parameter in _command.Parameters)
            {
                if (IsArrayParameter(parameter.ParameterName))
                {
                    string inClausePattern = $"IN\\s+{parameter.ParameterName}\\b";
                    if (System.Text.RegularExpressions.Regex.IsMatch(positionalQuery, inClausePattern,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        arrayInParams.Add(parameter.ParameterName);
                    }
                }
            }

            // Ahora recopilamos todos los parámetros
            foreach (DbParameter parameter in _command.Parameters)
            {
                string paramName = parameter.ParameterName;
                int index = _command.Parameters.IndexOf(parameter);
                string positionalName = $"${index + 1}";

                // Guardar en diccionario por nombre
                namedParams[paramName] = positionalName;
            }

            // Reemplazar los parámetros nombrados
            foreach (var param in namedParams)
            {
                string paramName = param.Key;
                string positionalName = param.Value;

                if (paramName.StartsWith("@") || paramName.StartsWith(":"))
                {
                    // Si es un array en una cláusula IN, usamos la sintaxis ANY
                    if (arrayInParams.Contains(paramName))
                    {
                        string inClausePattern = $"IN\\s+{paramName}\\b";
                        positionalQuery = System.Text.RegularExpressions.Regex.Replace(
                            positionalQuery,
                            inClausePattern,
                            $"= ANY({positionalName})", // Sintaxis PostgreSQL para arrays
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase
                        );
                    }
                    else
                    {
                        // Reemplazo normal para otros parámetros
                        positionalQuery = positionalQuery.Replace(paramName, positionalName);
                    }
                }
            }

            return positionalQuery;
        }

        /// <summary>
        /// Obtiene una representación de los parámetros como string con mejor soporte para arrays
        /// </summary>
        private string GetParametersString()
        {
            if (_command.Parameters.Count == 0)
                return "(sin parámetros)";

            var builder = new StringBuilder("{ ");
            foreach (DbParameter parameter in _command.Parameters)
            {
                builder.Append(parameter.ParameterName)
                       .Append('=');

                if (parameter.Value == null || parameter.Value == DBNull.Value)
                {
                    builder.Append("NULL");
                }
                // MEJORA: Formateo especial para arrays
                else if (parameter.Value is Array array)
                {
                    builder.Append('[');
                    bool first = true;
                    foreach (var item in array)
                    {
                        if (!first)
                            builder.Append(", ");

                        if (item == null)
                            builder.Append("NULL");
                        else if (item is string || item is DateTime || item is Guid)
                            builder.Append('\'').Append(item).Append('\'');
                        else
                            builder.Append(item);

                        first = false;
                    }
                    builder.Append(']');
                }
                // Manejo de colecciones genéricas
                else if (parameter.Value is IEnumerable<object> collection && !(parameter.Value is string))
                {
                    builder.Append('[');
                    bool first = true;
                    foreach (var item in collection)
                    {
                        if (!first)
                            builder.Append(", ");

                        if (item == null)
                            builder.Append("NULL");
                        else if (item is string || item is DateTime || item is Guid)
                            builder.Append('\'').Append(item).Append('\'');
                        else
                            builder.Append(item);

                        first = false;
                    }
                    builder.Append(']');
                }
                // Strings, DateTime, Guid con comillas
                else if (parameter.Value is string || parameter.Value is DateTime || parameter.Value is Guid)
                {
                    builder.Append('\'').Append(parameter.Value).Append('\'');
                }
                // Otros valores
                else
                {
                    builder.Append(parameter.Value);
                }

                builder.Append(", ");
            }

            if (_command.Parameters.Count > 0)
                builder.Length -= 2; // Eliminar última coma y espacio

            builder.Append(" }");
            return builder.ToString();
        }

        /// <summary>
        /// Obtiene la consulta SQL con los parámetros resueltos/reemplazados
        /// Versión mejorada para manejar arrays y parámetros posicionales PostgreSQL
        /// </summary>
        private string GetResolvedQuery()
        {
            var resolvedQuery = _command.CommandText;
            bool isNpgsql = _command is NpgsqlCommand;

            // Diccionario para mapear de nombres posicionales a valores
            var positionalParams = new Dictionary<string, string>();

            // Diccionario para almacenar todos los parámetros por nombre
            var namedParams = new Dictionary<string, string>();

            // Primero, recopilamos todos los parámetros y sus valores formateados
            foreach (DbParameter parameter in _command.Parameters)
            {
                string paramName = parameter.ParameterName;
                string paramValue = FormatParameterValue(parameter.Value);

                // Guardar en diccionario por nombre
                namedParams[paramName] = paramValue;

                // Si es PostgreSQL, añadir también los parámetros posicionales
                if (isNpgsql)
                {
                    int index = _command.Parameters.IndexOf(parameter);
                    positionalParams[$"${index + 1}"] = paramValue;
                }
            }

            // Ahora reemplazamos los parámetros posicionales
            foreach (var param in positionalParams)
            {
                resolvedQuery = resolvedQuery.Replace(param.Key, param.Value);
            }

            // Reemplazar los parámetros nombrados
            foreach (var param in namedParams)
            {
                string paramName = param.Key;
                string paramValue = param.Value;

                // Reemplazar los parámetros normales (con @ o :)
                if (paramName.StartsWith("@") || paramName.StartsWith(":"))
                {
                    // Para clausulas IN con arrays, hacemos un manejo especial
                    string inClausePattern = $"IN\\s+{paramName}\\b";
                    if (System.Text.RegularExpressions.Regex.IsMatch(resolvedQuery, inClausePattern,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase) &&
                        IsArrayParameter(param.Key))
                    {
                        // Extraer valores del array para formar la cláusula IN
                        string arrayValues = GetInClauseValues(param.Value);
                        resolvedQuery = System.Text.RegularExpressions.Regex.Replace(
                            resolvedQuery,
                            inClausePattern,
                            $"IN ({arrayValues})",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase
                        );
                    }
                    else
                    {
                        // Reemplazo normal para parámetros que no son arrays en cláusulas IN
                        resolvedQuery = resolvedQuery.Replace(paramName, paramValue);
                    }
                }
            }

            return resolvedQuery;
        }

        /// <summary>
        /// Determina si un valor de parámetro representa un array
        /// </summary>
        private bool IsArrayParameter(string parameterName)
        {
            foreach (DbParameter parameter in _command.Parameters)
            {
                if (parameter.ParameterName == parameterName)
                {
                    // Detección mejorada de arrays
                    if (parameter.Value == null)
                        return false;

                    var type = parameter.Value.GetType();

                    // Es un array directo
                    if (type.IsArray)
                        return true;

                    // Es una colección genérica
                    if (type.IsGenericType)
                    {
                        var genericTypeDefinition = type.GetGenericTypeDefinition();
                        return typeof(IEnumerable<>).IsAssignableFrom(genericTypeDefinition) &&
                               !typeof(string).IsAssignableFrom(type);
                    }

                    // Es una colección no genérica
                    return parameter.Value is System.Collections.IEnumerable &&
                           !(parameter.Value is string);
                }
            }
            return false;
        }

        /// <summary>
        /// Extrae los valores de un array para formar una cláusula IN (valor1, valor2, ...)
        /// </summary>
        private string GetInClauseValues(string arrayString)
        {
            // Si el formato es ARRAY[a, b, c], extraer los valores entre corchetes
            if (arrayString.StartsWith("ARRAY[") && arrayString.EndsWith("]"))
            {
                return arrayString.Substring(6, arrayString.Length - 7);
            }
            return arrayString;
        }

        /// <summary>
        /// Formatea valores de parámetros de manera especial según su tipo
        /// </summary>
        private string FormatParameterValue(object value)
        {
            if (value == null || value == DBNull.Value)
                return "NULL";

            // Manejo especial para arrays
            if (value is System.Array array)
            {
                var arrayValues = new List<string>();
                foreach (var item in array)
                {
                    if (item == null)
                        arrayValues.Add("NULL");
                    else if (item is string || item is DateTime || item is Guid)
                        arrayValues.Add($"'{item}'");
                    else
                        arrayValues.Add(item.ToString());
                }

                return $"ARRAY[{string.Join(", ", arrayValues)}]";
            }
            // Manejo especial para listas y colecciones
            else if (value is IEnumerable<object> collection && !(value is string))
            {
                var arrayValues = new List<string>();
                foreach (var item in collection)
                {
                    if (item == null)
                        arrayValues.Add("NULL");
                    else if (item is string || item is DateTime || item is Guid)
                        arrayValues.Add($"'{item}'");
                    else
                        arrayValues.Add(item.ToString());
                }

                return $"ARRAY[{string.Join(", ", arrayValues)}]";
            }
            // Strings, DateTime, Guid con comillas
            else if (value is string || value is DateTime || value is Guid)
            {
                return $"'{value}'";
            }
            // Valores numéricos y otros
            else
            {
                return value.ToString();
            }
        }

        public void Dispose()
        {
            _stopwatch.Stop();

            // Log al finalizar la consulta
            _logger.LogInformation(
                "[Dapper] Completada {OperationType} en {ElapsedMilliseconds}ms",
                _operationName,
                _stopwatch.ElapsedMilliseconds);
        }
    }

    // Extensiones para integrar con conexiones Dapper
    public static class DapperLoggingExtensions
    {
        /// <summary>
        /// Ejecuta una consulta con logging detallado
        /// </summary>
        public static async Task<T> ExecuteWithLoggingAsync<T>(
            this IDbConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null,
            ILogger logger = null,
            string operationName = "Query",
            bool resolveParameters = true)
        {
            // Obtenemos el comando subyacente
            var logCommand = connection.CreateCommand() as DbCommand;
            logCommand.CommandText = sql;
            logCommand.CommandType = commandType ?? CommandType.Text;

            if (commandTimeout.HasValue)
                logCommand.CommandTimeout = commandTimeout.Value;

            if (transaction != null)
                logCommand.Transaction = transaction as DbTransaction;

            // Añadir parámetros si existen
            if (param != null)
            {
                AddParametersToCommand(logCommand, param);
            }

            // Crear el logger solo si se proporcionó uno
            using var sqlLogger = logger != null ? new DapperSqlLogger(logCommand, logger, operationName, resolveParameters) : null;

            // Ejecutar la consulta original
            return await connection.QueryFirstOrDefaultAsync<T>(sql, param, transaction, commandTimeout, commandType);
        }

        /// <summary>
        /// Ejecuta múltiples consultas con logging detallado
        /// </summary>
        public static async Task<IEnumerable<T>> QueryWithLoggingAsync<T>(
            this IDbConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null,
            ILogger logger = null,
            string operationName = "Query",
            bool resolveParameters = true)
        {
            // Obtenemos el comando subyacente
            var logCommand = connection.CreateCommand() as DbCommand;
            logCommand.CommandText = sql;
            logCommand.CommandType = commandType ?? CommandType.Text;

            if (commandTimeout.HasValue)
                logCommand.CommandTimeout = commandTimeout.Value;

            if (transaction != null)
                logCommand.Transaction = transaction as DbTransaction;

            // Añadir parámetros si existen
            if (param != null)
            {
                AddParametersToCommand(logCommand, param);
            }

            // Crear el logger solo si se proporcionó uno
            using var sqlLogger = logger != null ? new DapperSqlLogger(logCommand, logger, operationName, resolveParameters) : null;

            // Ejecutar la consulta original
            return await connection.QueryAsync<T>(sql, param, transaction, commandTimeout, commandType);
        }

        /// <summary>
        /// Ejecuta una consulta escalar con logging detallado
        /// </summary>
        public static async Task<T> ExecuteScalarWithLoggingAsync<T>(
            this IDbConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null,
            ILogger logger = null,
            string operationName = "Scalar Query",
            bool resolveParameters = true)
        {
            // Obtenemos el comando subyacente
            var command = connection.CreateCommand() as DbCommand;
            command.CommandText = sql;
            command.CommandType = commandType ?? CommandType.Text;

            if (commandTimeout.HasValue)
                command.CommandTimeout = commandTimeout.Value;

            if (transaction != null)
                command.Transaction = transaction as DbTransaction;

            // Añadir parámetros si existen
            if (param != null)
            {
                AddParametersToCommand(command, param);
            }

            // Crear el logger solo si se proporcionó uno
            using var sqlLogger = logger != null ? new DapperSqlLogger(command, logger, operationName, resolveParameters) : null;

            // Ejecutar la consulta original
            return await connection.ExecuteScalarAsync<T>(sql, param, transaction, commandTimeout, commandType);
        }

        /// <summary>
        /// Añade parámetros desde un objeto anónimo o DynamicParameters a un DbCommand
        /// </summary>
        private static void AddParametersToCommand(DbCommand command, object param)
        {
            // Si es un DynamicParameters, extraemos los parámetros por reflexión
            if (param is DynamicParameters dynamicParams)
            {
                try
                {
                    // Obtenemos el diccionario interno de parámetros mediante reflexión
                    var parametersProperty = typeof(DynamicParameters)
                        .GetProperty("ParameterNames", BindingFlags.NonPublic | BindingFlags.Instance) ??
                        typeof(DynamicParameters)
                        .GetProperty("ParameterNames", BindingFlags.Public | BindingFlags.Instance);

                    if (parametersProperty != null)
                    {
                        var parameterNames = parametersProperty.GetValue(dynamicParams) as IEnumerable<string>;

                        if (parameterNames != null)
                        {
                            foreach (var name in parameterNames)
                            {
                                var value = dynamicParams.Get<object>(name);
                                AddParameter(command, name, value);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Si falla la reflexión, al menos añadimos información de que hay parámetros
                    AddParameter(command, "dapper_params", "(DynamicParameters - no se pueden extraer)");
                }
            }
            else if (param is IDictionary<string, object> paramDict)
            {
                // Si es un diccionario, lo añadimos directamente
                foreach (var kvp in paramDict)
                {
                    AddParameter(command, kvp.Key, kvp.Value);
                }
            }
            else
            {
                // Si es un objeto anónimo o de clase, extraemos las propiedades
                var properties = param.GetType().GetProperties();
                foreach (var prop in properties)
                {
                    var value = prop.GetValue(param);
                    AddParameter(command, prop.Name, value);
                }
            }
        }

        /// <summary>
        /// Añade un parámetro al comando
        /// </summary>
        private static void AddParameter(DbCommand command, string name, object value)
        {
            if (!name.StartsWith("@"))
                name = "@" + name;

            DbParameter parameter;

            if (command is NpgsqlCommand)
                parameter = new NpgsqlParameter(name, value ?? DBNull.Value);
            else
                parameter = command.CreateParameter();

            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;

            command.Parameters.Add(parameter);
        }
    }
}