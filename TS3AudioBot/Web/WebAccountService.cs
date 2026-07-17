using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace TS3AudioBot.Web
{
	public enum WebAccountRole { User, Admin }

	public sealed class WebAccountService
	{
		private const string Accounts = "webAccounts";
		private const string Sessions = "webSessions";
		private const string Settings = "webSettings";
		private readonly LiteCollection<WebAccount> accounts;
		private readonly LiteCollection<WebSession> sessions;
		private readonly LiteCollection<WebSetting> settings;
		private readonly object sync = new object();
		private readonly Dictionary<string, LoginAttempt> loginAttempts = new Dictionary<string, LoginAttempt>(StringComparer.OrdinalIgnoreCase);
		private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(14);
		private static readonly byte[] DummySalt = new byte[] { 0x54, 0x53, 0x33, 0x41, 0x42, 0x2D, 0x73, 0x61, 0x6C, 0x74, 0x2D, 0x76, 0x31, 0x2D, 0x30, 0x31 };

		public WebAccountService(DbStore database)
		{
			accounts = database.GetCollection<WebAccount>(Accounts);
			sessions = database.GetCollection<WebSession>(Sessions);
			settings = database.GetCollection<WebSetting>(Settings);
			accounts.EnsureIndex(x => x.Username, true);
			sessions.EnsureIndex(x => x.ExpiresAt);
		}

		public bool IsInitialized
		{
			get { lock (sync) return accounts.Count() > 0; }
		}
		public string BrandName => settings.FindById("brand_name")?.Value ?? "波点音乐";
		public void SetBrandName(string value)
		{
			if (string.IsNullOrWhiteSpace(value) || value.Length > 32) throw new ArgumentException("品牌名称长度必须为 1 至 32 个字符。");
			settings.Upsert(new WebSetting { Key = "brand_name", Value = value.Trim() });
		}

		public bool CreateInitialAdmin(string username, string password, out string error)
		{
			lock (sync)
			{
				if (accounts.Count() > 0) { error = "系统已完成初始化。"; return false; }
				return CreateAccount(username, password, WebAccountRole.Admin, out error);
			}
		}

		public bool CreateAccount(string username, string password, WebAccountRole role, out string error)
		{
			username = username?.Trim() ?? string.Empty;
			if (string.IsNullOrWhiteSpace(username) || username.Length > 32) { error = "账号长度必须为 1 至 32 个字符。"; return false; }
			if (string.IsNullOrWhiteSpace(password) || password.Length < 8) { error = "密码至少需要 8 个字符。"; return false; }
			lock (sync)
			{
				if (accounts.FindById(username) != null) { error = "该账号已存在。"; return false; }
				var salt = new byte[16]; RandomNumberGenerator.Fill(salt);
				var hash = Hash(password, salt);
				try
				{
					accounts.Insert(new WebAccount { Username = username, Role = role, Salt = Convert.ToBase64String(salt), PasswordHash = Convert.ToBase64String(hash), Enabled = true, CreatedAt = DateTime.UtcNow });
				}
				catch (Exception ex) when (ex.Message.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0 || ex.Message.IndexOf("unique", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					error = "该账号已存在。"; return false;
				}
				error = string.Empty; return true;
			}
		}

		public string? Login(string username, string password, string? clientKey = null)
		{
			username = username?.Trim() ?? string.Empty;
			var attemptKey = username + "|" + (string.IsNullOrWhiteSpace(clientKey) ? "unknown" : clientKey);
			lock (sync)
			{
				if (IsLoginBlocked(attemptKey)) return null;
				var account = accounts.FindById(username);
				var valid = account != null && account.Enabled && Verify(password, account);
				if (account == null || !account.Enabled) _ = Hash(password, DummySalt);
				if (!valid)
				{
					RegisterLoginFailure(attemptKey);
					return null;
				}
				loginAttempts.Remove(attemptKey);
				CleanupExpiredSessions();
			var id = ToBase64Url(RandomNumberGenerator.GetBytes(32));
				sessions.Insert(new WebSession { Id = id, Username = account!.Username, ExpiresAt = DateTime.UtcNow.Add(SessionLifetime) });
				return id;
			}
		}

		public WebAccount? GetAccount(string? sessionId)
		{
			if (string.IsNullOrEmpty(sessionId)) return null;
			lock (sync)
			{
				var session = sessions.FindById(sessionId);
				if (session == null || session.ExpiresAt < DateTime.UtcNow) { if (session != null) sessions.Delete(session.Id); return null; }
				var account = accounts.FindById(session.Username);
				if (account?.Enabled != true) return null;
				// Sliding expiry keeps active users signed in while retaining a hard inactivity window.
				session.ExpiresAt = DateTime.UtcNow.Add(SessionLifetime);
				sessions.Update(session);
				return account;
			}
		}

		public void Logout(string? sessionId) { if (!string.IsNullOrEmpty(sessionId)) lock (sync) sessions.Delete(sessionId); }

		public IReadOnlyList<WebAccount> ListAccounts()
			=> accounts.FindAll().OrderBy(x => x.CreatedAt).ToArray();

		public bool SetEnabled(string username, bool enabled, out string error)
		{
			username = username?.Trim() ?? string.Empty;
			lock (sync)
			{
				var account = accounts.FindById(username);
				if (account is null) { error = "账号不存在。"; return false; }
				if (account.Role == WebAccountRole.Admin && !enabled && accounts.FindAll().Count(x => x.Role == WebAccountRole.Admin && x.Enabled) <= 1)
				{ error = "至少要保留一个启用的管理员账号。"; return false; }
				account.Enabled = enabled;
				accounts.Update(account);
				error = string.Empty;
				return true;
			}
		}

		private static byte[] Hash(string password, byte[] salt) => new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256).GetBytes(32);
		private static bool Verify(string password, WebAccount account) => CryptographicOperations.FixedTimeEquals(Hash(password, Convert.FromBase64String(account.Salt)), Convert.FromBase64String(account.PasswordHash));
		private static string ToBase64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
		private void CleanupExpiredSessions()
		{
			var now = DateTime.UtcNow;
			foreach (var session in sessions.FindAll().Where(x => x.ExpiresAt < now).ToArray())
				sessions.Delete(session.Id);
		}
		private bool IsLoginBlocked(string username)
		{
			if (!loginAttempts.TryGetValue(username, out var attempt)) return false;
			if (attempt.BlockedUntil <= DateTime.UtcNow) { loginAttempts.Remove(username); return false; }
			return true;
		}
		private void RegisterLoginFailure(string username)
		{
			loginAttempts.TryGetValue(username, out var attempt);
			attempt.Failures++;
			attempt.BlockedUntil = attempt.Failures >= 5 ? DateTime.UtcNow.AddMinutes(5) : DateTime.UtcNow.AddSeconds(1);
			loginAttempts[username] = attempt;
		}

		private struct LoginAttempt
		{
			public int Failures;
			public DateTime BlockedUntil;
		}
	}

	public sealed class WebAccount { [BsonId] public string Username { get; set; } = string.Empty; public string PasswordHash { get; set; } = string.Empty; public string Salt { get; set; } = string.Empty; public WebAccountRole Role { get; set; } public bool Enabled { get; set; } public DateTime CreatedAt { get; set; } }
	public sealed class WebSession { [BsonId] public string Id { get; set; } = string.Empty; public string Username { get; set; } = string.Empty; public DateTime ExpiresAt { get; set; } }
	public sealed class WebSetting { [BsonId] public string Key { get; set; } = string.Empty; public string Value { get; set; } = string.Empty; }
}
