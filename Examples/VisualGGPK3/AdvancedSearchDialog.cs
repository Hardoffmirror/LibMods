using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Eto.Drawing;
using Eto.Forms;

using VisualGGPK3.TreeItems;

namespace VisualGGPK3;

/// <summary>
/// Диалог расширенного поиска файлов с фильтрами.
/// </summary>
public sealed class AdvancedSearchDialog : Dialog {
	private readonly DirectoryTreeItem? _rootDirectory;
	private readonly Action<FileTreeItem>? _onFileSelected;

	private readonly TextBox _searchTextBox;
	private readonly TextBox _extensionFilterBox;
	private readonly TextBox _folderFilterBox;
	private readonly DropDown _typeFilter;
	private readonly CheckBox _searchInContent;
	private readonly CheckBox _searchInFileName;
	private readonly CheckBox _caseSensitive;
	private readonly CheckBox _useRegex;
	private readonly GridView _resultsGrid;
	private readonly Label _statusLabel;
	private readonly ProgressBar _progressBar;

	private CancellationTokenSource? _searchCts;
	private List<SearchResultItem> _results = new();

	public AdvancedSearchDialog(DirectoryTreeItem? rootDirectory, Action<FileTreeItem>? onFileSelected = null) {
		_rootDirectory = rootDirectory;
		_onFileSelected = onFileSelected;

		Title = L.AdvancedSearch;
		MinimumSize = new Size(900, 700);
		Padding = new Padding(10);

		// Search controls
		_searchTextBox = new TextBox { PlaceholderText = L.SearchText.TrimEnd(':') };
		_extensionFilterBox = new TextBox { PlaceholderText = L.ExtensionPlaceholder };
		_folderFilterBox = new TextBox { PlaceholderText = L.FolderPlaceholder };

		_typeFilter = new DropDown {
			Items = {
				new ListItem { Text = L.AllTypes, Key = "all" },
				new ListItem { Text = L.Images, Key = "images" },
				new ListItem { Text = L.TextFiles, Key = "text" },
				new ListItem { Text = L.DataFiles, Key = "data" },
				new ListItem { Text = L.AudioFiles, Key = "audio" },
				new ListItem { Text = L.VideoFiles, Key = "video" },
				new ListItem { Text = L.OtherFiles, Key = "other" }
			},
			SelectedIndex = 0
		};

		_searchInContent = new CheckBox { Text = L.SearchInContent, Checked = false };
		_searchInFileName = new CheckBox { Text = L.SearchInFileName, Checked = true };
		_caseSensitive = new CheckBox { Text = L.CaseSensitive, Checked = false };
		_useRegex = new CheckBox { Text = L.UseRegex, Checked = false };

		// Results grid
		_resultsGrid = new GridView {
			AllowMultipleSelection = true,
			GridLines = GridLines.Both
		};

		_resultsGrid.Columns.Add(new GridColumn {
			HeaderText = L.ColName,
			DataCell = new TextBoxCell { Binding = Binding.Property<SearchResultItem, string>(i => i.Name) },
			Width = 200
		});

		_resultsGrid.Columns.Add(new GridColumn {
			HeaderText = L.ColType,
			DataCell = new TextBoxCell { Binding = Binding.Property<SearchResultItem, string>(i => i.Type) },
			Width = 80
		});

		_resultsGrid.Columns.Add(new GridColumn {
			HeaderText = L.ColSize,
			DataCell = new TextBoxCell { Binding = Binding.Property<SearchResultItem, string>(i => i.Size) },
			Width = 80
		});

		_resultsGrid.Columns.Add(new GridColumn {
			HeaderText = L.ColPath,
			DataCell = new TextBoxCell { Binding = Binding.Property<SearchResultItem, string>(i => i.Path) },
			Width = 400
		});

		_resultsGrid.CellDoubleClick += OnResultDoubleClick;

		// Status
		_statusLabel = new Label { Text = L.Ready };
		_progressBar = new ProgressBar { Visible = false };

		// Buttons
		var searchButton = new Button { Text = L.Search };
		searchButton.Click += OnSearch;

		var clearButton = new Button { Text = L.ClearResults };
		clearButton.Click += OnClear;

		var goToButton = new Button { Text = L.GoToFile };
		goToButton.Click += OnGoToFile;

		var copyPathButton = new Button { Text = L.CopyPathToClipboard };
		copyPathButton.Click += OnCopyPath;

		var extractButton = new Button { Text = L.ExtractSelected };
		extractButton.Click += OnExtract;

		var closeButton = new Button { Text = L.Close };
		closeButton.Click += (s, e) => Close();

		// Layout
		Content = new StackLayout {
			Spacing = 10,
			HorizontalContentAlignment = HorizontalAlignment.Stretch,
			Items = {
				// Search text
				new StackLayoutItem(new StackLayout {
					Orientation = Orientation.Horizontal,
					Spacing = 5,
					Items = {
						new Label { Text = L.SearchText, VerticalAlignment = VerticalAlignment.Center },
						new StackLayoutItem(_searchTextBox, true),
						searchButton
					}
				}),
				// Filters row 1
				new StackLayoutItem(new StackLayout {
					Orientation = Orientation.Horizontal,
					Spacing = 10,
					Items = {
						new Label { Text = L.FilterByExtension, VerticalAlignment = VerticalAlignment.Center },
						new StackLayoutItem(_extensionFilterBox) { Expand = true },
						new Label { Text = L.FilterByType, VerticalAlignment = VerticalAlignment.Center },
						_typeFilter
					}
				}),
				// Filters row 2
				new StackLayoutItem(new StackLayout {
					Orientation = Orientation.Horizontal,
					Spacing = 10,
					Items = {
						new Label { Text = L.FilterByFolder, VerticalAlignment = VerticalAlignment.Center },
						new StackLayoutItem(_folderFilterBox, true)
					}
				}),
				// Checkboxes
				new StackLayoutItem(new StackLayout {
					Orientation = Orientation.Horizontal,
					Spacing = 15,
					Items = {
						_searchInFileName,
						_searchInContent,
						_caseSensitive,
						_useRegex
					}
				}),
				// Results
				new StackLayoutItem(_resultsGrid, true),
				// Action buttons
				new StackLayoutItem(new StackLayout {
					Orientation = Orientation.Horizontal,
					Spacing = 10,
					Items = {
						goToButton,
						copyPathButton,
						extractButton,
						clearButton,
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

	private async void OnSearch(object? sender, EventArgs e) {
		if (_rootDirectory == null) {
			MessageBox.Show(this, L.NoDirectoryForSearch, L.Error, MessageBoxType.Error);
			return;
		}

		// Cancel previous search
		_searchCts?.Cancel();
		_searchCts = new CancellationTokenSource();
		var token = _searchCts.Token;

		_progressBar.Visible = true;
		_statusLabel.Text = L.SearchInProgress;
		_results.Clear();

		try {
			var searchText = _searchTextBox.Text;
			var extensions = _extensionFilterBox.Text
				.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(e => e.Trim().ToLowerInvariant())
				.Where(e => !string.IsNullOrEmpty(e))
				.Select(e => e.StartsWith('.') ? e : "." + e)
				.ToHashSet();

			var folderFilter = _folderFilterBox.Text?.Trim();
			var typeKey = _typeFilter.SelectedKey;
			var searchInContent = _searchInContent.Checked ?? false;
			var searchInFileName = _searchInFileName.Checked ?? true;
			var caseSensitive = _caseSensitive.Checked ?? false;
			var useRegex = _useRegex.Checked ?? false;

			Regex? regex = null;
			if (!string.IsNullOrEmpty(searchText) && useRegex) {
				var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
				regex = new Regex(searchText, options);
			}

			var allFiles = new List<FileTreeItem>();
			await Task.Run(() => CollectFiles(_rootDirectory, allFiles, token), token);

			_progressBar.MaxValue = allFiles.Count;
			var count = 0;

			foreach (var file in allFiles) {
				if (token.IsCancellationRequested)
					break;

				Application.Instance.AsyncInvoke(() => {
					_progressBar.Value = count;
					_statusLabel.Text = string.Format(L.Processing, file.Name);
				});

				var matches = MatchFile(file, searchText, extensions, folderFilter, typeKey,
					searchInFileName, searchInContent, caseSensitive, regex);

				if (matches) {
					_results.Add(new SearchResultItem(file));
				}

				count++;
			}

			Application.Instance.AsyncInvoke(() => {
				_resultsGrid.DataStore = _results;
				_statusLabel.Text = string.Format(L.SearchResultsFound, _results.Count);
				_progressBar.Visible = false;
			});
		} catch (OperationCanceledException) {
			_statusLabel.Text = L.OperationCancelled;
		} catch (Exception ex) {
			MessageBox.Show(this, ex.Message, L.Error, MessageBoxType.Error);
			_statusLabel.Text = L.Ready;
		} finally {
			_progressBar.Visible = false;
		}
	}

	private void CollectFiles(DirectoryTreeItem dir, List<FileTreeItem> files, CancellationToken token) {
		if (token.IsCancellationRequested)
			return;

		// Ensure directory is expanded to load children
		if (!dir.Initialized) {
			dir.Expanded = true;
		}

		foreach (var child in dir.ChildItems) {
			if (token.IsCancellationRequested)
				return;

			if (child is FileTreeItem file) {
				files.Add(file);
			} else if (child is DirectoryTreeItem subDir) {
				CollectFiles(subDir, files, token);
			}
		}
	}

	private bool MatchFile(FileTreeItem file, string searchText, HashSet<string> extensions,
		string? folderFilter, string typeKey, bool searchInFileName, bool searchInContent,
		bool caseSensitive, Regex? regex) {

		// Extension filter
		if (extensions.Count > 0) {
			var ext = Path.GetExtension(file.Name).ToLowerInvariant();
			if (!extensions.Contains(ext))
				return false;
		}

		// Type filter
		if (!MatchesType(file, typeKey))
			return false;

		// Folder filter
		if (!string.IsNullOrEmpty(folderFilter)) {
			var path = file.GetPath();
			var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
			if (!path.Contains(folderFilter, comparison))
				return false;
		}

		// Search text
		if (!string.IsNullOrEmpty(searchText)) {
			var foundInName = false;
			var foundInContent = false;

			if (searchInFileName) {
				if (regex != null) {
					foundInName = regex.IsMatch(file.Name);
				} else {
					var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
					foundInName = file.Name.Contains(searchText, comparison);
				}
			}

			if (searchInContent && !foundInName) {
				try {
					var data = file.Read();
					if (data.Length > 0 && data.Length < 10 * 1024 * 1024) { // Max 10MB
						var text = Encoding.UTF8.GetString(data.Span);
						if (regex != null) {
							foundInContent = regex.IsMatch(text);
						} else {
							var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
							foundInContent = text.Contains(searchText, comparison);
						}
					}
				} catch {
					// Ignore read errors
				}
			}

			if (!foundInName && !foundInContent)
				return false;
		}

		return true;
	}

	private static bool MatchesType(FileTreeItem file, string typeKey) {
		if (typeKey == "all")
			return true;

		var format = file.Format;

		return typeKey switch {
			"images" => format == FileTreeItem.DataFormat.Image || format == FileTreeItem.DataFormat.DdsImage,
			"text" => format == FileTreeItem.DataFormat.Text,
			"data" => format == FileTreeItem.DataFormat.Dat,
			"audio" => file.Name.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
					   file.Name.EndsWith(".bank", StringComparison.OrdinalIgnoreCase) ||
					   file.Name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
					   file.Name.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase),
			"video" => file.Name.EndsWith(".bk2", StringComparison.OrdinalIgnoreCase) ||
					   file.Name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase),
			"other" => format == FileTreeItem.DataFormat.Unknown,
			_ => true
		};
	}

	private void OnResultDoubleClick(object? sender, GridCellMouseEventArgs e) {
		OnGoToFile(sender, e);
	}

	private void OnGoToFile(object? sender, EventArgs e) {
		if (_resultsGrid.SelectedItem is SearchResultItem item && _onFileSelected != null) {
			_onFileSelected(item.FileItem);
			Close();
		}
	}

	private void OnCopyPath(object? sender, EventArgs e) {
		var paths = _resultsGrid.SelectedRows
			.Select(i => ((SearchResultItem)_resultsGrid.DataStore.ElementAt(i)).Path)
			.ToList();

		if (paths.Count > 0) {
			Clipboard.Instance.Text = string.Join("\n", paths);
		}
	}

	private void OnExtract(object? sender, EventArgs e) {
		var selectedItems = _resultsGrid.SelectedRows
			.Select(i => ((SearchResultItem)_resultsGrid.DataStore.ElementAt(i)).FileItem)
			.ToList();

		if (selectedItems.Count == 0) {
			MessageBox.Show(this, L.NoSelection, L.Warning, MessageBoxType.Warning);
			return;
		}

		using var sfd = new SaveFileDialog {
			CheckFileExists = false,
			FileName = "{OPEN IN A FOLDER}",
			Filters = { new FileFilter(L.AllFiles, "*") }
		};

		if (sfd.ShowDialog(this) != DialogResult.Ok)
			return;

		var dir = Path.GetDirectoryName(sfd.FileName)!;
		var count = 0;

		foreach (var file in selectedItems) {
			try {
				var filePath = Path.Combine(dir, file.GetPath());
				Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
				var data = file.Read().Span;
				using var handle = File.OpenHandle(filePath, FileMode.Create, FileAccess.Write,
					FileShare.None, FileOptions.None, data.Length);
				RandomAccess.Write(handle, data, 0);
				count++;
			} catch {
				// Continue with other files
			}
		}

		MessageBox.Show(this, string.Format(L.ExtractedFiles, count, dir), L.Done, MessageBoxType.Information);
	}

	private void OnClear(object? sender, EventArgs e) {
		_results.Clear();
		_resultsGrid.DataStore = _results;
		_statusLabel.Text = L.Ready;
	}

	private sealed class SearchResultItem {
		public SearchResultItem(FileTreeItem file) {
			FileItem = file;
			Name = file.Name;
			Path = file.GetPath();
			Type = file.Format.ToString();

			try {
				var size = file.Read().Length;
				Size = size switch {
					< 1024 => $"{size} B",
					< 1024 * 1024 => $"{size / 1024.0:F1} KB",
					_ => $"{size / (1024.0 * 1024.0):F1} MB"
				};
			} catch {
				Size = "?";
			}
		}

		public FileTreeItem FileItem { get; }
		public string Name { get; }
		public string Path { get; }
		public string Type { get; }
		public string Size { get; }
	}
}
