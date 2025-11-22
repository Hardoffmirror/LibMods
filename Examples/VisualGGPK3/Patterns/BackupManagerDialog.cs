using System;
using System.Linq;

using Eto.Drawing;
using Eto.Forms;

using VisualGGPK3.TreeItems;

namespace VisualGGPK3.Patterns;

/// <summary>
/// Dialog for managing backups and restoring files.
/// </summary>
public sealed class BackupManagerDialog : Dialog {
	private readonly BackupManager _backupManager;
	private readonly PatternMatcher _patternMatcher;
	private readonly Func<string, FileTreeItem?>? _fileResolver;
	private readonly GridView _sessionGrid;
	private readonly GridView _entryGrid;
	private readonly Label _statusLabel;

	public BackupManagerDialog(
		BackupManager backupManager,
		PatternMatcher patternMatcher,
		Func<string, FileTreeItem?>? fileResolver) {
		_backupManager = backupManager;
		_patternMatcher = patternMatcher;
		_fileResolver = fileResolver;

		Title = L.BackupManager;
		MinimumSize = new Size(700, 500);
		Padding = new Padding(10);

		// Session grid
		_sessionGrid = new GridView {
			AllowMultipleSelection = false
		};

		_sessionGrid.Columns.Add(new GridColumn {
			HeaderText = L.ColName,
			DataCell = new TextBoxCell { Binding = Binding.Property<SessionGridItem, string>(i => i.Name) },
			Width = 200
		});

		_sessionGrid.Columns.Add(new GridColumn {
			HeaderText = L.ColDate,
			DataCell = new TextBoxCell { Binding = Binding.Property<SessionGridItem, string>(i => i.Date) },
			Width = 150
		});

		_sessionGrid.Columns.Add(new GridColumn {
			HeaderText = L.ColFiles,
			DataCell = new TextBoxCell { Binding = Binding.Property<SessionGridItem, string>(i => i.FileCount) },
			Width = 60
		});

		_sessionGrid.SelectionChanged += OnSessionSelectionChanged;

		// Entry grid
		_entryGrid = new GridView {
			AllowMultipleSelection = true
		};

		_entryGrid.Columns.Add(new GridColumn {
			HeaderText = L.ColFilePath,
			DataCell = new TextBoxCell { Binding = Binding.Property<EntryGridItem, string>(i => i.FilePath) },
			Width = 300
		});

		_entryGrid.Columns.Add(new GridColumn {
			HeaderText = L.ColBackupTime,
			DataCell = new TextBoxCell { Binding = Binding.Property<EntryGridItem, string>(i => i.BackupTime) },
			Width = 150
		});

		// Buttons
		var restoreSessionButton = new Button { Text = L.RestoreSession };
		restoreSessionButton.Click += OnRestoreSession;

		var restoreSelectedButton = new Button { Text = L.RestoreSelected };
		restoreSelectedButton.Click += OnRestoreSelected;

		var deleteSessionButton = new Button { Text = L.DeleteSession };
		deleteSessionButton.Click += OnDeleteSession;

		var cleanupButton = new Button { Text = L.CleanupOld };
		cleanupButton.Click += OnCleanup;

		var closeButton = new Button { Text = L.Close };
		closeButton.Click += (s, e) => Close();

		_statusLabel = new Label { Text = GetStatusText() };

		// Layout
		Content = new StackLayout {
			Spacing = 10,
			HorizontalContentAlignment = HorizontalAlignment.Stretch,
			Items = {
				new StackLayoutItem(new Label { Text = L.BackupSessions, Font = SystemFonts.Bold() }),
				new StackLayoutItem(new Splitter {
					Panel1 = _sessionGrid,
					Panel1MinimumSize = 150,
					Panel2 = _entryGrid,
					Panel2MinimumSize = 150,
					Orientation = Orientation.Vertical,
					Position = 150
				}, true),
				new StackLayoutItem(new StackLayout {
					Orientation = Orientation.Horizontal,
					Spacing = 10,
					Items = {
						restoreSessionButton,
						restoreSelectedButton,
						deleteSessionButton,
						cleanupButton,
						new StackLayoutItem(null, true),
						closeButton
					}
				}),
				new StackLayoutItem(_statusLabel)
			}
		};

		AbortButton = closeButton;
		RefreshSessions();
	}

	private void RefreshSessions() {
		var sessions = _backupManager.GetAllSessions()
			.Select(s => new SessionGridItem(s))
			.ToList();
		_sessionGrid.DataStore = sessions;
		_statusLabel.Text = GetStatusText();
	}

	private void RefreshEntries(string sessionId) {
		var session = _backupManager.GetSession(sessionId);
		if (session == null) {
			_entryGrid.DataStore = null;
			return;
		}

		var entries = session.Entries
			.Select(e => new EntryGridItem(e))
			.ToList();
		_entryGrid.DataStore = entries;
	}

