using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SER.Utilitties.NetCore.Models;
using SER.Utilitties.NetCore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Services
{
    public class PatchManager<TContext, TRole>
        where TContext : DbContext
        where TRole : class
    {
        private readonly TContext _context;
        private readonly ILogger _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _config;
        private string Namespace;

        public PatchManager(TContext db,
            ILogger<PatchManager<TContext, TRole>> logger,
            IConfiguration config,
            IHttpContextAccessor contextAccessor)
        {
            _context = db;
            _config = config;
            _logger = logger;
            _httpContextAccessor = contextAccessor;
            Namespace = _config["Validation:namespace"];
        }

        public async Task<bool?> ValidateAsync(BaseValidationModel model)
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == typeof(TContext).Assembly.GetName().Name);
            asm = Assembly.Load(Namespace);
            Type type;
            //if (model.Model == "User") type = typeof(ApplicationUser);
            //else if (model.Model == "Role") type = typeof(ApplicationRole);

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
                    return (await CallMethodByReflection(type, nameof(ValidateObj), new object[] { model })) as bool?;
                }
            }
            return true;
        }

        public async Task<object> ReplacePatch(string id, SerPatchModel model, bool isList = false)
        {
            var modelo = isList ? model.Path.Split("/")[^3] : model.Path.Split("/")[^2];
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == typeof(TContext).Assembly.GetName().Name);
            asm = Assembly.Load(Namespace);
            Type type;
            //if (modelo == "User") type = typeof(ApplicationUser);
            //else if (modelo == "Role") type = typeof(TRole);

            type = asm.GetTypes().Where(x => !x.IsAbstract).SingleOrDefault(x => x.Name == modelo);
            var permission = modelo.ToLower();
            //  _logger.LogInformation($"----------------------type {type} model.Model { modelo } permission {permission}--------------");
            if (type != null)
            {
                if (!GetRolesUser().Contains(UtilConstants.SuperUser)
                    && !_context.Set<TRole>()
                        .Include("Claims")
                        .Where("@0.Contains(Name)", GetRolesUser()) // x => GetRolesUser().Contains(x.Name))
                        .SelectMany("Claims")
                        .Any($"ClaimValue == { permission }.update"))
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

        public async Task<bool> ValidateObj<T>(BaseValidationModel model) where T : class
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
                exist = _context.Set<T>().Where("@0(it) and @1(it)", expToEvaluate, expToExcept).Count() > 0;
            }
            else
            {
                exist = await _context.Set<T>().AnyAsync(expToEvaluate);
            }
            return exist;
        }

        public async Task<T> Patch<T>(dynamic id, SerPatchModel model) where T : class
        {
            var field = model.Path.Split("/").Last();
            var obj = _context.Set<T>().Find(id);
            if (obj != null && model.Op == Options.replace)
            {
                JsonElement ele = (JsonElement)model.Value;

                var propertyInfo = typeof(T).GetProperties().SingleOrDefault(x => x.Name == field);
                // Console.WriteLine($"-----------------type {typeof(T)} Value {ele} propertyInfo.Name {propertyInfo.Name} property type {propertyInfo.PropertyType}-----------------------");

                dynamic value = typeof(JsonExtensions)
                   .GetMethod("ElementToObject")
                   .MakeGenericMethod(propertyInfo.PropertyType)
                   .Invoke(null, new object[] { ele });

                try
                {
                    obj.GetType().GetProperty(propertyInfo.Name)?.SetValue(obj, value, null);
                    await _context.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Exception {e}");
                }
            }
            return obj;

        }

        public List<string> GetRolesUser()
        {
            return _httpContextAccessor.HttpContext.User.Claims.Where(x =>
                x.Type == UtilConstants.Role).Select(x => x.Value).ToList();
        }
        #endregion
    }
}
