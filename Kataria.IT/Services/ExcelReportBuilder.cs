using ClosedXML.Excel;
using Kataria.IT.Models;

namespace Kataria.IT.Services;

public interface IExcelReportBuilder
{
    /// <summary>Report must be loaded with ServerEntries.Server, ServerEntries.CheckResults.CheckItem and EvidenceFiles.</summary>
    byte[] Build(DailyReport report);
}

public class ExcelReportBuilder(IEvidenceStorage storage) : IExcelReportBuilder
{
    private static readonly XLColor Navy = XLColor.FromHtml("#14304f");
    private static readonly XLColor Ok = XLColor.FromHtml("#d1f2dd");
    private static readonly XLColor Bad = XLColor.FromHtml("#f8d0d0");
    private static readonly XLColor Pending = XLColor.FromHtml("#fdf0c8");
    private static readonly XLColor Grey = XLColor.FromHtml("#eef1f5");

    public byte[] Build(DailyReport r)
    {
        using var wb = new XLWorkbook();
        BuildReportSheet(wb.Worksheets.Add("Daily Report"), r);
        BuildPhotoSheet(wb.Worksheets.Add("Photos"), r);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void Paint(IXLCell cell, string status)
    {
        cell.Style.Fill.BackgroundColor = status switch
        {
            "OK" or "Normal" => Ok,
            "Issue" or "Critical" or "Attention Required" => Bad,
            _ => Pending
        };
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private void BuildReportSheet(IXLWorksheet ws, DailyReport r)
    {
        var entries = r.ServerEntries.OrderBy(e => e.Server.SortOrder).ToList();
        var items = entries.SelectMany(e => e.CheckResults).Select(c => c.CheckItem).DistinctBy(c => c.CheckItemId)
                           .OrderBy(c => c.SortOrder).ToList();
        int lastCol = 3 + items.Count + 1;

        ws.Cell(1, 1).Value = "KATARIA DISTRIBUTION CENTER";
        ws.Range(1, 1, 1, lastCol).Merge().Style
            .Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(Navy).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Cell(2, 1).Value = "Daily Server Monitoring & Status Report";
        ws.Range(2, 1, 2, lastCol).Merge().Style.Font.SetBold().Font.SetFontSize(12)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        int row = 4;
        void Info(string label, string? value, string? status = null)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Font.SetBold().Fill.SetBackgroundColor(Grey);
            ws.Range(row, 1, row, 2).Merge();
            var c = ws.Cell(row, 3);
            c.Value = value ?? "";
            ws.Range(row, 3, row, lastCol).Merge();
            c.Style.Alignment.SetWrapText(true).Alignment.SetVertical(XLAlignmentVerticalValues.Top);
            if (status != null) Paint(c, status);
            if ((value?.Length ?? 0) > 90) ws.Row(row).Height = 15 * Math.Ceiling((value!.Length) / 100.0);   // merged cells do not auto-fit
            c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            row++;
        }
        Info("Report date", r.ReportDate.ToString("dddd, dd MMMM yyyy"));
        Info("Shift", r.Shift);
        Info("Prepared by", r.PreparedByName);
        Info("Reviewed by", r.ReviewedBy);
        Info("Server room condition", r.RoomCondition, r.RoomCondition);
        Info("Manager notified", r.ManagerNotification);
        Info("Internet / network", $"{r.InternetStatus}" + (string.IsNullOrWhiteSpace(r.InternetNotes) ? "" : $" – {r.InternetNotes}"), r.InternetStatus);
        Info("UPS / electricity", $"{r.UpsStatus}" + (string.IsNullOrWhiteSpace(r.UpsNotes) ? "" : $" – {r.UpsNotes}"), r.UpsStatus);
        Info("OVERALL STATUS", r.OverallStatus, r.OverallStatus);
        ws.Cell(row - 1, 3).Style.Font.SetBold();
        Info("Summary / action taken", r.Summary);

        row++;
        var headers = new List<string> { "Server", "IP address", "Role" };
        headers.AddRange(items.Select(i => i.Label));
        headers.Add("Notes / action");
        for (int i = 0; i < headers.Count; i++)
        {
            var c = ws.Cell(row, i + 1);
            c.Value = headers[i];
            c.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(Navy)
                   .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetWrapText(true);
        }
        int headerRow = row++;

        foreach (var e in entries)
        {
            ws.Cell(row, 1).Value = e.Server.Name;
            ws.Cell(row, 2).Value = e.Server.IpAddress;
            ws.Cell(row, 3).Value = e.Server.Role;
            ws.Cell(row, 3).Style.Alignment.SetWrapText(true);
            for (int i = 0; i < items.Count; i++)
            {
                var status = e.CheckResults.FirstOrDefault(c => c.CheckItemId == items[i].CheckItemId)?.Status ?? "Not Checked";
                var cell = ws.Cell(row, 4 + i);
                cell.Value = status;
                Paint(cell, status);
            }
            ws.Cell(row, lastCol).Value = e.Notes ?? "";
            ws.Cell(row, lastCol).Style.Alignment.SetWrapText(true);
            ws.Row(row).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            row++;
        }
        ws.Range(headerRow, 1, row - 1, lastCol).Style.Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorder(XLBorderStyleValues.Thin);

        ws.Column(1).Width = 20; ws.Column(2).Width = 16; ws.Column(3).Width = 40;
        for (int i = 0; i < items.Count; i++) ws.Column(4 + i).Width = 14;
        ws.Column(lastCol).Width = 40;
        ws.SheetView.FreezeRows(headerRow);
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.FitToPages(1, 0);
    }

    private void BuildPhotoSheet(IXLWorksheet ws, DailyReport r)
    {
        ws.Column(1).Width = 70;
        ws.Cell(1, 1).Value = $"Photo evidence – {r.ReportDate:dd MMM yyyy}";
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);

        int row = 3;
        var groups = r.EvidenceFiles
            .OrderBy(e => e.Category == "Server" ? 0 : e.Category == "Internet" ? 1 : 2)
            .ThenBy(e => e.ServerId).ThenBy(e => e.EvidenceId);

        if (!r.EvidenceFiles.Any()) { ws.Cell(row, 1).Value = "Is report ke saath koi photo upload nahi hui."; return; }

        foreach (var ev in groups)
        {
            var caption = ev.Category switch
            {
                "Server" => $"{r.ServerEntries.FirstOrDefault(s => s.ServerId == ev.ServerId)?.Server.Name ?? "Server"} – {ev.OriginalName}",
                "Internet" => $"Internet / network – {ev.OriginalName}",
                _ => $"UPS / electricity – {ev.OriginalName}"
            };
            ws.Cell(row, 1).Value = caption;
            ws.Cell(row, 1).Style.Font.SetBold().Fill.SetBackgroundColor(Grey);
            row++;

            int rowsUsed = 3;
            try
            {
                var bytes = storage.ReadAll(ev.StoredName);
                if (bytes is null) throw new FileNotFoundException();
                using var ms = new MemoryStream(bytes);
                var pic = ws.AddPicture(ms).MoveTo(ws.Cell(row, 1));
                double scale = Math.Min(1.0, 480.0 / Math.Max(1, pic.Width));
                pic.Scale(scale);
                rowsUsed = (int)Math.Ceiling(pic.Height / 20.0) + 1;
            }
            catch
            {
                ws.Cell(row, 1).Value = "(photo Excel mein embed nahi ho saki – Google Drive folder mein dekhein)";
            }
            row += rowsUsed + 1;
        }
    }
}
