using System.Globalization;
using System.Text;

namespace AzureReports;

public static class StringBuilderExtensions
{
    public static void Th(this StringBuilder sb, string text, int colspan = 1)
    {
        sb.Append(CultureInfo.InvariantCulture, $"<th colspan=\"{colspan}\">{text}</th>");
    }

    public static void Td(this StringBuilder sb, string text, string tooltip = "", bool asDanger = false, bool asInfo = false)
    {
        if (asDanger)
        {
            sb.Td($"<span class=\"tag is-danger\">{text}</span>", tooltip);
        }
        else if (asInfo)
        {
            sb.Td($"<span class=\"tag is-info\">{text}</span>", tooltip);
        }
        else
        {
            sb.Append(CultureInfo.InvariantCulture, $"<td title=\"{tooltip}\" class=\"is-size-7\" style=\"cursor:default;\">{text}</td>");
        }
    }

    public static void BeginTable(this StringBuilder sb)
    {
        sb.Append("<table class=\"table is-fluid is-striped container\">");
    }

    public static void Thead(this StringBuilder sb, params object[] columns)
    {
        sb.AppendLine("<thead>");
        sb.BeginTr();

        var lastColspan = 1;
        foreach (var c in columns)
        {
            if (c is int colspan)
            {
                lastColspan = colspan;
            }
            else if (c is string text)
            {
                sb.Th(text, lastColspan);
                lastColspan = 1;
            }
        }

        sb.EndTr();
        sb.AppendLine("</thead>");
    }

    public static void BeginTbody(this StringBuilder sb)
    {
        sb.Append("<tbody>");
    }

    public static void EndTbody(this StringBuilder sb)
    {
        sb.Append("</tbody>");
    }

    public static void BeginTr(this StringBuilder sb)
    {
        sb.Append("<tr>");
    }

    public static void EndTr(this StringBuilder sb)
    {
        sb.Append("</tr>");
    }

    public static void EndTable(this StringBuilder sb)
    {
        sb.Append("</table>");
    }

    public static void BeginHtml(this StringBuilder sb)
    {
        sb.AppendLine("<!doctype html><html>");
        sb.AppendLine("<head><meta charset=\"utf-8\">");
        sb.AppendLine("<link href=\"https://cdnjs.cloudflare.com/ajax/libs/bulma/1.0.2/css/bulma.min.css\" rel=\"stylesheet\">");
        sb.AppendLine("<script src=\"https://cdnjs.cloudflare.com/ajax/libs/font-awesome/js/all.min.js\"></script>");
        sb.AppendLine("""
                        <style>
                            .tag {
                                display: inline-block;
                                height: auto;
                            }

                            .tag a {
                                color: hsl(var(--bulma-tag-h),var(--bulma-tag-s),var(--bulma-tag-color-l)) !important;
                            }
                        </style>
                      """);
        sb.AppendLine("</head><body>");
    }

    public static void EndHtml(this StringBuilder sb)
    {
        sb.AppendLine("</body></html>");
    }

    public static void Hero(this StringBuilder sb, string title)
    {
        sb.AppendLine("<section class=\"hero is-primary\"><div class=\"hero-body\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"<div class=\"container is-fluid\"><h1 class=\"title\">{title}</h1></div>");
        sb.AppendLine("</div>\n</section>");
    }
}
