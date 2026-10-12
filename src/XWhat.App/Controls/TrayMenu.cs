using System.ComponentModel;
using System.Drawing;
using Forms = System.Windows.Forms;

namespace XWhat.App.Controls;

/// <summary>Compact native tray menu; the owner supplies actions and the current overlay state.</summary>
internal sealed class TrayMenu : Forms.ContextMenuStrip
{
    private readonly Forms.ToolStripMenuItem _overlay;
    private readonly Font _menuFont = new("Consolas", 9);
    private readonly Font _detailFont = new("Consolas", 8.25f);

    public TrayMenu(Action settings, Action overlay, Action log, Action exit)
    {
        Font = _menuFont;
        ShowImageMargin = false;
        ShowCheckMargin = false;
        AutoSize = true;
        Padding = new Forms.Padding(1);
        Renderer = new TechnicalRenderer(_detailFont);
        Items.Add(new Forms.ToolStripMenuItem("XWHAT") { Tag = "MONITOR", Enabled = false });
        Add("&Настройки", "CONFIG", settings);
        _overlay = Add("&Оверлей", "", overlay);
        Add("&Журнал", "LOG", log);
        Items.Add(new Forms.ToolStripSeparator());
        Add("&Выход", "EXIT", exit);
        SetOverlayVisible(true);
        ConfigureLayout();
    }

    public void SetOverlayVisible(bool visible)
    {
        _overlay.Tag = (visible ? "ON" : "OFF") + " · Ctrl+Shift+F10";
        _overlay.AccessibleDescription = "Оверлей " + _overlay.Tag;
        _overlay.Invalidate();
    }

    private Forms.ToolStripMenuItem Add(string text, string detail, Action action)
    {
        var item = new Forms.ToolStripMenuItem(text) { Tag = detail, AccessibleDescription = detail };
        item.Click += (_, _) => action();
        Items.Add(item);
        return item;
    }

    protected override void OnOpening(CancelEventArgs e)
    {
        ConfigureLayout();
        base.OnOpening(e);
    }

    private void ConfigureLayout()
    {
        var scale = DeviceDpi / 96d;
        var width = (int)Math.Round(276 * scale);
        foreach (Forms.ToolStripItem item in Items)
        {
            item.AutoSize = false;
            item.Margin = Forms.Padding.Empty;
            item.Size = new Size(width - 2, (int)Math.Round((item is Forms.ToolStripSeparator ? 8 : 23) * scale));
        }
        MinimumSize = new Size(width, 0);
        MaximumSize = new Size(width, 0);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { _menuFont.Dispose(); _detailFont.Dispose(); }
    }

    private sealed class TechnicalRenderer(Font detailFont) : Forms.ToolStripRenderer
    {
        private static readonly Color Surface = Color.FromArgb(17, 22, 27);
        private static readonly Color Edge = Color.FromArgb(52, 66, 76);
        private static readonly Color Ink = Color.FromArgb(220, 229, 235);
        private static readonly Color Muted = Color.FromArgb(147, 165, 178);
        private static readonly Color Accent = Color.FromArgb(140, 185, 198);

        protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(Surface);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }
        protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Edge);
            e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
        protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected) return;
            using var brush = new SolidBrush(Color.FromArgb(38, 51, 61));
            e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
        }
        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            var inset = (int)Math.Round(8 * e.Graphics.DpiX / 96);
            var rect = new Rectangle(inset, 0, e.Item.Width - 2 * inset, e.Item.Height);
            var flags = Forms.TextFormatFlags.VerticalCenter | Forms.TextFormatFlags.SingleLine;
            flags |= e.TextFormat & Forms.TextFormatFlags.HidePrefix;
            Forms.TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, rect, e.Item.Enabled ? Ink : Muted, flags);
            Forms.TextRenderer.DrawText(e.Graphics, e.Item.Tag as string ?? "", detailFont, rect,
                e.Item.Selected ? Ink : Accent, flags | Forms.TextFormatFlags.Right | Forms.TextFormatFlags.NoPrefix);
        }
        protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(Edge);
            e.Graphics.DrawLine(pen, 8, e.Item.Height / 2, e.Item.Width - 8, e.Item.Height / 2);
        }
    }
}
