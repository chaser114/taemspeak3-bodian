// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TS3AudioBot.Config;
using TS3AudioBot.Dependency;

namespace TS3AudioBot.Web
{
	public sealed class WebServer : IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		private CancellationTokenSource? cancelToken;
		private readonly ConfWeb config;
		private readonly CoreInjector coreInjector;
		private readonly WebAccountService webAccounts;
		private readonly BotManager botManager;
		private readonly WebConsoleService console;
		private readonly ConfRoot rootConfig;
		private Api.WebApi? api;

		public WebServer(ConfWeb config, CoreInjector coreInjector, WebAccountService webAccounts, BotManager botManager, WebConsoleService console, ConfRoot rootConfig)
		{
			this.config = config;
			this.coreInjector = coreInjector;
			this.webAccounts = webAccounts;
			this.botManager = botManager;
			this.console = console;
			this.rootConfig = rootConfig;
		}

		// TODO write server to be reload-able
		public void StartWebServer()
		{
			var startWebServer = false;

			if (config.Api.Enabled || config.Interface.Enabled)
			{
				if (!config.Api.Enabled)
					Log.Warn("The api is required for the webinterface to work properly; The api is now implicitly enabled. Enable the api in the config to remove this warning.");

				if (!coreInjector.TryCreate<Api.WebApi>(out var api))
					throw new Exception("Could not create Api object.");

				this.api = api;
				startWebServer = true;
			}

			if (startWebServer)
			{
				StartWebServerInternal();
			}
		}

		public string? FindWebFolder()
		{
			var webData = config.Interface;
			if (string.IsNullOrEmpty(webData.Path))
			{
				for (int i = 0; i < 5; i++)
				{
					var up = Path.Combine(Enumerable.Repeat("..", i).ToArray());
					var checkDir = Path.Combine(up, "WebInterface");
					if (Directory.Exists(checkDir))
						return Path.GetFullPath(checkDir);
				}

				var asmPath = Path.GetDirectoryName(typeof(Core).Assembly.Location)!;
				var asmWebPath = Path.GetFullPath(Path.Combine(asmPath, "WebInterface"));
				if (Directory.Exists(asmWebPath))
					return asmWebPath;
			}
			else if (Directory.Exists(webData.Path))
			{
				return Path.GetFullPath(webData.Path);
			}

			return null;
		}

		private void StartWebServerInternal()
		{
			cancelToken?.Cancel();
			cancelToken?.Dispose();
			cancelToken = new CancellationTokenSource();

			var host = new WebHostBuilder()
				.SuppressStatusMessages(true)
				.ConfigureLogging((context, logging) =>
				{
					logging.ClearProviders();
				})
				.UseKestrel(kestrel =>
				{
					kestrel.Limits.MaxRequestBodySize = 3_000_000; // 3 MiB should be enough
				})
				.ConfigureServices(services =>
				{
					services.AddCors(options =>
					{
						options.AddPolicy("TS3AB", builder =>
						{
							builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
						});
					});
				})
				.Configure(app =>
				{
					app.UseCors("TS3AB");
					app.Map(new PathString("/console-api"), map => map.Run(HandleConsoleApi));
					app.Use(async (ctx, next) =>
					{
						await next();
						if (ctx.Response.StatusCode == StatusCodes.Status404NotFound && config.Interface.Enabled && !Path.HasExtension(ctx.Request.Path))
						{
							var folder = FindWebFolder();
							if (folder is null) return;
							ctx.Response.StatusCode = StatusCodes.Status200OK;
							ctx.Response.ContentType = "text/html; charset=utf-8";
							await ctx.Response.SendFileAsync(Path.Combine(folder, "index.html"));
						}
					});

					if (api != null) // api enabled
					{
						app.Map(new PathString("/api"), map =>
						{
							map.Run(async ctx =>
							{
								using var _ = NLog.MappedDiagnosticsLogicalContext.SetScoped("BotId", "Api");
								await Log.SwallowAsync(() => api.ProcessApiV1Call(ctx));
							});
						});
					}

					if (config.Interface.Enabled)
					{
						app.UseFileServer();
					}

					var applicationLifetime = app.ApplicationServices.GetRequiredService<IApplicationLifetime>();
					applicationLifetime.ApplicationStopping.Register(OnShutdown);
				});

			if (config.Interface.Enabled)
			{
				var baseDir = FindWebFolder();
				if (baseDir is null)
				{
					Log.Error("Can't find a WebInterface path to host. Try specifying the path to host in the config");
				}
				else
				{
					host.UseWebRoot(baseDir);
				}
			}

			var addrs = config.Hosts.Value;
			if (addrs.Contains("*"))
			{
				host.ConfigureKestrel(kestrel => { kestrel.ListenAnyIP(config.Port.Value); });
			}
			else if (addrs.Count == 1 && addrs[0] == "localhost")
			{
				host.ConfigureKestrel(kestrel => { kestrel.ListenLocalhost(config.Port.Value); });
			}
			else
			{
				host.UseUrls(addrs.Select(uri => new UriBuilder(uri) { Port = config.Port }.Uri.AbsoluteUri).ToArray());
			}

			Log.Info("Starting Webserver on port {0}", config.Port.Value);
			new Func<Task>(async () =>
			{
				try
				{
					await host.Build().RunAsync(cancelToken.Token);
				}
				catch (Exception ex)
				{
					Log.Error(ex, "The webserver could not be started");
					return;
				}
			})();
		}

