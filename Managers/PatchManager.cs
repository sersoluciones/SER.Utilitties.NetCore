﻿using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SER.Models;
using SER.Models.Patch;
using SER.Models.SERAudit;
using SER.Utilitties.NetCore.Configuration;
using SER.Utilitties.NetCore.Models;
using SER.Utilitties.NetCore.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Services
{
    public class PatchManager<TContext, TUser, TRole>
        where TContext : DbContext
        where TUser : class
        where TRole : class
    {
        private readonly TContext _context;
        private readonly ILogger _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _config;
        private readonly AuditManager _cRepositoryLog;
        private readonly IOptionsMonitor<SERRestOptions> _optionsDelegate;
        private string Namespace;

        public PatchManager(TContext db,
            ILogger<PatchManager<TContext, TUser, TRole>> logger,
            IConfiguration config,
            AuditManager cRepositoryLog,
            IOptionsMonitor<SERRestOptions> optionsDelegate,
            IHttpContextAccessor contextAccessor)
        {
            _context = db;
            _config = config;
            _logger = logger;
            _cRepositoryLog = cRepositoryLog;
            _httpContextAccessor = contextAccessor;
            _optionsDelegate = optionsDelegate;
            Namespace = _config["Validation:namespace"];
        }

        public async Task<bool?> ValidateAsync(BaseValidationModel model, bool validateAuthentication = true)
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == typeof(TContext).Assembly.GetName().Name);
            asm = Assembly.Load(Namespace);
            Type type;
            if (model.Model == "User") type = typeof(TUser);
            else if (model.Model == "Role") type = typeof(TRole);
            else
                type = asm.GetTypes().Where(x => !x.IsAbstract).SingleOrDefault(x => x.Name == model.Model);
            //_logger.LogInformation($"----------------------type {type} model.Model {model.Model }--------------");
            if (type != null)
            {
                Type fieldLocalType = null;
                foreach (var (propertyInfo, j) in type.GetProperties().Select((v, j) => (v, j)))
                {
                    if (propertyInfo.Name == model.Field)
                    {
                        fieldLocalType = propertyInfo.PropertyType;
                        if (fieldLocalType.IsGenericType && fieldLocalType.GetGenericTypeDefinition() == typeof(Nullable<>))
                            fieldLocalType = fieldLocalType.GetGenericArguments()[0];
                        break;
                    }
                }
                if (fieldLocalType != null)
                {
                    return (await CallMethodByReflection(type, nameof(ValidateObj), new object[] { model, validateAuthentication })) as bool?;
                }
            }
            return true;
        }

        public static List<UpdatePermission> GetOtherPermissions()
        {
            try
            {
                var jsonString = File.ReadAllText("permissions.graphql.json");
                return JsonSerializer.Deserialize<List<UpdatePermission>>(jsonString);
            }
            catch (Exception) { }

            return new List<UpdatePermission>();
        }



        public IQueryable GetQueryable(Type type) => GetType()
               .GetMethod("GetListHelper")
               .MakeGenericMethod(type)
               .Invoke(this, null) as IQueryable;

        public DbSet<T> GetListHelper<T>() where T : class
        {
            return _context.Set<T>();
        }

        #region Helpers            
        private static Expression FilterAny(Type type, string propertyName, object value)
        {
            var lambdaParam = Expression.Parameter(type);
            var property = Expression.Property(lambdaParam, propertyName);
            var expr = Expression.Call(
                           typeof(DbFunctions),
                           nameof(DbFunctions.Equals),
                           Type.EmptyTypes,
                           Expression.Property(null, typeof(EF), nameof(EF.Functions)),
                           property,
                           Expression.Constant($"{value}"));

            return Expression.Lambda(expr, lambdaParam);
            //return Expression.Lambda<Func<Country, bool>>(expr, lambdaParam);
        }

        public async Task<bool> ValidateObj<T>(BaseValidationModel model, bool validateAuthentication) where T : class
        {
            var expToEvaluate = DynamicExpressionParser.ParseLambda<T, bool>(new ParsingConfig(), true, $"{model.Field}.ToLower() = @0", $"{model.Value.ToLower()}");
            bool exist;
            if (!string.IsNullOrEmpty(model.Id))
            {
                var expToExcept = DynamicExpressionParser.ParseLambda<T, bool>(new ParsingConfig(), true, $"id != @0", model.Id);
                if (int.TryParse(model.Id, out int res))
                {
                    expToExcept = DynamicExpressionParser.ParseLambda<T, bool>(new ParsingConfig(), true, $"id != @0", res);
                }
                var query = _context.Set<T>().Where("@0(it) and @1(it)", expToEvaluate, expToExcept);
                if (_optionsDelegate.CurrentValue.EnableCustomFilter && validateAuthentication)
                    query = FilterQueryByCustomFilter(query, out bool find);
                exist = await query.AnyAsync();
            }
            else
            {
                var query = _context.Set<T>().Where(expToEvaluate);
                if (_optionsDelegate.CurrentValue.EnableCustomFilter && validateAuthentication)
                    query = FilterQueryByCustomFilter(query, out bool find);
                exist = await query.AnyAsync();
            }
            return exist;
        }


        private IQueryable<T> FilterQueryByCustomFilter<T>(IQueryable<T> query, out bool find, Type parentType = null, string columnName = "") where T : class
        {
            string nameField = _optionsDelegate.CurrentValue.NameCustomFilter;
            find = false;
            string companyId = null;
            var types = new Dictionary<string, Type>();
            var typeToEvaluate = typeof(T);
            if (parentType != null) typeToEvaluate = parentType;

            //Console.WriteLine($" *************** nameField {nameField} Name {_httpContextAccessor.HttpContext.User.Identity.Name} IsAuthenticated {_httpContextAccessor.HttpContext.User.Identity.IsAuthenticated}" +
            //    $" GetCompanyIdUser() { GetCompanyIdUser()}");
            foreach (var propertyInfo in typeToEvaluate.GetProperties())
            {
                if (propertyInfo.GetCustomAttributes(true).Any(x => x.GetType() == typeof(NotMappedAttribute))
                   || propertyInfo.GetCustomAttributes(true).Where(x => x.GetType() == typeof(ColumnAttribute)).Any(attr => ((ColumnAttribute)attr).TypeName == "geography"
                       || ((ColumnAttribute)attr).TypeName == "jsonb")
                    || (propertyInfo.PropertyType.IsArray)
                    || (propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(IList<>))
                    || (propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    || (propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>))
                    || (typeof(ICollection).IsAssignableFrom(propertyInfo.PropertyType))
                   )
                {
                    continue;
                }
                var field = propertyInfo.PropertyType;
                if (field.IsGenericType && field.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    field = field.GetGenericArguments()[0];
                }
                var childType = field ?? propertyInfo.GetType();
                var attrName = "";
                foreach (ForeignKeyAttribute attr in propertyInfo.GetCustomAttributes(true).Where(x => x.GetType() == typeof(ForeignKeyAttribute)))
                {
                    attrName = attr.Name;
                }

                if (attrName != "" && typeToEvaluate.GetProperties().SingleOrDefault(x => x.Name == attrName).GetCustomAttributes(true).Any(x => x.GetType() == typeof(RequiredAttribute)))
                    if (typeof(IBaseModel).IsAssignableFrom(childType)
                        || childType == typeof(TUser) || childType == typeof(TRole))
                    {
                        types.Add(propertyInfo.Name, childType);
                    }

                if (propertyInfo.Name.ToSnakeCase() == nameField)
                {
                    find = true;
                    if (_httpContextAccessor.HttpContext.User.Identity.IsAuthenticated && !string.IsNullOrEmpty(_httpContextAccessor.HttpContext.User.Identity.Name))
                        companyId = GetCustomFilterValue();
                    else
                        companyId = _httpContextAccessor.HttpContext.Session?.GetInt32(nameField)?.ToString();

                    if (typeof(TUser) == typeof(T)) query = query.Where($"{columnName}{propertyInfo.Name}  = @0 ", companyId);
                    else query = query.Where($"{columnName}{nameField}  = @0 ", companyId);
                    break;
                }
            }

            if (!find)
            {
                foreach (var dict in types.OrderByDescending(x => x.Key))
                {
                    //_logger.LogWarning($"---------------dict: {dict.Key}");
                    query = FilterQueryByCustomFilter(query, out bool finded, dict.Value, $"{dict.Key}.");
                    if (finded) break;
                }
            }

            return query;
        }

        public string GetCustomFilterValue()
        {
            return _httpContextAccessor.HttpContext.User.Claims.FirstOrDefault(x =>
                x.Type == _optionsDelegate.CurrentValue.NameCustomFilter)?.Value;
        }

        /// <summary>
        /// PATCH
        /// </summary>
        /// <param name="id"></param>
        /// <param name="model"></param>
        /// <param name="isList"></param>
        /// <returns></returns>
        public async Task<object> ReplacePatch(string id, SerPatchModel model, bool isList = false)
        {
            var modelo = isList ? model.Path.Split("/")[^3] : model.Path.Split("/")[^2];
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == typeof(TContext).Assembly.GetName().Name);
            // asm = Assembly.Load(Namespace);
            Type type;
            if (modelo == "User") type = typeof(TUser);
            else if (modelo == "Role") type = typeof(TRole);
            else
                type = asm.GetTypes().Where(x => !x.IsAbstract).SingleOrDefault(x => x.Name == modelo);
            var permission = modelo.ToLower();
            // _logger.LogInformation($"----------------------type {type} model.Model { modelo } permission {permission}--------------");
            if (type != null)
            {
                var otherPermissions = GetOtherPermissions();
                otherPermissions = otherPermissions.Where(x => x.Name == permission).ToList();
                var userPermissions = new List<string> { $"{ permission }.update" };
                userPermissions.AddRange(otherPermissions.SelectMany(x => x.Permissions.Update).ToList());
                //_logger.LogInformation($"----------------------userPermissions {string.Join(", ", userPermissions)}");
                if (!GetRolesUser().Contains(UtilConstants.SuperUser)
                    && !_context.Set<TRole>()
                    .Include("Claims")
                    .Where("@0.Contains(Name)", GetRolesUser()) // x => GetRolesUser().Contains(x.Name))
                    .SelectMany("Claims")
                    .Any($"@0.Contains(ClaimValue)", userPermissions)
                    )
                    return false;
                try
                {
                    if (int.TryParse(id, out int res))
                        return await CallMethodByReflection(type, nameof(Patch), new object[] { res, model });
                    else
                        return await CallMethodByReflection(type, nameof(Patch), new object[] { id, model });
                }
                catch (Exception e)
                {
                    _logger.LogError("-------------------- ERROR: " + e.ToString());
                }
            }
            return null;
        }

        private async Task<object> CallMethodByReflection(Type type, string nameMethod, object[] parameters)
        {
            var generic = GetType().GetMethod(nameMethod).MakeGenericMethod(type);
            var task = (Task)generic.Invoke(this, parameters: parameters);

            await task.ConfigureAwait(false);
            var resultProperty = task.GetType().GetProperty("Result");
            return resultProperty.GetValue(task);
        }

        public async Task<bool> ReplacePatchBatchAsync(List<SerPatchModel> objs)
        {
            foreach (var model in objs)
            {
                if (model.Path.Split("/").Length == 4)
                {
                    var id = model.Path.Split("/")[^2];
                    var res = await ReplacePatch(id, model, isList: true);
                    if (res is bool && !(bool)res) return false;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        public async Task<T> Patch<T>(dynamic id, SerPatchModel model) where T : class
        {
            var field = model.Path.Split("/").Last();
            var obj = _context.Set<T>().Find(id);
            if (obj != null && model.Op == SER.Models.Options.replace)
            {
                var propertyInfo = typeof(T).GetProperties().SingleOrDefault(x => x.Name == field);
                if (propertyInfo == null) return obj;
                //Console.WriteLine($"-----------------type {typeof(T)} Value {model.Value} field {field} propertyInfo.Name {propertyInfo?.Name} property type {propertyInfo?.PropertyType}-----------------------");

                dynamic value = model.Value;
                if (model.Value != null)
                {
                    JsonElement ele = (JsonElement)model.Value;

                    value = typeof(JsonExtensions)
                                      .GetMethod("ElementToObject")
                                      .MakeGenericMethod(propertyInfo.PropertyType)
                                      .Invoke(null, new object[] { ele });
                }

                try
                {
                    Type type = null;
                    var isList = !propertyInfo.PropertyType.IsArray && typeof(ICollection).IsAssignableFrom(propertyInfo.PropertyType); // propertyInfo.PropertyType.Name.Contains("List");
                    if (isList)
                        type = propertyInfo.PropertyType.GetGenericArguments().Count() > 0 ?
                            propertyInfo.PropertyType.GetGenericArguments()[0] : propertyInfo.PropertyType;

                    if (isList && type.BaseType == typeof(object) && value != null)
                        DeleteRelationsM2M(typeof(T), type, id);

                    obj.GetType().GetProperty(propertyInfo.Name)?.SetValue(obj, value, null);

                    if (_optionsDelegate.CurrentValue.EnableAudit)
                    {
                        var modified = await _cRepositoryLog.AddLog(_context, new AuditBinding()
                        {
                            action = AudiState.UPDATE,
                            objeto = typeof(T).Name,
                        }, id: id.ToString());
                        var propInfo = typeof(T).GetProperties().FirstOrDefault(x => x.Name.ToSnakeCase() == "last_movement");
                        if (propInfo != null)
                            obj.GetType().GetProperty(propInfo.Name)?.SetValue(obj, modified, null);

                        obj.GetType().GetProperty("update_date")?.SetValue(obj, DateTime.UtcNow, null);
                        obj.GetType().GetProperty("UpdateDate")?.SetValue(obj, DateTime.UtcNow, null);
                        obj.GetType().GetProperty("updated_by_id")?.SetValue(obj, GetCurrentUser(), null);
                    }

                    await _context.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    _logger.LogError($"Exception {e}");
                }
            }
            return obj;

        }

        private void DeleteRelationsM2M(Type baseType, Type type, int parentId)
        {
            try
            {
                GetType()
                    .GetMethod("DeleteRelations")
                    .MakeGenericMethod(type)
                    .Invoke(this, parameters: new object[] { baseType, parentId });
            }
            catch (Exception e)
            {
                _logger.LogError($"ERROR {e.ToString()} type {nameof(type)} parentId {parentId}");
            }
        }

        public void DeleteRelations<M>(Type baseType, int parentId) where M : class
        {
            var iQueryable = _context.Set<M>();
            var keyProperty = typeof(M).GetProperties();
            var paramFK = "";
            Type valueType = null;

            foreach (var prop in typeof(M).GetProperties())
            {
                var field = prop.PropertyType;
                if (field.IsGenericType && field.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    field = field.GetGenericArguments()[0];
                }

                if (field == baseType)
                {
                    paramFK = prop.GetCustomAttributes(true)
                        .Where(x => x.GetType() == typeof(ForeignKeyAttribute))
                        .Select(attr => ((ForeignKeyAttribute)attr).Name)
                        .FirstOrDefault();
                    valueType = field;
                    break;
                }
            }
            if (!string.IsNullOrEmpty(paramFK))
            {
                valueType = typeof(M).GetProperty(paramFK).PropertyType;
                var expToEvaluate = EqualPredicate<M>(typeof(M), paramFK, parentId, valueType);
                iQueryable.RemoveRange(iQueryable.Where(expToEvaluate));
                // _context.SaveChanges();
            }
        }

        private Expression<Func<M, bool>> EqualPredicate<M>(Type type, string propertyName, object value, Type valueType) where M : class
        {
            var parameter = Expression.Parameter(type, "x");
            // x => x.id == value
            //     |___|
            var property = Expression.Property(parameter, propertyName);

            // x => x.id == value
            //             |__|
            var numberValue = Expression.Convert(Expression.Constant(value), valueType); // Expression.Constant(value);

            // x => x.id == value
            //|________________|
            var exp = Expression.Equal(property, numberValue);
            return Expression.Lambda<Func<M, bool>>(exp, parameter);
        }


        public List<string> GetRolesUser()
        {
            return _httpContextAccessor.HttpContext.User.Claims.Where(x =>
                x.Type == UtilConstants.Role).Select(x => x.Value).ToList();
        }

        public string GetCurrentUser()
        {
            return _httpContextAccessor.HttpContext.User.Claims.FirstOrDefault(x => x.Type == UtilConstants.Subject)?.Value;
        }

        public string GetCurrenUserName()
        {
            return _httpContextAccessor.HttpContext.User.Claims.FirstOrDefault(x => x.Type == UtilConstants.Name)?.Value;
        }
        #endregion
    }
}
