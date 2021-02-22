using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
using System.IO;

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

        public async Task<string> AddLog(DbContext context, AuditBinding entity, string id = "", bool commit = false)
        {
            context.ChangeTracker.DetectChanges();
            string valuesToChange = null;
            var entities = context.ChangeTracker.Entries()
                .Where(x => x.State == EntityState.Modified
                || x.State == EntityState.Added
                || x.State == EntityState.Deleted && x.Entity != null).ToList();

            var writerOptions = new JsonWriterOptions
            {
                Indented = false
            };
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, writerOptions);
            writer.WriteStartObject();

            if (!string.IsNullOrEmpty(id))
            {
                writer.WritePropertyName("ObjectId");
                writer.WriteStringValue(id);
            }

            if (entity.extras != null)
            {
                foreach (var extra in entity.extras)
                {
                    writer.WritePropertyName(extra.Key);
                    writer.WriteStringValue(extra.Value);
                }
            }

            if ((new int[] { (int)AudiState.CREATE, (int)AudiState.UPDATE, (int)AudiState.DELETE }).ToList().Contains((int)entity.action))
            {
                foreach (var add in entities.Where(p => p.State == EntityState.Added))
                {
                    string entityName = add.Entity.GetType().Name;
                    _logger.LogInformation($"EntityState.Added, entityName {entityName}\n");
                }

                foreach (var change in entities.Where(p => p.State == EntityState.Modified))
                {
                    string entityName = change.Entity.GetType().Name;
                    _logger.LogInformation($"EntityState.Modified, entityName {entityName}\n");
                    //if (!(new string[] { "Claim" }).ToList().Contains(entity.Object))
                    //    entity.Object = entityName;
                    //entity.Action = UPDATE;
                    AuditEntityModified(writer, change, out valuesToChange);
                }

                foreach (var delete in entities.Where(p => p.State == EntityState.Deleted))
                {
                    string entityName = delete.Entity.GetType().Name;
                    _logger.LogInformation($"EntityState.Deleted, entityName {entityName}\n");
                }
            }
            var userId = GetCurrentUser();
            var userName = GetCurrenUserName();
            writer.WriteEndObject();
            writer.Flush();
            var data = Encoding.UTF8.GetString(stream.ToArray());

            var log = new Audit()
            {
                date = DateTime.UtcNow,
                action = entity.action,
                objeto = entity.objeto,
                username = userName,
                role = string.Join(",", GetRolesUser().ToArray()),
                json_browser = InfoBrowser(),
                json_request = GetInfoRequest(),
                data = data,
                user_id = userId
            };

            await context.Set<Audit>().AddAsync(log);

            var json = JsonSerializer.Serialize<object>(
                new
                {
                    CurrentDate = DateTime.UtcNow,
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


        public void AuditEntityModified(Utf8JsonWriter writer, EntityEntry objectStateEntry, out string valueString)
        {
            writer.WritePropertyName("Values");
            writer.WriteStartArray();
            var values = new List<AuditUpdate>();
            foreach (var prop in objectStateEntry.OriginalValues.Properties)
            {
                string originalValue = null;
                if (objectStateEntry.OriginalValues[prop] != null)
                    originalValue = objectStateEntry.OriginalValues[prop].ToString();
                string currentValue = null;
                if (objectStateEntry.CurrentValues[prop] != null)
                    currentValue = objectStateEntry.CurrentValues[prop].ToString();

                if (originalValue != currentValue) //Only create a log if the value changes
                {
                    values.Add(new AuditUpdate
                    {
                        PropertyName = prop.Name,
                        OldValue = originalValue,
                        NewValue = currentValue,
                    });
                    writer.WriteStartObject();
                    writer.WritePropertyName("PropertyName");
                    writer.WriteStringValue(prop.Name);

                    writer.WritePropertyName("OldValue");
                    writer.WriteStringValue(originalValue);

                    writer.WritePropertyName("NewValue");
                    writer.WriteStringValue(currentValue);
                    writer.WriteEndObject();
                }
            }
            writer.WriteEndArray();

            valueString = JsonSerializer.Serialize(values);
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

        public string GetInfoRequest()
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
            return JsonSerializer.Serialize<InfoRequest>(infoRequest);
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

        public static JsonElement ToJsonDocument(string response)
        {
            var documentOptions = new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip
            };
            return JsonDocument.Parse(response, documentOptions).RootElement;
        }
    }

    class AuditUpdate
    {

        public string PropertyName { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
    }
}