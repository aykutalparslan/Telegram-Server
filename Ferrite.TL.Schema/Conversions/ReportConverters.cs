// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;

namespace Ferrite.TL.Schema;

internal static class ReportConverters
{
    internal static LayerObjectValue UpgradeRequest(LayerConversionContext context,
        LayerObjectValue source)
    {
        LayerObjectValue reason = source.Get<LayerObjectValue>("reason");
        var consumed = new HashSet<string>(StringComparer.Ordinal) { "reason" };
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["option"] = Encoding.UTF8.GetBytes(Option(reason.Constructor.Name))
        };
        return context.ConvertCompatible(source, "messages.report", replacements,
            consumed);
    }

    private static string Option(string reason)
    {
        switch (reason)
        {
            case "inputReportReasonSpam":
                return "spam";
            case "inputReportReasonViolence":
                return "violence";
            case "inputReportReasonChildAbuse":
                return "child_abuse";
            case "inputReportReasonPornography":
                return "pornography";
            case "inputReportReasonIllegalDrugs":
                return "illegal_drugs";
            case "inputReportReasonPersonalDetails":
                return "personal_details";
            case "inputReportReasonCopyright":
                return "copyright";
            case "inputReportReasonFake":
                return "fake";
            case "inputReportReasonOther":
            case "inputReportReasonGeoIrrelevant":
                return "other";
            default:
                throw new InvalidOperationException("The report reason " + reason +
                                                    " has no base option.");
        }
    }

    internal static LayerObjectValue DowngradeResult(LayerConversionContext context,
        LayerObjectValue source)
    {
        return BooleanConverters.Create(context, true);
    }
}
