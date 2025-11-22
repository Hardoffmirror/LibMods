using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Eto.Drawing;
using Eto.Forms;

using VisualGGPK3.TreeItems;

namespace VisualGGPK3;

/// <summary>
/// Диалог для сравнения баз данных (отпечатки и различия).
/// </summary>
public sealed class DatabaseCompareDialog : Dialog {
	private readonly DirectoryTreeItem? _rootDirectory;

	private readonly Label _currentDbLabel;
	private readonly Label _snapshotLabel;
	private readonly GridView _resultsGrid;
	private readonly Label _statusLabel;
	private readonly ProgressBar _progressBar;
	private readonly CheckBox _showAdded;
	private readonly CheckBox _showRemoved;
	private readonly CheckBox _showModified;
	private readonly CheckBox _showUnchanged;

	private DatabaseSnapshot? _loadedSnapshot;
	private List<CompareResultItem> _compareResults = new();
	private CancellationTokenSource? _cts;

	public DatabaseCompareDialog(DirectoryTreeItem? rootDirectory) {
		_rootDirectory = rootDirectory;

		Title = L.DatabaseCompare;
		MinimumSize = new Size(1000, 700);
		Padding = new Padding(10);

		// Info labels
		_currentDbLabel = new Label { Text = GetCurrentDbInfo() };
		_snapshotLabel = new Label { Text = L.NotLoaded };

		// Filter checkboxes
		_showAdded = new CheckBox { Text = L.FilterAdded, Checked = true };
		_showRemoved = new CheckBox { Text = L.FilterRemoved, Checked = true };
		_showModified = new CheckBox { Text = L.FilterModified, Checked = true };
		_showUnchanged = new CheckBox { Text = L.FilterUnchanged, Checked = false };

		_showAdded.CheckedChanged += OnFilterChanged;
		_showRemoved.CheckedChanged += OnFilterChanged;
		_showModified.CheckedChanged += OnFilterChanged;
		_showUnchanged.CheckedChanged += OnFilterChanged;

		// Results grid
		_resultsGrid = new GridView {
			AllowMultipleSelection = true,
			GridLines = GridLines.Both
		};

		_resultsGrid.Columns.Add(new GridColumn {
			HeaderText = L.ColStatus,
			DataCell = new TextBoxCell { Binding = Binding.Property<CompareResultItem, string>(i => i.Status) },
			Width = 100
		});

		_resultsGrid.Columns.Add(new GridColumn {
			HeaderText = L.ColPath,
			DataCell = new TextBoxCell { Binding = Binding.Property<CompareResultItem, string>(i => i.Path) },
			Width = 400
		});

		_resultsGrid.Columns.Add(new GridColumn {
			HeaderText = L.ColOldSize,
			DataCell = new TextBoxCell { Binding = Binding.Property<CompareResultItem, string>(i => i.OldSize) },
			Width = 100
		});

		_resultsGrid.Columns.Add(new GridColumn {
			HeaderText = L.ColNewSize,
			DataCell = new TextBoxCell { Binding = Binding.Property<CompareResultItem, string>(i => i.NewSize) },
			Width = 100
		});

		// Status
		_statusLabel = new Label { Text = L.Ready };
		_progressBar = new ProgressBar { Visible = false };

		// Buttons
		var createSnapshotButton = new Button { Text = L.CreateSnapshot };
		createSnapshotButton.Click += OnCreateSnapshot;

		var loadSnapshotButton = new Button { Text = L.LoadSnapshot };
		loadSnapshotButton.Click += OnLoadSnapshot;

		var compareButton = new Button { Text = L.CompareWithCurrent };
		compareButton.Click += OnCompare;

		var exportButton = new Button { Text = L.ExportResults };
		exportButton.Click += OnExportResults;

		var copyPathButton = new Button { Text = L.CopyPath };
		copyPathButton.Click += OnCopyPath;

		var closeButton = new Button { Text = L.Close };
		closeButton.Click += (s, e) => Close();

		// Layout
		Content = new StackLayout {
			Spacing = 10,
			HorizontalContentAlignment = HorizontalAlignment.Stretch,
			Items = {
				// Info panel
				new StackLayoutItem(new GroupBox {
					Text = L.SnapshotInfo,
					Content = new StackLayout {
						Padding = new Padding(10),
						Spacing = 5,
						Items = {
							new StackLayout {
								Orientation = Orientation.Horizontal,
								Spacing = 20,
								Items = {
									new StackLayout {
										Spacing = 5,
										Items = {
											new Label { Text = L.CurrentDatabase, Font = SystemFonts.Bold() },
											_currentDbLabel
										}
									},
									new StackLayout {
										Spacing = 5,
										Items = {
											new Label { Text = L.LoadedSnapshot, Font = SystemFonts.Bold() },
											_snapshotLabel
										}
									}
								}
							}
						}
					}
				}),
				// Action buttons
				new StackLayoutItem(new StackLayout {
					Orientation = Orientation.Horizontal,
					Spacing = 10,
					Items = {
						createSnapshotButton,
						loadSnapshotButton,
						compareButton,
						exportButton
					}
				}),
				// Filters
				new StackLayoutItem(new StackLayout {
					Orientation = Orientation.Horizontal,
					Spacing = 15,
					Items = {
						_showAdded,
						_showRemoved,
						_showModified,
						_showUnchanged
					}
				}),
				// Results
				new StackLayoutItem(_resultsGrid, true),
				// Bottom buttons
				new StackLayoutItem(new StackLayout {
					Orientation = Orientation.Horizontal,
					Spacing = 10,
					Items = {
						copyPathButton,
						new StackLayoutItem(null, true),
						closeButton
					}
				}),
				// Status bar
				new StackLayoutItem(new StackLayout {
					Orientation = Orientation.Horizontal,
					Spacing = 10,
					Items = {
						new StackLayoutItem(_statusLabel, true),
						_progressBar
					}
				})
			}
		};

		AbortButton = closeButton;
	}

