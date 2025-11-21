using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VisualGGPK3.Patterns;

/// <summary>
/// Manages backups of file contents for undo/restore operations.
/// </summary>
public sealed class BackupManager {
	private static readonly JsonSerializerOptions JsonOptions = new() {
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		Converters = { new JsonStringEnumConverter() }
	};

	/// <summary>
	/// Default directory for storing backups.
	/// </summary>
	public static string DefaultBackupDirectory => Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
		"VisualGGPK3",
		"Backups"
	);

	private readonly Dictionary<string, BackupSession> _sessions = new();
	private BackupSession? _currentSession;

	public BackupManager() {
		Directory.CreateDirectory(DefaultBackupDirectory);
	}

	/// <summary>
	/// Current active backup session.
	/// </summary>
	public BackupSession? CurrentSession => _currentSession;

	/// <summary>
	/// Starts a new backup session.
	/// </summary>
	public BackupSession StartSession(string name) {
		_currentSession = new BackupSession {
			Id = Guid.NewGuid().ToString(),
			Name = name,
			CreatedAt = DateTime.UtcNow
		};
		_sessions[_currentSession.Id] = _currentSession;
		return _currentSession;
	}

	/// <summary>
	/// Ends the current session.
	/// </summary>
	public void EndSession() {
		if (_currentSession != null) {
			_currentSession.CompletedAt = DateTime.UtcNow;
			SaveSession(_currentSession);
			_currentSession = null;
		}
	}

	/// <summary>
	/// Adds a backup entry for a file.
	/// </summary>
	public void BackupFile(string filePath, byte[] originalContent) {
		if (_currentSession is null)
			throw new InvalidOperationException("No active backup session");

		// Check if already backed up in this session
		if (_currentSession.Entries.Any(e => e.FilePath == filePath))
			return;

		var entry = new BackupEntry {
			FilePath = filePath,
			OriginalContent = originalContent,
			BackupTime = DateTime.UtcNow
		};

		_currentSession.Entries.Add(entry);
	}

	/// <summary>
	/// Gets all entries that can be restored for a file path.
	/// </summary>
	public IEnumerable<BackupEntry> GetBackupsForFile(string filePath) {
		return _sessions.Values
			.SelectMany(s => s.Entries)
			.Where(e => e.FilePath == filePath)
			.OrderByDescending(e => e.BackupTime);
	}

	/// <summary>
	/// Gets all backup sessions.
	/// </summary>
	public IEnumerable<BackupSession> GetAllSessions() {
		LoadAllSessions();
		return _sessions.Values.OrderByDescending(s => s.CreatedAt);
	}

	/// <summary>
	/// Gets a specific backup session.
	/// </summary>
	public BackupSession? GetSession(string sessionId) {
		if (_sessions.TryGetValue(sessionId, out var session))
			return session;

		// Try to load from disk
		var filePath = GetSessionFilePath(sessionId);
		if (File.Exists(filePath)) {
			var json = File.ReadAllText(filePath);
			session = JsonSerializer.Deserialize<BackupSession>(json, JsonOptions);
			if (session != null)
				_sessions[sessionId] = session;
			return session;
		}

		return null;
	}

	/// <summary>
	/// Deletes a backup session.
	/// </summary>
	public void DeleteSession(string sessionId) {
		_sessions.Remove(sessionId);

		var filePath = GetSessionFilePath(sessionId);
		if (File.Exists(filePath))
			File.Delete(filePath);

		// Also delete backup data directory
		var dataDir = GetSessionDataDirectory(sessionId);
		if (Directory.Exists(dataDir))
			Directory.Delete(dataDir, true);
	}

	/// <summary>
	/// Clears old sessions, keeping only the most recent ones.
	/// </summary>
	public void CleanupOldSessions(int keepCount = 10) {
		LoadAllSessions();

		var sessionsToDelete = _sessions.Values
			.OrderByDescending(s => s.CreatedAt)
			.Skip(keepCount)
			.ToList();

		foreach (var session in sessionsToDelete) {
			DeleteSession(session.Id);
		}
	}

	/// <summary>
	/// Gets total size of all backups.
	/// </summary>
	public long GetTotalBackupSize() {
		if (!Directory.Exists(DefaultBackupDirectory))
			return 0;

		return Directory.GetFiles(DefaultBackupDirectory, "*", SearchOption.AllDirectories)
			.Sum(f => new FileInfo(f).Length);
	}

	private void SaveSession(BackupSession session) {
		// Save metadata
		var metaPath = GetSessionFilePath(session.Id);
		var metaDir = Path.GetDirectoryName(metaPath)!;
		Directory.CreateDirectory(metaDir);

		// Save entries to separate files (to avoid huge JSON files)
		var dataDir = GetSessionDataDirectory(session.Id);
		Directory.CreateDirectory(dataDir);

		foreach (var entry in session.Entries) {
			var entryPath = Path.Combine(dataDir, Convert.ToBase64String(
				System.Text.Encoding.UTF8.GetBytes(entry.FilePath)).Replace('/', '_') + ".backup");
			File.WriteAllBytes(entryPath, entry.OriginalContent);
			entry.BackupFilePath = entryPath;
			entry.OriginalContent = Array.Empty<byte>(); // Clear from memory
		}

		// Save session metadata
		var json = JsonSerializer.Serialize(session, JsonOptions);
		File.WriteAllText(metaPath, json);
	}

	private void LoadAllSessions() {
		if (!Directory.Exists(DefaultBackupDirectory))
			return;

		foreach (var file in Directory.GetFiles(DefaultBackupDirectory, "*.json")) {
			var sessionId = Path.GetFileNameWithoutExtension(file);
			if (!_sessions.ContainsKey(sessionId)) {
				try {
					var json = File.ReadAllText(file);
					var session = JsonSerializer.Deserialize<BackupSession>(json, JsonOptions);
					if (session != null)
						_sessions[sessionId] = session;
				} catch {
					// Ignore corrupted files
				}
			}
		}
	}

	private static string GetSessionFilePath(string sessionId) =>
		Path.Combine(DefaultBackupDirectory, sessionId + ".json");

	private static string GetSessionDataDirectory(string sessionId) =>
		Path.Combine(DefaultBackupDirectory, sessionId);
}