		private async Task HandleConsoleApi(HttpContext ctx)
		{
			var path = ctx.Request.Path.Value?.Trim('/').ToLowerInvariant();
			ctx.Response.ContentType = "application/json; charset=utf-8";
			if (path == "status") { await WriteJson(ctx, new { initialized = webAccounts.IsInitialized, brandName = webAccounts.BrandName, botConfigured = rootConfig.GetAllBots()?.Length > 0 }); return; }
			if (path == "setup" && ctx.Request.Method == "POST")
			{
				var body = await ReadJson(ctx); var username = body.Value<string>("username") ?? string.Empty; var password = body.Value<string>("password") ?? string.Empty;
				if (!webAccounts.CreateInitialAdmin(username, password, out var error)) { await WriteError(ctx, error, StatusCodes.Status400BadRequest); return; }
				await CreateSession(ctx, username, password); return;
			}
			if (path == "login" && ctx.Request.Method == "POST")
			{
				var body = await ReadJson(ctx); await CreateSession(ctx, body.Value<string>("username") ?? string.Empty, body.Value<string>("password") ?? string.Empty); return;
			}
			var account = webAccounts.GetAccount(ctx.Request.Cookies["ts3ab_session"]);
			if (account is null) { await WriteError(ctx, "请先登录。", StatusCodes.Status401Unauthorized); return; }
			if (path == "me") { await WriteJson(ctx, new { username = account.Username, role = account.Role.ToString().ToLowerInvariant(), brandName = webAccounts.BrandName }); return; }
			if (path == "bots" && account.Role == WebAccountRole.Admin)
			{
				var active = botManager.GetBotInfolist().ToDictionary(x => x.Name ?? string.Empty);
				var bots = (rootConfig.GetAllBots() ?? Array.Empty<ConfBot>()).Select(config =>
				{
					active.TryGetValue(config.Name, out var info);
					return new { id = config.Name, name = config.Connect.Name.Value, address = config.Connect.Address.Value, status = info?.Status.ToString().ToLowerInvariant() ?? "offline" };
				});
				await WriteJson(ctx, new { bots }); return;
			}
			if (path == "bots/delete" && account.Role == WebAccountRole.Admin && ctx.Request.Method == "POST")
			{
				var id = (await ReadJson(ctx)).Value<string>("id") ?? string.Empty;
				var info = botManager.GetBotInfolist().FirstOrDefault(x => x.Name == id);
				if (info?.Id is int botId && botManager.GetBotLock(botId) is Bot bot) await botManager.StopBot(bot);
				var deleted = rootConfig.DeleteBotConfig(id);
				if (!deleted.Ok) { await WriteError(ctx, deleted.Error.Str, StatusCodes.Status400BadRequest); return; }
				rootConfig.ClearBotConfigCache(id);
				await WriteJson(ctx, new { ok = true }); return;
			}
			if (path == "music/state") { await WriteJson(ctx, await console.GetState()); return; }
			if (path == "music/search" && ctx.Request.Method == "POST")
			{ try { await WriteJson(ctx, new { results = await console.Search((await ReadJson(ctx)).Value<string>("query") ?? string.Empty) }); } catch (Exception ex) { await WriteError(ctx, ex.Message, 400); } return; }
			if ((path == "music/play" || path == "music/add") && ctx.Request.Method == "POST")
			{
				try { var resource = (await ReadJson(ctx)).Value<JObject>("resource")?.ToObject<ResourceFactories.AudioResource>(); if (resource is null) throw new ArgumentException("歌曲数据无效。"); if (path == "music/play") await console.Play(account.Username, resource); else await console.Add(account.Username, resource); await WriteJson(ctx, new { ok = true }); }
				catch (Exception ex) { await WriteError(ctx, ex.Message, 422); } return;
			}
			if (path == "music/next" && ctx.Request.Method == "POST") { await ExecuteMusic(ctx, () => console.Next(account.Username)); return; }
			if (path == "music/previous" && ctx.Request.Method == "POST") { await ExecuteMusic(ctx, () => console.Previous(account.Username)); return; }
			if (path == "music/pause" && ctx.Request.Method == "POST") { await ExecuteMusic(ctx, console.TogglePause); return; }
			if (path == "music/clear" && ctx.Request.Method == "POST") { if (account.Role != WebAccountRole.Admin) { await WriteError(ctx, "只有管理员可以清空待播队列。", 403); return; } await ExecuteMusic(ctx, console.Clear); return; }
			if (path == "settings/brand" && ctx.Request.Method == "POST")
			{
				if (account.Role != WebAccountRole.Admin) { await WriteError(ctx, "仅管理员可修改品牌名称。", StatusCodes.Status403Forbidden); return; }
				try { webAccounts.SetBrandName((await ReadJson(ctx)).Value<string>("brandName") ?? string.Empty); await WriteJson(ctx, new { brandName = webAccounts.BrandName }); }
				catch (ArgumentException ex) { await WriteError(ctx, ex.Message, StatusCodes.Status400BadRequest); }
				return;
			}
			if (path == "accounts" && account.Role == WebAccountRole.Admin && ctx.Request.Method == "GET")
			{ await WriteJson(ctx, new { accounts = webAccounts.ListAccounts().Select(x => new { username = x.Username, role = x.Role.ToString().ToLowerInvariant(), enabled = x.Enabled }) }); return; }
			if (path == "accounts" && account.Role == WebAccountRole.Admin && ctx.Request.Method == "POST")
			{ var body = await ReadJson(ctx); var role = string.Equals(body.Value<string>("role"), "admin", StringComparison.OrdinalIgnoreCase) ? WebAccountRole.Admin : WebAccountRole.User; if (!webAccounts.CreateAccount(body.Value<string>("username") ?? string.Empty, body.Value<string>("password") ?? string.Empty, role, out var error)) { await WriteError(ctx, error, 400); return; } await WriteJson(ctx, new { ok = true }); return; }
			if (path == "accounts/enabled" && account.Role == WebAccountRole.Admin && ctx.Request.Method == "POST")
			{ var body = await ReadJson(ctx); if (!webAccounts.SetEnabled(body.Value<string>("username") ?? string.Empty, body.Value<bool?>("enabled") ?? false, out var error)) { await WriteError(ctx, error, 400); return; } await WriteJson(ctx, new { ok = true }); return; }
			if (path == "setup/bot" && ctx.Request.Method == "POST")
			{
				if (account.Role != WebAccountRole.Admin) { await WriteError(ctx, "仅管理员可配置机器人。", StatusCodes.Status403Forbidden); return; }
				var body = await ReadJson(ctx);
				var id = body.Value<string>("id")?.Trim() ?? string.Empty;
				var address = body.Value<string>("address")?.Trim() ?? string.Empty;
				var nickname = body.Value<string>("nickname")?.Trim() ?? "波点音乐";
				var password = body.Value<string>("serverPassword") ?? string.Empty;
				if (string.IsNullOrWhiteSpace(address)) { await WriteError(ctx, "请填写 TeamSpeak 服务器地址。", StatusCodes.Status400BadRequest); return; }
				if (string.IsNullOrWhiteSpace(id)) id = $"web-console-{Guid.NewGuid():N}";
				var running = botManager.GetBotInfolist().FirstOrDefault(x => x.Name == id);
				if (running?.Id is int runningId && botManager.GetBotLock(runningId) is Bot runningBot) await botManager.StopBot(runningBot);
				var existing = rootConfig.GetBotConfig(id);
				var config = existing.Ok ? existing.Value : botManager.CreateNewBot();
				config.Run.Value = true;
				config.Connect.Address.Value = address;
				config.Connect.Name.Value = string.IsNullOrWhiteSpace(nickname) ? "波点音乐" : nickname;
				config.Connect.ServerPassword.Password.Value = password;
				var saved = existing.Ok ? config.SaveWhenExists() : config.SaveNew(id);
				if (!saved.Ok) { await WriteError(ctx, saved.Error.Str, StatusCodes.Status500InternalServerError); return; }
				var started = await botManager.RunBot(config);
				if (!started.Ok) { await WriteJson(ctx, new { configured = true, connected = false, warning = started.Error }); return; }
				await WriteJson(ctx, new { id = started.Value.Id, name = started.Value.Name, server = started.Value.Server });
				return;
			}
			if (path == "logout" && ctx.Request.Method == "POST") { webAccounts.Logout(ctx.Request.Cookies["ts3ab_session"]); ctx.Response.Cookies.Delete("ts3ab_session"); await ctx.Response.WriteAsync("{}"); return; }
			ctx.Response.StatusCode = StatusCodes.Status501NotImplemented;
			await ctx.Response.WriteAsync("{\"error\":\"网页控制台正在初始化，请使用最新构建包。\"}");
		}

