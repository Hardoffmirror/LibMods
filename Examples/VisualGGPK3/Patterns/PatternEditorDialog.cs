using System;
using System.Linq;

using Eto.Drawing;
using Eto.Forms;

namespace VisualGGPK3.Patterns;

/// <summary>
/// Dialog for creating and editing search/replace patterns.
/// </summary>
public sealed class PatternEditorDialog : Dialog<SearchReplacePattern?> {
	private readonly TextBox _nameBox;
	private readonly TextArea _descriptionBox;
	private readonly TextBox _filePatternBox;
	private readonly DropDown _typeDropDown;
	private readonly TextArea _searchBox;
	private readonly TextArea _replaceBox;
	private readonly CheckBox _replaceAllCheck;
	private readonly CheckBox _enabledCheck;
	private readonly TextBox _tagsBox;

	private readonly SearchReplacePattern? _existingPattern;

	public PatternEditorDialog(SearchReplacePattern? existingPattern = null) {
		_existingPattern = existingPattern;
		Title = existingPattern is null ? "New Pattern" : "Edit Pattern";
		MinimumSize = new Size(500, 600);
		Padding = new Padding(10);

		// Initialize controls
		_nameBox = new TextBox { PlaceholderText = "Pattern name" };
		_descriptionBox = new TextArea { Height = 60 };
		_filePatternBox = new TextBox { PlaceholderText = "e.g., *.txt or Data/**/*.dat (empty = all files)" };

		_typeDropDown = new DropDown {
			Items = {
				new ListItem { Text = "Hex (bytes)", Key = PatternType.Hex.ToString() },
				new ListItem { Text = "Text (UTF-8)", Key = PatternType.Text.ToString() },
				new ListItem { Text = "Text (UTF-16)", Key = PatternType.TextUtf16.ToString() }
			}
		};

		_searchBox = new TextArea { Height = 100 };
		_replaceBox = new TextArea { Height = 100 };
		_replaceAllCheck = new CheckBox { Text = "Replace all occurrences", Checked = true };
		_enabledCheck = new CheckBox { Text = "Enabled", Checked = true };
		_tagsBox = new TextBox { PlaceholderText = "tag1, tag2, tag3" };

		// Populate with existing pattern
		if (existingPattern != null) {
			_nameBox.Text = existingPattern.Name;
			_descriptionBox.Text = existingPattern.Description;
			_filePatternBox.Text = existingPattern.FilePattern;
			_typeDropDown.SelectedKey = existingPattern.Type.ToString();
			_searchBox.Text = existingPattern.SearchPattern;
			_replaceBox.Text = existingPattern.ReplacePattern;
			_replaceAllCheck.Checked = existingPattern.ReplaceAll;
			_enabledCheck.Checked = existingPattern.IsEnabled;
			_tagsBox.Text = string.Join(", ", existingPattern.Tags);
		} else {
			_typeDropDown.SelectedIndex = 0;
		}

		// Buttons
		var okButton = new Button { Text = "OK" };
		okButton.Click += OnOkClicked;

		var cancelButton = new Button { Text = "Cancel" };
		cancelButton.Click += (s, e) => Close(null);

		var testButton = new Button { Text = "Test Pattern" };
		testButton.Click += OnTestClicked;

		// Layout
		Content = new StackLayout {
			Spacing = 10,
			HorizontalContentAlignment = HorizontalAlignment.Stretch,
			Items = {
				new StackLayoutItem(new Label { Text = "Name:" }),
				new StackLayoutItem(_nameBox),

				new StackLayoutItem(new Label { Text = "Description:" }),
				new StackLayoutItem(_descriptionBox),

				new StackLayoutItem(new Label { Text = "File Pattern (glob):" }),
				new StackLayoutItem(_filePatternBox),

				new StackLayoutItem(new Label { Text = "Pattern Type:" }),
				new StackLayoutItem(_typeDropDown),

				new StackLayoutItem(new Label { Text = "Search Pattern:" }),
				new StackLayoutItem(_searchBox),

				new StackLayoutItem(new Label { Text = "Replace With:" }),
				new StackLayoutItem(_replaceBox),

				new StackLayoutItem(_replaceAllCheck),
				new StackLayoutItem(_enabledCheck),

				new StackLayoutItem(new Label { Text = "Tags (comma-separated):" }),
				new StackLayoutItem(_tagsBox),

				new StackLayoutItem(new StackLayout {
					Orientation = Orientation.Horizontal,
					Spacing = 10,
					Items = {
						new StackLayoutItem(testButton),
						new StackLayoutItem(null, true),
						new StackLayoutItem(cancelButton),
						new StackLayoutItem(okButton)
					}
				})
			}
		};

		DefaultButton = okButton;
		AbortButton = cancelButton;
	}

	private void OnOkClicked(object? sender, EventArgs e) {
		// Validate
		if (string.IsNullOrWhiteSpace(_nameBox.Text)) {
			MessageBox.Show(this, "Please enter a pattern name", "Validation Error", MessageBoxType.Warning);
			return;
		}

		if (string.IsNullOrWhiteSpace(_searchBox.Text)) {
			MessageBox.Show(this, "Please enter a search pattern", "Validation Error", MessageBoxType.Warning);
			return;
		}

		// Validate pattern syntax
		try {
			var testPattern = CreatePattern();
			_ = testPattern.GetSearchBytes();
			_ = testPattern.GetReplaceBytes();
		} catch (Exception ex) {
			MessageBox.Show(this, $"Invalid pattern: {ex.Message}", "Validation Error", MessageBoxType.Error);
			return;
		}

		Close(CreatePattern());
	}

	private void OnTestClicked(object? sender, EventArgs e) {
		try {
			var pattern = CreatePattern();
			var searchBytes = pattern.GetSearchBytes();
			var replaceBytes = pattern.GetReplaceBytes();

			var message = $"Search Pattern ({searchBytes.Length} bytes):\n" +
				$"{BitConverter.ToString(searchBytes)}\n\n" +
				$"Replace Pattern ({replaceBytes.Length} bytes):\n" +
				$"{BitConverter.ToString(replaceBytes)}";

			MessageBox.Show(this, message, "Pattern Test", MessageBoxType.Information);
		} catch (Exception ex) {
			MessageBox.Show(this, $"Invalid pattern: {ex.Message}", "Pattern Test", MessageBoxType.Error);
		}
	}

	private SearchReplacePattern CreatePattern() {
		var pattern = _existingPattern is null
			? new SearchReplacePattern()
			: new SearchReplacePattern { Id = _existingPattern.Id, CreatedAt = _existingPattern.CreatedAt };

		pattern.Name = _nameBox.Text;
		pattern.Description = _descriptionBox.Text;
		pattern.FilePattern = _filePatternBox.Text;
		pattern.Type = Enum.Parse<PatternType>(_typeDropDown.SelectedKey);
		pattern.SearchPattern = _searchBox.Text;
		pattern.ReplacePattern = _replaceBox.Text;
		pattern.ReplaceAll = _replaceAllCheck.Checked ?? true;
		pattern.IsEnabled = _enabledCheck.Checked ?? true;
		pattern.ModifiedAt = DateTime.UtcNow;

		pattern.Tags = _tagsBox.Text
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.ToList();

		return pattern;
	}
}