/// <summary>
/// Represents a backup session containing multiple file backups.
/// </summary>
public sealed class BackupSession {
	/// <summary>
	/// Unique identifier for the session.
	/// </summary>
	public string Id { get; set; } = "";

	/// <summary>
	/// User-friendly name for the session.
	/// </summary>
	public string Name { get; set; } = "";

	/// <summary>
	/// Description of what operations were performed.
	/// </summary>
	public string Description { get; set; } = "";

	/// <summary>
	/// When the session was created.
	/// </summary>
	public DateTime CreatedAt { get; set; }

	/// <summary>
	/// When the session was completed.
	/// </summary>
	public DateTime? CompletedAt { get; set; }

	/// <summary>
	/// Backup entries in this session.
	/// </summary>
	public List<BackupEntry> Entries { get; set; } = new();

	/// <summary>
	/// IDs of patterns that were applied in this session.
	/// </summary>
	public List<string> AppliedPatternIds { get; set; } = new();
}

/// <summary>
/// Represents a single file backup entry.
/// </summary>
public sealed class BackupEntry {
	/// <summary>
	/// Path of the file in the GGPK/Bundle.
	/// </summary>
	public string FilePath { get; set; } = "";

	/// <summary>
	/// Original file content.
	/// </summary>
	[JsonIgnore]
	public byte[] OriginalContent { get; set; } = Array.Empty<byte>();

	/// <summary>
	/// Path to the backup file on disk.
	/// </summary>
	public string? BackupFilePath { get; set; }

	/// <summary>
	/// When the backup was created.
	/// </summary>
	public DateTime BackupTime { get; set; }

	/// <summary>
	/// Size of the original content.
	/// </summary>
	public long OriginalSize { get; set; }

	/// <summary>
	/// Loads the original content from the backup file.
	/// </summary>
	public byte[] LoadContent() {
		if (OriginalContent.Length > 0)
			return OriginalContent;

		if (BackupFilePath is null || !File.Exists(BackupFilePath))
			throw new FileNotFoundException("Backup file not found", BackupFilePath);

		return File.ReadAllBytes(BackupFilePath);
	}
}