		private async Task CreateSession(HttpContext ctx, string username, string password)
		{
			var session = webAccounts.Login(username, password);
			if (session is null) { await WriteError(ctx, "账号或密码错误。", StatusCodes.Status401Unauthorized); return; }
			ctx.Response.Cookies.Append("ts3ab_session", session, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = ctx.Request.IsHttps, MaxAge = TimeSpan.FromDays(14) });
			await WriteJson(ctx, new { username, brandName = webAccounts.BrandName });
		}
		private static async Task<JObject> ReadJson(HttpContext ctx) { using var reader = new StreamReader(ctx.Request.Body); var body = await reader.ReadToEndAsync(); return string.IsNullOrWhiteSpace(body) ? new JObject() : JObject.Parse(body); }
		private static async Task ExecuteMusic(HttpContext ctx, Func<Task> action) { try { await action(); await WriteJson(ctx, new { ok = true }); } catch (Exception ex) { await WriteError(ctx, ex.Message, 422); } }
		private static Task WriteJson(HttpContext ctx, object value) => ctx.Response.WriteAsync(JsonConvert.SerializeObject(value));
		private static async Task WriteError(HttpContext ctx, string error, int status) { ctx.Response.StatusCode = status; await WriteJson(ctx, new { error }); }

		public void OnShutdown()
		{
			Log.Info("WebServer has closed");
		}

		public void Dispose()
		{
			Log.Info("WebServer is closing");

			cancelToken?.Cancel();
			cancelToken?.Dispose();
			cancelToken = null;
		}
	}
}
