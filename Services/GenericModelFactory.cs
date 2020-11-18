using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using SER.Utilitties.NetCore.Models;
using SER.Utilitties.NetCore.Managers;
using SER.Utilitties.NetCore.Utilities;
using Newtonsoft.Json;
using SER.AmazonS3;
using SER.AmazonS3.Models;

namespace SER.Utilitties.NetCore.Services
{
    public class GenericModelFactory<T, TContext> : IRepository<T>
      where T : class
      where TContext : DbContext
    {
        private readonly TContext _context;
        private readonly ILogger _logger;
        private readonly IHttpContextAccessor contextAccessor;
        private readonly AuditManager _cRepositoryLog;
        private IConfiguration _config;
        private IMemoryCache _cache;
        public string model;

        public GenericModelFactory(
            TContext context,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration config)
        {
            _context = context;
            _config = config;
            contextAccessor = httpContextAccessor;
            model = typeof(T).Name;
            _cRepositoryLog = httpContextAccessor.HttpContext.RequestServices.GetService<AuditManager>();
            _cache = httpContextAccessor.HttpContext.RequestServices.GetService<IMemoryCache>();
            _logger = httpContextAccessor.HttpContext.RequestServices.GetService<ILogger<GenericModelFactory<T, TContext>>>();
        }

        public int? GetCompanyIdUser()
        {
            if (contextAccessor.HttpContext.User.Identity.IsAuthenticated)
                return int.Parse(contextAccessor.HttpContext.User.Claims.FirstOrDefault(x => x.Type == UtilConstants.CompanyId)?.Value);
            return null;
        }

        public string GetCurrentUser()
        {
            return contextAccessor.HttpContext.User.Claims.FirstOrDefault(x => x.Type == UtilConstants.Subject)?.Value;
        }

        public string GetCurrenUserName()
        {
            return contextAccessor.HttpContext.User.Claims.FirstOrDefault(x => x.Type == UtilConstants.Name)?.Value;

        }

        public List<string> GetRolesUser()
        {
            return contextAccessor.HttpContext.User.Claims.Where(x =>
                x.Type == UtilConstants.Role).Select(x => x.Value).ToList();
        }

        public virtual async Task<dynamic> GetAll()
        {
            return await GetModel
                .SortFilterAsync(contextAccessor, true);
        }

        public virtual async Task<T> Find(Expression<Func<T, bool>> condition)
        {
            /*if (!await Exist(condition))
            {
                return null;
            }*/
            //return await GetModel.FindAsync(condition);
            return await GetObj<T>(condition);
        }

        public virtual async Task<T> Add(T entity)
        {
            var obj = await GetModel.AddAsync(entity);

            try
            {
                await _context.SaveChangesAsync();
                entity = obj.Entity;

                var cacheKeySize = string.Format("_{0}_size", model);
                _cache.Remove(cacheKeySize);

                await _cRepositoryLog.AddLog(_context, new AuditBinding()
                {
                    action = AudiState.CREATE,
                    objeto = this.model
                }, id: GetKey(entity), commit: true);

            }
            catch (DbUpdateException error)
            {
                _logger.LogCritical(0, "Unable to save changes. " +
                    "Try again, and if the problem persists " +
                    "see your system administrator.Error: {0} {1}", error.Message, error.InnerException);
                return null;
            }
            finally
            {
                _context.Entry(entity).State = EntityState.Detached;
            }

            return entity;
        }

        public virtual async Task<T> Update(T entity, Expression<Func<T, bool>> condition)
        {
            if (!await Exist<T>(condition))
            {
                return null;
            }
            //GetModel.Update(entity);

            await _cRepositoryLog.AddLog(_context, new AuditBinding()
            {
                action = AudiState.UPDATE,
                objeto = this.model
            }, id: GetKey(entity));

            _context.Entry(entity).State = EntityState.Modified;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException error)
            {
                _logger.LogCritical(0, $"Unable to save changes. Optimistic concurrency failure, " +
                     $"object has been modified\n {error.Message} {error.InnerException}");
                error.Entries.Single().Reload();
                foreach (var entry in error.Entries)
                {
                    if (entry.Entity is T)
                    {
                        GetModel.Update(entity);
                    }
                }
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException e)
                {
                    _logger.LogCritical(0, "Unable to save changes. " +
                    "Try again, and if the problem persists " +
                    "see your system administrator.Error: {0} {1}", e.Message, e.InnerException);
                    return null;
                }
            }
            finally
            {
                _context.Entry(entity).State = EntityState.Detached;
            }