	private string GetCurrentDbInfo() {
		if (_rootDirectory == null)
			return L.NotLoaded;

		var allFiles = new List<FileTreeItem>();
		CollectFilesSync(_rootDirectory, allFiles);

		long totalSize = 0;
		foreach (var file in allFiles) {
			try {
				totalSize += file.Read().Length;
			} catch {
				// Ignore
			}
		}

		return $"{string.Format(L.TotalFiles, allFiles.Count)}\n{FormatSize(totalSize)}";
	}

	private async void OnCreateSnapshot(object? sender, EventArgs e) {
		if (_rootDirectory == null) {
			MessageBox.Show(this, L.NoDirectoryAvailable, L.Error, MessageBoxType.Error);
			return;
		}

		using var sfd = new SaveFileDialog {
			FileName = $"snapshot_{DateTime.Now:yyyyMMdd_HHmmss}.json",
			Filters = { new FileFilter(L.SnapshotFiles, "*.json") }
		};

		if (sfd.ShowDialog(this) != DialogResult.Ok)
			return;

		_cts?.Cancel();
		_cts = new CancellationTokenSource();
		var token = _cts.Token;

		_progressBar.Visible = true;
		_statusLabel.Text = L.CreatingSnapshot;

		try {
			var snapshot = await CreateSnapshotAsync(token);
			var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
			await File.WriteAllTextAsync(sfd.FileName, json, token);

			MessageBox.Show(this, string.Format(L.SnapshotCreatedFiles, snapshot.Files.Count),
				L.SnapshotCreated, MessageBoxType.Information);
		} catch (OperationCanceledException) {
			_statusLabel.Text = L.OperationCancelled;
		} catch (Exception ex) {
			MessageBox.Show(this, string.Format(L.FailedToCreateSnapshot, ex.Message),
				L.Error, MessageBoxType.Error);
		} finally {
			_progressBar.Visible = false;
			_statusLabel.Text = L.Ready;
		}
	}

	private async Task<DatabaseSnapshot> CreateSnapshotAsync(CancellationToken token) {
		var snapshot = new DatabaseSnapshot {
			CreatedAt = DateTime.UtcNow,
			Files = new List<FileEntry>()
		};

		var allFiles = new List<FileTreeItem>();
		await Task.Run(() => CollectFilesSync(_rootDirectory!, allFiles), token);

		Application.Instance.AsyncInvoke(() => _progressBar.MaxValue = allFiles.Count);

		var count = 0;
		foreach (var file in allFiles) {
			if (token.IsCancellationRequested)
				break;

			Application.Instance.AsyncInvoke(() => {
				_progressBar.Value = count;
				_statusLabel.Text = string.Format(L.Processing, file.Name);
			});

			try {
				var data = file.Read();
				var hash = ComputeHash(data.Span);

				snapshot.Files.Add(new FileEntry {
					Path = file.GetPath(),
					Size = data.Length,
					Hash = hash
				});
			} catch {
				// Skip files that can't be read
			}

			count++;
		}

		return snapshot;
	}

