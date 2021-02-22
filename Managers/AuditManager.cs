using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SER.Utilitties.NetCore.Hubs;
using SER.Utilitties.NetCore.Models;
using SER.Utilitties.NetCore.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Managers
{
    public enum AudiState
    {
        READ = 0,
        CREATE = 1,
        UPDATE = 2,
        DELETE = 3,
        EXECUTE = 4,
        LOGIN = 5,
        LOGOUT = 6
    }
    public class AuditManager
    {
        private readonly ILogger _logger;
        private readonly IHubContext<StateHub> _hub;
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly IMemoryCache _cache;

        public AuditManager(
            ILogger<AuditManager> logger,
            IHttpContextAccessor contextAccessor,
            IMemoryCache memoryCache,
            IHubContext<StateHub> hub)
        {
            _logger = logger;
            _contextAccessor = contextAccessor;
            _cache = memoryCache;
            _hub = hub;
        }

        public async Task<JArray> AddLog(DbContext context, AuditBinding entity, string id = "", bool commit = false)
        {
            context.ChangeTracker.DetectChanges();
            JArray valuesToChange = null;
            var entities = context.ChangeTracker.Entries()
                .Where(x => x.State == EntityState.Modified
                || x.State == EntityState.Added
                || x.State == EntityState.Deleted && x.Entity != null).ToList();


            if (!string.IsNullOrEmpty(id))
            {
                entity.json_observations.Add("ObjectId", id);
            }
            if ((new int[] { (int)AudiState.CREATE, (int)AudiState.UPDATE, (int)AudiState.DELETE }).ToList().Contains((int)entity.action))
            {
                foreach (var add in entities.Where(p => p.State == EntityState.Added))
                {
                    string entityName = add.Entity.GetType().Name;
                    _logger.LogWarning($"EntityState.Added, entityName {entityName}\n");
                }

                foreach (var change in entities.Where(p => p.State == EntityState.Modified))
                {
                    string entityName = change.Entity.GetType().Name;
                    _logger.LogWarning($"EntityState.Modified, entityName {entityName}\n");
                    //if (!(new string[] { "Claim" }).ToList().Contains(entity.Object))
                    //    entity.Object = entityName;
                    //entity.Action = UPDATE;
                    valuesToChange = AuditEntityModified(change);
                    entity.json_observations.Add("Values", valuesToChange);
                }

                foreach (var delete in entities.Where(p => p.State == EntityState.Deleted))
                {
                    string entityName = delete.Entity.GetType().Name;
                    _logger.LogWarning($"EntityState.Deleted, entityName {entityName}\n");
                }
            }
            var userId = GetCurrentUser();
            var userName = GetCurrenUserName();

            Audit log = new Audit()
            {
                current_date = DateTime.Now,
                action = (byte)entity.action,
                objeto = entity.objeto,
                username = userName,
                role = string.Join(",", GetRolesUser().ToArray()),
                json_browser = InfoBrowser(),
                json_request = infoRequest(),
                json_observation = entity.json_observations.ToString(),
                user_id = userId
            };

            await context.Set<Audit>().AddAsync(log);

            var json = JsonSerializer.Serialize<object>(
                new
                {
                    CurrentDate = DateTime.Now,
                    entity.action,
                    entity.objeto,
                },
                new JsonSerializerOptions { WriteIndented = true, });

            await SendMsgSignalR(json);
            if (commit) await context.SaveChangesAsync();

            return valuesToChange;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task SendMsgSignalR(string msg)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            //if (GetCurrenUserName() != null)
            //    await _hub.Clients.User(GetCurrenUserName())
            //        .SendAsync("ReceiveMessage", msg);
        }


        public JArray AuditEntityModified(EntityEntry objectStateEntry)
        {
            JArray jArrayProp = new JArray();
            foreach (var prop in objectStateEntry.OriginalValues.Properties)
            {
                string originalValue = null;
                if (objectStateEntry.OriginalValues[prop] != null)
                    originalValue = objectStateEntry.OriginalValues[prop].ToString();
                string currentValue = null;
                if (objectStateEntry.CurrentValues[prop] != null)
                    currentValue = objectStateEntry.CurrentValues[prop].ToString();
                JObject jObjectProp = new JObject();
                if (originalValue != currentValue) //Only create a log if the value changes
                {
                    jObjectProp.Add("PropertyName", prop.Name);
                    jObjectProp.Add("OldValue", originalValue);
                    jObjectProp.Add("NewValue", currentValue);
                    jArrayProp.Add(jObjectProp);
                }
            }
            _logger.LogWarning($"json values: {jArrayProp.ToString()}");
            return jArrayProp;
        }


        public string InfoBrowser()
        {
            UserAgent ua = new UserAgent();
            try
            {
                string userAgent = _contextAccessor.HttpContext.Request.Headers["User-Agent"];
                _logger.LogInformation(3, $"userAgent: {userAgent}");
                ua = new UserAgent(_contextAccessor.HttpContext.Request.Headers["User-Agent"]);
            }
            catch (Exception) { }
            //string.Join(",", dogs.ToArray());
            return JsonSerializer.Serialize(ua);
        }

        public string infoRequest()
        {
            string refer = _contextAccessor.HttpContext.Request.Headers["Referer"];
            var infoRequest = new InfoRequest
            {
                verb = string.Format("{0}", _contextAccessor.HttpContext.Request.Method),
                content_type = string.Format("{0}", _contextAccessor.HttpContext.Request.ContentType),
                encoded_url = string.Format("{0}", _contextAccessor.HttpContext.Request.GetEncodedUrl()),
                path = string.Format("{0}", _contextAccessor.HttpContext.Request.Path),
                remote_ip_address = string.Format("{0}", _contextAccessor.HttpContext.Connection.RemoteIpAddress),
                host = string.Format("{0}", _contextAccessor.HttpContext.Request.Host),
                refferer_url = string.Format("{0}", (string.IsNullOrEmpty(refer)) ? "" : refer)
            };
            return JsonSerializer.Serialize<InfoRequest>(infoRequest,
                new JsonSerializerOptions { WriteIndented = true, });
        }

        class InfoRequest
        {
            public string verb { get; set; }
            public string content_type { get; set; }
            public string encoded_url { get; set; }
            public string path { get; set; }
            public string remote_ip_address { get; set; }
            public string host { get; set; }
            public string refferer_url { get; set; }
        }

        public string GetCurrentUser()
        {
            return _contextAccessor.HttpContext.User.Claims.FirstOrDefault(x => x.Type == UtilConstants.Subject)?.Value;
        }

        public string GetCurrenUserName()
        {
            return _contextAccessor.HttpContext.User.Claims.FirstOrDefault(x => x.Type == UtilConstants.Name)?.Value;
        }

        public List<string> GetRolesUser()
        {
            return _contextAccessor.HttpContext.User.Claims.Where(x =>
                x.Type == UtilConstants.Role).Select(x => x.Value).ToList();
        }
    }
}