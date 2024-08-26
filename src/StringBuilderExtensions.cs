using System.Globalization;
using System.Text;

namespace AzureReporting
{
    public static class StringBuilderExtensions
    {
        public static void Th(this StringBuilder sb, string text)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<th>{text}</th>");
        }
        public static void Td(this StringBuilder sb, string text, bool asDanger = false, bool asInfo = false)
        {
            if (asDanger)
            {
                sb.Td($"<span class=\"tag is-danger\">{text}</span>");
            }
            else if (asInfo)
            {
                sb.Td($"<span class=\"tag is-info\">{text}</span>");
            }
            else
            {
                sb.Append(CultureInfo.InvariantCulture, $"<td class=\"is-size-7\">{text}</td>");
            }
        }

        public static void BeginTable(this StringBuilder sb)
        {
            sb.Append("<table class=\"table is-fullwidth is-striped container\">");
        }

        public static void Thead(this StringBuilder sb, params string[] columns)
        {
            sb.AppendLine("<thead>");
            sb.BeginTr();
            foreach (var c in columns)
            {
                sb.Th(c);
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
            sb.AppendLine("</head><body>");
        }

        public static void EndHtml(this StringBuilder sb)
        {
            sb.AppendLine("</body></html>");
        }

        public static void Hero(this StringBuilder sb, string title)
        {
            sb.AppendLine("<section class=\"hero is-primary\"><div class=\"hero-body\">");
            sb.AppendLine(CultureInfo.InvariantCulture, $"<div class=\"container\"><h1 class=\"title\">{title}</h1></div>");
            sb.AppendLine("</div>\n</section>");
        }
    }
}