	private async void OnLoadSnapshot(object? sender, EventArgs e) {
		using var ofd = new OpenFileDialog {
			Filters = { new FileFilter(L.SnapshotFiles, "*.json") }
		};

		if (ofd.ShowDialog(this) != DialogResult.Ok)
			return;

		try {
			var json = await File.ReadAllTextAsync(ofd.FileName);
			_loadedSnapshot = JsonSerializer.Deserialize<DatabaseSnapshot>(json);

			if (_loadedSnapshot != null) {
				_snapshotLabel.Text = $"{string.Format(L.TotalFiles, _loadedSnapshot.Files.Count)}\n" +
					$"{string.Format(L.CreatedAt, _loadedSnapshot.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm)}";

				MessageBox.Show(this, string.Format(L.SnapshotLoadedFiles, _loadedSnapshot.Files.Count),
					L.SnapshotLoaded, MessageBoxType.Information);
			}
		} catch (Exception ex) {
			MessageBox.Show(this, string.Format(L.FailedToLoadSnapshot, ex.Message),
				L.Error, MessageBoxType.Error);
		}
	}

	private async void OnCompare(object? sender, EventArgs e) {
		if (_rootDirectory == null) {
			MessageBox.Show(this, L.NoDirectoryAvailable, L.Error, MessageBoxType.Error);
			return;
		}

		if (_loadedSnapshot == null) {
			MessageBox.Show(this, L.PleaseLoadSnapshot, L.NoSnapshotLoaded, MessageBoxType.Warning);
			return;
		}

		_cts?.Cancel();
		_cts = new CancellationTokenSource();
		var token = _cts.Token;

		_progressBar.Visible = true;
		_statusLabel.Text = L.Comparing;
		_compareResults.Clear();

		try {
			var currentSnapshot = await CreateSnapshotAsync(token);
			var results = CompareSnapshots(_loadedSnapshot, currentSnapshot);
			_compareResults = results;

			Application.Instance.AsyncInvoke(() => {
				ApplyFilters();

				var added = results.Count(r => r.ChangeType == ChangeType.Added);
				var removed = results.Count(r => r.ChangeType == ChangeType.Removed);
				var modified = results.Count(r => r.ChangeType == ChangeType.Modified);
				var unchanged = results.Count(r => r.ChangeType == ChangeType.Unchanged);

				MessageBox.Show(this,
					string.Format(L.CompareResultFormat, added, removed, modified, unchanged),
					L.CompareComplete, MessageBoxType.Information);
			});
		} catch (OperationCanceledException) {
			_statusLabel.Text = L.OperationCancelled;
		} catch (Exception ex) {
			MessageBox.Show(this, ex.Message, L.Error, MessageBoxType.Error);
		} finally {
			_progressBar.Visible = false;
			_statusLabel.Text = L.Ready;
		}
	}

	private List<CompareResultItem> CompareSnapshots(DatabaseSnapshot oldSnapshot, DatabaseSnapshot newSnapshot) {
		var results = new List<CompareResultItem>();
		var oldFiles = oldSnapshot.Files.ToDictionary(f => f.Path);
		var newFiles = newSnapshot.Files.ToDictionary(f => f.Path);

		// Check for added and modified files
		foreach (var newFile in newSnapshot.Files) {
			if (oldFiles.TryGetValue(newFile.Path, out var oldFile)) {
				var changeType = oldFile.Hash == newFile.Hash ? ChangeType.Unchanged : ChangeType.Modified;
				results.Add(new CompareResultItem {
					Path = newFile.Path,
					ChangeType = changeType,
					OldSizeBytes = oldFile.Size,
					NewSizeBytes = newFile.Size
				});
			} else {
				results.Add(new CompareResultItem {
					Path = newFile.Path,
					ChangeType = ChangeType.Added,
					OldSizeBytes = 0,
					NewSizeBytes = newFile.Size
				});
			}
		}

		// Check for removed files
		foreach (var oldFile in oldSnapshot.Files) {
			if (!newFiles.ContainsKey(oldFile.Path)) {
				results.Add(new CompareResultItem {
					Path = oldFile.Path,
					ChangeType = ChangeType.Removed,
					OldSizeBytes = oldFile.Size,
					NewSizeBytes = 0
				});
			}
		}

		return results.OrderBy(r => r.ChangeType).ThenBy(r => r.Path).ToList();
	}

