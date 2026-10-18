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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TS3AudioBot.Config;
using TS3AudioBot.Dependency;
using TS3AudioBot.ResourceFactories;

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
		private readonly WebUpdateService webUpdate;
		private readonly WebProcessService webProcess;
		private Api.WebApi? api;

		public WebServer(ConfWeb config, CoreInjector coreInjector, WebAccountService webAccounts, BotManager botManager, WebConsoleService console, ConfRoot rootConfig, WebUpdateService webUpdate, WebProcessService webProcess)
		{
			this.config = config;
			this.coreInjector = coreInjector;
			this.webAccounts = webAccounts;
			this.botManager = botManager;
			this.console = console;
			this.rootConfig = rootConfig;
			this.webUpdate = webUpdate;
			this.webProcess = webProcess;
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
				// A published bot owns the WebInterface beside its assembly. Prefer it
				// over a stale directory inherited from the process working directory.
				var asmPath = Path.GetDirectoryName(typeof(Core).Assembly.Location)!;
				var asmWebPath = Path.GetFullPath(Path.Combine(asmPath, "WebInterface"));
				if (HasWebEntryPoint(asmWebPath))
					return asmWebPath;

				for (int i = 0; i < 5; i++)
				{
					var up = Path.Combine(Enumerable.Repeat("..", i).ToArray());
					var checkDir = Path.Combine(up, "WebInterface");
					if (HasWebEntryPoint(checkDir))
						return Path.GetFullPath(checkDir);
				}
			}
			else if (HasWebEntryPoint(webData.Path))
			{
				return Path.GetFullPath(webData.Path);
			}

			return null;
		}

		private static bool HasWebEntryPoint(string path)
		{
			return Directory.Exists(path) && File.Exists(Path.Combine(path, "index.html"));
		}

		private static bool IsNoCacheWebRequest(PathString path)
		{
			var value = path.Value;
			return string.IsNullOrEmpty(value)
				|| value == "/"
				|| value.Equals("/index.html", StringComparison.OrdinalIgnoreCase)
				|| value.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
				|| value.EndsWith(".css", StringComparison.OrdinalIgnoreCase);
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
				.ConfigureServices(_ => { })
			.Configure(app =>
			{
				if (config.Interface.Enabled)
				{
					app.Use(async (ctx, next) =>
					{
						if (IsNoCacheWebRequest(ctx.Request.Path))
							ctx.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
						await next();
					});
				}

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
					Log.Info("Hosting WebInterface from {0}", baseDir);
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
			try
			{
				await HandleConsoleApiCore(ctx);
			}
			catch (JsonReaderException ex)
			{
				Log.Debug(ex, "Malformed console API JSON request.");
				await WriteError(ctx, "请求数据不是有效的 JSON。", StatusCodes.Status400BadRequest);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Unhandled console API error for {0} {1}", ctx.Request.Method, ctx.Request.Path);
				if (!ctx.Response.HasStarted)
					await WriteError(ctx, "服务器处理请求时发生错误。", StatusCodes.Status500InternalServerError);
			}
		}

		private async Task HandleConsoleApiCore(HttpContext ctx)
		{
			var path = ctx.Request.Path.Value?.Trim('/').ToLowerInvariant();
			ctx.Response.ContentType = "application/json; charset=utf-8";
			if (path == "status") { await WriteJson(ctx, new { initialized = webAccounts.IsInitialized, brandName = webAccounts.BrandName, botConfigured = rootConfig.GetAllBots()?.Length > 0, guestEnabled = webAccounts.IsGuestEnabled && webAccounts.GuestBots.Count > 0 }); return; }
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
			// Guest mode: an admin may expose a fixed bot subset without login.
			if (path == "guest/login" && ctx.Request.Method == "POST")
			{
				if (!webAccounts.IsGuestEnabled || webAccounts.GuestBots.Count == 0)
				{ await WriteError(ctx, "访客模式未开启。", StatusCodes.Status403Forbidden); return; }
				var guestSession = webAccounts.CreateGuestSession();
				ctx.Response.Cookies.Append("ts3ab_guest", guestSession, GuestCookieOptions(ctx));
				await WriteJson(ctx, new { username = string.Empty, role = "guest", brandName = webAccounts.BrandName }); return;
			}
			var account = webAccounts.GetAccount(ctx.Request.Cookies["ts3ab_session"]);
			WebPrincipal principal;
			if (account != null)
			{
				principal = WebPrincipal.ForAccount(account);
			}
			else if (webAccounts.IsGuestSession(ctx.Request.Cookies["ts3ab_guest"]) && webAccounts.IsGuestEnabled && webAccounts.GuestBots.Count > 0)
			{
				principal = WebPrincipal.Guest(webAccounts.GuestBots);
			}
			else
			{
				await WriteError(ctx, "请先登录。", StatusCodes.Status401Unauthorized); return;
			}
			if (path == "me") { await WriteJson(ctx, new { username = principal.Username, role = principal.IsGuest ? "guest" : principal.Role.ToString().ToLowerInvariant(), brandName = webAccounts.BrandName, allowedBots = principal.IsAdmin ? Array.Empty<string>() : principal.AllowedBots }); return; }
			if (path == "update/status")
			{
				if (!principal.IsAdmin) { await WriteError(ctx, "仅管理员可查看更新状态。", StatusCodes.Status403Forbidden); return; }
				await WriteJson(ctx, webUpdate.GetStatus()); return;
			}
			if (path == "update/check" && ctx.Request.Method == "POST")
			{
				if (!principal.IsAdmin) { await WriteError(ctx, "仅管理员可检查更新。", StatusCodes.Status403Forbidden); return; }
				try
				{
					var body = await ReadJson(ctx);
					await WriteJson(ctx, await webUpdate.CheckAsync(body.Value<string>("source")));
				}
				catch (Exception ex)
				{
					Log.Warn(ex, "Console update check failed.");
					await WriteError(ctx, "检查更新失败：" + ex.Message, StatusCodes.Status422UnprocessableEntity);
				}
				return;
			}
			if (path == "update/apply" && ctx.Request.Method == "POST")
			{
				if (!principal.IsAdmin) { await WriteError(ctx, "仅管理员可执行更新。", StatusCodes.Status403Forbidden); return; }
				try
				{
					var body = await ReadJson(ctx);
					var password = body.Value<string>("password") ?? string.Empty;
					if (!webAccounts.VerifyCredentials(principal.Username, password))
					{
						await WriteError(ctx, "管理员密码不正确。", StatusCodes.Status401Unauthorized);
						return;
					}
					var source = body.Value<string>("source") ?? "bodian";
					await WriteJson(ctx, await webUpdate.ApplyAsync(source, "ok"));
				}
				catch (Exception ex)
				{
					Log.Warn(ex, "Console update apply failed.");
					await WriteError(ctx, "更新失败：" + ex.Message, StatusCodes.Status422UnprocessableEntity);
				}
				return;
			}
			if (path == "service/status")
			{
				if (!principal.IsAdmin) { await WriteError(ctx, "仅管理员可查看服务状态。", StatusCodes.Status403Forbidden); return; }
				await WriteJson(ctx, webProcess.GetStatus()); return;
			}
			if (path == "service/logs")
			{
				if (!principal.IsAdmin) { await WriteError(ctx, "仅管理员可查看日志。", StatusCodes.Status403Forbidden); return; }
				var linesText = ctx.Request.Query["lines"].ToString();
				var lines = int.TryParse(linesText, out var n) ? n : 200;
				await WriteJson(ctx, webProcess.GetLogs(lines, ctx.Request.Query["source"].ToString())); return;
			}
			if (path == "service/stop" && ctx.Request.Method == "POST")
			{
				if (!principal.IsAdmin) { await WriteError(ctx, "仅管理员可停止服务。", StatusCodes.Status403Forbidden); return; }
				try
				{
					var body = await ReadJson(ctx);
					var password = body.Value<string>("password") ?? string.Empty;
					if (!webAccounts.VerifyCredentials(principal.Username, password))
					{
						await WriteError(ctx, "管理员密码不正确。", StatusCodes.Status401Unauthorized);
						return;
					}
					await WriteJson(ctx, webProcess.ScheduleStop());
				}
				catch (Exception ex)
				{
					Log.Warn(ex, "Console service stop failed.");
					await WriteError(ctx, "停止失败：" + ex.Message, StatusCodes.Status422UnprocessableEntity);
				}
				return;
			}
			if (path == "service/restart" && ctx.Request.Method == "POST")
			{
				if (!principal.IsAdmin) { await WriteError(ctx, "仅管理员可重启服务。", StatusCodes.Status403Forbidden); return; }
				try
				{
					var body = await ReadJson(ctx);
					var password = body.Value<string>("password") ?? string.Empty;
					if (!webAccounts.VerifyCredentials(principal.Username, password))
					{
						await WriteError(ctx, "管理员密码不正确。", StatusCodes.Status401Unauthorized);
						return;
					}
					await WriteJson(ctx, webProcess.ScheduleRestart());
				}
				catch (Exception ex)
				{
					Log.Warn(ex, "Console service restart failed.");
					await WriteError(ctx, "重启失败：" + ex.Message, StatusCodes.Status422UnprocessableEntity);
				}
				return;
			}
			if (path == "bots")
			{
				var active = botManager.GetBotInfolist()
					.Where(x => !string.IsNullOrWhiteSpace(x.Name))
					.GroupBy(x => x.Name!, StringComparer.Ordinal)
					.ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
				var bots = (rootConfig.GetAllBots() ?? Array.Empty<ConfBot>())
					.Where(config => principal.CanControlBot(config.Name))
					.Select(config =>
				{
					active.TryGetValue(config.Name ?? string.Empty, out var info);
					return new { id = config.Name, name = config.Connect.Name.Value, address = principal.IsAdmin ? config.Connect.Address.Value : null, status = info?.Status.ToString().ToLowerInvariant() ?? "offline" };
				});
				await WriteJson(ctx, new { bots }); return;
			}
			if (path == "bots/description-permission")
			{
				if (!principal.IsAdmin) { await WriteError(ctx, "仅管理员可查看该提示。", StatusCodes.Status403Forbidden); return; }
				try
				{
					var force = string.Equals(ctx.Request.Query["force"].ToString(), "1", StringComparison.Ordinal)
						|| string.Equals(ctx.Request.Query["force"].ToString(), "true", StringComparison.OrdinalIgnoreCase);
					await WriteJson(ctx, await console.GetDescriptionPermissionStatus(force));
				}
				catch (Exception ex)
				{
					Log.Warn(ex, "Description permission check failed.");
					await WriteError(ctx, "检查简介权限失败。", StatusCodes.Status422UnprocessableEntity);
				}
				return;
			}
			if (path == "bots/description-permission/dismiss" && ctx.Request.Method == "POST")
			{
				if (!principal.IsAdmin) { await WriteError(ctx, "仅管理员可操作。", StatusCodes.Status403Forbidden); return; }
				await WriteJson(ctx, console.DismissDescriptionPermissionNotice()); return;
			}
			if (path == "bots/description-permission/reset" && ctx.Request.Method == "POST")
			{
				if (!principal.IsAdmin) { await WriteError(ctx, "仅管理员可操作。", StatusCodes.Status403Forbidden); return; }
				await WriteJson(ctx, console.ResetDescriptionPermissionNotice()); return;
			}
			if (path == "bots/delete" && principal.IsAdmin && ctx.Request.Method == "POST")
			{
				var id = (await ReadJson(ctx)).Value<string>("id") ?? string.Empty;
				var info = botManager.GetBotInfolist().FirstOrDefault(x => x.Name == id);
				if (info?.Id is int botId && botManager.GetBotLock(botId) is Bot bot) await botManager.StopBot(bot);
				var deleted = rootConfig.DeleteBotConfig(id);
				if (!deleted.Ok) { await WriteError(ctx, deleted.Error.Str, StatusCodes.Status400BadRequest); return; }
				rootConfig.ClearBotConfigCache(id);
				await WriteJson(ctx, new { ok = true }); return;
			}
			if (path == "music/state") { var stateBot = principal.ResolveBotId(ctx.Request.Query["botId"].ToString()); if (!await EnsureBotAccess(ctx, principal, stateBot)) return; await WriteJson(ctx, await console.GetState(stateBot)); return; }
			if (path == "music/lyrics")
			{
				var lyricsBot = principal.ResolveBotId(ctx.Request.Query["botId"].ToString()); if (!await EnsureBotAccess(ctx, principal, lyricsBot)) return; try { await WriteJson(ctx, await console.GetLyrics(lyricsBot)); }
				catch (Exception ex) { Log.Warn(ex, "Console lyrics request failed."); await WriteError(ctx, "获取歌词失败。", StatusCodes.Status422UnprocessableEntity); }
				return;
			}
			if (path == "music/search" && ctx.Request.Method == "POST")
			{ try { var body = await ReadJson(ctx); var searchBot = principal.ResolveBotId(body.Value<string>("botId")); if (!await EnsureBotAccess(ctx, principal, searchBot)) return; await WriteJson(ctx, new { results = await console.Search(body.Value<string>("query") ?? string.Empty, searchBot) }); } catch (JsonReaderException) { throw; } catch (ArgumentException ex) { await WriteError(ctx, ex.Message, StatusCodes.Status400BadRequest); } catch (Exception ex) { Log.Warn(ex, "Console music search failed."); await WriteError(ctx, "搜索失败，请稍后重试。", StatusCodes.Status422UnprocessableEntity); } return; }
			if ((path == "music/play" || path == "music/add") && ctx.Request.Method == "POST")
			{
				try { var body = await ReadJson(ctx); var playBot = principal.ResolveBotId(body.Value<string>("botId")); if (!await EnsureBotAccess(ctx, principal, playBot)) return; var resource = ParseKuwoResource(body.Value<JObject>("resource")); if (path == "music/play") await console.Play(principal.Username, resource, playBot); else await console.Add(principal.Username, resource, playBot); await WriteJson(ctx, new { ok = true }); }
				catch (JsonReaderException) { throw; }
				catch (ArgumentException ex) { await WriteError(ctx, ex.Message, StatusCodes.Status400BadRequest); }
				catch (Exception ex) { Log.Warn(ex, "Console music playback request failed."); await WriteError(ctx, "播放失败，请稍后重试。", StatusCodes.Status422UnprocessableEntity); } return;
			}
			if (path == "music/next" && ctx.Request.Method == "POST") { var body = await ReadJson(ctx); var nextBot = principal.ResolveBotId(body.Value<string>("botId")); if (!await EnsureBotAccess(ctx, principal, nextBot)) return; await ExecuteMusic(ctx, () => console.Next(principal.Username, nextBot)); return; }
			if (path == "music/previous" && ctx.Request.Method == "POST") { var body = await ReadJson(ctx); var prevBot = principal.ResolveBotId(body.Value<string>("botId")); if (!await EnsureBotAccess(ctx, principal, prevBot)) return; await ExecuteMusic(ctx, () => console.Previous(principal.Username, prevBot)); return; }
			if (path == "music/pause" && ctx.Request.Method == "POST") { var body = await ReadJson(ctx); var pauseBot = principal.ResolveBotId(body.Value<string>("botId")); if (!await EnsureBotAccess(ctx, principal, pauseBot)) return; await ExecuteMusic(ctx, () => console.TogglePause(pauseBot)); return; }
			if (path == "music/seek" && ctx.Request.Method == "POST")
			{
				try
				{
					var body = await ReadJson(ctx);
					var position = body.Value<double?>("position") ?? body.Value<double?>("seconds");
					if (position is null) { await WriteError(ctx, "请提供播放位置。", StatusCodes.Status400BadRequest); return; }
					var seekBot = principal.ResolveBotId(body.Value<string>("botId")); if (!await EnsureBotAccess(ctx, principal, seekBot)) return;
					await ExecuteMusic(ctx, () => console.Seek(position.Value, seekBot));
				}
				catch (ArgumentException ex) { await WriteError(ctx, ex.Message, StatusCodes.Status400BadRequest); }
				catch (Exception ex) { Log.Warn(ex, "Console seek failed."); await WriteError(ctx, "调整播放进度失败。", StatusCodes.Status422UnprocessableEntity); }
				return;
			}
			if (path == "music/clear" && ctx.Request.Method == "POST") { if (!principal.IsAdmin) { await WriteError(ctx, "只有管理员可以清空待播队列。", 403); return; } var body = await ReadJson(ctx); var clearBot = principal.ResolveBotId(body.Value<string>("botId")); if (!await EnsureBotAccess(ctx, principal, clearBot)) return; await ExecuteMusic(ctx, () => console.Clear(clearBot)); return; }
			if (path == "music/volume" && ctx.Request.Method == "POST")
			{
				try
				{
					var body = await ReadJson(ctx);
					var volume = body.Value<float?>("volume") ?? body.Value<float?>("value");
					if (volume is null) { await WriteError(ctx, "请提供音量 0-100。", StatusCodes.Status400BadRequest); return; }
					var volumeBot = principal.ResolveBotId(body.Value<string>("botId")); if (!await EnsureBotAccess(ctx, principal, volumeBot)) return;
					await ExecuteMusic(ctx, () => console.SetVolume(volume.Value, volumeBot));
				}
				catch (ArgumentException ex) { await WriteError(ctx, ex.Message, StatusCodes.Status400BadRequest); }
				catch (Exception ex) { Log.Warn(ex, "Console volume set failed."); await WriteError(ctx, "设置音量失败。", StatusCodes.Status422UnprocessableEntity); }
				return;
			}
			if (path == "music/loop" && ctx.Request.Method == "POST")
			{
				try
				{
					var body = await ReadJson(ctx);
					var loopBot = principal.ResolveBotId(body.Value<string>("botId")); if (!await EnsureBotAccess(ctx, principal, loopBot)) return;
					await ExecuteMusic(ctx, () => console.SetLoop(body.Value<string>("mode") ?? "off", loopBot));
				}
				catch (ArgumentException ex) { await WriteError(ctx, ex.Message, StatusCodes.Status400BadRequest); }
				catch (Exception ex) { Log.Warn(ex, "Console loop set failed."); await WriteError(ctx, "设置循环模式失败。", StatusCodes.Status422UnprocessableEntity); }
				return;
			}
			if (path == "music/random" && ctx.Request.Method == "POST")
			{
				try
				{
					var body = await ReadJson(ctx);
					var enabled = body.Value<bool?>("enabled") ?? body.Value<bool?>("value") ?? false;
					var randomBot = principal.ResolveBotId(body.Value<string>("botId")); if (!await EnsureBotAccess(ctx, principal, randomBot)) return;
					await ExecuteMusic(ctx, () => console.SetRandom(enabled, randomBot));
				}
				catch (Exception ex) { Log.Warn(ex, "Console random set failed."); await WriteError(ctx, "设置随机播放失败。", StatusCodes.Status422UnprocessableEntity); }
				return;
			}
			if (path == "settings/brand" && ctx.Request.Method == "POST")
			{
				if (!principal.IsAdmin) { await WriteError(ctx, "仅管理员可修改品牌名称。", StatusCodes.Status403Forbidden); return; }
				try { webAccounts.SetBrandName((await ReadJson(ctx)).Value<string>("brandName") ?? string.Empty); await WriteJson(ctx, new { brandName = webAccounts.BrandName }); }
				catch (ArgumentException ex) { await WriteError(ctx, ex.Message, StatusCodes.Status400BadRequest); }
				return;
			}
			if (path == "accounts" && principal.IsAdmin && ctx.Request.Method == "GET")
			{ await WriteJson(ctx, new { accounts = webAccounts.ListAccounts().Select(x => new { username = x.Username, role = x.Role.ToString().ToLowerInvariant(), enabled = x.Enabled, allowedBots = x.AllowedBots ?? new List<string>() }) }); return; }
			if (path == "accounts" && principal.IsAdmin && ctx.Request.Method == "POST")
			{
				try
				{
					var body = await ReadJson(ctx);
					var role = string.Equals(body.Value<string>("role"), "admin", StringComparison.OrdinalIgnoreCase) ? WebAccountRole.Admin : WebAccountRole.User;
					if (!webAccounts.CreateAccount(body.Value<string>("username") ?? string.Empty, body.Value<string>("password") ?? string.Empty, role, out var error))
					{ await WriteError(ctx, error, StatusCodes.Status400BadRequest); return; }
					await WriteJson(ctx, new { ok = true });
				}
				catch (JsonReaderException) { throw; }
				catch (Exception ex) { Log.Warn(ex, "Create account failed."); await WriteError(ctx, "创建账号失败，请稍后重试。", StatusCodes.Status422UnprocessableEntity); }
				return;
			}
			if (path == "accounts/enabled" && principal.IsAdmin && ctx.Request.Method == "POST")
			{ var body = await ReadJson(ctx); if (!webAccounts.SetEnabled(body.Value<string>("username") ?? string.Empty, body.Value<bool?>("enabled") ?? false, out var error)) { await WriteError(ctx, error, 400); return; } await WriteJson(ctx, new { ok = true }); return; }
			if (path == "accounts/bots" && principal.IsAdmin && ctx.Request.Method == "POST")
			{
				try
				{
					var body = await ReadJson(ctx);
					var targetUser = body.Value<string>("username") ?? string.Empty;
					var botIds = (body["bots"] as JArray)?.Select(x => x?.ToString() ?? string.Empty) ?? Enumerable.Empty<string>();
					if (!webAccounts.SetAccountBots(targetUser, botIds, out var botError))
					{ await WriteError(ctx, botError, StatusCodes.Status400BadRequest); return; }
					await WriteJson(ctx, new { ok = true });
				}
				catch (JsonReaderException) { throw; }
				catch (Exception ex) { Log.Warn(ex, "Set account bots failed."); await WriteError(ctx, "保存机器人权限失败。", StatusCodes.Status422UnprocessableEntity); }
				return;
			}
			if (path == "guest/settings" && principal.IsAdmin && ctx.Request.Method == "GET")
			{ await WriteJson(ctx, new { enabled = webAccounts.IsGuestEnabled, bots = webAccounts.GuestBots }); return; }
			if (path == "guest/settings" && principal.IsAdmin && ctx.Request.Method == "POST")
			{
				try
				{
					var body = await ReadJson(ctx);
					// Guest exposure is security sensitive: confirm the admin password like other risky actions.
					if (!webAccounts.VerifyCredentials(principal.Username, body.Value<string>("password") ?? string.Empty))
					{ await WriteError(ctx, "管理员密码不正确。", StatusCodes.Status401Unauthorized); return; }
					var botIds = (body["bots"] as JArray)?.Select(x => x?.ToString() ?? string.Empty) ?? Enumerable.Empty<string>();
					webAccounts.SetGuestAccess(body.Value<bool?>("enabled") ?? false, botIds);
					await WriteJson(ctx, new { enabled = webAccounts.IsGuestEnabled, bots = webAccounts.GuestBots });
				}
				catch (JsonReaderException) { throw; }
				catch (ArgumentException ex) { await WriteError(ctx, ex.Message, StatusCodes.Status400BadRequest); }
				catch (Exception ex) { Log.Warn(ex, "Save guest settings failed."); await WriteError(ctx, "保存访客设置失败。", StatusCodes.Status422UnprocessableEntity); }
				return;
			}
			if (path == "accounts/password" && ctx.Request.Method == "POST")
			{
				try
				{
					var body = await ReadJson(ctx);
					var target = body.Value<string>("username")?.Trim() ?? string.Empty;
					var newPassword = body.Value<string>("newPassword") ?? body.Value<string>("password") ?? string.Empty;
					// Admin changing someone else's password.
					if (!string.IsNullOrWhiteSpace(target) && !string.Equals(target, principal.Username, StringComparison.OrdinalIgnoreCase))
					{
						if (!principal.IsAdmin) { await WriteError(ctx, "仅管理员可修改他人密码。", StatusCodes.Status403Forbidden); return; }
						if (!webAccounts.AdminSetPassword(principal.Username, target, newPassword, out var adminError))
						{ await WriteError(ctx, adminError, StatusCodes.Status400BadRequest); return; }
						await WriteJson(ctx, new { ok = true, message = "已修改该账号密码，对方需重新登录。" }); return;
					}
					// Changing own password (admin or user).
					var currentPassword = body.Value<string>("currentPassword") ?? body.Value<string>("oldPassword") ?? string.Empty;
					if (!webAccounts.ChangeOwnPassword(principal.Username, currentPassword, newPassword, out var ownError))
					{ await WriteError(ctx, ownError, StatusCodes.Status400BadRequest); return; }
					await WriteJson(ctx, new { ok = true, message = "密码已修改，请重新登录。" });
				}
				catch (JsonReaderException) { throw; }
				catch (Exception ex) { Log.Warn(ex, "Change password failed."); await WriteError(ctx, "修改密码失败，请稍后重试。", StatusCodes.Status422UnprocessableEntity); }
				return;
			}
			if (path == "setup/bot" && ctx.Request.Method == "POST")
			{
				if (!principal.IsAdmin) { await WriteError(ctx, "仅管理员可配置机器人。", StatusCodes.Status403Forbidden); return; }
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
				if (!started.Ok) { Log.Warn("Bot {0} was configured but could not connect: {1}", id, started.Error); await WriteJson(ctx, new { configured = true, connected = false, warning = "机器人已保存，但当前未能连接 TeamSpeak。" }); return; }
				await WriteJson(ctx, new { id = started.Value.Id, name = started.Value.Name, server = started.Value.Server });
				return;
			}
			if (path == "logout" && ctx.Request.Method == "POST") { webAccounts.Logout(ctx.Request.Cookies["ts3ab_session"]); webAccounts.Logout(ctx.Request.Cookies["ts3ab_guest"]); ctx.Response.Cookies.Delete("ts3ab_session"); ctx.Response.Cookies.Delete("ts3ab_guest"); await ctx.Response.WriteAsync("{}"); return; }
			ctx.Response.StatusCode = StatusCodes.Status501NotImplemented;
			await ctx.Response.WriteAsync("{\"error\":\"网页控制台正在初始化，请使用最新构建包。\"}");
		}

		private async Task CreateSession(HttpContext ctx, string username, string password)
		{
			var clientKey = ctx.Connection.RemoteIpAddress?.ToString();
			var session = webAccounts.Login(username, password, clientKey);
			if (session is null) { await WriteError(ctx, "账号或密码错误。", StatusCodes.Status401Unauthorized); return; }
			var forwardedProto = ctx.Request.Headers["X-Forwarded-Proto"].ToString();
			var isHttps = ctx.Request.IsHttps || string.Equals(forwardedProto, "https", StringComparison.OrdinalIgnoreCase);
			ctx.Response.Cookies.Append("ts3ab_session", session, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = isHttps, MaxAge = TimeSpan.FromDays(14) });
			await WriteJson(ctx, new { username, brandName = webAccounts.BrandName });
		}
		/// <summary>
		/// Gate every bot-scoped console action. Guests and restricted accounts may only
		/// touch the bots an admin assigned to them; this is the single enforcement point.
		/// </summary>
		private static async Task<bool> EnsureBotAccess(HttpContext ctx, WebPrincipal principal, string? botId)
		{
			if (principal.CanControlBot(botId)) return true;
			await WriteError(ctx, "没有控制该机器人的权限，请联系管理员分配。", StatusCodes.Status403Forbidden);
			return false;
		}

		private static CookieOptions GuestCookieOptions(HttpContext ctx)
		{
			var forwardedProto = ctx.Request.Headers["X-Forwarded-Proto"].ToString();
			var isHttps = ctx.Request.IsHttps || string.Equals(forwardedProto, "https", StringComparison.OrdinalIgnoreCase);
			return new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = isHttps, MaxAge = TimeSpan.FromDays(14) };
		}

		private static async Task<JObject> ReadJson(HttpContext ctx) { using var reader = new StreamReader(ctx.Request.Body); var body = await reader.ReadToEndAsync(); return string.IsNullOrWhiteSpace(body) ? new JObject() : JObject.Parse(body); }
		private static async Task ExecuteMusic(HttpContext ctx, Func<Task> action) { try { await action(); await WriteJson(ctx, new { ok = true }); } catch (Exception ex) { Log.Warn(ex, "Console music control failed."); await WriteError(ctx, "音乐控制失败，请稍后重试。", StatusCodes.Status422UnprocessableEntity); } }
		private static AudioResource ParseKuwoResource(JObject? value)
		{
			if (value is null) throw new ArgumentException("歌曲数据无效。");
			var type = value.Value<string>("type");
			var resid = value.Value<string>("resid");
			var title = value.Value<string>("title");
			var add = value["add"] as JObject;
			var query = add?.Value<string>("query")?.Trim();
			var selection = add?.Value<string>("selection");
			if (!string.Equals(type, "kuwo", StringComparison.OrdinalIgnoreCase)
				|| string.IsNullOrWhiteSpace(resid)
				|| !long.TryParse(resid, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
				|| id <= 0
				|| string.IsNullOrWhiteSpace(query) || query.Length > 200
				|| !int.TryParse(selection, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1 || number > 10)
				throw new ArgumentException("歌曲数据无效，请重新搜索后选择。");
			return new AudioResource(id.ToString(CultureInfo.InvariantCulture), string.IsNullOrWhiteSpace(title) ? null : title.Substring(0, Math.Min(title.Length, 200)), "kuwo")
				.Add("query", query)
				.Add("selection", number.ToString(CultureInfo.InvariantCulture));
		}
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
