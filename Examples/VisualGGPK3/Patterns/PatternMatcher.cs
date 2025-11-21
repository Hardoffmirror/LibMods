using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using LibGGPK3.Records;

using VisualGGPK3.TreeItems;

namespace VisualGGPK3.Patterns;

/// <summary>
/// Service for applying search/replace patterns to files.
/// </summary>
public sealed class PatternMatcher {
	private readonly BackupManager _backupManager;

	public PatternMatcher(BackupManager backupManager) {
		_backupManager = backupManager;
	}

	/// <summary>
	/// Event raised when progress is updated.
	/// </summary>
	public event Action<int, int, string>? ProgressChanged;

	/// <summary>
	/// Applies a pattern to a single file.
	/// </summary>
	public PatternApplicationResult ApplyPattern(
		SearchReplacePattern pattern,
		FileTreeItem fileItem,
		bool createBackup = true) {
		var result = new PatternApplicationResult {
			Pattern = pattern,
			FilePath = fileItem.GetPath()
		};

		try {
			// Check file pattern match
			if (!MatchesFilePattern(pattern.FilePattern, result.FilePath)) {
				result.Success = true;
				result.ReplacementCount = 0;
				return result;
			}

			// Read file content
			var content = fileItem.Read();
			if (content.Length == 0) {
				result.Success = true;
				return result;
			}

			var data = content.ToArray();
			var originalData = createBackup ? (byte[])data.Clone() : null;

			// Get search and replace bytes
			var searchBytes = pattern.GetSearchBytes();
			var replaceBytes = pattern.GetReplaceBytes();

			if (searchBytes.Length == 0) {
				result.Success = false;
				result.ErrorMessage = "Search pattern is empty";
				return result;
			}

			// Find and replace
			var newData = FindAndReplace(data, searchBytes, replaceBytes, pattern.ReplaceAll, out int count);
			result.ReplacementCount = count;

			if (count > 0) {
				// Backup original content
				if (createBackup && originalData != null) {
					_backupManager.BackupFile(result.FilePath, originalData);
				}

				// Write modified content
				fileItem.Write(newData);
				result.OriginalContent = originalData;
			}

			result.Success = true;
		} catch (Exception ex) {
			result.Success = false;
			result.ErrorMessage = ex.Message;
		}

		return result;
	}

	/// <summary>
	/// Applies multiple patterns to a single file.
	/// </summary>
	public List<PatternApplicationResult> ApplyPatterns(
		IEnumerable<SearchReplacePattern> patterns,
		FileTreeItem fileItem,
		bool createBackup = true) {
		var results = new List<PatternApplicationResult>();

		foreach (var pattern in patterns) {
			if (!pattern.IsEnabled)
				continue;

			var result = ApplyPattern(pattern, fileItem, createBackup);
			results.Add(result);

			// Stop on first error
			if (!result.Success)
				break;
		}

		return results;
	}

	/// <summary>
	/// Applies patterns to all files in a directory recursively.
	/// </summary>
	public async Task<List<PatternApplicationResult>> ApplyPatternsToDirectoryAsync(
		IEnumerable<SearchReplacePattern> patterns,
		DirectoryTreeItem directory,
		bool createBackup = true,
		CancellationToken cancellationToken = default) {
		var results = new List<PatternApplicationResult>();
		var files = CollectFiles(directory);
		var patternList = new List<SearchReplacePattern>(patterns);

		int processed = 0;
		int total = files.Count;

		foreach (var file in files) {
			cancellationToken.ThrowIfCancellationRequested();

			ProgressChanged?.Invoke(processed, total, file.GetPath());

			foreach (var pattern in patternList) {
				if (!pattern.IsEnabled)
					continue;

				var result = ApplyPattern(pattern, file, createBackup);
				if (result.ReplacementCount > 0 || !result.Success) {
					results.Add(result);
				}
			}

			processed++;
			await Task.Yield(); // Allow UI updates
		}

		ProgressChanged?.Invoke(total, total, "Complete");
		return results;
	}

	/// <summary>
	/// Searches for a pattern in files without replacing.
	/// </summary>
	public List<SearchMatch> Search(
		SearchReplacePattern pattern,
		DirectoryTreeItem directory,
		int maxResults = 1000,
		CancellationToken cancellationToken = default) {
		var matches = new List<SearchMatch>();
		var files = CollectFiles(directory);
		var searchBytes = pattern.GetSearchBytes();

		int processed = 0;
		int total = files.Count;

		foreach (var file in files) {
			cancellationToken.ThrowIfCancellationRequested();

			if (matches.Count >= maxResults)
				break;

			ProgressChanged?.Invoke(processed++, total, file.GetPath());

			var filePath = file.GetPath();
			if (!MatchesFilePattern(pattern.FilePattern, filePath))
				continue;

			try {
				var content = file.Read();
				if (content.Length == 0)
					continue;

				var data = content.Span;
				var positions = FindAll(data, searchBytes);

				foreach (var pos in positions) {
					if (matches.Count >= maxResults)
						break;

					matches.Add(new SearchMatch {
						FilePath = filePath,
						Position = pos,
						Context = GetContext(data, pos, searchBytes.Length, 32)
					});
				}
			} catch {
				// Skip files that can't be read
			}
		}

		ProgressChanged?.Invoke(total, total, "Complete");
		return matches;
	}

