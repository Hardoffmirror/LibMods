using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VisualGGPK3.Patterns;

/// <summary>
/// Represents a search and replace pattern for file content modification.
/// </summary>
public sealed class SearchReplacePattern {
	/// <summary>
	/// Unique identifier for the pattern.
	/// </summary>
	public string Id { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// User-friendly name for the pattern.
	/// </summary>
	public string Name { get; set; } = "";

	/// <summary>
	/// Description of what this pattern does.
	/// </summary>
	public string Description { get; set; } = "";

	/// <summary>
	/// File path pattern to match (glob pattern, e.g., "*.txt", "Data/*.dat").
	/// Empty means all files.
	/// </summary>
	public string FilePattern { get; set; } = "";

	/// <summary>
	/// The type of search pattern.
	/// </summary>
	public PatternType Type { get; set; } = PatternType.Hex;

	/// <summary>
	/// The search pattern (hex string or text).
	/// For Hex type: "4D 5A 90 00" or "4D5A9000"
	/// For Text type: plain text
	/// </summary>
	public string SearchPattern { get; set; } = "";

	/// <summary>
	/// The replacement pattern (hex string or text).
	/// </summary>
	public string ReplacePattern { get; set; } = "";

	/// <summary>
	/// Whether to replace all occurrences or just the first one.
	/// </summary>
	public bool ReplaceAll { get; set; } = true;

	/// <summary>
	/// Whether this pattern is enabled.
	/// </summary>
	public bool IsEnabled { get; set; } = true;

	/// <summary>
	/// Creation date of the pattern.
	/// </summary>
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	/// <summary>
	/// Last modification date.
	/// </summary>
	public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

	/// <summary>
	/// Tags for organizing patterns.
	/// </summary>
	public List<string> Tags { get; set; } = new();

	/// <summary>
	/// Converts the search pattern to bytes.
	/// </summary>
	public byte[] GetSearchBytes() => Type switch {
		PatternType.Hex => ParseHexString(SearchPattern),
		PatternType.Text => System.Text.Encoding.UTF8.GetBytes(SearchPattern),
		PatternType.TextUtf16 => System.Text.Encoding.Unicode.GetBytes(SearchPattern),
		_ => throw new ArgumentException($"Unknown pattern type: {Type}")
	};

	/// <summary>
	/// Converts the replacement pattern to bytes.
	/// </summary>
	public byte[] GetReplaceBytes() => Type switch {
		PatternType.Hex => ParseHexString(ReplacePattern),
		PatternType.Text => System.Text.Encoding.UTF8.GetBytes(ReplacePattern),
		PatternType.TextUtf16 => System.Text.Encoding.Unicode.GetBytes(ReplacePattern),
		_ => throw new ArgumentException($"Unknown pattern type: {Type}")
	};

	/// <summary>
	/// Parses a hex string to byte array.
	/// </summary>
	private static byte[] ParseHexString(string hex) {
		// Remove spaces and other separators
		hex = hex.Replace(" ", "").Replace("-", "").Replace(":", "");

		if (hex.Length % 2 != 0)
			throw new ArgumentException("Hex string must have even number of characters");

		var bytes = new byte[hex.Length / 2];
		for (int i = 0; i < bytes.Length; i++) {
			bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
		}
		return bytes;
	}

	/// <summary>
	/// Creates a deep copy of this pattern.
	/// </summary>
	public SearchReplacePattern Clone() => new() {
		Id = Guid.NewGuid().ToString(),
		Name = Name + " (Copy)",
		Description = Description,
		FilePattern = FilePattern,
		Type = Type,
		SearchPattern = SearchPattern,
		ReplacePattern = ReplacePattern,
		ReplaceAll = ReplaceAll,
		IsEnabled = IsEnabled,
		CreatedAt = DateTime.UtcNow,
		ModifiedAt = DateTime.UtcNow,
		Tags = new List<string>(Tags)
	};
}

/// <summary>
/// Type of pattern matching.
/// </summary>
public enum PatternType {
	/// <summary>
	/// Hexadecimal byte pattern.
	/// </summary>
	Hex,

	/// <summary>
	/// UTF-8 text pattern.
	/// </summary>
	Text,

	/// <summary>
	/// UTF-16 text pattern.
	/// </summary>
	TextUtf16
}

/// <summary>
/// Collection of patterns for a project.
/// </summary>
public sealed class PatternCollection {
	/// <summary>
	/// Name of the collection.
	/// </summary>
	public string Name { get; set; } = "Default";

	/// <summary>
	/// Description of the collection.
	/// </summary>
	public string Description { get; set; } = "";

	/// <summary>
	/// List of patterns in this collection.
	/// </summary>
	public List<SearchReplacePattern> Patterns { get; set; } = new();

	/// <summary>
	/// Version for compatibility.
	/// </summary>
	public int Version { get; set; } = 1;

	/// <summary>
	/// Creation date.
	/// </summary>
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	/// <summary>
	/// Last modification date.
	/// </summary>
	public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result of a pattern application.
/// </summary>
public sealed class PatternApplicationResult {
	/// <summary>
	/// The pattern that was applied.
	/// </summary>
	public required SearchReplacePattern Pattern { get; init; }

	/// <summary>
	/// Path of the file that was modified.
	/// </summary>
	public required string FilePath { get; init; }

	/// <summary>
	/// Number of replacements made.
	/// </summary>
	public int ReplacementCount { get; set; }

	/// <summary>
	/// Original file content (for backup/undo).
	/// </summary>
	public byte[]? OriginalContent { get; set; }

	/// <summary>
	/// Whether the operation was successful.
	/// </summary>
	public bool Success { get; set; }

	/// <summary>
	/// Error message if operation failed.
	/// </summary>
	public string? ErrorMessage { get; set; }

	/// <summary>
	/// Timestamp of the operation.
	/// </summary>
	public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
