using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;
using System.Dynamic;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using SER.Utilitties.NetCore.Models;
using System.Collections;

namespace SER.Utilitties.NetCore.Utilities
{
    public static class QueryableExt
    {
        public static async Task<dynamic> PaginationAsync<T>(this IQueryable<T> query, IHttpContextAccessor _contextAccessor) where T : class
        {
            bool allowCache = true;
            if (_contextAccessor.HttpContext.Request.Query.Any(x => x.Key.Equals("filter_by")))
            {
                var columnStr = _contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("filter_by")).Value.ToString();
                string[] columns = columnStr.Split(';');
                if (columns.Count() > 0) allowCache = false;
            }

            var pageSizeRequest = _contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("take")).Value;
            var currentPageRequest = _contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("page")).Value;
            int pageSize = string.IsNullOrEmpty(pageSizeRequest) ? 20 : int.Parse(pageSizeRequest);
            int pageNumber = string.IsNullOrEmpty(currentPageRequest) ? 1 : int.Parse(currentPageRequest);
            var result = new PagedResult<T>();

            result.current_page = pageNumber;
            result.page_size = pageSize;
            int? rowCount = null;
            if (allowCache)
                rowCount = CacheGetOrCreate(query, _contextAccessor);

            result.row_count = rowCount ?? await query.CountAsync();

            var pageCount = (double)result.row_count / pageSize;
            result.page_count = (int)Math.Ceiling(pageCount);

            string selectArgs = null;
            // select args
            if (_contextAccessor.HttpContext.Request.Query.Any(x => x.Key.Equals("select_args")))
            {
                selectArgs = _contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("select_args")).Value.ToString();
                selectArgs = $"new({selectArgs})";

                IDictionary<string, object> expando = new ExpandoObject();

                foreach (var propertyInfo in typeof(PagedResultBase).GetProperties())
                {
                    var currentValue = propertyInfo.GetValue(result);
                    expando.Add(propertyInfo.Name, currentValue);
                }
                expando.Add("results", await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).AsNoTracking()
                    .Select(selectArgs).ToDynamicListAsync());
                return expando as ExpandoObject;
            }
            else
            {
                result.results = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).AsNoTracking().ToListAsync();
            }
            //var skip = (page - 1) * pageSize;
            return result;
        }

        private static int CacheGetOrCreate<E>(IQueryable<E> query, IHttpContextAccessor _contextAccessor)
         where E : class
        {
            //var provider = new ServiceCollection()
            //           .AddMemoryCache()
            //           .BuildServiceProvider();
            var cache = _contextAccessor.HttpContext.RequestServices.GetService<IMemoryCache>();

            var cacheKeySize = string.Format("_{0}_size", typeof(E).Name);
            var cacheEntry = cache.GetOrCreate(cacheKeySize, entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromDays(1);
                entry.Size = 1000;
                return query.Count();
            });

            return cacheEntry;
        }

        public static async Task<dynamic> SortFilterAsync<E>(this IQueryable<E> source, IHttpContextAccessor _contextAccessor, bool pagination = true)
              where E : class
        {
            // Filter By
            if (_contextAccessor.HttpContext.Request.Query.Any(x => x.Key.Equals("filter_by")))
            {
                var properties = new Dictionary<string, Type>();

                foreach (var propertyInfo in typeof(E).GetProperties())
                {
                    //Console.WriteLine($"_________________TRACEEEEEEEEEEEEEEEEE____________: key: {propertyInfo.Name} value: {propertyInfo.PropertyType.Name}");
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

                var columnStr = _contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("filter_by")).Value.ToString();
                string pattern = @"\/|\|";
                string[] columns = Regex.Split(columnStr, pattern);
                Match match = Regex.Match(columnStr, pattern);

                var listExpOR = new List<Expression<Func<E, bool>>>();
                var listExpAND = new List<Expression<Func<E, bool>>>();

                int index = 0;
                //Procesamiento query
                foreach (var column in columns)
                {
                    var patternStr = @"\=|¬";
                    string[] value = Regex.Split(column, patternStr);
                    //Console.WriteLine($"=======================index {index} count {value.Count()} {string.Join(",", value)}");
                    if (value.Count() == 0 || string.IsNullOrEmpty(value[0]) || string.IsNullOrEmpty(value[1])) continue;

                    if (value[0] == "$")
                    {
                        try
                        {
                            foreach (var (field, i) in properties.Select((v, i) => (v, i)))
                            {
                                ConcatFilter(listExpOR, listExpAND, index + i, field.Key, value[1], "¬", field.Value);
                            }
                            break;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.ToString());
                        }
                    }

                    Type fieldType = null;
                    var matchStr = Regex.Match(column, patternStr);
                    if (matchStr.Success)
                    {
                        var fieldName = Regex.Replace(value[0], patternStr, "");
                        foreach (var (propertyInfo, j) in typeof(E).GetProperties().Select((v, j) => (v, j)))
                        {
                            if (propertyInfo.Name == fieldName)
                            {
                                fieldType = propertyInfo.PropertyType;
                                if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(Nullable<>))
                                {
                                    fieldType = fieldType.GetGenericArguments()[0];
                                }
                                break;
                            }
                        }
                        if (fieldType == null && value[0].Contains(".")) fieldType = typeof(ISERQueryable);
                        ConcatFilter(listExpOR, listExpAND, index, value[0], value[1], matchStr.Value, fieldType, match);
                    }

                    index++;
                }

                if (listExpOR.Count() > 0)
                    source = source.Where(Join(Expression.Or, listExpOR));
                if (listExpAND.Count() > 0)
                    source = source.Where(Join(Expression.And, listExpAND));
            }

            // Order By
            if (_contextAccessor.HttpContext.Request.Query.Any(x => x.Key.Equals("order_by")))
            {
                source = source.OrderBy(_contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("order_by")).Value.ToString());
            }

            string selectArgs = null;
            // select args
            if (_contextAccessor.HttpContext.Request.Query.Any(x => x.Key.Equals("select_args")))
            {
                selectArgs = _contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("select_args")).Value.ToString();
                selectArgs = $"new({selectArgs})";
                //IQueryable query = source.Select(selectArgs);
            }

            // Pagination
            if (pagination && _contextAccessor.HttpContext.Request.Query.Any(x => x.Key.Equals("take"))
                && _contextAccessor.HttpContext.Request.Query.Any(x => x.Key.Equals("page")))
                return await source.PaginationAsync(_contextAccessor);

            if (!string.IsNullOrEmpty(selectArgs))
            {
                return await source.AsNoTracking().Select(selectArgs).ToDynamicListAsync();
            }

            return await source.AsNoTracking().ToListAsync();
        }

        public static T ReplaceParameter<T>(T expr, ParameterExpression toReplace, ParameterExpression replacement)
         where T : Expression
        {
            var replacer = new ExpressionReplacer(e => e == toReplace ? replacement : e);
            return (T)replacer.Visit(expr);
        }

        public static Expression<Func<T, TReturn>> Join<T, TReturn>(Func<Expression, Expression, BinaryExpression> joiner,
          IReadOnlyCollection<Expression<Func<T, TReturn>>> expressions)
        {
            if (!expressions.Any())
            {
                throw new ArgumentException("No expressions were provided");
            }
            var firstExpression = expressions.First();
            var otherExpressions = expressions.Skip(1);
            var firstParameter = firstExpression.Parameters.Single();
            var otherExpressionsWithParameterReplaced = otherExpressions.Select(e => ReplaceParameter(e.Body, e.Parameters.Single(), firstParameter));
            var bodies = new[] { firstExpression.Body }.Concat(otherExpressionsWithParameterReplaced);
            var joinedBodies = bodies.Aggregate(joiner);
            return Expression.Lambda<Func<T, TReturn>>(joinedBodies, firstParameter);
        }

        public static void ConcatFilter<T>(List<Expression<Func<T, bool>>> listExpOR, List<Expression<Func<T, bool>>> listExpAND,
         int index, string key, object value, string patternToEvaluate, Type fieldType, Match match = null) where T : class
        {
            string select = "";
            Expression<Func<T, bool>> expToEvaluate = null;

            if (patternToEvaluate == "¬")
            {
                if (fieldType == typeof(string))
                {
                    // expToEvaluate = FilterILike<T>(key, $"%{value}%");
                    expToEvaluate = (b => EF.Functions.ILike(EF.Property<string>(b, key), $"%{value}%"));
                }
                else if (fieldType == typeof(ISERQueryable))
                {
                    select = string.Format("{0}.ToLower().Contains(@{1})", key, 0);
                    expToEvaluate = DynamicExpressionParser.ParseLambda<T, bool>(new ParsingConfig(), true, select, ((string)value).ToLower());

                }
                else if (TypeExtensions.IsNumber(fieldType))
                {
                    select = string.Format("string(object({0})).Contains(@{1})", key, 0);
                    expToEvaluate = DynamicExpressionParser.ParseLambda<T, bool>(new ParsingConfig(), true, select, value);
                }

            }
            else
            {
                if (value is DateTime)
                {
                }
                else
                {
                    select = string.Format("{0} = @{1}", key, 0);
                    expToEvaluate = DynamicExpressionParser.ParseLambda<T, bool>(new ParsingConfig(), true, select, value);
                }
            }

            if (match == null || index == 0) { if (expToEvaluate != null) listExpOR.Add(expToEvaluate); }
            else
            {
                // query filtro por AND o OR  
                if (index > 1)
                    match = match.NextMatch();

                if (match.Success)
                {
                    if (match.Value == "/")
                    {
                        if (expToEvaluate != null) listExpAND.Add(expToEvaluate);
                    }
                    else
                    {
                        if (expToEvaluate != null) listExpOR.Add(expToEvaluate);
                    }
                }
            }

        }

        public static bool ConcatFilter(List<object> values, StringBuilder expresion, string paramName,
             string key, string value, string column, Type typeProperty = null, int? index = null, bool isList = false,
             bool isValid = false)
        {
            var select = "";
            var enable = true;
            var expValided = false;
            var patternStr = @"\=|¬";
            if (typeProperty != null)
            {
                if (typeProperty == typeof(string))
                {
                    expValided = true;
                    values.Add(value.ToLower());
                    select = string.Format(".ToLower().Contains({0})", paramName);
                }
            }
            else
            {
                expValided = true;
                if (int.TryParse(value, out int number))
                {
                    values.Add(number);
                    select = string.Format(" = {0}", paramName);
                }
                else if (bool.TryParse(value, out bool boolean))
                {
                    values.Add(boolean);
                    select = string.Format(" = {0}", paramName);
                }
                else if (float.TryParse(value, out float fnumber))
                {
                    values.Add(fnumber);
                    select = string.Format(" = {0}", paramName);
                }
                else if (double.TryParse(value, out double dnumber))
                {
                    values.Add(dnumber);
                    select = string.Format(" = {0}", paramName);
                }
                else if (decimal.TryParse(value, out decimal denumber))
                {
                    values.Add(denumber);
                    select = string.Format(" = {0}", paramName);
                }
                else if (DateTime.TryParse(value, out DateTime dateTime) == true)
                {
                    values.Add(dateTime.Date);
                    select = string.Format(" = {0}", paramName);
                }
                else
                {
                    if (typeProperty != null && typeProperty != typeof(string))
                    {
                        enable = false;
                    }
                    Match matchStr = Regex.Match(column, patternStr);
                    if (matchStr.Success)
                    {
                        if (matchStr.Value == "=")
                        {
                            values.Add(value);
                            select = string.Format(" = {0}", paramName);
                        }
                        else
                        {
                            values.Add(value.ToLower());
                            select = string.Format(".ToLower().Contains({0})", paramName);
                        }
                    }

                }
            }

            if (enable)
            {
                if (index != null && index > 0 && expresion.Length > 3 && isValid && expValided)
                {
                    if (isList)
                        expresion.Append(")");
                    expresion.Append(" OR ");
                }

                if (expValided)
                {
                    expresion.Append(key);
                    expresion.Append(select);
                }

            }
            return expValided;
        }

        public static async Task<dynamic> SortFilterSelectAsync<E>(this IQueryable source, IHttpContextAccessor _contextAccessor)
              where E : class
        {
            // select args
            if (_contextAccessor.HttpContext.Request.Query.Any(x => x.Key.Equals("select_args")))
            {
                var selectArgs = _contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("select_args")).Value.ToString();
                selectArgs = $"new({selectArgs})";
                source = source.Select(selectArgs);
            }

            return await source.ToDynamicListAsync();
        }

        public static List<T> GetPaged<T, U>(this IQueryable<T> query,
                                          int page, int pageSize) where U : class
        {
            var result = new PagedResult<U>
            {
                current_page = page,
                page_size = pageSize,
                row_count = query.Count()
            };

            var pageCount = (double)result.row_count / pageSize;
            result.page_count = (int)Math.Ceiling(pageCount);

            var skip = (page - 1) * pageSize;
            //var res = query.Skip(skip)
            //                      .Take(pageSize)
            //                      .ProjectTo<U>()
            //                      .ToList();
            return query.ToList();
        }
    }
}
