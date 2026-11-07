using System.Drawing;
using System.Windows.Forms;

namespace Ledger.Desktop.Ui;

public static class UiFactory
{
    public static Button CreateButton(string text)
    {
        return new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(84, 30),
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(0, 4, 8, 0),
            UseVisualStyleBackColor = true
        };
    }

    public static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(0, 8, 4, 0)
        };
    }

    public static CheckBox CreateCheckBox(string text)
    {
        return new CheckBox
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(6, 8, 8, 0)
        };
    }

    public static ComboBox CreateComboBox(int width)
    {
        return new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = width,
            Margin = new Padding(0, 4, 8, 0)
        };
    }

    public static DateTimePicker CreateDatePicker(bool optional)
    {
        return new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd",
            ShowCheckBox = optional,
            Checked = !optional,
            Width = 112,
            Margin = new Padding(0, 4, 8, 0)
        };
    }

    public static FlowLayoutPanel CreateToolbar()
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Padding = new Padding(8, 6, 8, 2),
            BackColor = SystemColors.Control
        };
    }

    public static DataGridView CreateGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AllowUserToOrderColumns = false,
            AutoGenerateColumns = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = Color.FromArgb(232, 232, 232),
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = 32,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
        };

        grid.RowTemplate.Height = 28;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(243, 244, 246);
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 250);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 120, 215);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;

        return grid;
    }

    public static DataGridViewTextBoxColumn CreateColumn(
        string propertyName,
        string headerText,
        int fillWeight,
        string? format = null,
        DataGridViewContentAlignment alignment = DataGridViewContentAlignment.MiddleLeft)
    {
        var column = new DataGridViewTextBoxColumn
        {
            DataPropertyName = propertyName,
            HeaderText = headerText,
            FillWeight = fillWeight,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = alignment }
        };

        if (format is not null)
        {
            column.DefaultCellStyle.Format = format;
        }

        return column;
    }
}
