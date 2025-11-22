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

		Title = L.PatternManager;
		MinimumSize = new Size(800, 600);
		Padding = new Padding(10);

		// Pattern grid
		_patternGrid = new GridView {
			AllowMultipleSelection = true,
			GridLines = GridLines.Both
		};

		_patternGrid.Columns.Add(new GridColumn {
			HeaderText = L.ColEnabled,
			DataCell = new CheckBoxCell { Binding = Binding.Property<PatternGridItem, bool?>(i => i.IsEnabled) },
			Width = 60
		});

		_patternGrid.Columns.Add(new GridColumn {
			HeaderText = L.ColName,
			DataCell = new TextBoxCell { Binding = Binding.Property<PatternGridItem, string>(i => i.Name) },
			Width = 150
		});

		_patternGrid.Columns.Add(new GridColumn {
			HeaderText = L.ColType,
			DataCell = new TextBoxCell { Binding = Binding.Property<PatternGridItem, string>(i => i.Type) },
			Width = 80
		});

		_patternGrid.Columns.Add(new GridColumn {
			HeaderText = L.ColFilePattern,
			DataCell = new TextBoxCell { Binding = Binding.Property<PatternGridItem, string>(i => i.FilePattern) },
			Width = 120
		});

		_patternGrid.Columns.Add(new GridColumn {
			HeaderText = L.ColDescription,
			DataCell = new TextBoxCell { Binding = Binding.Property<PatternGridItem, string>(i => i.Description) },
			Width = 200
		});

		_patternGrid.CellDoubleClick += OnPatternDoubleClick;

		// Status bar
		_statusLabel = new Label { Text = L.Ready };
		_progressBar = new ProgressBar { Visible = false };

		// Toolbar buttons
		var newButton = new Button { Text = L.New };
		newButton.Click += OnNewPattern;

		var editButton = new Button { Text = L.Edit };
		editButton.Click += OnEditPattern;

		var deleteButton = new Button { Text = L.Delete };
		deleteButton.Click += OnDeletePattern;

		var duplicateButton = new Button { Text = L.Duplicate };
		duplicateButton.Click += OnDuplicatePattern;

		var moveUpButton = new Button { Text = L.Up };
		moveUpButton.Click += OnMoveUp;

		var moveDownButton = new Button { Text = L.Down };
		moveDownButton.Click += OnMoveDown;

		var applyButton = new Button { Text = L.ApplySelected };
		applyButton.Click += OnApplySelected;

		var applyAllButton = new Button { Text = L.ApplyAllEnabled };
		applyAllButton.Click += OnApplyAll;

		var searchButton = new Button { Text = L.Search };
		searchButton.Click += OnSearch;

		// File operations
		var loadButton = new Button { Text = L.Load };
		loadButton.Click += OnLoad;

		var saveButton = new Button { Text = L.Save };
		saveButton.Click += OnSave;

		var saveAsButton = new Button { Text = L.SaveAs };
		saveAsButton.Click += OnSaveAs;

		var importButton = new Button { Text = L.Import };
		importButton.Click += OnImport;

		var exportButton = new Button { Text = L.Export };
		exportButton.Click += OnExport;

		// Backup operations
		var showBackupsButton = new Button { Text = L.Backups };
		showBackupsButton.Click += OnShowBackups;

		var closeButton = new Button { Text = L.Close };
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
			MessageBox.Show(this, L.SelectPatternToEdit, L.NoSelection, MessageBoxType.Warning);
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
			MessageBox.Show(this, L.SelectPatternToDelete, L.NoSelection, MessageBoxType.Warning);
			return;
		}

		var result = MessageBox.Show(this,
			string.Format(L.ConfirmDeletePattern, pattern.Name),
			L.ConfirmDelete,
			MessageBoxButtons.YesNo,
			MessageBoxType.Question);

		if (result == DialogResult.Yes) {
			_patternManager.RemovePattern(pattern.Id);
		}
	}

	private void OnDuplicatePattern(object? sender, EventArgs e) {
		var pattern = GetSelectedPattern();
		if (pattern == null) {
			MessageBox.Show(this, L.SelectPatternToDuplicate, L.NoSelection, MessageBoxType.Warning);
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
			MessageBox.Show(this, L.NoDirectoryAvailable, L.Error, MessageBoxType.Error);
			return;
		}

		var pattern = GetSelectedPattern();
		if (pattern == null) {
			MessageBox.Show(this, L.SelectPatternToApply, L.NoSelection, MessageBoxType.Warning);
			return;
		}

		await ApplyPatternsAsync(new[] { pattern });
	}

	private async void OnApplyAll(object? sender, EventArgs e) {
		if (_rootDirectory == null) {
			MessageBox.Show(this, L.NoDirectoryAvailable, L.Error, MessageBoxType.Error);
			return;
		}

		var patterns = _patternManager.GetEnabledPatterns().ToList();
		if (patterns.Count == 0) {
			MessageBox.Show(this, L.NoEnabledPatterns, L.NoPatterns, MessageBoxType.Warning);
			return;
		}

		await ApplyPatternsAsync(patterns);
	}

	private async Task ApplyPatternsAsync(IEnumerable<SearchReplacePattern> patterns) {
		var patternList = patterns.ToList();

		var confirm = MessageBox.Show(this,
			string.Format(L.ConfirmApplyPatterns, patternList.Count),
			L.ConfirmApply,
			MessageBoxButtons.YesNo,
			MessageBoxType.Question);

		if (confirm != DialogResult.Yes)
			return;

		_progressBar.Visible = true;
		_statusLabel.Text = L.ApplyingPatterns;

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

			var message = string.Format(L.ApplyResultFormat, patternList.Count, filesModified, totalReplacements, failures);

			MessageBox.Show(this, message, L.ApplyComplete, MessageBoxType.Information);
		} catch (OperationCanceledException) {
			_statusLabel.Text = L.OperationCancelled;
		} catch (Exception ex) {
			MessageBox.Show(this, string.Format(L.ErrorApplyingPatterns, ex.Message), L.Error, MessageBoxType.Error);
		} finally {
			_patternMatcher.ProgressChanged -= OnProgressChanged;
			_progressBar.Visible = false;
			_statusLabel.Text = L.Ready;
		}
	}

	private void OnSearch(object? sender, EventArgs e) {
		if (_rootDirectory == null) {
			MessageBox.Show(this, L.NoDirectoryForSearch, L.Error, MessageBoxType.Error);
			return;
		}

		var pattern = GetSelectedPattern();
		if (pattern == null) {
			MessageBox.Show(this, L.SelectPatternToSearch, L.NoSelection, MessageBoxType.Warning);
			return;
		}

		_progressBar.Visible = true;
		_statusLabel.Text = L.Searching;
		_patternMatcher.ProgressChanged += OnProgressChanged;

		try {
			var matches = _patternMatcher.Search(pattern, _rootDirectory);

			var matchList = string.Join("\n", matches.Take(20).Select(m => $"{m.FilePath} @ {m.Position}"));
			var message = string.Format(L.FoundMatches, matches.Count, matchList);

			if (matches.Count > 20) {
				message += string.Format(L.AndMore, matches.Count - 20);
			}

			MessageBox.Show(this, message, L.SearchResults, MessageBoxType.Information);
		} finally {
			_patternMatcher.ProgressChanged -= OnProgressChanged;
			_progressBar.Visible = false;
			_statusLabel.Text = L.Ready;
		}
	}

	private void OnProgressChanged(int current, int total, string file) {
		Application.Instance.AsyncInvoke(() => {
			_progressBar.MaxValue = total;
			_progressBar.Value = current;
			_statusLabel.Text = string.Format(L.Processing, Path.GetFileName(file));
		});
	}

	private void OnLoad(object? sender, EventArgs e) {
		using var ofd = new OpenFileDialog {
			Directory = new Uri(PatternManager.DefaultPatternsDirectory),
			Filters = { new FileFilter(L.PatternFiles, "*.json") }
		};

		if (ofd.ShowDialog(this) == DialogResult.Ok) {
			try {
				_patternManager.Load(ofd.FileName);
				RefreshGrid();
			} catch (Exception ex) {
				MessageBox.Show(this, string.Format(L.FailedToLoad, ex.Message), L.Error, MessageBoxType.Error);
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
			MessageBox.Show(this, string.Format(L.FailedToSave, ex.Message), L.Error, MessageBoxType.Error);
		}
	}

	private void OnSaveAs(object? sender, EventArgs e) {
		using var sfd = new SaveFileDialog {
			Directory = new Uri(PatternManager.DefaultPatternsDirectory),
			FileName = _patternManager.CurrentCollection.Name + ".json",
			Filters = { new FileFilter(L.PatternFiles, "*.json") }
		};

		if (sfd.ShowDialog(this) == DialogResult.Ok) {
			try {
				_patternManager.Save(sfd.FileName);
				UpdateTitle();
			} catch (Exception ex) {
				MessageBox.Show(this, string.Format(L.FailedToSave, ex.Message), L.Error, MessageBoxType.Error);
			}
		}
	}

	private void OnImport(object? sender, EventArgs e) {
		using var ofd = new OpenFileDialog {
			Filters = { new FileFilter(L.PatternFiles, "*.json") }
		};

		if (ofd.ShowDialog(this) == DialogResult.Ok) {
			try {
				var count = _patternManager.Import(ofd.FileName);
				MessageBox.Show(this, string.Format(L.ImportedPatterns, count), L.ImportComplete, MessageBoxType.Information);
			} catch (Exception ex) {
				MessageBox.Show(this, string.Format(L.FailedToImport, ex.Message), L.Error, MessageBoxType.Error);
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
			Filters = { new FileFilter(L.PatternFiles, "*.json") }
		};

		if (sfd.ShowDialog(this) == DialogResult.Ok) {
			try {
				_patternManager.Export(sfd.FileName, selectedIds);
				MessageBox.Show(this, L.PatternsExported, L.ExportComplete, MessageBoxType.Information);
			} catch (Exception ex) {
				MessageBox.Show(this, string.Format(L.FailedToExport, ex.Message), L.Error, MessageBoxType.Error);
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