	private string GetStatusText() {
		var size = _backupManager.GetTotalBackupSize();
		var sizeStr = size switch {
			< 1024 => $"{size} B",
			< 1024 * 1024 => $"{size / 1024.0:F1} KB",
			_ => $"{size / (1024.0 * 1024.0):F1} MB"
		};
		return string.Format(L.TotalBackupSize, sizeStr);
	}

	private void OnSessionSelectionChanged(object? sender, EventArgs e) {
		if (_sessionGrid.SelectedItem is SessionGridItem item) {
			RefreshEntries(item.Id);
		} else {
			_entryGrid.DataStore = null;
		}
	}

	private void OnRestoreSession(object? sender, EventArgs e) {
		if (_fileResolver == null) {
			MessageBox.Show(this, L.FileResolverNotAvailable, L.Error, MessageBoxType.Error);
			return;
		}

		if (_sessionGrid.SelectedItem is not SessionGridItem item) {
			MessageBox.Show(this, L.SelectSessionToRestore, L.NoSelection, MessageBoxType.Warning);
			return;
		}

		var session = _backupManager.GetSession(item.Id);
		if (session == null) {
			MessageBox.Show(this, L.SessionNotFound, L.Error, MessageBoxType.Error);
			return;
		}

		var confirm = MessageBox.Show(this,
			string.Format(L.ConfirmRestoreSession, session.Entries.Count, session.Name),
			L.ConfirmRestore,
			MessageBoxButtons.YesNo,
			MessageBoxType.Question);

		if (confirm != DialogResult.Yes)
			return;

		var restored = _patternMatcher.RestoreSession(session, _fileResolver);
		MessageBox.Show(this,
			string.Format(L.RestoredFiles, restored, session.Entries.Count),
			L.RestoreComplete,
			MessageBoxType.Information);
	}

	private void OnRestoreSelected(object? sender, EventArgs e) {
		if (_fileResolver == null) {
			MessageBox.Show(this, L.FileResolverNotAvailable, L.Error, MessageBoxType.Error);
			return;
		}

		if (_sessionGrid.SelectedItem is not SessionGridItem sessionItem) {
			MessageBox.Show(this, L.SelectSessionFirst, L.NoSelection, MessageBoxType.Warning);
			return;
		}

		var session = _backupManager.GetSession(sessionItem.Id);
		if (session == null) {
			MessageBox.Show(this, L.SessionNotFound, L.Error, MessageBoxType.Error);
			return;
		}

		var selectedIndices = _entryGrid.SelectedRows.ToList();
		if (selectedIndices.Count == 0) {
			MessageBox.Show(this, L.SelectFilesToRestore, L.NoSelection, MessageBoxType.Warning);
			return;
		}

		int restored = 0;
		foreach (var index in selectedIndices) {
			if (index < session.Entries.Count) {
				var entry = session.Entries[index];
				var fileItem = _fileResolver(entry.FilePath);
				if (fileItem != null && _patternMatcher.RestoreFromBackup(entry, fileItem)) {
					restored++;
				}
			}
		}

		MessageBox.Show(this,
			string.Format(L.RestoredFiles, restored, selectedIndices.Count),
			L.RestoreComplete,
			MessageBoxType.Information);
	}

	private void OnDeleteSession(object? sender, EventArgs e) {
		if (_sessionGrid.SelectedItem is not SessionGridItem item) {
			MessageBox.Show(this, L.SelectSessionToDelete, L.NoSelection, MessageBoxType.Warning);
			return;
		}

		var confirm = MessageBox.Show(this,
			string.Format(L.ConfirmDeleteSession, item.Name),
			L.ConfirmDelete,
			MessageBoxButtons.YesNo,
			MessageBoxType.Warning);

		if (confirm != DialogResult.Yes)
			return;

		_backupManager.DeleteSession(item.Id);
		RefreshSessions();
		_entryGrid.DataStore = null;
	}

	private void OnCleanup(object? sender, EventArgs e) {
		var confirm = MessageBox.Show(this,
			L.ConfirmCleanupOld,
			L.ConfirmCleanup,
			MessageBoxButtons.YesNo,
			MessageBoxType.Question);

		if (confirm != DialogResult.Yes)
			return;

		_backupManager.CleanupOldSessions(10);
		RefreshSessions();
		_entryGrid.DataStore = null;
		MessageBox.Show(this, L.CleanupComplete, L.Done, MessageBoxType.Information);
	}

	private sealed class SessionGridItem {
		public SessionGridItem(BackupSession session) {
			Id = session.Id;
			Name = session.Name;
			Date = session.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
			FileCount = session.Entries.Count.ToString();
		}

		public string Id { get; }
		public string Name { get; }
		public string Date { get; }
		public string FileCount { get; }
	}

	private sealed class EntryGridItem {
		public EntryGridItem(BackupEntry entry) {
			FilePath = entry.FilePath;
			BackupTime = entry.BackupTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
		}

		public string FilePath { get; }
		public string BackupTime { get; }
	}
}