	private void OnFilterChanged(object? sender, EventArgs e) {
		ApplyFilters();
	}

	private void ApplyFilters() {
		var filtered = _compareResults.Where(r => {
			return r.ChangeType switch {
				ChangeType.Added => _showAdded.Checked ?? true,
				ChangeType.Removed => _showRemoved.Checked ?? true,
				ChangeType.Modified => _showModified.Checked ?? true,
				ChangeType.Unchanged => _showUnchanged.Checked ?? false,
				_ => true
			};
		}).ToList();

		_resultsGrid.DataStore = filtered;
		_statusLabel.Text = string.Format(L.SearchResultsFound, filtered.Count);
	}

	private void OnExportResults(object? sender, EventArgs e) {
		if (_compareResults.Count == 0) {
			MessageBox.Show(this, L.NoSearchResults, L.Warning, MessageBoxType.Warning);
			return;
		}

		using var sfd = new SaveFileDialog {
			FileName = $"compare_results_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
			Filters = { new FileFilter(L.TextFiles, "*.txt") }
		};

		if (sfd.ShowDialog(this) != DialogResult.Ok)
			return;

		var lines = new List<string> {
			$"Database Comparison Results - {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
			"",
			$"Added: {_compareResults.Count(r => r.ChangeType == ChangeType.Added)}",
			$"Removed: {_compareResults.Count(r => r.ChangeType == ChangeType.Removed)}",
			$"Modified: {_compareResults.Count(r => r.ChangeType == ChangeType.Modified)}",
			$"Unchanged: {_compareResults.Count(r => r.ChangeType == ChangeType.Unchanged)}",
			"",
			"Details:",
			""
		};

		foreach (var result in _compareResults) {
			lines.Add($"[{result.Status}] {result.Path} ({result.OldSize} -> {result.NewSize})");
		}

		File.WriteAllLines(sfd.FileName, lines);
		MessageBox.Show(this, string.Format(L.SavedFile, sfd.FileName), L.Done, MessageBoxType.Information);
	}

	private void OnCopyPath(object? sender, EventArgs e) {
		var paths = _resultsGrid.SelectedRows
			.Select(i => ((CompareResultItem)_resultsGrid.DataStore.ElementAt(i)).Path)
			.ToList();

		if (paths.Count > 0) {
			Clipboard.Instance.Text = string.Join("\n", paths);
		}
	}

	private void CollectFilesSync(DirectoryTreeItem dir, List<FileTreeItem> files) {
		if (!dir.Initialized) {
			dir.Expanded = true;
		}

		foreach (var child in dir.ChildItems) {
			if (child is FileTreeItem file) {
				files.Add(file);
			} else if (child is DirectoryTreeItem subDir) {
				CollectFilesSync(subDir, files);
			}
		}
	}

	private static string ComputeHash(ReadOnlySpan<byte> data) {
		Span<byte> hash = stackalloc byte[32];
		SHA256.HashData(data, hash);
		return Convert.ToHexString(hash);
	}

	private static string FormatSize(long size) {
		return size switch {
			< 1024 => $"{size} B",
			< 1024 * 1024 => $"{size / 1024.0:F1} KB",
			< 1024 * 1024 * 1024 => $"{size / (1024.0 * 1024.0):F1} MB",
			_ => $"{size / (1024.0 * 1024.0 * 1024.0):F1} GB"
		};
	}

	private enum ChangeType {
		Added,
		Removed,
		Modified,
		Unchanged
	}

	private sealed class CompareResultItem {
		public string Path { get; set; } = "";
		public ChangeType ChangeType { get; set; }
		public long OldSizeBytes { get; set; }
		public long NewSizeBytes { get; set; }

		public string Status => ChangeType switch {
			ChangeType.Added => L.StatusAdded,
			ChangeType.Removed => L.StatusRemoved,
			ChangeType.Modified => L.StatusModified,
			ChangeType.Unchanged => L.StatusUnchanged,
			_ => "?"
		};

		public string OldSize => OldSizeBytes == 0 ? "-" : FormatSize(OldSizeBytes);
		public string NewSize => NewSizeBytes == 0 ? "-" : FormatSize(NewSizeBytes);
	}

	private sealed class DatabaseSnapshot {
		public DateTime CreatedAt { get; set; }
		public List<FileEntry> Files { get; set; } = new();
	}

	private sealed class FileEntry {
		public string Path { get; set; } = "";
		public long Size { get; set; }
		public string Hash { get; set; } = "";
	}
}
