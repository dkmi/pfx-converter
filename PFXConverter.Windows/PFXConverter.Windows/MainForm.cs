using System.Drawing;
using System.Windows.Forms;

namespace PFXConverter.Windows;

public sealed class MainForm : Form
{
    private readonly TextBox _certificateTextBox = new();
    private readonly TextBox _keyTextBox = new();
    private readonly TextBox _outputTextBox = new();
    private readonly TextBox _passwordTextBox = new();
    private readonly TextBox _confirmPasswordTextBox = new();
    private readonly ComboBox _engineComboBox = new();
    private readonly Label _engineDetailsLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Button _convertButton = new();
    private readonly Button _clearButton = new();
    private readonly Button _refreshButton = new();

    private List<ConversionEngine> _engines = [];
    private bool _isBusy;

    public MainForm()
    {
        Text = "PFX Converter";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(760, 560);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimumSize = Size;
        MaximumSize = Size;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);

        BuildLayout();
        RefreshEngines();
        UpdateConvertButton();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(22),
            AutoSize = false,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var title = new Label
        {
            Text = "PFX Converter",
            Font = new Font(Font.FontFamily, 20F, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };
        var subtitle = new Label
        {
            Text = "Convert a .crt file containing the certificate chain and a .key file into a Windows-ready .pfx file.",
            AutoSize = false,
            Width = 690,
            Height = 36,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 18)
        };
        var header = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = false, Dock = DockStyle.Fill };
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        root.Controls.Add(header);

        root.Controls.Add(CreateFileRow("Certificate + chain (.crt)", "Choose certificate.crt", _certificateTextBox, BrowseCertificate));
        root.Controls.Add(CreateFileRow("Private key (.key)", "Choose private.key", _keyTextBox, BrowsePrivateKey));
        root.Controls.Add(CreateFileRow("Output Windows certificate (.pfx)", "Choose where to save certificate.pfx", _outputTextBox, BrowseOutput));
        root.Controls.Add(CreateEnginePanel());
        root.Controls.Add(CreatePasswordPanel());
        root.Controls.Add(CreateFooterPanel());
    }

    private Control CreateFileRow(string labelText, string placeholder, TextBox textBox, EventHandler browseHandler)
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 2,
            Dock = DockStyle.Fill,
            AutoSize = false,
            Margin = new Padding(0, 0, 0, 14)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        var label = new Label
        {
            Text = labelText,
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 6)
        };
        panel.Controls.Add(label, 0, 0);
        panel.SetColumnSpan(label, 2);

        textBox.PlaceholderText = placeholder;
        textBox.ReadOnly = true;
        textBox.Dock = DockStyle.Fill;
        textBox.Margin = new Padding(0, 0, 10, 0);
        textBox.TextChanged += (_, _) => UpdateConvertButton();
        panel.Controls.Add(textBox, 0, 1);

        var button = new Button
        {
            Text = labelText.StartsWith("Output", StringComparison.Ordinal) ? "Save as" : "Choose",
            Dock = DockStyle.Fill,
            Height = 30
        };
        button.Click += browseHandler;
        panel.Controls.Add(button, 1, 1);

        return panel;
    }

    private Control CreateEnginePanel()
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 3,
            Dock = DockStyle.Fill,
            AutoSize = false,
            Margin = new Padding(0, 0, 0, 14)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var label = new Label
        {
            Text = "Conversion engine",
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 6)
        };
        panel.Controls.Add(label, 0, 0);

        _refreshButton.Text = "Refresh";
        _refreshButton.Dock = DockStyle.Fill;
        _refreshButton.Click += (_, _) => RefreshEngines();
        panel.Controls.Add(_refreshButton, 1, 0);

        _engineComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _engineComboBox.Dock = DockStyle.Fill;
        _engineComboBox.SelectedIndexChanged += (_, _) =>
        {
            UpdateEngineDetails();
            UpdateConvertButton();
        };
        panel.Controls.Add(_engineComboBox, 0, 1);
        panel.SetColumnSpan(_engineComboBox, 2);

        _engineDetailsLabel.AutoSize = false;
        _engineDetailsLabel.Dock = DockStyle.Fill;
        _engineDetailsLabel.ForeColor = SystemColors.GrayText;
        _engineDetailsLabel.Margin = new Padding(0, 6, 0, 0);
        panel.Controls.Add(_engineDetailsLabel, 0, 2);
        panel.SetColumnSpan(_engineDetailsLabel, 2);

        return panel;
    }

    private Control CreatePasswordPanel()
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 2,
            Dock = DockStyle.Fill,
            AutoSize = false,
            Margin = new Padding(0, 0, 0, 18)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        var label = new Label
        {
            Text = "PFX password",
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 6)
        };
        panel.Controls.Add(label, 0, 0);
        panel.SetColumnSpan(label, 2);

        _passwordTextBox.PlaceholderText = "Password";
        _passwordTextBox.UseSystemPasswordChar = true;
        _passwordTextBox.Dock = DockStyle.Fill;
        _passwordTextBox.Margin = new Padding(0, 0, 8, 0);
        _passwordTextBox.TextChanged += (_, _) => UpdateConvertButton();
        panel.Controls.Add(_passwordTextBox, 0, 1);

        _confirmPasswordTextBox.PlaceholderText = "Confirm password";
        _confirmPasswordTextBox.UseSystemPasswordChar = true;
        _confirmPasswordTextBox.Dock = DockStyle.Fill;
        _confirmPasswordTextBox.Margin = new Padding(8, 0, 0, 0);
        _confirmPasswordTextBox.TextChanged += (_, _) => UpdateConvertButton();
        panel.Controls.Add(_confirmPasswordTextBox, 1, 1);

        return panel;
    }

    private Control CreateFooterPanel()
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = 3,
            RowCount = 1,
            Dock = DockStyle.Fill,
            AutoSize = false,
            Margin = new Padding(0)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 102));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _convertButton.Text = "Generate .pfx";
        _convertButton.Dock = DockStyle.Top;
        _convertButton.Height = 32;
        _convertButton.Margin = new Padding(0, 0, 12, 0);
        _convertButton.Click += Convert;
        panel.Controls.Add(_convertButton, 0, 0);

        _clearButton.Text = "Clear";
        _clearButton.Dock = DockStyle.Top;
        _clearButton.Height = 32;
        _clearButton.Margin = new Padding(0, 0, 12, 0);
        _clearButton.Click += (_, _) => Clear();
        panel.Controls.Add(_clearButton, 1, 0);

        _statusLabel.AutoSize = false;
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Text = "Ready to choose files.";
        _statusLabel.ForeColor = SystemColors.GrayText;
        _statusLabel.Margin = new Padding(0, 8, 0, 0);
        panel.Controls.Add(_statusLabel, 2, 0);

        return panel;
    }

    private void BrowseCertificate(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose Certificate",
            Filter = "Certificate files (*.crt;*.cer;*.pem)|*.crt;*.cer;*.pem|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _certificateTextBox.Text = dialog.FileName;
            SetStatus("Ready to generate.", false);
        }
    }

    private void BrowsePrivateKey(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose Private Key",
            Filter = "Private key files (*.key;*.pem)|*.key;*.pem|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _keyTextBox.Text = dialog.FileName;
            SetStatus("Ready to generate.", false);
        }
    }

    private void BrowseOutput(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Save Windows Certificate",
            Filter = "PFX files (*.pfx)|*.pfx",
            FileName = "certificate.pfx",
            DefaultExt = "pfx",
            AddExtension = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _outputTextBox.Text = dialog.FileName;
            SetStatus("Ready to generate.", false);
        }
    }

    private void RefreshEngines()
    {
        var previousSelection = (_engineComboBox.SelectedItem as ConversionEngine)?.Id;
        _engines = PfxExporter.DiscoverEngines();
        _engineComboBox.Items.Clear();

        foreach (var engine in _engines)
        {
            _engineComboBox.Items.Add(engine);
        }

        var selected = _engines.FirstOrDefault(engine => engine.Id == previousSelection) ?? _engines.FirstOrDefault();
        _engineComboBox.SelectedItem = selected;
        UpdateEngineDetails();
        UpdateConvertButton();
    }

    private void UpdateEngineDetails()
    {
        if (_engineComboBox.SelectedItem is ConversionEngine engine)
        {
            _engineDetailsLabel.Text = engine.Description;
            return;
        }

        _engineDetailsLabel.Text = "No conversion engine is available.";
    }

    private void Convert(object? sender, EventArgs e)
    {
        if (_engineComboBox.SelectedItem is not ConversionEngine engine)
        {
            SetStatus("Choose a conversion engine before generating the .pfx file.", true);
            return;
        }

        if (_passwordTextBox.Text.Length == 0 || _passwordTextBox.Text != _confirmPasswordTextBox.Text)
        {
            SetStatus("Enter and confirm a password for the .pfx file.", true);
            return;
        }

        SetBusy(true);
        SetStatus("Generating .pfx file...", false);

        try
        {
            PfxExporter.Export(
                _certificateTextBox.Text,
                _keyTextBox.Text,
                _outputTextBox.Text,
                _passwordTextBox.Text,
                engine);
            SetStatus($"Done: {_outputTextBox.Text}", false);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Clear()
    {
        _certificateTextBox.Clear();
        _keyTextBox.Clear();
        _outputTextBox.Clear();
        _passwordTextBox.Clear();
        _confirmPasswordTextBox.Clear();
        SetStatus("Ready to choose files.", false);
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        _clearButton.Enabled = !isBusy;
        _refreshButton.Enabled = !isBusy;
        _engineComboBox.Enabled = !isBusy;
        Cursor = isBusy ? Cursors.WaitCursor : Cursors.Default;
        UpdateConvertButton();
    }

    private void UpdateConvertButton()
    {
        var passwordsMatch = _passwordTextBox.Text.Length > 0 && _passwordTextBox.Text == _confirmPasswordTextBox.Text;
        _convertButton.Enabled =
            !_isBusy &&
            _certificateTextBox.Text.Length > 0 &&
            _keyTextBox.Text.Length > 0 &&
            _outputTextBox.Text.Length > 0 &&
            passwordsMatch &&
            _engineComboBox.SelectedItem is ConversionEngine;
    }

    private void SetStatus(string message, bool isError)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = isError ? Color.Firebrick : SystemColors.GrayText;
    }
}

public enum ConversionEngineKind
{
    WindowsBuiltIn,
    OpenSsl
}

public sealed record ConversionEngine(
    string Id,
    string DisplayName,
    string Description,
    ConversionEngineKind Kind,
    string? ExecutablePath,
    bool SupportsLegacyFlag)
{
    public override string ToString() => DisplayName;
}
