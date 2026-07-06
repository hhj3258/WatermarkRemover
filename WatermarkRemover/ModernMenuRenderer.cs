using System.Drawing.Drawing2D;

namespace WatermarkRemover;

internal sealed class ModernMenuRenderer : ToolStripRenderer
{
    // 색상 팔레트
    private static readonly Color BgColor       = ColorTranslator.FromHtml("#2D2D30");
    private static readonly Color HoverColor    = ColorTranslator.FromHtml("#3E3E42");
    private static readonly Color TextColor     = Color.White;
    private static readonly Color DisabledColor = ColorTranslator.FromHtml("#808080");
    private static readonly Color SepColor      = ColorTranslator.FromHtml("#4A4A4D");
    private static readonly Color AccentColor   = ColorTranslator.FromHtml("#0078D4");

    // 상태 배경색 (TrayApp에서 Tag로 설정)
    public static readonly Color StatusGreen  = Color.FromArgb(40, 167, 69);   // 차단 중
    public static readonly Color StatusYellow = Color.FromArgb(180, 140, 20);  // 재시작 대기
    public static readonly Color StatusOrange = Color.FromArgb(200, 100, 30);  // 차단 해제됨

    private const int Radius = 8;
    private const int ItemPaddingY = 4;

    // 메뉴 전체 배경
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var path = RoundedRect(new Rectangle(Point.Empty, e.ToolStrip.Size), Radius);
        using var brush = new SolidBrush(BgColor);
        g.FillPath(brush, path);

        // 라운드 밖 영역 클리핑
        e.ToolStrip.Region = new Region(path);
    }

    // 기본 흰색 보더 억제 (메뉴 상단/하단에 하얀 픽셀 라인이 생기는 문제 차단)
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        // 의도적으로 비움: 라운드 코너 배경만 그리고 보더는 그리지 않는다.
    }

    // 왼쪽 이미지 마진 (배경색으로 채움, 체크마크 영역 유지)
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(BgColor);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    // 개별 아이템 배경 (상태 배경 or 호버)
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // 메뉴 전체 폭 기준 가운데 정렬
        int menuWidth = e.Item.Owner?.Width ?? e.Item.Width;
        int itemX = e.Item.Bounds.X;
        int margin = 4;
        var rc = new Rectangle(margin - itemX, 1, menuWidth - margin * 2, e.Item.Height - 2);

        // 상태 아이템: Tag에 Color가 설정된 경우 해당 색상으로 배경 채움
        if (e.Item.Tag is Color statusColor)
        {
            using var path = RoundedRect(rc, 4);
            using var brush = new SolidBrush(statusColor);
            g.FillPath(brush, path);
            return;
        }

        if (!e.Item.Selected && !e.Item.Pressed) return;

        using var hoverPath = RoundedRect(rc, 4);
        using var hoverBrush = new SolidBrush(HoverColor);
        g.FillPath(hoverBrush, hoverPath);
    }

    // 텍스트 (세로 가운데 정렬)
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        bool isStatus = e.Item.Tag is Color;
        var font = new Font("Segoe UI", 9f);
        var color = isStatus ? Color.White : (e.Item.Enabled ? TextColor : DisabledColor);

        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var textSize = g.MeasureString(e.Item.Text, font);
        float x = e.TextRectangle.X + 4;
        float y = (e.Item.Height - textSize.Height) / 2f;

        using var brush = new SolidBrush(color);
        g.DrawString(e.Item.Text, font, brush, x, y);
    }

    // 구분선
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var g = e.Graphics;
        int y = e.Item.Height / 2;
        using var pen = new Pen(SepColor);
        g.DrawLine(pen, 12, y, e.Item.Width - 12, y);
    }

    // 체크마크
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rc = new Rectangle(e.ImageRectangle.X + 2, e.ImageRectangle.Y + 2,
                               e.ImageRectangle.Width - 4, e.ImageRectangle.Height - 4);

        // 체크 배경 원
        using var bgBrush = new SolidBrush(AccentColor);
        g.FillEllipse(bgBrush, rc);

        // 체크마크 (✓)
        using var pen = new Pen(Color.White, 1.5f) { LineJoin = LineJoin.Round };
        var cx = rc.X + rc.Width / 2;
        var cy = rc.Y + rc.Height / 2;
        g.DrawLines(pen, new[]
        {
            new Point(cx - 3, cy),
            new Point(cx - 1, cy + 3),
            new Point(cx + 4, cy - 3),
        });
    }

    // 라운드 사각형 유틸리티
    private static GraphicsPath RoundedRect(Rectangle rc, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rc.X, rc.Y, d, d, 180, 90);
        path.AddArc(rc.Right - d, rc.Y, d, d, 270, 90);
        path.AddArc(rc.Right - d, rc.Bottom - d, d, d, 0, 90);
        path.AddArc(rc.X, rc.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
