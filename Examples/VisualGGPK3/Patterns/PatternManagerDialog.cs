using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Eto.Drawing;
using Eto.Forms;

using VisualGGPK3.TreeItems;

namespace VisualGGPK3.Patterns;

/// <summary>
/// Dialog for managing pattern collections.
/// </summary>
public sealed class PatternManagerDialog : Dialog {
	private readonly PatternManager _patternManager;
	private readonly PatternMatcher _patternMatcher;
	private readonly BackupManager _backupManager;
	private readonly GridView _patternGrid;
	private readonly Label _statusLabel;
	private readonly ProgressBar _progressBar;

	private readonly Func<string, FileTreeItem?>? _fileResolver;
	private readonly DirectoryTreeItem? _rootDirectory;

	public PatternManagerDialog(
		PatternManager patternManager,
		PatternMatcher patternMatcher,
		BackupManager backupManager,
		DirectoryTreeItem? rootDirectory = null,
		Func<string, FileTreeItem?>? fileResolver = null) {
		_patternManager = patternManager;
		_patternMatcher = patternMatcher;
		_backupManager = backupManager;
		_rootDirectory = rootDirectory;
		_fileResolver = fileResolver;

		Title = "Pattern Manager";
		MinimumSize = new Size(800, 600);
		Padding = new Padding(10);

		// Pattern grid
		_patternGrid = new GridView {
			AllowMultipleSelection = true,
			GridLines = GridLines.Both
		};

		_patternGrid.Columns.Add(new GridColumn {
			HeaderText = "Enabled",
			DataCell = new CheckBoxCell { Binding = Binding.Property<PatternGridItem, bool?>(i => i.IsEnabled) },
			Width = 60
		});

		_patternGrid.Columns.Add(new GridColumn {
			HeaderText = "Name",
			DataCell = new TextBoxCell { Binding = Binding.Property<PatternGridItem, string>(i => i.Name) },
			Width = 150
		});

		_patternGrid.Columns.Add(new GridColumn {
			HeaderText = "Type",
			DataCell = new TextBoxCell { Binding = Binding.Property<PatternGridItem, string>(i => i.Type) },
			Width = 80
		});

		_patternGrid.Columns.Add(new GridColumn {
			HeaderText = "File Pattern",
			DataCell = new TextBoxCell { Binding = Binding.Property<PatternGridItem, string>(i => i.FilePattern) },
			Width = 120
		});

		_patternGrid.Columns.Add(new GridColumn {
			HeaderText = "Description",
			DataCell = new TextBoxCell { Binding = Binding.Property<PatternGridItem, string>(i => i.Description) },
			Width = 200
		});

		_patternGrid.CellDoubleClick += OnPatternDoubleClick;

		// Status bar
		_statusLabel = new Label { Text = "Ready" };
		_progressBar = new ProgressBar { Visible = false };

		// Toolbar buttons
		var newButton = new Button { Text = "New" };
		newButton.Click += OnNewPattern;

		var editButton = new Button { Text = "Edit" };
		editButton.Click += OnEditPattern;

		var deleteButton = new Button { Text = "Delete" };
		deleteButton.Click += OnDeletePattern;

		var duplicateButton = new Button { Text = "Duplicate" };
		duplicateButton.Click += OnDuplicatePattern;

		var moveUpButton = new Button { Text = "Up" };
		moveUpButton.Click += OnMoveUp;

		var moveDownButton = new Button { Text = "Down" };
		moveDownButton.Click += OnMoveDown;

		var applyButton = new Button { Text = "Apply Selected" };
		applyButton.Click += OnApplySelected;

		var applyAllButton = new Button { Text = "Apply All Enabled" };
		applyAllButton.Click += OnApplyAll;

		var searchButton = new Button { Text = "Search" };
		searchButton.Click += OnSearch;

		// File operations
		var loadButton = new Button { Text = "Load" };
		loadButton.Click += OnLoad;

		var saveButton = new Button { Text = "Save" };
		saveButton.Click += OnSave;

		var saveAsButton = new Button { Text = "Save As" };
		saveAsButton.Click += OnSaveAs;

		var importButton = new Button { Text = "Import" };
		importButton.Click += OnImport;

		var exportButton = new Button { Text = "Export" };
		exportButton.Click += OnExport;

		// Backup operations
		var showBackupsButton = new Button { Text = "Backups" };
		showBackupsButton.Click += OnShowBackups;

		var closeButton = new Button { Text = "Close" };
		closeButton.Click += (s, e) => Close();

		// Layout
		Content = new StackLayout {
			Spacing = 10,
			HorizontalContentAlignment = HorizontalAlignment.Stretch,
			Items = {
				// Toolbar row 1
				new StackLayoutItem(new StackLayout {
					Orientation = Orientation.Horizontal,
					Spacing = 5,
					Items = {
						newButton, editButton, deleteButton, duplicateButton,
						new Label { Text = "  " },
						moveUpButton, moveDownButton
					}
				}),
				// Toolbar row 2
				new StackLayoutItem(new StackLayout {
					Orientation = Orientation.Horizontal,
					Spacing = 5,
					Items = {
						loadButton, saveButton, saveAsButton,
						new Label { Text = "  " },
						importButton, exportButton,
						new Label { Text = "  " },
						showBackupsButton
					}
				}),
				// Grid
				new StackLayoutItem(_patternGrid, true),
				// Action buttons
				new StackLayoutItem(new StackLayout {
					Orientation = Orientation.Horizontal,
					Spacing = 10,
					Items = {
						searchButton, applyButton, applyAllButton,
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
		RefreshGrid();

		_patternManager.CollectionChanged += (s, e) => Application.Instance.AsyncInvoke(RefreshGrid);
	}

	private void RefreshGrid() {
		var items = _patternManager.CurrentCollection.Patterns
			.Select(p => new PatternGridItem(p))
			.ToList();
		_patternGrid.DataStore = items;
		UpdateTitle();
	}

	private void UpdateTitle() {
		var name = _patternManager.CurrentCollection.Name;
		var modified = _patternManager.HasUnsavedChanges ? " *" : "";
		Title = $"Pattern Manager - {name}{modified}";
	}

	private SearchReplacePattern? GetSelectedPattern() {
		if (_patternGrid.SelectedItem is PatternGridItem item) {
			return _patternManager.GetPattern(item.Id);
		}
		return null;
	}

	private void OnNewPattern(object? sender, EventArgs e) {
		var dialog = new PatternEditorDialog();
		var result = dialog.ShowModal(this);
		if (result != null) {
			_patternManager.AddPattern(result);
		}
	}

	private void OnEditPattern(object? sender, EventArgs e) {
		var pattern = GetSelectedPattern();
		if (pattern == null) {
			MessageBox.Show(this, "Please select a pattern to edit", "No Selection", MessageBoxType.Warning);
			return;
		}

		var dialog = new PatternEditorDialog(pattern);
		var result = dialog.ShowModal(this);
		if (result != null) {
			_patternManager.UpdatePattern(result);
		}
	}

	private void OnPatternDoubleClick(object? sender, GridCellMouseEventArgs e) {
		OnEditPattern(sender, e);
	}

	private void OnDeletePattern(object? sender, EventArgs e) {
		var pattern = GetSelectedPattern();
		if (pattern == null) {
			MessageBox.Show(this, "Please select a pattern to delete", "No Selection", MessageBoxType.Warning);
			return;
		}

		var result = MessageBox.Show(this,
			$"Are you sure you want to delete '{pattern.Name}'?",
			"Confirm Delete",
			MessageBoxButtons.YesNo,
			MessageBoxType.Question);

		if (result == DialogResult.Yes) {
			_patternManager.RemovePattern(pattern.Id);
		}
	}

	private void OnDuplicatePattern(object? sender, EventArgs e) {
		var pattern = GetSelectedPattern();
		if (pattern == null) {
			MessageBox.Show(this, "Please select a pattern to duplicate", "No Selection", MessageBoxType.Warning);
			return;
		}

		_patternManager.DuplicatePattern(pattern.Id);
	}

	private void OnMoveUp(object? sender, EventArgs e) {
		var pattern = GetSelectedPattern();
		if (pattern != null) {
			_patternManager.MovePatternUp(pattern.Id);
		}
	}

	private void OnMoveDown(object? sender, EventArgs e) {
		var pattern = GetSelectedPattern();
		if (pattern != null) {
			_patternManager.MovePatternDown(pattern.Id);
		}
	}

	private async void OnApplySelected(object? sender, EventArgs e) {
		if (_rootDirectory == null) {
			MessageBox.Show(this, "No directory available for pattern application", "Error", MessageBoxType.Error);
			return;
		}

		var pattern = GetSelectedPattern();
		if (pattern == null) {
			MessageBox.Show(this, "Please select a pattern to apply", "No Selection", MessageBoxType.Warning);
			return;
		}

		await ApplyPatternsAsync(new[] { pattern });
	}

	private async void OnApplyAll(object? sender, EventArgs e) {
		if (_rootDirectory == null) {
			MessageBox.Show(this, "No directory available for pattern application", "Error", MessageBoxType.Error);
			return;
		}

		var patterns = _patternManager.GetEnabledPatterns().ToList();
		if (patterns.Count == 0) {
			MessageBox.Show(this, "No enabled patterns to apply", "No Patterns", MessageBoxType.Warning);
			return;
		}

		await ApplyPatternsAsync(patterns);
	}

	private async Task ApplyPatternsAsync(IEnumerable<SearchReplacePattern> patterns) {
		var patternList = patterns.ToList();

		var confirm = MessageBox.Show(this,
			$"Apply {patternList.Count} pattern(s) to all files?\nThis operation will create a backup.",
			"Confirm Apply",
			MessageBoxButtons.YesNo,
			MessageBoxType.Question);

		if (confirm != DialogResult.Yes)
			return;

		_progressBar.Visible = true;
		_statusLabel.Text = "Applying patterns...";

		var cts = new CancellationTokenSource();
		_patternMatcher.ProgressChanged += OnProgressChanged;

		try {
			// Start backup session
			_backupManager.StartSession($"Pattern application - {DateTime.Now:yyyy-MM-dd HH:mm}");

			var results = await _patternMatcher.ApplyPatternsToDirectoryAsync(
				patternList, _rootDirectory!, true, cts.Token);

			// End backup session
			_backupManager.EndSession();

			// Show results
			var totalReplacements = results.Sum(r => r.ReplacementCount);
			var failures = results.Count(r => !r.Success);
			var filesModified = results.Where(r => r.ReplacementCount > 0).Select(r => r.FilePath).Distinct().Count();

			var message = $"Applied {patternList.Count} pattern(s):\n" +
				$"- Files modified: {filesModified}\n" +
				$"- Total replacements: {totalReplacements}\n" +
				$"- Failures: {failures}";

			MessageBox.Show(this, message, "Apply Complete", MessageBoxType.Information);
		} catch (OperationCanceledException) {
			_statusLabel.Text = "Operation cancelled";
		} catch (Exception ex) {
			MessageBox.Show(this, $"Error applying patterns: {ex.Message}", "Error", MessageBoxType.Error);
		} finally {
			_patternMatcher.ProgressChanged -= OnProgressChanged;
			_progressBar.Visible = false;
			_statusLabel.Text = "Ready";
		}
	}

	private void OnSearch(object? sender, EventArgs e) {
		if (_rootDirectory == null) {
			MessageBox.Show(this, "No directory available for searching", "Error", MessageBoxType.Error);
			return;
		}

		var pattern = GetSelectedPattern();
		if (pattern == null) {
			MessageBox.Show(this, "Please select a pattern to search for", "No Selection", MessageBoxType.Warning);
			return;
		}

		_progressBar.Visible = true;
		_statusLabel.Text = "Searching...";
		_patternMatcher.ProgressChanged += OnProgressChanged;

		try {
			var matches = _patternMatcher.Search(pattern, _rootDirectory);

			var message = $"Found {matches.Count} match(es):\n\n" +
				string.Join("\n", matches.Take(20).Select(m => $"{m.FilePath} @ {m.Position}"));

			if (matches.Count > 20) {
				message += $"\n\n... and {matches.Count - 20} more";
			}

			MessageBox.Show(this, message, "Search Results", MessageBoxType.Information);
		} finally {
			_patternMatcher.ProgressChanged -= OnProgressChanged;
			_progressBar.Visible = false;
			_statusLabel.Text = "Ready";
		}
	}

	private void OnProgressChanged(int current, int total, string file) {
		Application.Instance.AsyncInvoke(() => {
			_progressBar.MaxValue = total;
			_progressBar.Value = current;
			_statusLabel.Text = $"Processing: {Path.GetFileName(file)}";
		});
	}

	private void OnLoad(object? sender, EventArgs e) {
		using var ofd = new OpenFileDialog {
			Directory = new Uri(PatternManager.DefaultPatternsDirectory),
			Filters = { new FileFilter("Pattern Files", "*.json") }
		};

		if (ofd.ShowDialog(this) == DialogResult.Ok) {
			try {
				_patternManager.Load(ofd.FileName);
				RefreshGrid();
			} catch (Exception ex) {
				MessageBox.Show(this, $"Failed to load patterns: {ex.Message}", "Error", MessageBoxType.Error);
			}
		}
	}

	private void OnSave(object? sender, EventArgs e) {
		if (_patternManager.CurrentFilePath == null) {
			OnSaveAs(sender, e);
			return;
		}

		try {
			_patternManager.Save();
			UpdateTitle();
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to save patterns: {ex.Message}", "Error", MessageBoxType.Error);
		}
	}

	private void OnSaveAs(object? sender, EventArgs e) {
		using var sfd = new SaveFileDialog {
			Directory = new Uri(PatternManager.DefaultPatternsDirectory),
			FileName = _patternManager.CurrentCollection.Name + ".json",
			Filters = { new FileFilter("Pattern Files", "*.json") }
		};

		if (sfd.ShowDialog(this) == DialogResult.Ok) {
			try {
				_patternManager.Save(sfd.FileName);
				UpdateTitle();
			} catch (Exception ex) {
				MessageBox.Show(this, $"Failed to save patterns: {ex.Message}", "Error", MessageBoxType.Error);
			}
		}
	}

	private void OnImport(object? sender, EventArgs e) {
		using var ofd = new OpenFileDialog {
			Filters = { new FileFilter("Pattern Files", "*.json") }
		};

		if (ofd.ShowDialog(this) == DialogResult.Ok) {
			try {
				var count = _patternManager.Import(ofd.FileName);
				MessageBox.Show(this, $"Imported {count} pattern(s)", "Import Complete", MessageBoxType.Information);
			} catch (Exception ex) {
				MessageBox.Show(this, $"Failed to import patterns: {ex.Message}", "Error", MessageBoxType.Error);
			}
		}
	}

	private void OnExport(object? sender, EventArgs e) {
		var selectedIds = _patternGrid.SelectedRows
			.Select(i => ((PatternGridItem)_patternGrid.DataStore.ElementAt(i)).Id)
			.ToList();

		if (selectedIds.Count == 0) {
			selectedIds = null; // Export all
		}

		using var sfd = new SaveFileDialog {
			FileName = "exported_patterns.json",
			Filters = { new FileFilter("Pattern Files", "*.json") }
		};

		if (sfd.ShowDialog(this) == DialogResult.Ok) {
			try {
				_patternManager.Export(sfd.FileName, selectedIds);
				MessageBox.Show(this, "Patterns exported successfully", "Export Complete", MessageBoxType.Information);
			} catch (Exception ex) {
				MessageBox.Show(this, $"Failed to export patterns: {ex.Message}", "Error", MessageBoxType.Error);
			}
		}
	}

	private void OnShowBackups(object? sender, EventArgs e) {
		var dialog = new BackupManagerDialog(_backupManager, _patternMatcher, _fileResolver);
		dialog.ShowModal(this);
	}

	/// <summary>
	/// Grid item for displaying patterns.
	/// </summary>
	private sealed class PatternGridItem {
		public PatternGridItem(SearchReplacePattern pattern) {
			Id = pattern.Id;
			Name = pattern.Name;
			Description = pattern.Description;
			Type = pattern.Type.ToString();
			FilePattern = pattern.FilePattern;
			IsEnabled = pattern.IsEnabled;
		}

		public string Id { get; }
		public string Name { get; }
		public string Description { get; }
		public string Type { get; }
		public string FilePattern { get; }
		public bool? IsEnabled { get; }
	}
}