            return entity;
        }

        public virtual async Task<T> SelfUpdate(Expression<Func<T, bool>> condition, T entity)
        {
            var obj = await GetModel.FirstOrDefaultAsync(condition);
            if (obj != null)
            {
                foreach (var propertyInfo in typeof(T).GetProperties())
                {
                    if (propertyInfo.Name == "id") continue;
                    try
                    {
                        var oldValue = propertyInfo.GetValue(obj);
                        var newValue = propertyInfo.GetValue(entity);
                        var currentpropertyInfo = obj.GetType().GetProperty(propertyInfo.Name);
                        if (currentpropertyInfo != null)
                        {
                            currentpropertyInfo.SetValue(obj, newValue, null);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e.ToString());
                    }
                }

                _context.Entry(obj).State = EntityState.Modified;
                await _context.SaveChangesAsync();
            }
            return obj;
        }


        public virtual async Task<T> Remove(Expression<Func<T, bool>> condition)
        {
            if (!await Exist<T>(condition))
            {
                return null;
            }
            var entity = await GetObj<T>(condition);
            //_db.Entry(entity).State = EntityState.Deleted;
            GetModel.Remove(entity);
            try
            {
                await _context.SaveChangesAsync();

                var cacheKeySize = string.Format("_{0}_size", model);
                _cache.Remove(cacheKeySize);

                await _cRepositoryLog.AddLog(_context, new AuditBinding()
                {
                    action = AudiState.DELETE,
                    objeto = this.model
                }, id: GetKey(entity), commit: true);

            }
            catch (DbUpdateConcurrencyException error)
            {
                _logger.LogCritical(0, $"Unable to save changes. Optimistic concurrency failure, " +
                    $"object has been modified\n {error.Message} {error.InnerException}");
                error.Entries.Single().Reload();
                foreach (var entry in error.Entries)
                {

                    if (entry.Entity is T)
                    {
                        GetModel.Remove(entity);
                    }
                }
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException e)
                {
                    _logger.LogCritical(0, "Unable to save changes. " +
                    "Try again, and if the problem persists " +
                    "see your system administrator.Error: {0} {1}", e.Message, e.InnerException);
                    return null;
                }
            }
            return entity;
        }

        public virtual async Task<T> UploadFileAsync(IFormFile file, Expression<Func<T, bool>> condition, string propertyName, string S3Path)
        {
            var entity = await _context.Set<T>().SingleOrDefaultAsync(condition);
            if (entity == null) return null;

            var service = new AmazonFileService(contextAccessor, _config);
            var propertyInfo = typeof(T).GetProperty(propertyName);
            if (propertyInfo == null) return null;
            var oldValue = (string)propertyInfo.GetValue(entity);

            if (!string.IsNullOrEmpty(oldValue)) await service.DeleteS3File(oldValue);
            AWSS3Response fileInfo = await service.UploadS3File(file, S3Path);
            propertyInfo.SetValue(entity, fileInfo.Key, null);
            _context.Entry(entity).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<T> DeleteFiles(Expression<Func<T, bool>> condition, DocS3Binding model)
        {
            T entity = await Find(condition);
            if (entity == null) return null;
            var service = new AmazonFileService(contextAccessor, _config);
            if (entity is DocS3)
            {
                if (!string.IsNullOrEmpty(((DocS3)(object)entity).photos))
                {
                    var jObjDocs = JObject.Parse(((DocS3)(object)entity).photos);

                    foreach (JProperty jProp in (JToken)(model.docs_json))
                    {
                        var docToDelete = jObjDocs.GetValue(jProp.Name).ToString();
                        if (!string.IsNullOrEmpty(docToDelete))
                        {
                            _logger.LogCritical($"file remove {jProp.Name}");
                            await service.DeleteS3File(docToDelete);
                            jObjDocs.Remove(jProp.Name);
                        }
                    }

                    int count = 1;
                    JObject jObj = new JObject();
                    foreach (var fileInfo in jObjDocs)
                    {
                        jObj.Add($"DOC-{count}", fileInfo.Value);
                        count++;
                    }
                    ((DocS3)(object)entity).photos = jObj.ToString(Formatting.None);
                }
                await Update(entity, condition);
            }
            return entity;
        }

        public async Task Clear()
        {
            GetModel.RemoveRange(GetModel);
            await _context.SaveChangesAsync();
        }

        private DbSet<T> GetModel
        {
            get { return _context.Set<T>(); }
        }

        public virtual DbSet<T> GetDbSet() => GetModel;
        public virtual IQueryable<T> GetQueryable() => GetModel;


        public async Task<bool> Exist<E>(Expression<Func<E, bool>> condition)
            where E : class
        {
            return await _context.Set<E>().AsNoTracking().AnyAsync(condition);
        }

        public bool Exists<E>(E entity)
            where E : class
        {
            return _context.Set<E>().AsNoTracking().Count(x => x == entity) > 0;
        }

        public async Task<E> GetObj<E>(Expression<Func<E, bool>> condition)
            where E : class
        {
            return await _context.Set<E>().SingleOrDefaultAsync(condition);
        }

        public dynamic GetPaged<E>(IQueryable<E> source) where E : class
        {
            bool allowCache = true;
            if (contextAccessor.HttpContext.Request.Query.Any(x => x.Key.Equals("filter_by")))
            {
                var columnStr = contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("filter_by")).Value.ToString();
                string[] columns = columnStr.Split(';');
                if (columns.Count() > 0) allowCache = false;
            }

            if (contextAccessor.HttpContext.Request.Query.Any(x => x.Key.Equals("enable_pagination"))
            && bool.TryParse(contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("enable_pagination")).Value.ToString(),
               out bool enablePagination))
            {
                var pageSizeRequest = contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("page_size")).Value;
                var currentPageRequest = contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("current_page")).Value;
                int pageSize = string.IsNullOrEmpty(pageSizeRequest) ? 20 : int.Parse(pageSizeRequest);
                int pageNumber = string.IsNullOrEmpty(currentPageRequest) ? 1 : int.Parse(currentPageRequest);

                var result = new PagedResult<E>();

                result.current_page = pageNumber;
                result.page_size = pageSize;

                int? rowCount = null;
                if (allowCache)
                    rowCount = CacheGetOrCreate(GetModel);

                result.row_count = rowCount ?? source.CountAsync().Result;

                var pageCount = (double)result.row_count / pageSize;
                result.page_count = (int)Math.Ceiling(pageCount);

                result.results = source.Skip((pageNumber - 1) * pageSize).Take(pageSize).AsNoTracking().ToList();
                return result;
            }
            return source.AsNoTracking().ToList();
        }

        public virtual string GetKey(T entity)
        {
            var keyName = _context.Model.FindEntityType(typeof(T)).FindPrimaryKey()?.Properties
                .Select(x => x.Name).FirstOrDefault(); // .Single();
            return entity.GetType().GetProperty(keyName).GetValue(entity, null).ToString();
        }

        public int CacheGetOrCreate<E>(IQueryable<E> query)
            where E : class
        {
            var cacheKeySize = string.Format("_{0}_size", typeof(T).Name);
            var cacheEntry = _cache.GetOrCreate(cacheKeySize, entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromDays(1);
                entry.Size = 1000;
                return query.Count();
            });
            _logger.LogInformation($"cacheKeySize: {cacheKeySize} cacheEntry {cacheEntry}");
            return cacheEntry;
        }

        #region Just use if AWS S3 is not available for this project
        public async Task<string> SaveFileInServer(string fileName, string path, byte[] bytes)
        {
            var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), path);
            if (!Directory.Exists(webRootPath))
            {
                _logger.LogInformation("That path not exists already.");
                // Try to create the directory.
                DirectoryInfo di = Directory.CreateDirectory(webRootPath);
                _logger.LogInformation("The directory was created successfully at {0}.", Directory.GetCreationTime(webRootPath));

            }
            var filePath = webRootPath + $@"/{fileName}";
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await stream.WriteAsync(bytes, 0, bytes.Length);
            }
            return filePath;
        }

        public async Task<string[]> SaveFile(IFormFile file, string path, string name = "")
        {
            if (file.Length > 0)
            {
                var fileName = file.FileName.Trim();
                var extFile = Path.GetExtension(fileName);
                string fileNameGuid;
                if (string.IsNullOrEmpty(name))
                    fileNameGuid = string.Format("file_{0}{1}", Guid.NewGuid(), extFile.Trim('"'));
                else
                    fileNameGuid = string.Format("file_{0}{1}", name, extFile.Trim('"'));

                var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), path);
                if (!Directory.Exists(webRootPath))
                {
                    _logger.LogInformation("That path not exists already.");
                    // Try to create the directory.
                    DirectoryInfo di = Directory.CreateDirectory(webRootPath);
                    _logger.LogInformation("The directory was created successfully at {0}.", Directory.GetCreationTime(webRootPath));

                }
                var filePath = webRootPath + $@"/{fileNameGuid}";
                _logger.LogCritical(LoggingEvents.GET_ITEM, "Copied the uploaded file {filePath}", filePath);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                return new string[] { fileNameGuid, filePath };
            }
            return new string[] { "", "" };
        }

        //protected string[] SaveImage(HttpRequest Request, string path)
        //{
        //    long size = 0;
        //    var files = Request.Form.Files;
        //    var filePath = "";
        //    string fileNameGuid = "";
        //    foreach (var file in files)
        //    {
        //        string fileName = ContentDispositionHeaderValue
        //                        .Parse(file.ContentDisposition)
        //                        .FileName
        //                        .Trim().Value;
        //        var extFile = Path.GetExtension(fileName);
        //        fileNameGuid = string.Format("image_{0}{1}", Guid.NewGuid(), extFile);

        //        var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), path);
        //        filePath = webRootPath + $@"\{fileNameGuid}";
        //        size += file.Length;
        //        using (FileStream fs = File.Create(filePath))
        //        {
        //            file.CopyTo(fs);
        //            fs.Flush();
        //        }
        //        _logger.LogCritical(LoggingEvents.GET_ITEM, "Copied the uploaded image file {filePath}", filePath);
        //        break;
        //    }
        //    return new string[] { fileNameGuid, filePath };
        //}
        #endregion
    }
}
