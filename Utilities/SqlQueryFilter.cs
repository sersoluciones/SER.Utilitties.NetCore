using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace SER.Utilitties.NetCore.Utilities
{
    /// <summary>
    /// Validación de lo que llega del cliente y termina pegado al SQL. Los valores de filter_by ya van como parámetros,
    /// pero su columna y el order_by se concatenan tal cual: con `filter_by=true)) or ((true=1` se anulaba el WHERE del
    /// consumidor (su filtro de alcance) y una subconsulta en la "columna" o en el orden leía cualquier tabla. Aquí
    /// solo pasan columnas (`columna` o `alias.columna`) y, en el orden, asc/desc y nulls first/last.
    /// </summary>
    public static class SqlQueryFilter
    {
        private const int MaxOrderTerms = 5;

        private static readonly Regex Column = new(@"^[a-z_][a-z0-9_]*(?:\.[a-z_][a-z0-9_]*)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex OrderTerm = new(
            @"^(?:(?:[a-z_][a-z0-9_]*\.)?[a-z_][a-z0-9_]*|[1-9][0-9]?)(?:\s+(?:asc|desc))?(?:\s+nulls\s+(?:first|last))?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex Spaces = new(@"\s+");

        /// <summary>
        /// columna de una condición de filter_by: `columna`, `alias.columna` o `all` (todas las columnas del modelo);
        /// se ignoran los espacios alrededor
        /// </summary>
        public static bool IsSafeFilterColumn(string column)
        {
            if (string.IsNullOrWhiteSpace(column)) return false;
            var trimmed = column.Trim();
            return trimmed == "all" || Column.IsMatch(trimmed);
        }

        /// <summary>
        /// orden normalizado (términos separados por coma: columna o posición, con asc/desc y nulls first/last
        /// opcionales), o null si viene vacío o no es una lista de columnas
        /// </summary>
        public static string SanitizeOrderBy(string orderBy)
        {
            if (string.IsNullOrWhiteSpace(orderBy)) return null;

            var terms = orderBy.Split(',').Select(x => Spaces.Replace(x.Trim(), " ")).ToArray();
            if (terms.Length > MaxOrderTerms || terms.Any(x => !OrderTerm.IsMatch(x))) return null;
            return string.Join(", ", terms);
        }
    }

    /// <summary>
    /// filter_by u order_by del query string con algo distinto de columnas. El consumidor la puede responder con 400.
    /// </summary>
    public class InvalidQueryFilterException : ArgumentException
    {
        public InvalidQueryFilterException(string parameter, string value)
            : base($"El parámetro {parameter} solo admite columnas: '{value}'", parameter)
        {
            Parameter = parameter;
        }

        /// <summary>
        /// filter_by u order_by
        /// </summary>
        public string Parameter { get; }
    }
}