	/// <summary>
	/// Restores a file from backup.
	/// </summary>
	public bool RestoreFromBackup(BackupEntry entry, FileTreeItem fileItem) {
		try {
			var content = entry.LoadContent();
			fileItem.Write(content);
			return true;
		} catch {
			return false;
		}
	}

	/// <summary>
	/// Restores all files in a backup session.
	/// </summary>
	public int RestoreSession(BackupSession session, Func<string, FileTreeItem?> fileResolver) {
		int restored = 0;

		foreach (var entry in session.Entries) {
			var fileItem = fileResolver(entry.FilePath);
			if (fileItem == null)
				continue;

			if (RestoreFromBackup(entry, fileItem))
				restored++;
		}

		return restored;
	}

	private static List<FileTreeItem> CollectFiles(DirectoryTreeItem directory) {
		var files = new List<FileTreeItem>();
		CollectFilesRecursive(directory, files);
		return files;
	}

	private static void CollectFilesRecursive(DirectoryTreeItem directory, List<FileTreeItem> files) {
		// Ensure directory is initialized
		if (!directory.Initialized) {
			directory.Expanded = true;
		}

		foreach (var child in directory.ChildItems) {
			if (child is FileTreeItem file) {
				files.Add(file);
			} else if (child is DirectoryTreeItem dir) {
				CollectFilesRecursive(dir, files);
			}
		}
	}

	private static bool MatchesFilePattern(string pattern, string filePath) {
		if (string.IsNullOrEmpty(pattern))
			return true;

		// Convert glob pattern to regex
		var regexPattern = "^" + Regex.Escape(pattern)
			.Replace("\\*\\*", ".*")
			.Replace("\\*", "[^/]*")
			.Replace("\\?", ".") + "$";

		return Regex.IsMatch(filePath, regexPattern, RegexOptions.IgnoreCase);
	}

	private static byte[] FindAndReplace(byte[] data, byte[] search, byte[] replace, bool replaceAll, out int count) {
		count = 0;
		var positions = FindAll(data, search);

		if (positions.Count == 0)
			return data;

		if (!replaceAll && positions.Count > 0) {
			positions = new List<int> { positions[0] };
		}

		// Calculate new size
		int sizeDiff = replace.Length - search.Length;
		int newSize = data.Length + (sizeDiff * positions.Count);
		var result = new byte[newSize];

		int srcPos = 0;
		int dstPos = 0;

		foreach (var pos in positions) {
			// Copy data before match
			int copyLen = pos - srcPos;
			if (copyLen > 0) {
				Array.Copy(data, srcPos, result, dstPos, copyLen);
				dstPos += copyLen;
			}

			// Copy replacement
			Array.Copy(replace, 0, result, dstPos, replace.Length);
			dstPos += replace.Length;

			srcPos = pos + search.Length;
			count++;
		}

		// Copy remaining data
		if (srcPos < data.Length) {
			Array.Copy(data, srcPos, result, dstPos, data.Length - srcPos);
		}

		return result;
	}

	private static List<int> FindAll(ReadOnlySpan<byte> data, byte[] pattern) {
		var positions = new List<int>();
		int pos = 0;

		while (pos <= data.Length - pattern.Length) {
			int found = IndexOf(data[pos..], pattern);
			if (found < 0)
				break;

			positions.Add(pos + found);
			pos += found + pattern.Length;
		}

		return positions;
	}

	private static int IndexOf(ReadOnlySpan<byte> data, byte[] pattern) {
		for (int i = 0; i <= data.Length - pattern.Length; i++) {
			bool match = true;
			for (int j = 0; j < pattern.Length; j++) {
				if (data[i + j] != pattern[j]) {
					match = false;
					break;
				}
			}
			if (match)
				return i;
		}
		return -1;
	}

	private static byte[] GetContext(ReadOnlySpan<byte> data, int position, int matchLength, int contextSize) {
		int start = Math.Max(0, position - contextSize);
		int end = Math.Min(data.Length, position + matchLength + contextSize);
		return data[start..end].ToArray();
	}
}

/// <summary>
/// Represents a search match in a file.
/// </summary>
public sealed class SearchMatch {
	/// <summary>
	/// Path of the file containing the match.
	/// </summary>
	public string FilePath { get; set; } = "";

	/// <summary>
	/// Position of the match in the file.
	/// </summary>
	public int Position { get; set; }

	/// <summary>
	/// Context bytes around the match.
	/// </summary>
	public byte[] Context { get; set; } = Array.Empty<byte>();
}
