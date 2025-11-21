using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VisualGGPK3.Patterns;

/// <summary>
/// Manages pattern collections - saving, loading, and organizing patterns.
/// </summary>
public sealed class PatternManager {
	private static readonly JsonSerializerOptions JsonOptions = new() {
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		Converters = { new JsonStringEnumConverter() }
	};

	/// <summary>
	/// Default directory for storing pattern files.
	/// </summary>
	public static string DefaultPatternsDirectory => Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
		"VisualGGPK3",
		"Patterns"
	);

	/// <summary>
	/// Currently loaded pattern collection.
	/// </summary>
	public PatternCollection CurrentCollection { get; private set; } = new();

	/// <summary>
	/// Path to the currently loaded file.
	/// </summary>
	public string? CurrentFilePath { get; private set; }

	/// <summary>
	/// Whether there are unsaved changes.
	/// </summary>
	public bool HasUnsavedChanges { get; private set; }

	/// <summary>
	/// Event raised when the collection changes.
	/// </summary>
	public event EventHandler? CollectionChanged;

	public PatternManager() {
		Directory.CreateDirectory(DefaultPatternsDirectory);
	}

	/// <summary>
	/// Creates a new empty pattern collection.
	/// </summary>
	public void NewCollection(string name = "New Collection") {
		CurrentCollection = new PatternCollection {
			Name = name,
			CreatedAt = DateTime.UtcNow,
			ModifiedAt = DateTime.UtcNow
		};
		CurrentFilePath = null;
		HasUnsavedChanges = false;
		CollectionChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// Loads a pattern collection from a file.
	/// </summary>
	public void Load(string filePath) {
		var json = File.ReadAllText(filePath);
		CurrentCollection = JsonSerializer.Deserialize<PatternCollection>(json, JsonOptions)
			?? throw new InvalidDataException("Failed to deserialize pattern collection");
		CurrentFilePath = filePath;
		HasUnsavedChanges = false;
		CollectionChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// Saves the current collection to a file.
	/// </summary>
	public void Save(string? filePath = null) {
		filePath ??= CurrentFilePath ?? throw new InvalidOperationException("No file path specified");

		CurrentCollection.ModifiedAt = DateTime.UtcNow;
		var json = JsonSerializer.Serialize(CurrentCollection, JsonOptions);

		Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
		File.WriteAllText(filePath, json);

		CurrentFilePath = filePath;
		HasUnsavedChanges = false;
	}

	/// <summary>
	/// Adds a pattern to the current collection.
	/// </summary>
	public void AddPattern(SearchReplacePattern pattern) {
		CurrentCollection.Patterns.Add(pattern);
		HasUnsavedChanges = true;
		CollectionChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// Removes a pattern from the current collection.
	/// </summary>
	public bool RemovePattern(string patternId) {
		var removed = CurrentCollection.Patterns.RemoveAll(p => p.Id == patternId) > 0;
		if (removed) {
			HasUnsavedChanges = true;
			CollectionChanged?.Invoke(this, EventArgs.Empty);
		}
		return removed;
	}

	/// <summary>
	/// Updates an existing pattern.
	/// </summary>
	public bool UpdatePattern(SearchReplacePattern pattern) {
		var index = CurrentCollection.Patterns.FindIndex(p => p.Id == pattern.Id);
		if (index < 0)
			return false;

		pattern.ModifiedAt = DateTime.UtcNow;
		CurrentCollection.Patterns[index] = pattern;
		HasUnsavedChanges = true;
		CollectionChanged?.Invoke(this, EventArgs.Empty);
		return true;
	}

	/// <summary>
	/// Gets a pattern by ID.
	/// </summary>
	public SearchReplacePattern? GetPattern(string patternId) {
		return CurrentCollection.Patterns.FirstOrDefault(p => p.Id == patternId);
	}

	/// <summary>
	/// Gets all enabled patterns.
	/// </summary>
	public IEnumerable<SearchReplacePattern> GetEnabledPatterns() {
		return CurrentCollection.Patterns.Where(p => p.IsEnabled);
	}

	/// <summary>
	/// Gets patterns by tag.
	/// </summary>
	public IEnumerable<SearchReplacePattern> GetPatternsByTag(string tag) {
		return CurrentCollection.Patterns.Where(p => p.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Gets all unique tags in the collection.
	/// </summary>
	public IEnumerable<string> GetAllTags() {
		return CurrentCollection.Patterns
			.SelectMany(p => p.Tags)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(t => t);
	}

	/// <summary>
	/// Lists all pattern files in the default directory.
	/// </summary>
	public IEnumerable<string> ListPatternFiles() {
		if (!Directory.Exists(DefaultPatternsDirectory))
			return Enumerable.Empty<string>();

		return Directory.GetFiles(DefaultPatternsDirectory, "*.json")
			.OrderByDescending(f => File.GetLastWriteTime(f));
	}

	/// <summary>
	/// Exports patterns to a file for sharing.
	/// </summary>
	public void Export(string filePath, IEnumerable<string>? patternIds = null) {
		var collection = new PatternCollection {
			Name = CurrentCollection.Name + " (Export)",
			Description = "Exported patterns",
			Patterns = patternIds is null
				? new List<SearchReplacePattern>(CurrentCollection.Patterns)
				: CurrentCollection.Patterns.Where(p => patternIds.Contains(p.Id)).ToList()
		};

		var json = JsonSerializer.Serialize(collection, JsonOptions);
		File.WriteAllText(filePath, json);
	}

	/// <summary>
	/// Imports patterns from a file.
	/// </summary>
	public int Import(string filePath, bool replaceExisting = false) {
		var json = File.ReadAllText(filePath);
		var importedCollection = JsonSerializer.Deserialize<PatternCollection>(json, JsonOptions)
			?? throw new InvalidDataException("Failed to deserialize pattern collection");

		int imported = 0;
		foreach (var pattern in importedCollection.Patterns) {
			var existing = CurrentCollection.Patterns.FindIndex(p => p.Id == pattern.Id);
			if (existing >= 0) {
				if (replaceExisting) {
					CurrentCollection.Patterns[existing] = pattern;
					imported++;
				}
			} else {
				CurrentCollection.Patterns.Add(pattern);
				imported++;
			}
		}

		if (imported > 0) {
			HasUnsavedChanges = true;
			CollectionChanged?.Invoke(this, EventArgs.Empty);
		}

		return imported;
	}

	/// <summary>
	/// Duplicates a pattern.
	/// </summary>
	public SearchReplacePattern DuplicatePattern(string patternId) {
		var original = GetPattern(patternId)
			?? throw new ArgumentException($"Pattern not found: {patternId}");

		var clone = original.Clone();
		AddPattern(clone);
		return clone;
	}

	/// <summary>
	/// Moves a pattern up in the list.
	/// </summary>
	public void MovePatternUp(string patternId) {
		var index = CurrentCollection.Patterns.FindIndex(p => p.Id == patternId);
		if (index > 0) {
			(CurrentCollection.Patterns[index], CurrentCollection.Patterns[index - 1]) =
				(CurrentCollection.Patterns[index - 1], CurrentCollection.Patterns[index]);
			HasUnsavedChanges = true;
			CollectionChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	/// <summary>
	/// Moves a pattern down in the list.
	/// </summary>
	public void MovePatternDown(string patternId) {
		var index = CurrentCollection.Patterns.FindIndex(p => p.Id == patternId);
		if (index >= 0 && index < CurrentCollection.Patterns.Count - 1) {
			(CurrentCollection.Patterns[index], CurrentCollection.Patterns[index + 1]) =
				(CurrentCollection.Patterns[index + 1], CurrentCollection.Patterns[index]);
			HasUnsavedChanges = true;
			CollectionChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
