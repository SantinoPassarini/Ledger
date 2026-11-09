using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using Ledger.Desktop.Models;

namespace Ledger.Desktop.Ui;

public sealed class MonthlyBarChart : Control
{
    private static readonly Color IncomeColor = Color.FromArgb(21, 128, 61);
    private static readonly Color ExpenseColor = Color.FromArgb(185, 28, 28);
    private static readonly Color GridColor = Color.FromArgb(232, 232, 232);
    private static readonly Color AxisColor = Color.FromArgb(203, 213, 225);
    private static readonly Color TextColor = Color.FromArgb(71, 85, 105);

    private readonly ToolTip _toolTip;
    private IReadOnlyList<DashboardMonthlyTotalModel> _data = Array.Empty<DashboardMonthlyTotalModel>();
    private string _currency = "ARS";
    private RectangleF[] _monthHitAreas = Array.Empty<RectangleF>();
    private string[] _monthTooltips = Array.Empty<string>();
    private string _lastTooltip = string.Empty;

    public MonthlyBarChart()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        BackColor = SystemColors.Window;
        Font = new Font("Segoe UI", 9F);
        AccessibleName = "Monthly income and expenses chart";

        _toolTip = new ToolTip
        {
            InitialDelay = 250,
            ReshowDelay = 100,
            AutoPopDelay = 5000,
            ShowAlways = true
        };
    }

    public IReadOnlyList<DashboardMonthlyTotalModel> Data => _data;

    public string Currency => _currency;

    public void SetData(
        IReadOnlyList<DashboardMonthlyTotalModel> data,
        string currency)
    {
        _data = data ?? Array.Empty<DashboardMonthlyTotalModel>();
        _currency = string.IsNullOrWhiteSpace(currency) ? "ARS" : currency;
        _lastTooltip = string.Empty;
        _toolTip.Hide(this);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(BackColor);

        var scale = DeviceDpi / 96f;
        var plot = RectangleF.FromLTRB(
            64f * scale,
            34f * scale,
            Width - 16f * scale,
            Height - 34f * scale);

        if (plot.Width <= 10f || plot.Height <= 10f)
        {
            return;
        }

        using var axisFont = new Font(
            Font.FontFamily,
            Math.Max(7f, Font.SizeInPoints * 0.82f));
        using var legendFont = new Font(
            Font.FontFamily,
            Math.Max(7f, Font.SizeInPoints * 0.9f));

        DrawLegend(graphics, plot, legendFont, scale);
        DrawCurrency(graphics, axisFont, scale);

        if (_data.Count == 0)
        {
            _monthHitAreas = Array.Empty<RectangleF>();
            _monthTooltips = Array.Empty<string>();

            TextRenderer.DrawText(
                graphics,
                "No data for the selected period",
                Font,
                Rectangle.Round(plot),
                SystemColors.GrayText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            return;
        }

        var maxValue = _data.Max(entry => Math.Max(entry.Income, entry.Expenses));
        var max = NiceCeiling(maxValue);
        DrawGridAndAxis(graphics, plot, max, axisFont, scale);

        var groupWidth = plot.Width / _data.Count;
        var hitAreas = new List<RectangleF>(_data.Count);
        var tooltips = new List<string>(_data.Count);

        for (var index = 0; index < _data.Count; index++)
        {
            var entry = _data[index];
            var groupLeft = plot.Left + groupWidth * index;
            var groupCenter = groupLeft + groupWidth / 2f;

            var barWidth = Math.Max(2f, groupWidth * 0.25f);
            var gap = Math.Max(1f, groupWidth * 0.04f);
            var pairWidth = barWidth * 2f + gap;
            var startX = groupCenter - pairWidth / 2f;

            DrawBar(graphics, startX, plot, barWidth, entry.Income, max, IncomeColor);
            DrawBar(
                graphics,
                startX + barWidth + gap,
                plot,
                barWidth,
                entry.Expenses,
                max,
                ExpenseColor);

            TextRenderer.DrawText(
                graphics,
                entry.Month.ToString("MMM yy", CultureInfo.CurrentCulture),
                axisFont,
                new Rectangle(
                    (int)Math.Round(groupLeft),
                    (int)Math.Round(plot.Bottom + 4f * scale),
                    (int)Math.Round(groupWidth),
                    (int)Math.Round(18f * scale)),
                TextColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            hitAreas.Add(new RectangleF(groupLeft, plot.Top, groupWidth, plot.Height));
            tooltips.Add(BuildTooltip(entry));
        }

        _monthHitAreas = hitAreas.ToArray();
        _monthTooltips = tooltips.ToArray();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        for (var index = 0; index < _monthHitAreas.Length; index++)
        {
            if (!_monthHitAreas[index].Contains(e.X, e.Y))
            {
                continue;
            }

            var text = _monthTooltips[index];

            if (!string.Equals(text, _lastTooltip, StringComparison.Ordinal))
            {
                _lastTooltip = text;
                _toolTip.Show(text, this, e.X + 14, e.Y + 14, 5000);
            }

            return;
        }

        _lastTooltip = string.Empty;
        _toolTip.Hide(this);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _lastTooltip = string.Empty;
        _toolTip.Hide(this);
        base.OnMouseLeave(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private static void DrawBar(
        Graphics graphics,
        float x,
        RectangleF plot,
        float width,
        decimal value,
        decimal max,
        Color color)
    {
        if (value <= 0m)
        {
            return;
        }

        var height = (float)(value / max) * plot.Height;

        using var brush = new SolidBrush(color);
        graphics.FillRectangle(
            brush,
            new RectangleF(x, plot.Bottom - height, width, height));
    }

    private static void DrawGridAndAxis(
        Graphics graphics,
        RectangleF plot,
        decimal max,
        Font axisFont,
        float scale)
    {
        const int intervalCount = 4;

        using var gridPen = new Pen(GridColor, 1f);
        using var axisPen = new Pen(AxisColor, 1f);

        for (var index = 0; index <= intervalCount; index++)
        {
            var value = max * index / intervalCount;
            var y = plot.Bottom - plot.Height * index / intervalCount;

            if (index > 0)
            {
                graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            }

            TextRenderer.DrawText(
                graphics,
                FormatCompact(value),
                axisFont,
                new Rectangle(
                    0,
                    (int)Math.Round(y - 9f * scale),
                    (int)Math.Round(plot.Left - 8f * scale),
                    (int)Math.Round(18f * scale)),
                TextColor,
                TextFormatFlags.Right |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }

        graphics.DrawLine(axisPen, plot.Left, plot.Top, plot.Left, plot.Bottom);
        graphics.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
    }

    private void DrawLegend(
        Graphics graphics,
        RectangleF plot,
        Font font,
        float scale)
    {
        var x = plot.Right;
        var y = 6f * scale;
        var textHeight = 18f * scale;
        var swatchSize = 10f * scale;

        var items = new[]
        {
            ("Income", IncomeColor),
            ("Expenses", ExpenseColor)
        };

        for (var index = items.Length - 1; index >= 0; index--)
        {
            var (text, color) = items[index];
            var textSize = TextRenderer.MeasureText(
                text,
                font,
                Size.Empty,
                TextFormatFlags.NoPadding);

            x -= textSize.Width;

            TextRenderer.DrawText(
                graphics,
                text,
                font,
                new Rectangle((int)Math.Round(x), (int)Math.Round(y), textSize.Width, (int)Math.Round(textHeight)),
                TextColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            x -= 8f * scale;

            using var brush = new SolidBrush(color);
            graphics.FillRectangle(
                brush,
                x,
                y + (textHeight - swatchSize) / 2f,
                swatchSize,
                swatchSize);

            x -= 16f * scale;
        }
    }

    private void DrawCurrency(Graphics graphics, Font font, float scale)
    {
        TextRenderer.DrawText(
            graphics,
            _currency,
            font,
            new Rectangle(
                0,
                (int)Math.Round(6f * scale),
                (int)Math.Round(56f * scale),
                (int)Math.Round(18f * scale)),
            TextColor,
            TextFormatFlags.Right |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPadding);
    }

    private string BuildTooltip(DashboardMonthlyTotalModel entry)
    {
        return string.Join(
            Environment.NewLine,
            entry.Month.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
            $"Income: {_currency} {entry.Income.ToString("N2", CultureInfo.CurrentCulture)}",
            $"Expenses: {_currency} {entry.Expenses.ToString("N2", CultureInfo.CurrentCulture)}",
            $"Net: {_currency} {entry.Net.ToString("N2", CultureInfo.CurrentCulture)}");
    }

    private static decimal NiceCeiling(decimal value)
    {
        if (value <= 0m)
        {
            return 100m;
        }

        var magnitude = (decimal)Math.Pow(
            10d,
            Math.Floor(Math.Log10((double)value)));
        var normalized = value / magnitude;

        var nice = normalized <= 1m
            ? 1m
            : normalized <= 2m
                ? 2m
                : normalized <= 2.5m
                    ? 2.5m
                    : normalized <= 5m
                        ? 5m
                        : 10m;

        return nice * magnitude;
    }

    private static string FormatCompact(decimal value)
    {
        var culture = CultureInfo.CurrentCulture;

        if (value >= 1_000_000m)
        {
            return $"{(value / 1_000_000m).ToString("0.#", culture)}M";
        }

        if (value >= 1_000m)
        {
            return $"{(value / 1_000m).ToString("0.#", culture)}k";
        }

        return value.ToString("0.#", culture);
    }
}
